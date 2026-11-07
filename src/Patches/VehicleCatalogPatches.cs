using System;
using HarmonyLib;

namespace PlayableVehicles
{
    [OptionalPatch("mod's own vehicle prefabs (falls back to a later check)")]
    [HarmonyPatch(typeof(Encyclopedia), "AfterLoad", typeof(Encyclopedia))]
    internal static class Encyclopedia_AfterLoad_Patch
    {
        private static void Postfix(Encyclopedia __0)
        {
            try
            {
                VehicleCatalog.Install(__0);
            }
            catch (Exception e)
            {
                ModLog.Error("Vehicle catalog install failed: " + e);
            }
        }
    }
}
