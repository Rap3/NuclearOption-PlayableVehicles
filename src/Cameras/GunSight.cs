using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class GunSight
    {
        private const float OpticForward = 1.25f;
        private const float OpticHeightTank = 0.7f;
        private const float OpticHeightLight = 0.85f;
        private const float OpticHeightTruck = 1.0f;

        private static readonly Dictionary<Turret, Vector3> OpticLocal = new Dictionary<Turret, Vector3>();

        public static bool TryGetOpticPoint(GroundVehicle vehicle, out Vector3 position)
        {
            position = default;
            if (!HasOptic(vehicle)) return false;
            Turret turret = VehicleSession.Driver.Turrets.PrimaryTurret;
            Vector3 offset = new Vector3(ModConfig.SightOffsetX.Value, ModConfig.SightOffsetY.Value, ModConfig.SightOffsetZ.Value);
            position = turret.transform.TransformPoint(LocalOptic(vehicle, turret) + offset);
            return true;
        }

        public static bool HasOptic(GroundVehicle vehicle)
        {
            if (vehicle == null) return false;
            VehicleDriver driver = VehicleSession.Driver;
            TurretController turrets = driver != null ? driver.Turrets : null;
            if (turrets == null || turrets.PrimaryTurret == null) return false;
            return ModConfig.SightForArtillery.Value || !IsArtillery(vehicle, turrets);
        }

        public static int HiddenCount => Hidden.Count;

        private static bool IsArtillery(GroundVehicle vehicle, TurretController turrets)
        {
            VehicleDefinition definition = vehicle.definition as VehicleDefinition;
            if (definition != null && definition.vehicleType == VehicleType.ART) return true;
            return turrets != null && turrets.HasArtillery;
        }

        private static float OpticHeight(GroundVehicle vehicle)
        {
            VehicleDefinition definition = vehicle.definition as VehicleDefinition;
            VehicleType type = definition != null ? definition.vehicleType : VehicleType.TRUCK;
            switch (type)
            {
                case VehicleType.MBT:
                case VehicleType.AFV:
                    return OpticHeightTank;
                case VehicleType.LCV:
                    return OpticHeightLight;
                default:
                    return OpticHeightTruck;
            }
        }

        public static Vector3 LocalOptic(GroundVehicle vehicle, Turret turret)
        {
            if (OpticLocal.TryGetValue(turret, out Vector3 cached)) return cached;
            Transform turretTransform = turret.transform;
            Transform scanner = FindScanner(turretTransform);
            float x = scanner != null ? turretTransform.InverseTransformPoint(scanner.position).x : 0f;
            Vector3 local = new Vector3(x, OpticHeight(vehicle), OpticForward);
            OpticLocal[turret] = local;
            return local;
        }

        private static Renderer[] _hideInSight = new Renderer[0];
        private static GroundVehicle _hideOwner;
        private static readonly List<Renderer> Hidden = new List<Renderer>();

        public static void ApplyOcclusion(GroundVehicle vehicle)
        {
            if (vehicle == null || Hidden.Count > 0) return;
            if (_hideOwner != vehicle || _hideInSight.Length == 0) RebuildHidden(vehicle);
            for (int i = 0; i < _hideInSight.Length; i++)
            {
                Renderer renderer = _hideInSight[i];
                if (renderer == null || !renderer.enabled) continue;
                renderer.enabled = false;
                Hidden.Add(renderer);
            }
        }

        private static void RebuildHidden(GroundVehicle vehicle)
        {
            _hideOwner = vehicle;
            VehicleDriver driver = VehicleSession.Driver;
            Turret turret = driver != null && driver.Turrets != null ? driver.Turrets.PrimaryTurret : null;
            if (turret == null) { _hideInSight = new Renderer[0]; return; }
            Transform elevation = GameAccess.TurretElevationTransform != null
                ? GameAccess.TurretElevationTransform(turret)
                : null;
            var list = new List<Renderer>();
            Renderer[] all = turret.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in all)
            {
                if (renderer == null) continue;
                if (elevation != null && renderer.transform.IsChildOf(elevation)) continue;
                list.Add(renderer);
            }
            _hideInSight = list.ToArray();
        }

        public static void RestoreOcclusion()
        {
            for (int i = 0; i < Hidden.Count; i++)
            {
                if (Hidden[i] != null) Hidden[i].enabled = true;
            }
            Hidden.Clear();
            _hideInSight = new Renderer[0];
            _hideOwner = null;
        }

        private static Transform FindScanner(Transform turret)
        {
            for (int i = 0; i < turret.childCount; i++)
            {
                Transform child = turret.GetChild(i);
                if (child != null && child.name == "scanner") return child;
            }
            Transform[] all = turret.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                if (t == null || t == turret) continue;
                string name = t.name.ToLowerInvariant();
                if (name == "scanner" || name.Contains("optic") || name.Contains("periscope") || name.Contains("sight")) return t;
            }
            return null;
        }

        public static void ResetCache()
        {
            OpticLocal.Clear();
        }
    }
}
