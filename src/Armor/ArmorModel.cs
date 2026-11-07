using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class ArmorModel
    {
        private const float RicochetMin = 70f;
        private const float RicochetMax = 80f;
        private const float InteriorDepth = 8f;
        private const float SpallApex = 0.3f;
        private const int SpallSamples = 8;
        private const float SpallMaxDamage = 30f;
        private const float SpallRadius = 0.4f;
        private const float SpallRadiusPerPierce = 0.0008f;
        private const float SpallRadiusMax = 1.2f;
        private const float SpallLength = 1.5f;
        private const float SpallLengthPerPierce = 0.001f;
        private const float SpallLengthMax = 2.5f;
        private const float SpallShare = 0.35f;
        private const float DamagePerPierce = 0.2f;
        private const float ModuleHp = 100f;
        private const float OvermatchRatio = 3f;
        private const float GrazingAngle = 85f;
        private const int CrewLossLimit = 2;
        private const float AmmoDetonationChance = 0.5f;
        private const float AmmoLossShare = 0.5f;

        private struct KindInfo
        {
            public float Durability;
            public float Absorb;
            public float MinPierce;

            public KindInfo(float durability, float absorb, float minPierce = 0f)
            {
                Durability = durability;
                Absorb = absorb;
                MinPierce = minPierce;
            }
        }

        private static readonly Dictionary<string, KindInfo> Kinds = new Dictionary<string, KindInfo>
        {
            { "engine", new KindInfo(1.5f, 400f) },
            { "transmission", new KindInfo(1.5f, 300f) },
            { "fuel", new KindInfo(1f, 150f) },
            { "ammo", new KindInfo(1f, 150f) },
            { "ammo_bustle", new KindInfo(1f, 150f) },
            { "crew", new KindInfo(1f, 60f) },
            { "gun", new KindInfo(2f, 400f) },
            { "turret_ring", new KindInfo(2f, 300f) },
            { "turret_drive", new KindInfo(1f, 100f) },
            { "optics", new KindInfo(1f, 50f) },
            { "track", new KindInfo(4f, 300f, 250f) },
            { "wheel", new KindInfo(3f, 100f, 200f) },
            { "launcher", new KindInfo(1f, 100f) },
            { "radar", new KindInfo(1f, 50f) },
        };

        private static KindInfo KindOf(string kind) => Kinds.TryGetValue(kind ?? "", out KindInfo info) ? info : new KindInfo(1f, 100f);

        private sealed class Module
        {
            public ArmorProfiles.ModuleData Data;
            public Transform Part;
            public Vector3 Center;
            public Vector3 Half;
            public KindInfo Kind;
            public Module Target;
            public float Hp = ModuleHp;
            public bool Handled;
            public bool Destroyed => Hp <= 0f;
        }

        private sealed class VehicleArmor
        {
            public Unit Unit;
            public ArmorProfiles.ProfileData Profile;
            public readonly Dictionary<UnitPart, float[]> Parts = new Dictionary<UnitPart, float[]>();
            public UnitPart Root;
            public readonly List<Module> Modules = new List<Module>();
            public readonly List<Region> Regions = new List<Region>();
            public bool Finished;
        }

        private sealed class Region
        {
            public ArmorProfiles.RegionData Data;
            public Transform Part;
            public Vector3 Center;
            public Vector3 Half;

            public bool Contains(Vector3 world)
            {
                Vector3 p = Part.InverseTransformPoint(world) - Center;
                return Mathf.Abs(p.x) <= Half.x && Mathf.Abs(p.y) <= Half.y && Mathf.Abs(p.z) <= Half.z;
            }
        }

        private static readonly Dictionary<Unit, VehicleArmor> _vehicles = new Dictionary<Unit, VehicleArmor>();
        private static readonly List<Unit> _dead = new List<Unit>();
        private static readonly List<KeyValuePair<float, Module>> _onRay = new List<KeyValuePair<float, Module>>();

        public static bool Handles(IDamageable damageable)
        {
            if (!ModConfig.ZonalArmor.Value) return false;
            UnitPart part = damageable as UnitPart;
            if (part == null || !(part.parentUnit is GroundVehicle) || !part.parentUnit.IsServer || part.parentUnit.disabled) return false;
            VehicleArmor armor = Get(part.parentUnit);
            return armor != null && armor.Parts.ContainsKey(part);
        }

        public static void Hit(UnitPart part, RaycastHit hit, Vector3 velocity, float muzzleVelocity, float pierceDamage, bool jet, PersistentID dealer)
        {
            VehicleArmor armor = Get(part.parentUnit);
            if (armor == null || armor.Finished) return;

            Vector3 dir = velocity.normalized;
            Vector3 localNormal = part.transform.InverseTransformDirection(hit.normal);
            int zone = ZoneOf(localNormal, armor.Profile);
            Region region = RegionAt(armor, part, zone, hit.point);
            float mult = region != null ? region.Data.mult : armor.Parts[part][zone];
            float spallScale = region != null ? region.Data.spall : 1f;
            float detonation = region != null && region.Data.detonation >= 0f ? region.Data.detonation : AmmoDetonationChance;
            float zoneArmor = part.GetArmorProperties().pierceArmor * mult;
            string zoneName = region != null ? region.Data.id : ArmorProfiles.ZoneNames[zone];

            float cos = Mathf.Clamp(Vector3.Dot(dir, -hit.normal), 0f, 1f);
            float angle = Mathf.Acos(cos) * Mathf.Rad2Deg;
            float pierce = pierceDamage * (velocity.magnitude / Mathf.Max(muzzleVelocity, 1f));
            float minCos = Mathf.Cos(Mathf.Min(armor.Profile.maxAngle, 84f) * Mathf.Deg2Rad);
            float effective = zoneArmor / Mathf.Max(cos, minCos);

            HitExternal(armor, hit.point, pierce);

            bool ricochet = !jet && angle > RicochetMin && (angle >= GrazingAngle || pierce < zoneArmor * OvermatchRatio)
                && (angle >= RicochetMax || Random.value < (angle - RicochetMin) / (RicochetMax - RicochetMin));
            if (ricochet || pierce <= effective)
            {
                CheckDestroyed(armor, dealer, detonation);
                return;
            }

            float residual = pierce - effective;
            HitInterior(armor, hit.point, dir, residual, spallScale);

            float structure = armor.Profile.structureShare < 0f
                ? residual / Mathf.Max(part.GetArmorProperties().pierceTolerance, 0.01f)
                : Mathf.Min(residual * armor.Profile.structureShare, armor.Profile.structureMax);
            if (structure > 0f && !armor.Unit.disabled) part.TakeDamage(0f, 0f, 1f, 0f, structure, dealer);
            CheckDestroyed(armor, dealer, detonation);
        }

        private static Region RegionAt(VehicleArmor armor, UnitPart part, int zone, Vector3 point)
        {
            foreach (Region region in armor.Regions)
            {
                if (region.Data.zone == zone && region.Part == part.transform && region.Contains(point)) return region;
            }
            return null;
        }

        private static int ZoneOf(Vector3 n, ArmorProfiles.ProfileData profile)
        {
            if (n.z > profile.frontThreshold) return ArmorProfiles.Front;
            if (n.z < -profile.frontThreshold) return ArmorProfiles.Rear;
            if (n.y > profile.roofThreshold) return ArmorProfiles.Roof;
            if (n.y < -profile.roofThreshold) return ArmorProfiles.Belly;
            return ArmorProfiles.Side;
        }

        private static void HitExternal(VehicleArmor armor, Vector3 point, float pierce)
        {
            foreach (Module module in armor.Modules)
            {
                if (!module.Data.external || module.Part == null) continue;
                Vector3 local = module.Part.InverseTransformPoint(point) - module.Center;
                Vector3 h = module.Half + Vector3.one * 0.15f;
                if (pierce >= module.Kind.MinPierce && Mathf.Abs(local.x) <= h.x && Mathf.Abs(local.y) <= h.y && Mathf.Abs(local.z) <= h.z)
                {
                    Damage(module, pierce * DamagePerPierce / module.Kind.Durability, "");
                }
            }
        }

        private static void HitInterior(VehicleArmor armor, Vector3 entry, Vector3 dir, float residual, float spallScale)
        {
            float spallRadius = Mathf.Min(SpallRadius + residual * SpallRadiusPerPierce, SpallRadiusMax) * spallScale;
            float spallLength = Mathf.Min(SpallLength + residual * SpallLengthPerPierce, SpallLengthMax) * spallScale;
            float spallMax = SpallMaxDamage * spallScale;
            _onRay.Clear();
            foreach (Module module in armor.Modules)
            {
                if (module.Data.external || module.Part == null) continue;
                Vector3 origin = module.Part.InverseTransformPoint(entry) - module.Center;
                Vector3 localDir = module.Part.InverseTransformDirection(dir);
                if (RayBox(origin, localDir, module.Half, out float enter) && enter <= InteriorDepth)
                {
                    _onRay.Add(new KeyValuePair<float, Module>(enter, module));
                }
                else
                {
                    float edge = SpallReach(origin, localDir, module.Half, spallRadius, spallLength);
                    if (edge <= 1f)
                    {
                        float damage = Mathf.Min(residual * DamagePerPierce * SpallShare, spallMax) * (1f - 0.5f * edge);
                        Damage(module, damage / module.Kind.Durability, "~");
                    }
                }
            }
            _onRay.Sort((a, b) => a.Key.CompareTo(b.Key));

            float left = residual;
            foreach (KeyValuePair<float, Module> onRay in _onRay)
            {
                if (left <= 0f) break;
                Module module = onRay.Value;
                Damage(module, left * DamagePerPierce / module.Kind.Durability, "");
                left -= module.Kind.Absorb;
            }
        }

        private static float SpallReach(Vector3 origin, Vector3 dir, Vector3 half, float radius, float length)
        {
            float best = float.MaxValue;
            for (int i = 0; i <= SpallSamples; i++)
            {
                float f = (float)i / SpallSamples;
                Vector3 p = origin + dir * (length * f);
                Vector3 outside = new Vector3(Mathf.Max(Mathf.Abs(p.x) - half.x, 0f), Mathf.Max(Mathf.Abs(p.y) - half.y, 0f),
                    Mathf.Max(Mathf.Abs(p.z) - half.z, 0f));
                float allowed = Mathf.Lerp(SpallApex, radius, f);
                best = Mathf.Min(best, outside.magnitude / allowed);
            }
            return best;
        }

        private static bool RayBox(Vector3 origin, Vector3 dir, Vector3 half, out float enter)
        {
            float tMin = 0f, tMax = float.MaxValue;
            enter = 0f;
            for (int i = 0; i < 3; i++)
            {
                float o = origin[i], d = dir[i], h = half[i];
                if (Mathf.Abs(d) < 1e-6f)
                {
                    if (o < -h || o > h) return false;
                    continue;
                }
                float t1 = (-h - o) / d, t2 = (h - o) / d;
                if (t1 > t2) { float s = t1; t1 = t2; t2 = s; }
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax) return false;
            }
            enter = tMin;
            return true;
        }

        private static void Damage(Module module, float amount, string tag)
        {
            Module target = module.Target ?? module;
            if (amount <= 0f || target.Destroyed) return;
            target.Hp -= amount;
        }

        private static void CheckDestroyed(VehicleArmor armor, PersistentID dealer, float detonation)
        {
            if (armor.Finished || armor.Unit.disabled) return;
            string cause = null;
            int crewLost = 0;
            foreach (Module module in armor.Modules)
            {
                if (!module.Destroyed) continue;
                if (module.Data.kind == "crew" && module.Target == null) crewLost++;
                if (module.Handled) continue;
                module.Handled = true;
                if (module.Data.kind != "ammo") continue;
                if (Random.value < detonation)
                {
                    cause = "ammo detonation";
                }
                else
                {
                    int lost = LoseAmmo(armor.Unit, AmmoLossShare);
                    ModLog.Info("Armor: " + Tag(armor) + " ammo rack hit, no detonation - lost " + lost + " rounds");
                }
            }
            if (cause == null && crewLost >= CrewLossLimit) cause = "crew knocked out";
            if (cause == null || armor.Root == null) return;

            armor.Finished = true;
            ModLog.Info("Armor: " + Tag(armor) + " destroyed - " + cause);
            armor.Root.TakeDamage(0f, 0f, 1f, 0f, armor.Root.hitPoints + 1000f, dealer);
        }

        private static int LoseAmmo(Unit unit, float share)
        {
            if (GameAccess.GunBulletsLoaded == null || GameAccess.GunMagazines == null
                || GameAccess.GunMagazineCapacity == null || GameAccess.GunMaxMagazines == null || unit.weaponStations == null) return 0;
            int stationIndex = -1;
            float best = 0f;
            for (int i = 0; i < unit.weaponStations.Count; i++)
            {
                WeaponStation station = unit.weaponStations[i];
                if (station?.Weapons == null || station.Weapons.Count == 0 || !(station.Weapons[0] is Gun gun) || gun.info == null) continue;
                if (gun.info.pierceDamage > best)
                {
                    best = gun.info.pierceDamage;
                    stationIndex = i;
                }
            }
            if (stationIndex < 0) return 0;

            WeaponStation main = unit.weaponStations[stationIndex];
            int lost = 0;
            foreach (Weapon weapon in main.Weapons)
            {
                if (!(weapon is Gun gun)) continue;
                int take = Mathf.FloorToInt(gun.ammo * share);
                ref int loaded = ref GameAccess.GunBulletsLoaded(gun);
                ref int magazines = ref GameAccess.GunMagazines(gun);
                int capacity = GameAccess.GunMagazineCapacity(gun);
                if (GameAccess.GunMaxMagazines(gun) > 0 && capacity > 0)
                {
                    int mags = Mathf.Min(magazines, take / capacity);
                    magazines -= mags;
                    take -= mags * capacity;
                    lost += mags * capacity;
                }
                int fromLoaded = Mathf.Min(loaded, take);
                loaded -= fromLoaded;
                lost += fromLoaded;
                gun.ammo = loaded + magazines * Mathf.Max(capacity, 0);
            }
            main.AccountAmmo();
            unit.RpcSyncAmmoCount((byte)stationIndex, main.Ammo);
            return lost;
        }

        private static VehicleArmor Get(Unit unit)
        {
            if (unit == null) return null;
            if (_vehicles.TryGetValue(unit, out VehicleArmor armor)) return armor;

            if (_vehicles.Count > 64) Prune();
            ArmorProfiles.ProfileData profile = ArmorProfiles.Find(unit);
            armor = profile != null ? Build(unit, profile) : null;
            _vehicles[unit] = armor;
            return armor;
        }

        private static VehicleArmor Build(Unit unit, ArmorProfiles.ProfileData profile)
        {
            var armor = new VehicleArmor { Unit = unit, Profile = profile };
            foreach (ArmorProfiles.PartData data in profile.parts)
            {
                Transform t = string.IsNullOrEmpty(data.path) ? unit.transform : unit.transform.Find(data.path);
                UnitPart part = t != null ? t.GetComponent<UnitPart>() : null;
                if (part == null)
                {
                    ModLog.Warning("Armor profile " + profile.prefab + ": no damage part at '" + data.path + "'");
                    continue;
                }
                armor.Parts[part] = data.zones;
                if (string.IsNullOrEmpty(data.path)) armor.Root = part;
            }
            foreach (ArmorProfiles.RegionData data in profile.regions ?? new ArmorProfiles.RegionData[0])
            {
                Transform t = string.IsNullOrEmpty(data.part) ? unit.transform : unit.transform.Find(data.part);
                if (t == null)
                {
                    ModLog.Warning("Armor profile " + profile.prefab + ": no part '" + data.part + "' for region " + data.id);
                    continue;
                }
                armor.Regions.Add(new Region
                {
                    Data = data,
                    Part = t,
                    Center = new Vector3(data.center[0], data.center[1], data.center[2]),
                    Half = new Vector3(data.size[0], data.size[1], data.size[2]) * 0.5f,
                });
            }
            var byId = new Dictionary<string, Module>();
            foreach (ArmorProfiles.ModuleData data in profile.modules)
            {
                Transform t = string.IsNullOrEmpty(data.part) ? unit.transform : unit.transform.Find(data.part);
                if (t == null)
                {
                    ModLog.Warning("Armor profile " + profile.prefab + ": no part '" + data.part + "' for module " + data.id);
                    continue;
                }
                var module = new Module
                {
                    Data = data,
                    Part = t,
                    Center = new Vector3(data.center[0], data.center[1], data.center[2]),
                    Half = new Vector3(data.size[0], data.size[1], data.size[2]) * 0.5f,
                    Kind = KindOf(data.kind),
                };
                armor.Modules.Add(module);
                if (data.id != null) byId[data.id] = module;
            }
            foreach (Module module in armor.Modules)
            {
                if (string.IsNullOrEmpty(module.Data.link)) continue;
                if (byId.TryGetValue(module.Data.link, out Module target) && target != module) module.Target = target;
                else ModLog.Warning("Armor profile " + profile.prefab + ": module " + module.Data.id + " links to missing '" + module.Data.link + "'");
            }
            return armor.Parts.Count > 0 ? armor : null;
        }

        private static void Prune()
        {
            _dead.Clear();
            foreach (Unit unit in _vehicles.Keys)
            {
                if (unit == null) _dead.Add(unit);
            }
            foreach (Unit unit in _dead) _vehicles.Remove(unit);
        }

        private static string Tag(VehicleArmor armor)
        {
            Unit unit = armor.Unit;
            string name = unit.SavedUnit != null && !string.IsNullOrEmpty(unit.SavedUnit.UniqueName) ? unit.SavedUnit.UniqueName : unit.persistentID.ToString();
            return unit.unitName + " [" + name + "]";
        }

        public static void Reset()
        {
            _vehicles.Clear();
        }
    }
}
