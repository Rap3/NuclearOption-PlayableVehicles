using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal enum FireMissionKind
    {
        None,
        Artillery,
        Ballistic,
        AirDefense,
    }

    internal static class FireMissions
    {
        private static readonly Dictionary<MissileDefinition, MissileSeeker> _seekers = new Dictionary<MissileDefinition, MissileSeeker>();

        public static FireMissionKind KindOf(WeaponStation station)
        {
            if (station == null) return FireMissionKind.None;
            if (station.Turrets != null)
            {
                foreach (Turret turret in station.Turrets)
                {
                    if (ArtillerySolver.IsArtillery(turret)) return FireMissionKind.Artillery;
                }
            }

            MissileSeeker seeker = Seeker(Launcher(station));
            if (seeker is BallisticMissileGuidance) return FireMissionKind.Ballistic;
            if (seeker is SARHSeeker || seeker is IRSeeker || seeker is ARHSeeker) return FireMissionKind.AirDefense;
            if (seeker != null) return FireMissionKind.Ballistic;
            return FireMissionKind.None;
        }

        public static MissileLauncher Launcher(WeaponStation station)
        {
            if (station == null || station.Weapons == null) return null;
            foreach (Weapon weapon in station.Weapons)
            {
                if (weapon is MissileLauncher launcher && launcher.missile != null) return launcher;
            }
            return null;
        }

        public static MissileSeeker Seeker(MissileLauncher launcher)
        {
            MissileDefinition missile = launcher != null ? launcher.missile : null;
            if (missile == null) return null;
            if (!_seekers.TryGetValue(missile, out MissileSeeker seeker))
            {
                seeker = missile.unitPrefab != null ? missile.unitPrefab.GetComponentInChildren<MissileSeeker>(true) : null;
                _seekers[missile] = seeker;
            }
            return seeker;
        }

        public static bool NeedsRadar(WeaponStation station)
        {
            MissileSeeker seeker = Seeker(Launcher(station));
            if (seeker is SARHSeeker) return true;
            return seeker != null && seeker.GetType().Name == "AryxABMSeeker";
        }

        public static bool HasRadar(GroundVehicle vehicle)
        {
            if (vehicle == null) return false;
            if (vehicle.radar != null || !vehicle.IsServer) return true;
            foreach (Turret turret in vehicle.GetComponentsInChildren<Turret>(true))
            {
                if (turret == null) continue;
                if (RadarLinkWatchdog.HasDetectorsOrFireControl(turret)) return true;
            }
            return false;
        }

        public static ArtillerySolver.Solution Check(FireMissionKind kind, GroundVehicle vehicle, WeaponStation station, Unit target)
        {
            var solution = new ArtillerySolver.Solution { Status = ArtillerySolver.Status.OutOfRange };
            WeaponInfo info = station != null ? station.WeaponInfo : null;
            if (vehicle == null || info == null || target == null) return solution;

            TargetRequirements req = info.targetRequirements;
            Vector3 origin = vehicle.transform.position;
            Vector3 point = TargetLock.AimPoint(target);
            Vector3 toTarget = point - origin;
            solution.Direction = toTarget;

            if (kind == FireMissionKind.Ballistic)
            {
                solution.Range = new Vector3(toTarget.x, 0f, toTarget.z).magnitude;
                solution.Status = Within(solution.Range, req);
                return solution;
            }

            solution.Range = toTarget.magnitude;
            if (!(target is Aircraft || target is Missile) || target.radarAlt < Mathf.Max(req.minAltitude, 1f))
            {
                solution.Status = ArtillerySolver.Status.NotAirborne;
                return solution;
            }
            if (req.maxAltitude > 0f && target.radarAlt > req.maxAltitude)
            {
                solution.Status = ArtillerySolver.Status.AltitudeLimit;
                return solution;
            }
            solution.Status = Within(solution.Range, req);
            if (solution.Status != ArtillerySolver.Status.Ready) return solution;

            if (NeedsRadar(station) && !HasRadar(vehicle)) solution.Status = ArtillerySolver.Status.NoRadar;
            else if (req.lineOfSight && !TerrainClear(origin + Vector3.up * 3f, target)) solution.Status = ArtillerySolver.Status.NoLineOfSight;
            return solution;
        }

        private static bool TerrainClear(Vector3 origin, Unit target)
        {
            Vector3 point = target.transform.position;
            return !Physics.Linecast(origin, point, out RaycastHit hit, PhysicsLayers.StaticsMask)
                || FastMath.InRange(hit.point, point, target.maxRadius * 2f);
        }

        private static ArtillerySolver.Status Within(float range, in TargetRequirements req)
        {
            if (req.maxRange > 0f && range > req.maxRange) return ArtillerySolver.Status.OutOfRange;
            if (req.minRange > 0f && range < req.minRange) return ArtillerySolver.Status.TooClose;
            return ArtillerySolver.Status.Ready;
        }
    }
}
