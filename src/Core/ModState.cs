using System;

namespace PlayableVehicles
{
    internal static class ModState
    {
        public static void ResetAll()
        {
            ModLog.Info("Mission reset: clearing mod state");

            Step("VehicleSession", VehicleSession.Reset);
            Step("HudOverrides", HudOverrides.Reset);
            Step("AircraftAutopilot", AircraftAutopilot.Disengage);
            Step("RemoteDrivers", RemoteDrivers.EndAll);
            Step("KillCredit", KillCredit.Reset);
            Step("ControlRegistry", ControlRegistry.Reset);
            Step("PointDefence", PointDefence.Reset);
            Step("ArtilleryAi", ArtilleryAi.Reset);
            Step("RadarLinkWatchdog", RadarLinkWatchdog.Reset);
            Step("VehicleSupply", VehicleSupply.Reset);
            Step("TurretSync", TurretSync.ClientReset);
            Step("HitFeedback", HitFeedback.Clear);
            Step("AmmoSync", AmmoSync.Clear);
            Step("ShotPrediction", ShotPrediction.Clear);
            Step("ArmorModel", ArmorModel.Reset);
            Step("SurfaceMap", SurfaceMap.Reset);
            Step("AiDriveLod", AiDriveLod.Reset);
            Step("VehicleRadialMenu", VehicleRadialMenu.Reset);
            Step("ProjectileCam", ProjectileCam.Reset);
            Step("GameUiLinks", GameUiLinks.Reset);
            Step("OwnVehicleMarkers", OwnVehicleMarkers.Reset);
            Step("DepotMarkers", DepotMarkers.Reset);
            Step("FireControlPanel", FireControlPanel.Reset);
            Step("SpawnScreen", SpawnScreen.Reset);
            Step("PurchasePanel", PurchasePanel.Reset);
            Step("VehicleSelectionMenu", VehicleSelectionMenu.Reset);
            Step("PurchaseClient", PurchaseClient.Reset);
            Step("PurchaseServer", PurchaseServer.Reset);
            Step("PurchasePoint", PurchasePoint.Reset);
        }

        private static void Step(string name, Action reset)
        {
            try
            {
                reset();
            }
            catch (Exception e)
            {
                ModLog.Error("Mission reset: '" + name + "' failed: " + e);
            }
        }
    }
}
