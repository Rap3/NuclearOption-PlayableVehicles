using System;
using HarmonyLib;
using UnityEngine;

namespace PlayableVehicles
{
    [OptionalPatch("projectile camera for shells")]
    [HarmonyPatch(typeof(Gun), "SpawnBullet")]
    internal static class ProjectileCam_SpawnBullet_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("Gun.SpawnBullet projectile-cam patch");

        private static void Postfix(Gun __instance)
        {
            try
            {
                ProjectileCam.RecordShellShot(__instance);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }

    [OptionalPatch("projectile camera for missiles")]
    [HarmonyPatch(typeof(Spawner), "SpawnMissile",
        new[] { typeof(MissileDefinition), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(Unit), typeof(Unit) })]
    internal static class ProjectileCam_SpawnMissile_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("Spawner.SpawnMissile projectile-cam patch");

        private static void Postfix(Missile __result, Unit owner)
        {
            try
            {
                ProjectileCam.RecordMissile(__result, owner);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }

    [OptionalPatch("projectile camera for missiles (client)")]
    [HarmonyPatch(typeof(Missile), "OnStartClient")]
    internal static class ProjectileCam_MissileClient_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("Missile.OnStartClient projectile-cam patch");

        private static void Postfix(Missile __instance)
        {
            try
            {
                ProjectileCam.RecordMissile(__instance, __instance != null ? __instance.owner : null);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }
}
