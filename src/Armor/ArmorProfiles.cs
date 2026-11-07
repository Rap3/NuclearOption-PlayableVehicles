using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace PlayableVehicles
{
    internal static class ArmorProfiles
    {
        private const string ResourcePrefix = "PlayableVehicles.Armor.";

        public const int Front = 0, Side = 1, Rear = 2, Roof = 3, Belly = 4;
        public static readonly string[] ZoneNames = { "front", "side", "rear", "roof", "belly" };

        public class ProfileData
        {
            public string prefab;
            public float frontThreshold = 0.25f;
            public float roofThreshold = 0.7f;
            public float maxAngle = 84f;
            public float structureShare = 0.02f;
            public float structureMax = 35f;
            public PartData[] parts;
            public RegionData[] regions;
            public ModuleData[] modules;
        }

        public class RegionData
        {
            public string id;
            public string part;
            public int zone;
            public float[] center;
            public float[] size;
            public float mult;
            public float spall = 1f;
            public float detonation = -1f;
        }

        public class PartData
        {
            public string path;
            public float[] zones;
        }

        public class ModuleData
        {
            public string id;
            public string kind;
            public string part;
            public float[] center;
            public float[] size;
            public bool external;
            public string link;
        }

        private static Dictionary<string, ProfileData> _profiles;

        public static ProfileData Find(Unit unit)
        {
            if (_profiles == null) Load();
            if (unit == null || unit.definition == null || unit.definition.unitPrefab == null) return null;
            return _profiles.TryGetValue(unit.definition.unitPrefab.name, out ProfileData profile) ? profile : null;
        }

        private static void Load()
        {
            _profiles = new Dictionary<string, ProfileData>();
            Assembly assembly = typeof(ArmorProfiles).Assembly;
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal)) continue;
                try
                {
                    using (Stream stream = assembly.GetManifestResourceStream(name))
                    using (var reader = new StreamReader(stream))
                    {
                        ProfileData profile = FromJson(reader.ReadToEnd());
                        string problem = Validate(profile);
                        if (problem != null)
                        {
                            ModLog.Error("Armor profile " + name + ": " + problem);
                            continue;
                        }
                        _profiles[profile.prefab] = profile;
                    }
                }
                catch (Exception e)
                {
                    ModLog.Error("Armor profile " + name + " failed to load: " + e.Message);
                }
            }
            ModLog.Info("Armor profiles: " + _profiles.Count);
        }

        private static ProfileData FromJson(string text)
        {
            var root = JsonReader.Parse(text) as Dictionary<string, object>;
            if (root == null) return null;
            var profile = new ProfileData
            {
                prefab = Str(root, "prefab"),
                frontThreshold = Num(root, "frontThreshold", 0.25f),
                roofThreshold = Num(root, "roofThreshold", 0.7f),
                maxAngle = Num(root, "maxAngle", 84f),
                structureShare = Num(root, "structureShare", 0.02f),
                structureMax = Num(root, "structureMax", 35f),
            };
            var parts = new List<PartData>();
            foreach (Dictionary<string, object> part in Objects(root, "parts"))
            {
                parts.Add(new PartData { path = Str(part, "path") ?? "", zones = Floats(part, "zones") });
            }
            profile.parts = parts.ToArray();
            var regions = new List<RegionData>();
            foreach (Dictionary<string, object> region in Objects(root, "regions"))
            {
                regions.Add(new RegionData
                {
                    id = Str(region, "id"),
                    part = Str(region, "part") ?? "",
                    zone = Array.IndexOf(ZoneNames, Str(region, "zone")),
                    center = Floats(region, "center"),
                    size = Floats(region, "size"),
                    mult = Num(region, "mult", 1f),
                    spall = Num(region, "spall", 1f),
                    detonation = Num(region, "detonation", -1f),
                });
            }
            profile.regions = regions.ToArray();
            var modules = new List<ModuleData>();
            foreach (Dictionary<string, object> module in Objects(root, "modules"))
            {
                modules.Add(new ModuleData
                {
                    id = Str(module, "id"),
                    kind = Str(module, "kind"),
                    part = Str(module, "part") ?? "",
                    center = Floats(module, "center"),
                    size = Floats(module, "size"),
                    external = module.TryGetValue("external", out object external) && external is bool b && b,
                    link = Str(module, "link") ?? "",
                });
            }
            profile.modules = modules.ToArray();
            return profile;
        }

        private static string Str(Dictionary<string, object> obj, string key) => obj.TryGetValue(key, out object v) ? v as string : null;

        private static float Num(Dictionary<string, object> obj, string key, float fallback) =>
            obj.TryGetValue(key, out object v) && v is double d ? (float)d : fallback;

        private static float[] Floats(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object v) || !(v is List<object> list)) return null;
            var result = new float[list.Count];
            for (int i = 0; i < list.Count; i++) result[i] = list[i] is double d ? (float)d : 0f;
            return result;
        }

        private static IEnumerable<Dictionary<string, object>> Objects(Dictionary<string, object> obj, string key)
        {
            if (!obj.TryGetValue(key, out object v) || !(v is List<object> list)) yield break;
            foreach (object item in list)
            {
                if (item is Dictionary<string, object> d) yield return d;
            }
        }

        private static string Validate(ProfileData profile)
        {
            if (profile == null || string.IsNullOrEmpty(profile.prefab)) return "no prefab";
            if (profile.parts == null || profile.parts.Length == 0) return "no parts";
            foreach (PartData part in profile.parts)
            {
                if (part.zones == null || part.zones.Length != ZoneNames.Length) return "part '" + part.path + "': need " + ZoneNames.Length + " zones";
            }
            foreach (RegionData region in profile.regions ?? new RegionData[0])
            {
                if (region.zone < 0 || region.center == null || region.center.Length != 3 || region.size == null || region.size.Length != 3)
                    return "region '" + region.id + "': bad zone or box";
            }
            if (profile.modules == null) profile.modules = new ModuleData[0];
            foreach (ModuleData module in profile.modules)
            {
                if (module.center == null || module.center.Length != 3 || module.size == null || module.size.Length != 3) return "module '" + module.id + "': bad box";
            }
            return null;
        }
    }
}
