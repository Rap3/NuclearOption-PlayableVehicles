using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class TargetLock
    {
        private const float MinLockDistance = 10000f;
        private const float LockAccuracy = 100f;

        private readonly List<Unit> _candidates = new List<Unit>();

        public System.Func<Unit, bool> TargetAllowed { get; set; }

        public Unit Target { get; private set; }

        public bool IsLocked => Target != null;

        public void Clear()
        {
            Target = null;
        }

        public void Select(GroundVehicle vehicle, Camera camera)
        {
            Unit candidate = camera != null ? FindCandidate(vehicle, camera) : null;
            if (candidate == null || candidate == Target) return;

            Target = candidate;
            ModLog.Notify("Target locked: " + candidate.unitName);
        }

        public void Release()
        {
            if (Target == null) return;
            Target = null;
            ModLog.Notify("Target lock released");
        }

        public void Set(GroundVehicle vehicle, Unit target)
        {
            if (target == null)
            {
                Target = null;
                return;
            }
            FactionHQ hq = vehicle != null ? vehicle.NetworkHQ : null;
            if (hq != null && IsLockable(target, vehicle, hq) && Allowed(target)) Target = target;
        }

        public void Validate(GroundVehicle vehicle, bool notify)
        {
            if (Target == null) return;

            FactionHQ hq = vehicle != null ? vehicle.NetworkHQ : null;
            if (Target.disabled || hq == null || !hq.IsTargetPositionAccurate(Target, LockAccuracy)
                || (TargetAllowed != null && !TargetAllowed(Target)))
            {
                if (notify) ModLog.Notify("Target lost");
                Target = null;
            }
        }

        public static Vector3 AimPoint(Unit unit)
        {
            Rigidbody rb = unit.rb;
            if (rb != null) return rb.worldCenterOfMass;
            float halfHeight = unit.definition != null ? unit.definition.height * 0.5f : 1f;
            return unit.transform.position + unit.transform.up * halfHeight;
        }

        public static Vector3 Velocity(Unit unit)
        {
            Rigidbody rb = unit.rb;
            return rb != null && unit.speed >= 1f ? rb.velocity : Vector3.zero;
        }

        private Unit FindCandidate(GroundVehicle vehicle, Camera camera)
        {
            FactionHQ hq = vehicle.NetworkHQ;
            if (hq == null) return null;

            float maxDistance = LockDistance(vehicle);

            Ray ray = VehicleCamera.ViewRay(camera);
            if (AimRaycast.Cast(ray.origin, ray.direction, maxDistance, vehicle.transform, out RaycastHit hit))
            {
                Unit hitUnit = hit.collider.GetComponentInParent<Unit>();
                if (IsLockable(hitUnit, vehicle, hq) && Allowed(hitUnit)) return hitUnit;
            }

            CollectCandidates(vehicle, hq);
            Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            float maxPixels = Screen.height * ModConfig.LockScreenRadius.Value;
            Unit best = null;
            float bestPixels = maxPixels;
            foreach (Unit unit in _candidates)
            {
                if (!hq.TryGetKnownPosition(unit, out GlobalPosition known)) continue;
                Vector3 screen = camera.WorldToScreenPoint(known.ToLocalPosition());
                if (screen.z <= 0f || screen.z > maxDistance) continue;

                float pixels = Vector2.Distance(center, new Vector2(screen.x, screen.y));
                if (pixels < bestPixels)
                {
                    bestPixels = pixels;
                    best = unit;
                }
            }
            return best;
        }

        private static float LockDistance(GroundVehicle vehicle)
        {
            float distance = MinLockDistance;
            if (vehicle.weaponStations == null) return distance;
            foreach (WeaponStation station in vehicle.weaponStations)
            {
                WeaponInfo info = station != null ? station.WeaponInfo : null;
                if (info != null) distance = Mathf.Max(distance, info.targetRequirements.maxRange);
            }
            return distance;
        }

        private void CollectCandidates(GroundVehicle vehicle, FactionHQ hq)
        {
            _candidates.Clear();
            foreach (PersistentID id in hq.trackingDatabase.Keys)
            {
                if (UnitRegistry.TryGetUnit(new PersistentID?(id), out Unit unit) && IsLockable(unit, vehicle, hq) && Allowed(unit))
                {
                    _candidates.Add(unit);
                }
            }
        }

        private bool Allowed(Unit unit)
        {
            return TargetAllowed == null || TargetAllowed(unit);
        }

        public static bool IsKnownForeignTarget(Unit unit, GroundVehicle vehicle)
        {
            FactionHQ hq = vehicle != null ? vehicle.NetworkHQ : null;
            return hq != null && IsLockable(unit, vehicle, hq) && hq.TryGetKnownPosition(unit, out _);
        }

        private static bool IsLockable(Unit unit, GroundVehicle vehicle, FactionHQ hq)
        {
            if (unit == null || unit.disabled || unit == vehicle || unit.NetworkHQ == hq || unit is Scenery) return false;
            if (!hq.IsTargetPositionAccurate(unit, LockAccuracy)) return false;
            return hq.TryGetKnownPosition(unit, out _);
        }
    }
}
