using UnityEngine;

namespace PlayableVehicles
{
    internal struct GunProfile
    {
        public Transform Muzzle;
        public float Speed;
        public float GravMult;
        public float DragCoef;
        public float Lifetime;
        public Rigidbody InheritVelocityFrom;

        public Vector3 InheritedVelocity => InheritVelocityFrom != null ? InheritVelocityFrom.velocity : Vector3.zero;
    }

    internal static class Ballistics
    {
        private const float Step = 0.02f;
        private const int StepsPerSegment = 5;
        private const int NearSteps = 50;
        private const int StepsPerFarSegment = 12;
        private const float DefaultLifetime = 5f;
        private const int SolveIterations = 4;
        private const int InterceptIterations = 3;

        public static bool TryGetGunProfile(WeaponStation station, Transform fallbackMuzzle, out GunProfile profile)
        {
            profile = default;
            if (station == null || station.Weapons == null) return false;

            foreach (Weapon weapon in station.Weapons)
            {
                if (!(weapon is Gun gun) || gun == null) continue;
                WeaponInfo info = gun.info;
                if (info == null || info.muzzleVelocity <= 0f) continue;
                if (GameAccess.GunGuidedProjectile != null && GameAccess.GunGuidedProjectile(gun) != null) continue;

                Transform[] muzzles = GameAccess.GunMuzzles != null ? GameAccess.GunMuzzles(gun) : null;
                profile.Muzzle = muzzles != null && muzzles.Length > 0 && muzzles[0] != null ? muzzles[0] : fallbackMuzzle;
                if (profile.Muzzle == null) continue;

                profile.Speed = info.muzzleVelocity;
                profile.GravMult = info.gravMult;
                profile.DragCoef = info.dragCoef;
                float lifetime = GameAccess.GunBulletSelfDestruct != null ? GameAccess.GunBulletSelfDestruct(gun) : DefaultLifetime;
                profile.Lifetime = lifetime > 0f ? lifetime : DefaultLifetime;
                profile.InheritVelocityFrom = gun.velocityInherit;
                return true;
            }
            return false;
        }

        public static bool PredictImpact(in GunProfile profile, Transform ignoreRoot, out Vector3 point, out float timeOfFlight)
        {
            return PredictImpact(profile.Muzzle.position,
                profile.InheritedVelocity + profile.Muzzle.forward * profile.Speed,
                profile.GravMult, profile.Speed > 0f ? profile.DragCoef / profile.Speed : 0f,
                profile.Lifetime, ignoreRoot, out point, out timeOfFlight);
        }

        public static bool PredictImpact(Vector3 position, Vector3 velocity, float gravMult, float dragK,
            float lifetime, Transform ignoreRoot, out Vector3 point, out float timeOfFlight)
        {
            float speed0Sqr = velocity.sqrMagnitude;
            Vector3 segmentStart = position;
            float minSpeedSqr = speed0Sqr * 0.02f;
            int steps = Mathf.CeilToInt(lifetime / Step);
            int segmentStep = 0;

            for (int i = 1; i <= steps; i++)
            {
                Integrate(gravMult, dragK, ref position, ref velocity);
                bool last = i == steps || velocity.sqrMagnitude < minSpeedSqr;

                segmentStep++;
                if (segmentStep >= (i <= NearSteps ? StepsPerSegment : StepsPerFarSegment) || last)
                {
                    segmentStep = 0;
                    Vector3 segment = position - segmentStart;
                    float length = segment.magnitude;
                    if (length > 0.001f && AimRaycast.Cast(segmentStart, segment / length, length, ignoreRoot, out RaycastHit hit))
                    {
                        point = hit.point;
                        timeOfFlight = i * Step;
                        return true;
                    }
                    segmentStart = position;
                }
                if (last) break;
            }

            point = position;
            timeOfFlight = steps * Step;
            return false;
        }

        public static bool Solve(in GunProfile profile, Vector3 target, out Vector3 offset, out float timeOfFlight)
        {
            timeOfFlight = 0f;
            Vector3 origin = profile.Muzzle.position;
            Vector3 inherited = profile.InheritedVelocity;
            Vector3 aimPoint = target;

            for (int iteration = 0; iteration < SolveIterations; iteration++)
            {
                Vector3 direction = (aimPoint - origin).normalized;
                if (!ClosestApproach(profile, origin, inherited + direction * profile.Speed, target, out Vector3 miss, out _)) break;
                aimPoint -= miss;
            }
            offset = aimPoint - target;

            Vector3 finalDirection = (aimPoint - origin).normalized;
            if (!ClosestApproach(profile, origin, inherited + finalDirection * profile.Speed, target, out Vector3 finalMiss, out timeOfFlight)) return false;
            float tolerance = Mathf.Max(1.5f, Vector3.Distance(origin, target) * 0.002f);
            return finalMiss.magnitude <= tolerance;
        }

        private static bool ClosestApproach(in GunProfile profile, Vector3 origin, Vector3 velocity, Vector3 target, out Vector3 miss, out float time)
        {
            Vector3 position = origin;
            time = 0f;
            int steps = Mathf.CeilToInt(profile.Lifetime / Step);
            float minSpeedSqr = profile.Speed * profile.Speed * 0.02f;
            for (int i = 0; i < steps; i++)
            {
                if (velocity.sqrMagnitude < minSpeedSqr) break;
                Vector3 previous = position;
                Integrate(profile, ref position, ref velocity);
                if (Vector3.Dot(velocity, target - position) <= 0f)
                {
                    Vector3 stepVector = position - previous;
                    float t = Mathf.Clamp01(Vector3.Dot(target - previous, stepVector) / Mathf.Max(stepVector.sqrMagnitude, 1e-6f));
                    miss = previous + stepVector * t - target;
                    time = (i + t) * Step;
                    return true;
                }
            }
            miss = Vector3.zero;
            return false;
        }

        public static bool SolveIntercept(in GunProfile profile, Vector3 targetPosition, Vector3 targetVelocity,
            out Vector3 interceptPoint, out Vector3 offset)
        {
            interceptPoint = targetPosition;
            offset = Vector3.zero;
            bool reachable = false;
            for (int iteration = 0; iteration < InterceptIterations; iteration++)
            {
                reachable = Solve(profile, interceptPoint, out offset, out float time);
                if (time <= 0f) break;
                interceptPoint = targetPosition + targetVelocity * time;
            }
            return reachable && Solve(profile, interceptPoint, out offset, out _);
        }

        public static bool CrossDescending(in GunProfile profile, Vector3 origin, Vector3 velocity, float targetY, out Vector3 point, out float time)
        {
            Vector3 position = origin;
            int steps = Mathf.CeilToInt(profile.Lifetime / Step);
            float minSpeedSqr = profile.Speed * profile.Speed * 0.02f;
            for (int i = 0; i < steps; i++)
            {
                if (velocity.sqrMagnitude < minSpeedSqr) break;
                Vector3 previous = position;
                Integrate(profile, ref position, ref velocity);
                if (velocity.y < 0f && previous.y > targetY && position.y <= targetY)
                {
                    float t = (previous.y - targetY) / Mathf.Max(previous.y - position.y, 1e-6f);
                    point = Vector3.Lerp(previous, position, t);
                    time = (i + t) * Step;
                    return true;
                }
            }
            point = position;
            time = 0f;
            return false;
        }

        private static void Integrate(in GunProfile profile, ref Vector3 position, ref Vector3 velocity)
        {
            Integrate(profile.GravMult, profile.Speed > 0f ? profile.DragCoef / profile.Speed : 0f, ref position, ref velocity);
        }

        private static void Integrate(float gravMult, float dragK, ref Vector3 position, ref Vector3 velocity)
        {
            velocity.y -= 9.81f * Step * gravMult;
            velocity -= velocity.magnitude * velocity * (dragK * Step);
            position += velocity * Step;
        }
    }
}
