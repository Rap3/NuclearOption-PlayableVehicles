using System;
using System.Collections.Generic;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class VehicleSession
    {
        public static bool IsActive { get; private set; }
        public static bool IsClientDriver => IsActive && _mode == VehicleDriver.DriveMode.ClientDriver;
        public static bool AircraftHandsOff => IsActive || VehicleSelectionMenu.IsOpen;
        private static VehicleDriver.DriveMode _mode;
        public static GroundVehicle Vehicle { get; private set; }
        public static VehicleDriver Driver { get; private set; }
        public static VehicleCamera ViewCamera { get; private set; }

        private static Aircraft _previousAircraft;
        private static bool _aircraftLost;
        private const float AutoMapWindow = 20f;
        private static float _autoMapUntil = -1f;
        private static bool _mapWasMaximized;
        private static bool _wasPaused;
        private static int _mapKeyFrame = -10;
        private const float ReturnToSpawnDelay = 2f;
        private static float _returnToSpawnAt = -1f;
        private static VehicleHud _hud;
        private static bool _wasInCockpit;
        private static bool _exiting;
        private static readonly ErrorThrottle CameraErrors = new ErrorThrottle("VehicleCamera.UpdateCamera");
        private static readonly ErrorThrottle HudErrors = new ErrorThrottle("VehicleHud.Refresh");
        private static readonly ErrorThrottle SupplyErrors = new ErrorThrottle("SupplyMissionLock.Tick");
        private static readonly ErrorThrottle RadialErrors = new ErrorThrottle("VehicleRadialMenu.Tick");

        public static void Initialize()
        {
            ViewCamera = new VehicleCamera();
            _hud = new VehicleHud();
        }

        public static void Tick()
        {
            if (IsActive && (Vehicle == null || Vehicle.disabled))
            {
                Exit("Vehicle lost.", true, true);
            }
            if (IsActive)
            {
                CheckAircraftLost();
                AutoMapTick();
            }
            VehicleSupply.Tick(IsActive ? Vehicle : null);
            try
            {
                VehicleRadialMenu.Tick();
            }
            catch (Exception e)
            {
                RadialErrors.Report(e);
            }
            try
            {
                SupplyMissionLock.Tick();
            }
            catch (Exception e)
            {
                SupplyErrors.Report(e);
            }

            if (_returnToSpawnAt >= 0f && Time.unscaledTime >= _returnToSpawnAt)
            {
                _returnToSpawnAt = -1f;
                DynamicMap map = SceneSingleton<DynamicMap>.i;
                if (!IsActive && !HasLivingAircraft() && !DynamicMap.mapMaximized && map != null
                    && GameManager.gameState.IsSingleOrMultiplayer())
                {
                    map.Maximize();
                }
            }
        }

        private static void CheckAircraftLost()
        {
            if (_aircraftLost || (object)_previousAircraft == null) return;
            if (IsAircraftUsable(_previousAircraft)) return;

            _aircraftLost = true;
            _autoMapUntil = Time.time + AutoMapWindow;
            _mapWasMaximized = DynamicMap.mapMaximized;
            _wasPaused = GameplayUI.GameIsPaused;
            AircraftAutopilot.Disengage();

            string what;
            if (_previousAircraft == null || IsPilotDead(_previousAircraft) || (_previousAircraft.disabled && !_previousAircraft.HasEjected()))
            {
                what = "Your aircraft was destroyed.";
            }
            else if (_previousAircraft.HasEjected())
            {
                what = "Pilot ejected from your aircraft.";
            }
            else
            {
                what = "Your aircraft is no longer available.";
            }
            ModLog.Notify(what + " Leaving the vehicle [" + ModConfig.ToggleKey.Value + "] returns to the spawn screen.");
        }

        private static void AutoMapTick()
        {
            bool noAircraft = !_aircraftLost && !HasLivingAircraft();
            if (!_aircraftLost && !noAircraft) return;

            Rewired.Player input = GameManager.playerInput;
            if (input != null && input.GetButtonDown("Map")) _mapKeyFrame = Time.frameCount;

            bool maximized = DynamicMap.mapMaximized;
            if (maximized && !_mapWasMaximized)
            {
                bool byPlayer = Time.frameCount - _mapKeyFrame <= 2 || FireControlPanel.IsOpen;
                bool auto = (_aircraftLost && Time.time <= _autoMapUntil) || _wasPaused;
                DynamicMap map = SceneSingleton<DynamicMap>.i;
                if (!byPlayer && auto && map != null)
                {
                    map.Minimize();
                    maximized = DynamicMap.mapMaximized;
                    ModLog.Info("[VEHICLE] Map opened by the game " + (_aircraftLost ? "after the aircraft was lost" : "after the pause") + ": minimized");
                }
            }
            _mapWasMaximized = maximized;
            _wasPaused = GameplayUI.GameIsPaused;
        }

        public static void LateTick()
        {
            if (!IsActive || Vehicle == null || Vehicle.disabled || Driver == null) return;
            if (GameplayUI.GameIsPaused) return;
            HudOverrides.Tick();
        }

        public static void UpdateCamera()
        {
            if (!IsActive) return;
            try
            {
                ViewCamera.UpdateCamera();
            }
            catch (Exception e)
            {
                CameraErrors.Report(e);
            }
            try
            {
                _hud.Refresh(ViewCamera.CurrentCamera);
            }
            catch (Exception e)
            {
                HudErrors.Report(e);
            }
        }

        public static void Toggle()
        {
            if (IsActive) Exit(HasLivingAircraft() ? "Exited vehicle. Returned to aircraft view." : "Exited vehicle. Returning to the spawn screen.");
            else if (VehicleSelectionMenu.IsOpen) VehicleSelectionMenu.Close();
            else if (PurchasePanel.IsOpen) PurchasePanel.Close();
            else if (!DynamicMap.mapMaximized && AsVehicle(FollowedUnit()) != null) TryEnterSelected();
            else if (TryGetSelectedPoint(out PurchasePoint point))
            {
                if (VehicleSelectionMenu.Available) VehicleSelectionMenu.Open(point);
                else PurchasePanel.Open(point);
            }
            else TryEnterSelected();
        }

        private static bool TryGetSelectedPoint(out PurchasePoint point)
        {
            point = null;
            if (!VehicleShop.Enabled || !ModConfig.Enabled.Value || !GameManager.GetLocalHQ(out FactionHQ hq)) return false;

            if (FollowedUnit() is VehicleDepot followed && PurchasePoint.Of(followed).IsOwn(hq))
            {
                point = PurchasePoint.Of(followed);
                return true;
            }

            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.selectedIcons == null) return false;
            foreach (MapIcon icon in map.selectedIcons)
            {
                PurchasePoint candidate = icon is UnitMapIcon unitIcon && unitIcon.unit is VehicleDepot depot ? PurchasePoint.Of(depot)
                    : icon is AirbaseMapIcon airbaseIcon ? PurchasePoint.Of(airbaseIcon.airbase)
                    : null;
                if (candidate != null && candidate.IsOwn(hq))
                {
                    point = candidate;
                    return true;
                }
            }
            return false;
        }

        public static void Shutdown()
        {
            if (IsActive) Exit("Exited vehicle.");
            KillCredit.RestoreAll();
        }

        public static void Reset()
        {
            _wasInCockpit = false;
            if (IsActive)
            {
                ModLog.Info("[VEHICLE] Mission ended while driving: session closed");
                GroundVehicle vehicle = Vehicle;
                VehicleDriver driver = Driver;
                ExitStep("driver release", () =>
                {
                    if (vehicle != null && driver != null)
                    {
                        driver.Release();
                        driver.enabled = false;
                    }
                });
                if (_hud != null) ExitStep("HUD", _hud.Hide);
                if (ViewCamera != null) ExitStep("camera", ViewCamera.Detach);
            }
            Vehicle = null;
            Driver = null;
            IsActive = false;
            _previousAircraft = null;
            _exiting = false;
            SupplyMissionLock.Reset();
            OwnedUnitKills.Reset();
            _returnToSpawnAt = -1f;
            ResetAircraftLoss();
        }

        private static void ResetAircraftLoss()
        {
            _aircraftLost = false;
            _autoMapUntil = -1f;
            _mapWasMaximized = false;
            _wasPaused = false;
            _mapKeyFrame = -10;
        }

        private static Unit FollowedUnit()
        {
            CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
            return csm != null ? csm.followingUnit : null;
        }

        public static void EnterAsClient(GroundVehicle vehicle)
        {
            if (IsActive || vehicle == null || vehicle.disabled) return;
            Enter(vehicle, VehicleDriver.DriveMode.ClientDriver);
        }

        public static void EndFromHost(string message)
        {
            if (IsActive) Exit(message, false, true);
        }

        public static void ExitWithMessage(string message)
        {
            if (IsActive) Exit(message);
        }

        public static void EnterDeployed(GroundVehicle vehicle)
        {
            if (IsActive || vehicle == null || vehicle.disabled) return;
            if (NetBridge.IsClientOnly) ClientLink.RequestEnter(vehicle);
            else Enter(vehicle, VehicleDriver.DriveMode.Local);
        }

        private static bool HasLivingAircraft()
        {
            return GameManager.GetLocalAircraft(out Aircraft aircraft) && IsAircraftUsable(aircraft);
        }

        private static bool IsAircraftUsable(Aircraft aircraft)
        {
            if (aircraft == null || aircraft.disabled || aircraft.HasEjected() || IsPilotDead(aircraft)) return false;
            return GameManager.GetLocalAircraft(out Aircraft local) && local == aircraft;
        }

        private static bool IsPilotDead(Aircraft aircraft)
        {
            Pilot[] pilots = aircraft != null ? aircraft.pilots : null;
            return pilots != null && pilots.Length > 0 && pilots[0] != null && pilots[0].dead;
        }

        private static void TryEnterSelected()
        {
            GroundVehicle vehicle = FindSelectedVehicle();
            if (vehicle == null)
            {
                ModLog.Notify("No ground vehicle selected! Select one with Target Select, on the map (M) or with the camera.");
                return;
            }

            if (GameManager.GetLocalHQ(out FactionHQ playerHQ))
            {
                if (vehicle.NetworkHQ != null && playerHQ != null && vehicle.NetworkHQ != playerHQ)
                {
                    ModLog.Notify("Access Denied: Unit belongs to enemy faction!");
                    return;
                }
            }

            if (!ModConfig.Enabled.Value)
            {
                ModLog.Notify("Vehicles are disabled on this server");
                return;
            }

            if (vehicle.IsSlung())
            {
                ModLog.Notify(vehicle.unitName + " is on a helicopter sling");
                return;
            }

            if (ControlRegistry.IsControlled(vehicle) || KillCredit.TryGetActiveDriver(vehicle, out _))
            {
                string driver = KillCredit.TryGetActiveDriver(vehicle, out Player busyBy) ? KillCredit.DisplayName(busyBy) : "another player";
                ModLog.Notify(vehicle.unitName + " is already driven by " + driver);
                return;
            }

            if (NetBridge.IsServer && GameManager.GetLocalPlayer(out Player localPlayer) && !VehicleAccess.CanDrive(localPlayer, vehicle, true))
            {
                ModLog.Notify(VehicleAccess.DeniedText);
                return;
            }

            if (NetBridge.IsClientOnly)
            {
                ClientLink.RequestEnter(vehicle);
                return;
            }

            Enter(vehicle, VehicleDriver.DriveMode.Local);
        }

        private static GroundVehicle FindSelectedVehicle()
        {
            CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
            if (csm != null && AsVehicle(csm.followingUnit) is GroundVehicle followed) return followed;

            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            List<Unit> targets = combatHud != null ? combatHud.GetTargetList() : null;
            if (targets != null)
            {
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    if (AsVehicle(targets[i]) is GroundVehicle target) return target;
                }
            }

            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map != null && map.selectedIcons != null)
            {
                foreach (MapIcon icon in map.selectedIcons)
                {
                    if (icon is UnitMapIcon unitIcon && AsVehicle(unitIcon.unit) is GroundVehicle mapped) return mapped;
                }
            }
            return null;
        }

        private static GroundVehicle AsVehicle(Unit unit)
        {
            return unit is GroundVehicle vehicle && vehicle != null && !vehicle.disabled ? vehicle : null;
        }

        private static void Enter(GroundVehicle vehicle, VehicleDriver.DriveMode mode)
        {
            try
            {
                EnterSteps(vehicle, mode);
            }
            catch (Exception e)
            {
                ModLog.Error("Vehicle session: enter failed, rolling back: " + e);
                Exit(null, true, true);
                ModLog.Notify("Could not take control of " + (vehicle != null ? vehicle.unitName : "vehicle") + ": mod error, see log.");
            }
        }

        private static void EnterSteps(GroundVehicle vehicle, VehicleDriver.DriveMode mode)
        {
            Vehicle = vehicle;
            IsActive = true;
            _mode = mode;

            _previousAircraft = GameManager.GetLocalAircraft(out Aircraft plane) && IsAircraftUsable(plane) ? plane : null;
            _returnToSpawnAt = -1f;
            ResetAircraftLoss();
            AircraftAutopilot.Engage(_previousAircraft);
            if (GameManager.GetLocalPlayer(out Player player)) KillCredit.Begin(vehicle, player);

            try
            {
                DynamicMap map = SceneSingleton<DynamicMap>.i;
                if (map != null) map.Minimize();
            }
            catch { }

            CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
            _wasInCockpit = csm != null && csm.currentState == csm.cockpitState && _previousAircraft != null;
            if (csm != null) csm.SetFollowingUnit(vehicle);

            Driver = vehicle.gameObject.GetComponent<VehicleDriver>() ?? vehicle.gameObject.AddComponent<VehicleDriver>();
            Driver.enabled = true;
            Driver.Init(vehicle, mode);
            try
            {
                SupplyMissionLock.Lock(vehicle);
            }
            catch (Exception e)
            {
                SupplyErrors.Report(e);
            }

            ViewCamera.Attach(vehicle);

            HudOverrides.Apply(vehicle);
            _hud.Show(vehicle, Driver, ViewCamera);

            ModLog.Info("Entered vehicle: " + vehicle.unitName);
        }

        private static void Exit(string message, bool notifyHost = true, bool lost = false)
        {
            if (_exiting) return;
            _exiting = true;
            try
            {
                GroundVehicle vehicle = Vehicle;
                VehicleDriver driver = Driver;
                ExitStep("towing", () => Towing.Release(vehicle));
                ExitStep("driver release", () =>
                {
                    if (vehicle != null && driver != null)
                    {
                        driver.Release();
                        driver.enabled = false;
                    }
                });
                ExitStep("client load", () =>
                {
                    if ((object)driver != null) driver.ClientLoad.Clear();
                });
                ExitStep("control registry", () => ControlRegistry.Forget(vehicle));
                ExitStep("supply missions", () => SupplyMissionLock.Release(vehicle));
                ExitStep("projectile cam", ProjectileCam.Stop);

                Vehicle = null;
                Driver = null;
                IsActive = false;
                ExitStep("autopilot", AircraftAutopilot.Disengage);
                ExitStep("kill credit", () => KillCredit.End(vehicle));
                if (_mode == VehicleDriver.DriveMode.ClientDriver && notifyHost) ExitStep("notify host", () => ClientLink.SendExit(vehicle));

                ExitStep("HUD", _hud.Hide);
                ExitStep("HUD overrides", HudOverrides.Restore);

                ExitStep("camera", ViewCamera.Detach);

                ExitStep("aircraft camera", () =>
                {
                    CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
                    if (csm != null && IsAircraftUsable(_previousAircraft))
                    {
                        csm.SetFollowingUnit(_previousAircraft);
                        if (_wasInCockpit) csm.SwitchState(csm.cockpitState);
                    }
                    _wasInCockpit = false;
                });
                _previousAircraft = null;

                ExitStep("return to spawn", () =>
                {
                    if (!HasLivingAircraft()) _returnToSpawnAt = Time.unscaledTime + (lost ? ReturnToSpawnDelay : 0f);
                });
                if (message != null) ModLog.Notify(message);
            }
            finally
            {
                Vehicle = null;
                Driver = null;
                IsActive = false;
                _previousAircraft = null;
                ResetAircraftLoss();
                _exiting = false;
            }
        }

        private static void ExitStep(string name, Action step)
        {
            try
            {
                step();
            }
            catch (Exception e)
            {
                ModLog.Error("Vehicle session: exit step '" + name + "' failed: " + e);
            }
        }
    }
}
