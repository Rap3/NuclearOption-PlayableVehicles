using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlayableVehicles
{
    internal static class VehicleCatalog
    {
        private static GameObject _holder;
        private static readonly Dictionary<VehicleDefinition, GameObject> _vanilla = new Dictionary<VehicleDefinition, GameObject>();
        private static float _nextTry;

        public static bool Installed { get; private set; }

        public static void Install(Encyclopedia encyclopedia)
        {
            if (encyclopedia == null || encyclopedia.vehicles == null) return;
            if (_holder == null)
            {
                _holder = new GameObject("PlayableVehicles.VehiclePrefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
                _holder.hideFlags = HideFlags.HideAndDontSave;
            }

            int replaced = 0;
            foreach (VehicleDefinition definition in encyclopedia.vehicles)
            {
                if (definition == null || definition.unitPrefab == null || _vanilla.ContainsKey(definition)) continue;
                try
                {
                    GameObject vanilla = definition.unitPrefab;
                    GameObject prefab = Object.Instantiate(vanilla, _holder.transform);
                    prefab.name = vanilla.name;
                    prefab.AddComponent<PvVehicle>().Definition = definition;
                    if (DriveSpec.Has(prefab.name) && prefab.GetComponent<GroundVehicle>() != null)
                    {
                        TuneRecoil(prefab);
                        prefab.AddComponent<GroundAiDriver>();
                        prefab.AddComponent<RemoteVehicleVisuals>();
                    }
                    _vanilla[definition] = vanilla;
                    definition.unitPrefab = prefab;
                    replaced++;
                }
                catch (Exception e)
                {
                    ModLog.Error("Vehicle catalog: " + definition.unitName + " kept vanilla: " + e.Message);
                }
            }
            Installed = true;
            if (replaced > 0) ModLog.Info("Vehicle catalog: " + replaced + " vehicle prefabs replaced with the mod's");
        }

        public static void Tick()
        {
            if (Installed || Time.unscaledTime < _nextTry) return;
            _nextTry = Time.unscaledTime + 1f;
            Encyclopedia encyclopedia;
            try
            {
                encyclopedia = Encyclopedia.i;
            }
            catch
            {
                return;
            }
            if (encyclopedia != null && encyclopedia.vehicles != null && encyclopedia.vehicles.Count > 0) Install(encyclopedia);
        }

        private const float RecoilPerRound = 0.45f;

        private static void TuneRecoil(GameObject prefab)
        {
            if (GameAccess.GunRecoilImpulse == null) return;
            foreach (Gun gun in prefab.GetComponentsInChildren<Gun>(true))
            {
                WeaponInfo info = gun != null ? gun.info : null;
                if (info == null) continue;
                ref float impulse = ref GameAccess.GunRecoilImpulse(gun);
                if (impulse <= 0f) continue;
                float real = RecoilPerRound * Mathf.Max(info.massPerRound, 0f) * info.muzzleVelocity;
                float tuned = real > 0f ? Mathf.Min(impulse, real) : impulse;
                ModLog.Info("Recoil " + prefab.name + " " + info.weaponName + ": " + impulse.ToString("0") + " -> " + tuned.ToString("0")
                    + " N*s (round " + info.massPerRound.ToString("0.###") + " kg, " + info.muzzleVelocity.ToString("0") + " m/s)");
                impulse = tuned;
            }
        }

        public static GameObject VanillaPrefab(VehicleDefinition definition)
        {
            return definition != null && _vanilla.TryGetValue(definition, out GameObject prefab) ? prefab : definition != null ? definition.unitPrefab : null;
        }

        public static bool IsModVehicle(Unit unit)
        {
            return unit != null && unit.GetComponent<PvVehicle>() != null;
        }
    }

    internal sealed class PvVehicle : MonoBehaviour
    {
        private const float WakeImpactSpeed = 1.5f;

        public VehicleDefinition Definition;

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.rigidbody == null || collision.rigidbody.isKinematic) return;
            if (collision.relativeVelocity.sqrMagnitude < WakeImpactSpeed * WakeImpactSpeed || !NetBridge.IsServer) return;
            var vehicle = GetComponent<GroundVehicle>();
            if (vehicle == null || vehicle.disabled || !vehicle.NetworknetworkStationary || vehicle.IsSlung()) return;
            if (ControlRegistry.IsControlled(vehicle)) return;
            if (GameAccess.GroundVehicleAnchored != null && GameAccess.GroundVehicleAnchored(vehicle)) return;
            GroundAiDriver ai = GetComponent<GroundAiDriver>();
            if (ai != null && ai.Wake()) return;
            GameAccess.GroundVehicleResetStationary(vehicle) = true;
            vehicle.NetworknetworkStationary = false;
        }
    }
}
