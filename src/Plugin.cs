using System;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace PlayableVehicles
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "PlayableVehicles";
        public const string Name = "Nuclear Option Playable Ground Vehicles";
        public const string Version = "1.1.1";

        private Harmony _harmony;
        private bool _ready;
        private bool _disabled;
        private bool _disabledShown;
        private GuardedTick[] _updateTicks;
        private GuardedTick _fixedTick;
        private GuardedTick _lateTick;

        private void Awake()
        {
            ModLog.Init(Logger);
            ModLog.Info("=== Playable Vehicles " + Version + " initializing ===");

            LogGameBuild();
            ModConfig.Bind(Config);

            bool membersFound;
            try
            {
                membersFound = GameAccess.Initialize();
            }
            catch (Exception e)
            {
                ModLog.Error("Game member lookup failed, mod disabled: " + e);
                _disabled = true;
                return;
            }
            if (!membersFound)
            {
                ModLog.Error("Game members not found, mod disabled: " + string.Join(", ", GameAccess.Missing));
                _disabled = true;
                return;
            }

            try
            {
                _harmony = new Harmony(Guid);
                if (!PatchLoader.ApplyAll(_harmony))
                {
                    _disabled = true;
                    return;
                }
            }
            catch (Exception e)
            {
                ModLog.Error("Harmony patching failed, mod disabled: " + e);
                _disabled = true;
                _harmony?.UnpatchSelf();
                return;
            }

            VehicleSession.Initialize();

            _updateTicks = new[]
            {
                new GuardedTick("VehicleCatalog.Tick", VehicleCatalog.Tick),
                new GuardedTick("WeaponIcons.Tick", WeaponIcons.Tick),
                new GuardedTick("NetBridge.Tick", NetBridge.Tick),
                new GuardedTick("RemoteDrivers.Tick", RemoteDrivers.Tick),
                new GuardedTick("TurretSync.ServerTick", TurretSync.ServerTick),
                new GuardedTick("ClientLink.Tick", ClientLink.Tick),
                new GuardedTick("PurchaseClient.Tick", PurchaseClient.Tick),
                new GuardedTick("KillCredit.Tick", KillCredit.Tick),
                new GuardedTick("VehicleSession.Tick", VehicleSession.Tick),
                new GuardedTick("PurchasePanel.Tick", PurchasePanel.Tick),
                new GuardedTick("FireControlPanel.Tick", FireControlPanel.Tick),
                new GuardedTick("SpawnScreen.Tick", SpawnScreen.Tick),
                new GuardedTick("VehicleSelectionMenu.Tick", VehicleSelectionMenu.Tick),
                new GuardedTick("VehicleThumbnails.Tick", VehicleThumbnails.Tick),
                new GuardedTick("DepotMarkers.Prune", DepotMarkers.Prune),
                new GuardedTick("ControlRegistry.Tick", ControlRegistry.Tick),
                new GuardedTick("ArtilleryAi.Tick", ArtilleryAi.Tick),
                new GuardedTick("RadarLinkWatchdog.Tick", RadarLinkWatchdog.Tick),
                new GuardedTick("Hotkeys", HandleHotkeys),
            };
            _fixedTick = new GuardedTick("TurretSync.ClientFixedTick", TurretSync.ClientFixedTick);
            _lateTick = new GuardedTick("VehicleSession.LateTick", VehicleSession.LateTick);
            _ready = true;
        }

        private static void LogGameBuild()
        {
            try
            {
                ModLog.Info("Game version " + Application.version + ", Assembly-CSharp MVID "
                    + typeof(Unit).Assembly.ManifestModule.ModuleVersionId);
            }
            catch (Exception e)
            {
                ModLog.Warning("Game build info unavailable: " + e.Message);
            }
        }

        private void Update()
        {
            if (!_ready)
            {
                if (_disabled && !_disabledShown)
                {
                    try
                    {
                        ShowDisabledOnce();
                    }
                    catch (Exception)
                    {
                        _disabledShown = true;
                    }
                }
                return;
            }

            for (int i = 0; i < _updateTicks.Length; i++)
            {
                _updateTicks[i].Run();
            }
        }

        private static void HandleHotkeys()
        {
            if (InputGate.PauseMenuOpen) return;
            if (!GameManager.gameState.IsSingleOrMultiplayer()) return;

            if (InputGate.KeyDown(ModConfig.ToggleKey.Value))
            {
                VehicleSession.Toggle();
            }
            if (InputGate.KeyDown(ModConfig.FireControlKey.Value))
            {
                FireControlPanel.Toggle();
            }
            if (InputGate.KeyDown(ModConfig.ProjectileCamKey.Value))
            {
                ProjectileCam.Toggle();
            }
        }

        private void ShowDisabledOnce()
        {
            if (!_disabled || _disabledShown || !GameManager.gameState.IsSingleOrMultiplayer() || Time.timeSinceLevelLoad < 5f) return;
            _disabledShown = true;
            try
            {
                ModLog.Notify("PlayableVehicles is disabled: this game version is not supported yet (details in BepInEx/LogOutput.log)");
            }
            catch (Exception) { }
        }

        private void FixedUpdate()
        {
            if (_ready) _fixedTick.Run();
        }

        private void LateUpdate()
        {
            if (_ready) _lateTick.Run();
        }

        private void OnDestroy()
        {
            if (_ready)
            {
                try
                {
                    VehicleSession.Shutdown();
                }
                catch (Exception e)
                {
                    ModLog.Error("VehicleSession.Shutdown failed: " + e);
                }
                try
                {
                    RemoteDrivers.EndAll();
                }
                catch (Exception e)
                {
                    ModLog.Error("RemoteDrivers.EndAll failed: " + e);
                }
            }
            _harmony?.UnpatchSelf();
        }
    }

    internal sealed class ErrorThrottle
    {
        private const float Interval = 5f;

        private readonly string _name;
        private float _nextLogAt = float.NegativeInfinity;
        private int _suppressed;

        public ErrorThrottle(string name)
        {
            _name = name;
        }

        public void Report(Exception e)
        {
            float now = Time.unscaledTime;
            if (now < _nextLogAt)
            {
                _suppressed++;
                return;
            }
            _nextLogAt = now + Interval;
            string repeats = _suppressed > 0 ? " (+" + _suppressed + " more since last report)" : "";
            _suppressed = 0;
            ModLog.Error(_name + " failed" + repeats + ": " + e);
        }
    }

    internal sealed class GuardedTick
    {
        private readonly Action _tick;
        private readonly ErrorThrottle _errors;

        public GuardedTick(string name, Action tick)
        {
            _tick = tick;
            _errors = new ErrorThrottle(name);
        }

        public void Run()
        {
            try
            {
                _tick();
            }
            catch (Exception e)
            {
                _errors.Report(e);
            }
        }
    }
}
