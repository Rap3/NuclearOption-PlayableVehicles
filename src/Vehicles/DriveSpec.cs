using UnityEngine;

namespace PlayableVehicles
{
    internal abstract class DriveSpec
    {
        public string Prefab;
        public string Prototype;
        public float Mass;
        public float PowerKw;
        public float MaxRpm;
        public float IdleRpm;
        public float[] Gears;
        public float ReverseKmh;
        public float TopKmh;
        public float CenterOfMassY;

        public static DriveSpec For(Unit unit)
        {
            if (unit == null || unit.definition == null || unit.definition.unitPrefab == null) return null;
            string name = unit.definition.unitPrefab.name;
            if (TankSpec.ByPrefab.TryGetValue(name, out TankSpec tank)) return tank;
            if (WheeledSpec.ByPrefab.TryGetValue(name, out WheeledSpec wheeled)) return wheeled;
            return null;
        }

        public static IGroundDrive CreateDrive(Unit unit)
        {
            DriveSpec spec = For(unit);
            if (spec is TankSpec) return new TrackedDrive();
            if (spec is WheeledSpec) return new WheeledDrive();
            return null;
        }

        public static bool Has(string prefabName)
        {
            return TankSpec.ByPrefab.ContainsKey(prefabName) || WheeledSpec.ByPrefab.ContainsKey(prefabName);
        }

        public float MassOf(Unit unit)
        {
            if (Mass > 0f) return Mass;
            return unit != null && unit.definition != null && unit.definition.mass > 0f ? unit.definition.mass : 10000f;
        }
    }

    internal interface IGroundDrive
    {
        DriveSpec Spec { get; }
        float Mass { get; }
        float TopSpeed { get; }
        bool CanPivot { get; }
        float Speed { get; }
        bool Grounded { get; }
        bool OnPaved { get; }
        Surface Surface { get; }
        int Contacts { get; }
        float RadarAlt { get; }
        float Brake { get; }
        Powertrain Engine { get; }

        bool Init(GroundVehicle vehicle, Rigidbody rb, bool ai);
        void ApplyBody();
        void Step(float throttle, float steer);
    }

    internal static class GroundProbe
    {
        private static readonly int Mask = ~PhysicsLayers.ExclusionZonesMask;
        private const float Lift = 1f;

        public static bool Cast(Transform vehicle, Rigidbody body, Vector3 top, float radius, float travel, RaycastHit[] buffer, out RaycastHit ground, out float distance, Rigidbody ignore = null)
        {
            ground = default;
            distance = 0f;
            Vector3 up = vehicle.up;
            int count = Physics.SphereCastNonAlloc(top + up * Lift, radius, -up, buffer, travel + Lift, Mask, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int h = 0; h < count; h++)
            {
                RaycastHit hit = buffer[h];
                if (hit.collider == null || hit.distance <= 0f || hit.distance >= nearest) continue;
                if (hit.collider.attachedRigidbody == body || hit.collider.transform.IsChildOf(vehicle)) continue;
                if (ignore != null && (hit.collider.attachedRigidbody == ignore || hit.collider.transform.IsChildOf(ignore.transform))) continue;
                nearest = hit.distance;
                ground = hit;
            }
            if (nearest == float.MaxValue) return false;
            distance = nearest - Lift;
            return true;
        }

        private const float BumpZone = 0.08f;
        private const float BumpGain = 4f;
        private const float MaxRate = 6f;

        public static float SpringLoad(float compression, float travel, float preload, float rate, float spring, float bump, float rebound, float maxLoad)
        {
            float load = spring * (Mathf.Min(compression, travel) - preload);
            float x = compression - (travel - BumpZone);
            if (x > 0f) load += spring * BumpGain * x * x / BumpZone;
            rate = Mathf.Clamp(rate, -MaxRate, MaxRate);
            load += rate > 0f ? bump * rate : rebound * rate;
            return Mathf.Clamp(load, 0f, maxLoad);
        }

        public static float Height(Transform vehicle, Rigidbody body, Vector3 point, float max, RaycastHit[] buffer)
        {
            int count = Physics.RaycastNonAlloc(point, Vector3.down, buffer, max, Mask, QueryTriggerInteraction.Ignore);
            float nearest = max;
            for (int h = 0; h < count; h++)
            {
                Collider c = buffer[h].collider;
                if (c == null || c.attachedRigidbody == body || c.transform.IsChildOf(vehicle)) continue;
                nearest = Mathf.Min(nearest, buffer[h].distance);
            }
            return nearest;
        }

        public static float Tanh(float x)
        {
            if (x > 8f) return 1f;
            if (x < -8f) return -1f;
            float e = Mathf.Exp(2f * x);
            return (e - 1f) / (e + 1f);
        }
    }
}
