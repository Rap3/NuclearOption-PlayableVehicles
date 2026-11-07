using System;
using HarmonyLib;

namespace PlayableVehicles
{
    [OptionalPatch("weapon wheel and radial menu in vehicles")]
    [HarmonyPatch(typeof(RadialMenuMain), "Update")]
    internal static class RadialMenuMain_Update_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("RadialMenuMain.Update patch");

        private static bool Prefix()
        {
            try
            {
                return !VehicleSession.IsActive;
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }
    }

    [OptionalPatch("Drive vehicle in the aircraft radial menu")]
    [HarmonyPatch(typeof(RadialMenuMain), "SetupMain")]
    internal static class RadialMenuMain_SetupMain_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("RadialMenuMain.SetupMain patch");

        private static void Prefix(RadialMenuMain __instance)
        {
            try
            {
                VehicleRadialMenu.AddDriveAction(__instance);
            }
            catch (Exception e)
            {
                Errors.Report(e);
            }
        }
    }

    [OptionalPatch("Drive vehicle in the aircraft radial menu")]
    [HarmonyPatch(typeof(RadialMenuAction), nameof(RadialMenuAction.TriggerAction))]
    internal static class RadialMenuAction_TriggerAction_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("RadialMenuAction.TriggerAction patch");

        private static bool Prefix(RadialMenuAction __instance)
        {
            try
            {
                if (!VehicleRadialMenu.IsDriveAction(__instance)) return true;
                VehicleSession.Toggle();
                return false;
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return false;
            }
        }
    }
}
