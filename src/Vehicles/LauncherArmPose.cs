using System.Reflection;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class LauncherArmPose
    {
        private const float ElevationRateDeg = 30f;
        private const float PoseEpsilon = 1f;

        private static readonly FieldInfo ElevationField =
            typeof(Turret).GetField("elevationAngle", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo MinElevationField =
            typeof(Turret).GetField("minElevation", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ElevationTransformField =
            typeof(Turret).GetField("elevationTransform", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool AppliesTo(GroundVehicle vehicle)
        {
            if (vehicle == null) return false;
            string prefab = vehicle.name;
            if (prefab == "Aryx_HLT_HSM" || prefab == "Aryx_HLT_HSM_Nuclear") return true;
            string name = vehicle.unitName;
            return name == "MSV Ballistic Missile Launcher"
                || name == "MSV Nuclear Ballistic Missile Launcher";
        }

        public static bool Tick(GroundVehicle vehicle)
        {
            if (ElevationField == null || ElevationTransformField == null) return false;
            if (!AppliesTo(vehicle) || vehicle.weaponStations == null) return false;
            bool deployed = VehicleDeploy.IsDeployed(vehicle);
            bool moving = false;
            for (int s = 0; s < vehicle.weaponStations.Count; s++)
            {
                WeaponStation station = vehicle.weaponStations[s];
                if (station == null || station.Turrets == null) continue;
                for (int i = 0; i < station.Turrets.Count; i++)
                {
                    Turret turret = station.Turrets[i];
                    if (turret == null) continue;
                    float current = (float)ElevationField.GetValue(turret);
                    float target = deployed ? RaisedElevation(turret) : 0f;
                    float next = Mathf.MoveTowards(current, target, ElevationRateDeg * Time.fixedDeltaTime);
                    if (Mathf.Approximately(next, current)) continue;
                    ElevationField.SetValue(turret, next);
                    if (ElevationTransformField.GetValue(turret) is Transform et)
                        et.localEulerAngles = new Vector3(next, 0f, 0f);
                    if (Mathf.Abs(next - target) > PoseEpsilon) moving = true;
                }
            }
            return moving;
        }

        public static bool InProgress(GroundVehicle vehicle)
        {
            if (ElevationField == null || !AppliesTo(vehicle) || vehicle.weaponStations == null) return false;
            bool deployed = VehicleDeploy.IsDeployed(vehicle);
            for (int s = 0; s < vehicle.weaponStations.Count; s++)
            {
                WeaponStation station = vehicle.weaponStations[s];
                if (station == null || station.Turrets == null) continue;
                for (int i = 0; i < station.Turrets.Count; i++)
                {
                    Turret turret = station.Turrets[i];
                    if (turret == null) continue;
                    float target = deployed ? RaisedElevation(turret) : 0f;
                    if (Mathf.Abs((float)ElevationField.GetValue(turret) - target) > PoseEpsilon) return true;
                }
            }
            return false;
        }

        private static float RaisedElevation(Turret turret)
        {
            return MinElevationField != null ? -(float)MinElevationField.GetValue(turret) : -45f;
        }
    }
}
