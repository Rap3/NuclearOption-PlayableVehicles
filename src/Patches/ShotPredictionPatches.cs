using System;
using HarmonyLib;

namespace PlayableVehicles
{
    [OptionalPatch("instant cannon shots for client drivers")]
    [HarmonyPatch(typeof(WeaponStation), nameof(WeaponStation.RemoteFireSingle))]
    internal static class WeaponStation_RemoteFireSingle_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("WeaponStation.RemoteFireSingle patch");

        private static bool Prefix(WeaponStation __instance, Unit __0)
        {
            try
            {
                return !ShotPrediction.ConsumeEcho(__0, __instance);
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }
}
