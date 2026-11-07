using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class AiDriveLod
    {
        private const float Hysteresis = 0.1f;
        private const float RefreshInterval = 0.5f;

        private static readonly List<Vector3> _points = new List<Vector3>();
        private static float _nextRefresh;
        private static float _distance = 1000f;
        private static float _nextDistanceRead;

        public static float Distance
        {
            get
            {
                float now = Time.unscaledTime;
                if (now >= _nextDistanceRead)
                {
                    _nextDistanceRead = now + 1f;
                    _distance = ModConfig.AiPhysicsDistance != null ? Mathf.Max(0f, ModConfig.AiPhysicsDistance.Value) : 1000f;
                }
                return _distance;
            }
        }

        public static bool WantsOwnDrive(Vector3 position, bool near)
        {
            Refresh();
            float limit = Distance * (near ? 1f + Hysteresis : 1f - Hysteresis);
            float limitSqr = limit * limit;
            foreach (Vector3 point in _points)
            {
                if ((point - position).sqrMagnitude <= limitSqr) return true;
            }
            return false;
        }

        public static void Reset()
        {
            _points.Clear();
            _nextRefresh = 0f;
            _nextDistanceRead = 0f;
        }

        private static void Refresh()
        {
            if (Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + RefreshInterval;
            _points.Clear();

            foreach (Player player in UnitRegistry.playerLookup.Values)
            {
                if (player == null) continue;
                Aircraft aircraft = player.Aircraft;
                if (aircraft != null && !aircraft.disabled) _points.Add(aircraft.transform.position);
            }
            if (VehicleSession.IsActive && VehicleSession.Vehicle != null) _points.Add(VehicleSession.Vehicle.transform.position);
            foreach (RemoteDrivers.Session session in RemoteDrivers.Sessions)
            {
                if (session.Vehicle != null) _points.Add(session.Vehicle.transform.position);
            }
            CameraStateManager cam = SceneSingleton<CameraStateManager>.i;
            if (cam != null) _points.Add(cam.transform.position);
        }
    }
}
