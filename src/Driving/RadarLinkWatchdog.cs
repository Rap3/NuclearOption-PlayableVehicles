using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class RadarLinkWatchdog
    {
        private const float CheckInterval = 3f;
        private static float _nextCheckAt;

        private static readonly MethodInfo SearchMethod = typeof(Turret).GetMethod("SearchForRadar",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo _modeField;
        private static FieldInfo _stowedField;
        private static FieldInfo _detectorsField;
        private static FieldInfo _fireControlField;

        public static void Tick()
        {
            float now = Time.unscaledTime;
            if (now < _nextCheckAt) return;
            _nextCheckAt = now + CheckInterval;
            if (!NetBridge.IsServer) return;

            List<Unit> units = UnitRegistry.allUnits;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i] is GroundVehicle vehicle && !vehicle.disabled && vehicle.radar == null) Check(vehicle);
            }
        }

        public static void Reset()
        {
            _nextCheckAt = 0f;
        }

        private static void Check(GroundVehicle vehicle)
        {
            Turret[] turrets = vehicle.GetComponentsInChildren<Turret>(true);
            if (turrets.Length == 0) return;
            foreach (Turret turret in turrets)
            {
                if (turret == null || Stowed(turret)) continue;
                if (DetectorRadar(turret) is Radar radar) { vehicle.radar = radar; return; }
            }
            foreach (Turret turret in turrets)
            {
                if (turret == null || Mode(turret) != "searchForRadar" || Stowed(turret)) continue;
                if (Detectors(turret) > 0 || FireControl(turret)) return;
            }
            foreach (Turret turret in turrets)
            {
                if (turret == null || Mode(turret) != "searchForRadar" || Stowed(turret)) continue;
                SearchMethod?.Invoke(turret, null);
            }
        }

        private static string Mode(Turret turret)
        {
            _modeField ??= typeof(Turret).GetField("targetAcquisitionMode", BindingFlags.Instance | BindingFlags.NonPublic);
            return _modeField?.GetValue(turret)?.ToString();
        }

        private static bool Stowed(Turret turret)
        {
            _stowedField ??= typeof(Turret).GetField("stowed", BindingFlags.Instance | BindingFlags.NonPublic);
            return _stowedField != null && (bool)_stowedField.GetValue(turret);
        }

        private static int Detectors(Turret turret)
        {
            _detectorsField ??= typeof(Turret).GetField("targetDetectors", BindingFlags.Instance | BindingFlags.NonPublic);
            return _detectorsField?.GetValue(turret) is List<TargetDetector> detectors ? detectors.Count : 0;
        }

        private static Radar DetectorRadar(Turret turret)
        {
            _detectorsField ??= typeof(Turret).GetField("targetDetectors", BindingFlags.Instance | BindingFlags.NonPublic);
            if (_detectorsField?.GetValue(turret) is List<TargetDetector> detectors)
            {
                foreach (TargetDetector d in detectors)
                {
                    if (d is Radar radar) return radar;
                }
            }
            return null;
        }

        private static bool FireControl(Turret turret)
        {
            _fireControlField ??= typeof(Turret).GetField("fireControl", BindingFlags.Instance | BindingFlags.NonPublic);
            return _fireControlField != null && _fireControlField.GetValue(turret) != null;
        }

        public static bool HasDetectorsOrFireControl(Turret turret)
        {
            return Detectors(turret) > 0 || FireControl(turret);
        }
    }
}
