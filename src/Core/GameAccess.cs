using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using TMPro;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class GameAccess
    {
        public static AccessTools.FieldRef<Turret, Unit> TurretAttachedUnit { get; private set; }
        public static Action<Turret, Vector3> TurretAimTurret { get; private set; }
        public static AccessTools.FieldRef<Turret, Transform> TurretElevationTransform { get; private set; }
        public static AccessTools.FieldRef<Turret, float> TurretTraverseAngle { get; private set; }
        public static AccessTools.FieldRef<Turret, float> TurretElevationAngle { get; private set; }
        public static AccessTools.FieldRef<Turret, FireControl> TurretFireControl { get; private set; }

        public static AccessTools.FieldRef<UnitPart, bool> UnitPartCriticalPart { get; private set; }

        public static AccessTools.FieldRef<GroundVehicle, bool> GroundVehicleResetStationary { get; private set; }
        public static Action<GroundVehicle> GroundVehicleUpdate { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, Array> GroundVehicleDeployableParts { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, bool> GroundVehicleMobile { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, GameObject[]> GroundVehicleWheels { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, Renderer> GroundVehicleTracks { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, AudioSource> GroundVehicleEngineIdleSound { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, AudioSource> GroundVehicleEngineDriveSound { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, float> GroundVehicleEngineMinPitch { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, float> GroundVehicleEngineMaxPitch { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, PathfindingAgent> GroundVehiclePathfinder { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, List<Obstacle>> GroundVehicleObstacles { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, bool> GroundVehicleAnchored { get; private set; }
        public static AccessTools.FieldRef<PathfindingAgent, List<GlobalPosition>> PathfindingAgentWaypoints { get; private set; }
        public static AccessTools.FieldRef<PathfindingAgent, List<RoadPathfinding.Node>> PathfindingAgentNodes { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, float> GroundVehicleAcceleration { get; private set; }
        public static AccessTools.FieldRef<Gun, float> GunRecoilImpulse { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, float> GroundVehicleTopSpeedOnroad { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, float> GroundVehicleTopSpeedOffroad { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, float> GroundVehicleSuspensionTravel { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, float> GroundVehicleSpringRate { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, float> GroundVehicleDampingRate { get; private set; }
        public static AccessTools.FieldRef<GroundVehicle, float> GroundVehicleFrictionCoef { get; private set; }
        public static Action<MobileArtilleryAI, int> MobileArtilleryAISetState { get; private set; }
        public static Func<MobileArtilleryAI, UniTask> MobileArtilleryAITargetSearch { get; private set; }
        public static AccessTools.FieldRef<FireControl, Unit> FireControlAttachedUnit { get; private set; }
        public static AccessTools.FieldRef<UnitDebug, TMPro.TMP_Text> UnitDebugTarget { get; private set; }
        public static AccessTools.FieldRef<UnitDebug, Unit> UnitDebugFollowingUnit { get; private set; }
        public static AccessTools.FieldRef<Turret, AimSolver> TurretAimSolver { get; private set; }
        public static AccessTools.FieldRef<AimSolver, bool> AimSolverArtillery { get; private set; }
        public static AccessTools.FieldRef<AimSolver, float> AimSolverDragCoef { get; private set; }
        public static AccessTools.FieldRef<Turret, float> TurretMaxElevation { get; private set; }
        public static AccessTools.FieldRef<HeadMountedDisplay, HUDApp> HmdSpeed { get; private set; }
        public static AccessTools.FieldRef<HeadMountedDisplay, HUDApp> HmdAltitude { get; private set; }
        public static AccessTools.FieldRef<HeadMountedDisplay, HUDApp> HmdBearing { get; private set; }
        public static AccessTools.FieldRef<SpeedGauge, TextMeshProUGUI> SpeedGaugeText { get; private set; }
        public static AccessTools.FieldRef<SpeedGauge, TextMeshProUGUI> SpeedGaugeOverspeed { get; private set; }
        public static AccessTools.FieldRef<Altitude, TextMeshProUGUI> AltitudeRadarText { get; private set; }
        public static AccessTools.FieldRef<Altitude, TextMeshProUGUI> AltitudeAbsText { get; private set; }
        public static AccessTools.FieldRef<Bearing, TextMeshProUGUI> BearingText { get; private set; }
        public static AccessTools.FieldRef<FlightHud, RawImage> FlightHudCompass { get; private set; }
        public static AccessTools.FieldRef<CombatHUD, GameObject> CombatHudTurretUI { get; private set; }
        public static AccessTools.FieldRef<CombatHUD, WeaponStatus> CombatHudWeaponStatus { get; private set; }
        public static AccessTools.FieldRef<HUDTurretState, GameObject> TurretStateCrosshairPrefab { get; private set; }
        public static AccessTools.FieldRef<HUDTurretCrosshair, Image> TurretCrosshairCircle { get; private set; }
        public static AccessTools.FieldRef<HUDTurretCrosshair, Image> TurretCrosshairReadiness { get; private set; }
        public static AccessTools.FieldRef<HUDTurretCrosshair, Image> TurretCrosshairCross { get; private set; }
        public static AccessTools.FieldRef<WeaponStatus, Image> WeaponStatusImage { get; private set; }
        public static AccessTools.FieldRef<WeaponStatus, Image> WeaponStatusReloadImage { get; private set; }
        public static AccessTools.FieldRef<WeaponStatus, Image> WeaponStatusSafety { get; private set; }
        public static AccessTools.FieldRef<WeaponStatus, TextMeshProUGUI> WeaponStatusName { get; private set; }
        public static AccessTools.FieldRef<WeaponStatus, TextMeshProUGUI> WeaponStatusAmmo { get; private set; }
        public static AccessTools.FieldRef<WeaponStatus, RectTransform> WeaponStatusReload { get; private set; }
        public static AccessTools.FieldRef<WeaponStatus, RectTransform> WeaponStatusPanel { get; private set; }
        public static AccessTools.FieldRef<Unit, Dictionary<PersistentID, float>> UnitDamageCredit { get; private set; }
        public static AccessTools.FieldRef<FuelGauge, Image> FuelGaugeBar { get; private set; }
        public static AccessTools.FieldRef<FuelGauge, Image> FuelGaugePointer { get; private set; }
        public static AccessTools.FieldRef<FuelGauge, TextMeshProUGUI> FuelGaugeReading { get; private set; }
        public static AccessTools.FieldRef<FuelGauge, TextMeshProUGUI> FuelGaugeLabel { get; private set; }
        public static AccessTools.FieldRef<FuelGauge, Transform> FuelGaugePivot { get; private set; }
        public static AccessTools.FieldRef<FlightHud, Transform> FlightHudCenter { get; private set; }
        public static AccessTools.FieldRef<CombatHUD, GameObject> CombatHudCargoUI { get; private set; }
        public static AccessTools.FieldRef<HUDCargoState, GameObject> CargoCapturePanel { get; private set; }
        public static AccessTools.FieldRef<HUDCargoState, RectTransform> CargoCaptureBar { get; private set; }
        public static AccessTools.FieldRef<HUDCargoState, Image> CargoCapturePanelImage { get; private set; }
        public static AccessTools.FieldRef<HUDCargoState, Image> CargoCaptureBarImage { get; private set; }
        public static AccessTools.FieldRef<HUDCargoState, TMP_Text> CargoAirbaseTitle { get; private set; }
        public static AccessTools.FieldRef<HUDCargoState, TMP_Text> CargoCaptureText { get; private set; }
        public static AccessTools.FieldRef<HUDCargoState, CanvasGroup> CargoCaptureCanvasGroup { get; private set; }
        public static AccessTools.FieldRef<Missile, MissileSeeker> MissileSeeker { get; private set; }
        public static AccessTools.FieldRef<MissileSeeker, Unit> SeekerTargetUnit { get; private set; }
        public static AccessTools.FieldRef<InertialSeekerShell, bool> InertialSeekerUseDatalink { get; private set; }
        public static AccessTools.FieldRef<InertialSeekerShell, bool> InertialSeekerUseLaser { get; private set; }
        public static AccessTools.FieldRef<CombatHUD, ThreatList> CombatHudThreatList { get; private set; }
        public static AccessTools.FieldRef<ThreatList, GameObject> ThreatListItemPrefab { get; private set; }
        public static AccessTools.FieldRef<ThreatItem, Text> ThreatItemText { get; private set; }
        private static FieldInfo _threatAlarmTypes;
        private static FieldInfo _alarmSeekerType;
        private static FieldInfo _alarmClips;
        private static FieldInfo _alarmAlert;

        internal struct ThreatAlarm
        {
            public string SeekerType;
            public AudioClip[] Clips;
            public AudioClip Alert;
        }

        public static List<ThreatAlarm> ThreatAlarms(ThreatList list)
        {
            var result = new List<ThreatAlarm>();
            if (list == null || _threatAlarmTypes == null || _alarmSeekerType == null || _alarmClips == null) return result;
            if (!(_threatAlarmTypes.GetValue(list) is Array alarms)) return result;
            foreach (object alarm in alarms)
            {
                if (alarm == null) continue;
                result.Add(new ThreatAlarm
                {
                    SeekerType = _alarmSeekerType.GetValue(alarm) as string,
                    Clips = _alarmClips.GetValue(alarm) as AudioClip[],
                    Alert = _alarmAlert != null ? _alarmAlert.GetValue(alarm) as AudioClip : null,
                });
            }
            return result;
        }

        public static AccessTools.FieldRef<PilotBaseState, ControlInputs> PilotControlInputs { get; private set; }

        public static AccessTools.FieldRef<FlightHud, Canvas> FlightHudCanvas { get; private set; }

        public static AccessTools.FieldRef<CombatHUD, GameObject> CombatHudTopRightPanel { get; private set; }
        public static AccessTools.FieldRef<CombatHUD, GameObject> CombatHudCountermeasureBackground { get; private set; }
        public static AccessTools.FieldRef<CombatHUD, TextMeshProUGUI> CombatHudTargetText { get; private set; }
        public static AccessTools.FieldRef<CombatHUD, TextMeshProUGUI> CombatHudTargetInfo { get; private set; }
        public static Action<CombatHUD> CombatHudUpdateHitMarkers { get; private set; }

        public static AccessTools.FieldRef<CombatHUD, GameObject> CombatHudUnitMarkerPrefab { get; private set; }
        public static AccessTools.FieldRef<CombatHUD, GameObject> CombatHudHitMarker { get; private set; }
        public static AccessTools.FieldRef<Gun, Transform[]> GunMuzzles { get; private set; }
        public static AccessTools.FieldRef<Gun, float> GunBulletSelfDestruct { get; private set; }
        public static AccessTools.FieldRef<Gun, float> GunMuzzleVelocity { get; private set; }
        public static AccessTools.FieldRef<Gun, MissileDefinition> GunGuidedProjectile { get; private set; }
        public static AccessTools.FieldRef<Gun, bool> GunProximityTimer { get; private set; }
        public static AccessTools.FieldRef<Gun, float> GunFireInterval { get; private set; }
        public static AccessTools.FieldRef<Gun, float> GunReloadTime { get; private set; }
        public static AccessTools.FieldRef<Gun, int> GunBulletsLoaded { get; private set; }
        public static AccessTools.FieldRef<Gun, int> GunMagazines { get; private set; }
        public static AccessTools.FieldRef<Gun, int> GunMagazineCapacity { get; private set; }
        public static AccessTools.FieldRef<Gun, int> GunMaxMagazines { get; private set; }
        public static AccessTools.FieldRef<VehicleDepot, float> VehicleDepotSpawnCooldown { get; private set; }
        public static AccessTools.FieldRef<VehicleDepot, float> VehicleDepotLastSpawnedTime { get; private set; }
        public static AccessTools.FieldRef<VehicleDepot, Transform> VehicleDepotSpawnTransform { get; private set; }
        public static Func<VehicleDepot, GroundVehicle, UniTask> VehicleDepotMoveVehicleFromDepot { get; private set; }
        public static AccessTools.FieldRef<Airbase, RoadPathfinding.RoadNetwork> AirbaseTaxiNetwork { get; private set; }
        public static AccessTools.FieldRef<GameplayUI, Airbase> GameplayUIHomeAirbase { get; private set; }
        public static Action<Player, string> PlayerConvoyDonationMessage { get; private set; }
        public static AccessTools.FieldRef<ContributeToFaction, GameObject> ContributeConvoyPrefab { get; private set; }
        public static AccessTools.FieldRef<GameplayUI, GameObject> GameplayUIAircraftSelectionMenu { get; private set; }
        public static AccessTools.FieldRef<GameplayUI, TMP_Text> GameplayUIAirbaseName { get; private set; }
        public static AccessTools.FieldRef<GameplayUI, Button> GameplayUISelectAircraftButton { get; private set; }
        public static AccessTools.FieldRef<CameraSelectionState, Transform> SelectionCameraTarget { get; private set; }
        public static AccessTools.FieldRef<CameraSelectionState, bool> SelectionCameraFixedTarget { get; private set; }
        public static AccessTools.FieldRef<CameraSelectionState, bool> SelectionCameraAllowMoveToDropFocus { get; private set; }
        public static AccessTools.FieldRef<CameraSelectionState, Transform> SelectionCameraFollowProxy { get; private set; }
        public static AccessTools.FieldRef<CameraSelectionState, float> SelectionCameraDistance { get; private set; }
        public static AccessTools.FieldRef<CameraSelectionState, float> SelectionCameraViewDistance { get; private set; }
        public static AccessTools.FieldRef<CameraSelectionState, float> SelectionCameraOrbitalAngle { get; private set; }

        public static AccessTools.FieldRef<RadialMenuMain, GameObject> RadialMenuObject { get; private set; }
        public static AccessTools.FieldRef<RadialMenuMain, CanvasGroup> RadialMenuCanvasGroup { get; private set; }
        public static AccessTools.FieldRef<RadialMenuMain, GameObject> RadialMenuMainContainer { get; private set; }
        public static AccessTools.FieldRef<RadialMenuMain, GameObject> RadialMenuWeaponsContainer { get; private set; }
        public static AccessTools.FieldRef<RadialMenuMain, GameObject> RadialMenuSector { get; private set; }
        public static AccessTools.FieldRef<RadialMenuMain, GameObject> RadialMenuActionPrefab { get; private set; }
        public static AccessTools.FieldRef<RadialMenuMain, RadialMenuAction> RadialMenuWeaponAction { get; private set; }
        public static AccessTools.FieldRef<RadialMenuMain, RadialMenuAction[]> RadialMenuMainActions { get; private set; }
        public static AccessTools.FieldRef<RadialMenuMain, TMP_Text> RadialMenuSelectedText { get; private set; }
        public static AccessTools.FieldRef<RadialMenuAction, Sprite> RadialMenuActionIcon { get; private set; }
        public static AccessTools.FieldRef<RadialMenuAction, RadialMenuAction.ActionType> RadialMenuActionKind { get; private set; }

        public static bool RadialMenuAvailable =>
            RadialMenuObject != null && RadialMenuCanvasGroup != null && RadialMenuMainContainer != null
            && RadialMenuWeaponsContainer != null && RadialMenuSector != null && RadialMenuActionPrefab != null
            && RadialMenuWeaponAction != null && RadialMenuMainActions != null && RadialMenuSelectedText != null
            && RadialMenuActionIcon != null && RadialMenuActionKind != null;

        public static bool SelectionCameraAvailable =>
            SelectionCameraTarget != null && SelectionCameraFixedTarget != null && SelectionCameraAllowMoveToDropFocus != null
            && SelectionCameraFollowProxy != null && SelectionCameraDistance != null && SelectionCameraViewDistance != null
            && SelectionCameraOrbitalAngle != null;

        public static bool VehicleShopAvailable =>
            VehicleDepotSpawnCooldown != null && VehicleDepotLastSpawnedTime != null && VehicleDepotSpawnTransform != null
            && VehicleDepotMoveVehicleFromDepot != null && PlayerConvoyDonationMessage != null && ContributeConvoyPrefab != null
            && GameplayUIAircraftSelectionMenu != null;

        private static readonly List<string> _missing = new List<string>();
        public static IReadOnlyList<string> Missing => _missing;

        public static bool Initialize()
        {
            _missing.Clear();

            TurretAttachedUnit = Field<Turret, Unit>("attachedUnit");
            TurretAimTurret = Method<Action<Turret, Vector3>>(typeof(Turret), "AimTurret", typeof(Vector3));
            TurretElevationTransform = Field<Turret, Transform>("elevationTransform");
            TurretTraverseAngle = Field<Turret, float>("traverseAngle");
            TurretElevationAngle = Field<Turret, float>("elevationAngle");
            TurretFireControl = OptionalField<Turret, FireControl>("fireControl");

            UnitPartCriticalPart = Field<UnitPart, bool>("criticalPart");

            GroundVehicleResetStationary = Field<GroundVehicle, bool>("resetStationary");
            GroundVehicleUpdate = Method<Action<GroundVehicle>>(typeof(GroundVehicle), "Update");

            PilotControlInputs = Field<PilotBaseState, ControlInputs>("controlInputs");

            FlightHudCanvas = Field<FlightHud, Canvas>("canvas");

            CombatHudTopRightPanel = Field<CombatHUD, GameObject>("topRightPanel");
            CombatHudCountermeasureBackground = Field<CombatHUD, GameObject>("countermeasureBackground");
            CombatHudTargetText = Field<CombatHUD, TextMeshProUGUI>("targetText");
            CombatHudTargetInfo = Field<CombatHUD, TextMeshProUGUI>("targetInfo");
            CombatHudUpdateHitMarkers = Method<Action<CombatHUD>>(typeof(CombatHUD), "UpdateHitMarkers");

            CombatHudUnitMarkerPrefab = OptionalField<CombatHUD, GameObject>("unitMarker");
            CombatHudHitMarker = OptionalField<CombatHUD, GameObject>("hitMarker");
            GunMuzzles = OptionalField<Gun, Transform[]>("muzzles");
            GunBulletSelfDestruct = OptionalField<Gun, float>("bulletSelfDestruct");
            GunMuzzleVelocity = OptionalField<Gun, float>("muzzleVelocity");
            GunGuidedProjectile = OptionalField<Gun, MissileDefinition>("guidedProjectile");
            GunProximityTimer = OptionalField<Gun, bool>("proximityTimer");
            GunFireInterval = OptionalField<Gun, float>("fireInterval");
            GunReloadTime = OptionalField<Gun, float>("reloadTime");
            GunBulletsLoaded = OptionalField<Gun, int>("bulletsLoaded");
            GunMagazines = OptionalField<Gun, int>("magazines");
            GunMagazineCapacity = OptionalField<Gun, int>("magazineCapacity");
            GunMaxMagazines = OptionalField<Gun, int>("maxMagazines");
            GroundVehicleDeployableParts = OptionalField<GroundVehicle, Array>("deployableParts");
            GroundVehicleMobile = OptionalField<GroundVehicle, bool>("mobile");
            GroundVehicleWheels = OptionalField<GroundVehicle, GameObject[]>("wheels");
            GroundVehicleTracks = OptionalField<GroundVehicle, Renderer>("tracks");
            GroundVehicleEngineIdleSound = OptionalField<GroundVehicle, AudioSource>("engineIdleSound");
            GroundVehicleEngineDriveSound = OptionalField<GroundVehicle, AudioSource>("engineDriveSound");
            GroundVehicleEngineMinPitch = OptionalField<GroundVehicle, float>("engineMinPitch");
            GroundVehicleEngineMaxPitch = OptionalField<GroundVehicle, float>("engineMaxPitch");
            GroundVehiclePathfinder = OptionalField<GroundVehicle, PathfindingAgent>("pathfinder");
            GroundVehicleObstacles = OptionalField<GroundVehicle, List<Obstacle>>("obstacles");
            GroundVehicleAnchored = OptionalField<GroundVehicle, bool>("anchored");
            PathfindingAgentWaypoints = OptionalField<PathfindingAgent, List<GlobalPosition>>("waypoints");
            PathfindingAgentNodes = OptionalField<PathfindingAgent, List<RoadPathfinding.Node>>("nodes");
            GroundVehicleAcceleration = OptionalField<GroundVehicle, float>("acceleration");
            GroundVehicleTopSpeedOnroad = OptionalField<GroundVehicle, float>("topSpeedOnroad");
            GunRecoilImpulse = OptionalField<Gun, float>("recoilImpulse");
            GroundVehicleTopSpeedOffroad = OptionalField<GroundVehicle, float>("topSpeedOffroad");
            GroundVehicleSuspensionTravel = OptionalField<GroundVehicle, float>("suspensionTravel");
            GroundVehicleSpringRate = OptionalField<GroundVehicle, float>("springRate");
            GroundVehicleDampingRate = OptionalField<GroundVehicle, float>("dampingRate");
            GroundVehicleFrictionCoef = OptionalField<GroundVehicle, float>("frictionCoef");
            MobileArtilleryAISetState = OptionalEnumSetter<MobileArtilleryAI>("currentState");
            MobileArtilleryAITargetSearch = OptionalMethod<Func<MobileArtilleryAI, UniTask>>(typeof(MobileArtilleryAI), "TargetSearch");
            FireControlAttachedUnit = OptionalField<FireControl, Unit>("attachedUnit");
            RadialMenuObject = OptionalField<RadialMenuMain, GameObject>("menuObject");
            RadialMenuCanvasGroup = OptionalField<RadialMenuMain, CanvasGroup>("canvasGroup");
            RadialMenuMainContainer = OptionalField<RadialMenuMain, GameObject>("actionsMainContainer");
            RadialMenuWeaponsContainer = OptionalField<RadialMenuMain, GameObject>("actionsWeaponsContainer");
            RadialMenuSector = OptionalField<RadialMenuMain, GameObject>("sectorObject");
            RadialMenuActionPrefab = OptionalField<RadialMenuMain, GameObject>("actionPrefab");
            RadialMenuWeaponAction = OptionalField<RadialMenuMain, RadialMenuAction>("actionWeaponPrefab");
            RadialMenuMainActions = OptionalField<RadialMenuMain, RadialMenuAction[]>("actionsMain");
            RadialMenuSelectedText = OptionalField<RadialMenuMain, TMP_Text>("selectedActionText");
            RadialMenuActionIcon = OptionalField<RadialMenuAction, Sprite>("iconSprite");
            RadialMenuActionKind = OptionalField<RadialMenuAction, RadialMenuAction.ActionType>("actionType");
            UnitDebugTarget = OptionalField<UnitDebug, TMPro.TMP_Text>("target");
            UnitDebugFollowingUnit = OptionalField<UnitDebug, Unit>("followingUnit");
            TurretAimSolver = OptionalField<Turret, AimSolver>("aimSolver");
            AimSolverArtillery = OptionalField<AimSolver, bool>("artillery");
            AimSolverDragCoef = OptionalField<AimSolver, float>("artilleryDragCoef");
            TurretMaxElevation = OptionalField<Turret, float>("maxElevation");
            HmdSpeed = OptionalField<HeadMountedDisplay, HUDApp>("speed");
            HmdAltitude = OptionalField<HeadMountedDisplay, HUDApp>("altitude");
            HmdBearing = OptionalField<HeadMountedDisplay, HUDApp>("bearing");
            SpeedGaugeText = OptionalField<SpeedGauge, TextMeshProUGUI>("airspeedDisplay");
            SpeedGaugeOverspeed = OptionalField<SpeedGauge, TextMeshProUGUI>("overspeedDisplay");
            AltitudeRadarText = OptionalField<Altitude, TextMeshProUGUI>("radarAlt");
            AltitudeAbsText = OptionalField<Altitude, TextMeshProUGUI>("absAlt");
            BearingText = OptionalField<Bearing, TextMeshProUGUI>("bearing");
            FlightHudCompass = OptionalField<FlightHud, RawImage>("compass");
            CombatHudTurretUI = OptionalField<CombatHUD, GameObject>("TurretUI");
            CombatHudWeaponStatus = OptionalField<CombatHUD, WeaponStatus>("weaponStatus");
            TurretStateCrosshairPrefab = OptionalField<HUDTurretState, GameObject>("turretCrosshairPrefab");
            TurretCrosshairCircle = OptionalField<HUDTurretCrosshair, Image>("circle");
            TurretCrosshairReadiness = OptionalField<HUDTurretCrosshair, Image>("readinessCircle");
            TurretCrosshairCross = OptionalField<HUDTurretCrosshair, Image>("crosshair");
            WeaponStatusImage = OptionalField<WeaponStatus, Image>("weaponImage");
            WeaponStatusReloadImage = OptionalField<WeaponStatus, Image>("reloadProgressImage");
            WeaponStatusSafety = OptionalField<WeaponStatus, Image>("safetyImage");
            WeaponStatusName = OptionalField<WeaponStatus, TextMeshProUGUI>("nameText");
            WeaponStatusAmmo = OptionalField<WeaponStatus, TextMeshProUGUI>("ammoText");
            WeaponStatusReload = OptionalField<WeaponStatus, RectTransform>("reloadProgress");
            WeaponStatusPanel = OptionalField<WeaponStatus, RectTransform>("panel");
            UnitDamageCredit = OptionalField<Unit, Dictionary<PersistentID, float>>("damageCredit");
            FuelGaugeBar = OptionalField<FuelGauge, Image>("fuelBar");
            FuelGaugePointer = OptionalField<FuelGauge, Image>("fuelPointer");
            FuelGaugeReading = OptionalField<FuelGauge, TextMeshProUGUI>("fuelReading");
            FuelGaugeLabel = OptionalField<FuelGauge, TextMeshProUGUI>("fuelLabel");
            FuelGaugePivot = OptionalField<FuelGauge, Transform>("fuelReadingPivot");
            FlightHudCenter = OptionalField<FlightHud, Transform>("HUDCenter");
            CombatHudCargoUI = OptionalField<CombatHUD, GameObject>("CargoUI");
            CargoCapturePanel = OptionalField<HUDCargoState, GameObject>("capturePanel");
            CargoCaptureBar = OptionalField<HUDCargoState, RectTransform>("captureBar");
            CargoCapturePanelImage = OptionalField<HUDCargoState, Image>("capturePanelImage");
            CargoCaptureBarImage = OptionalField<HUDCargoState, Image>("captureBarImage");
            CargoAirbaseTitle = OptionalField<HUDCargoState, TMP_Text>("currentAirbaseTitle");
            CargoCaptureText = OptionalField<HUDCargoState, TMP_Text>("captureText");
            CargoCaptureCanvasGroup = OptionalField<HUDCargoState, CanvasGroup>("captureCanvasGroup");
            MissileSeeker = OptionalField<Missile, MissileSeeker>("seeker");
            SeekerTargetUnit = OptionalField<MissileSeeker, Unit>("targetUnit");
            InertialSeekerUseDatalink = OptionalField<InertialSeekerShell, bool>("useDatalink");
            InertialSeekerUseLaser = OptionalField<InertialSeekerShell, bool>("useLaser");
            CombatHudThreatList = OptionalField<CombatHUD, ThreatList>("threatList");
            ThreatListItemPrefab = OptionalField<ThreatList, GameObject>("threatItemPrefab");
            ThreatItemText = OptionalField<ThreatItem, Text>("text");
            _threatAlarmTypes = AccessTools.Field(typeof(ThreatList), "alarmTypes");
            Type alarmType = AccessTools.Inner(typeof(ThreatList), "MissileAlarm");
            if (alarmType != null)
            {
                _alarmSeekerType = AccessTools.Field(alarmType, "seekerType");
                _alarmClips = AccessTools.Field(alarmType, "alarmClips");
                _alarmAlert = AccessTools.Field(alarmType, "alertClip");
            }
            if (_threatAlarmTypes == null || _alarmSeekerType == null || _alarmClips == null)
                ModLog.Warning("Optional game member not found: ThreatList missile alarms - vehicle missile warning is silent");
            VehicleDepotSpawnCooldown = OptionalField<VehicleDepot, float>("spawnCooldown");
            VehicleDepotLastSpawnedTime = OptionalField<VehicleDepot, float>("lastSpawnedTime");
            VehicleDepotSpawnTransform = OptionalField<VehicleDepot, Transform>("spawnTransform");
            VehicleDepotMoveVehicleFromDepot = OptionalMethod<Func<VehicleDepot, GroundVehicle, UniTask>>(typeof(VehicleDepot), "MoveVehicleFromDepot", typeof(GroundVehicle));
            AirbaseTaxiNetwork = OptionalField<Airbase, RoadPathfinding.RoadNetwork>("taxiNetwork");
            GameplayUIHomeAirbase = OptionalField<GameplayUI, Airbase>("homeAirbase");
            PlayerConvoyDonationMessage = OptionalMethod<Action<Player, string>>(typeof(Player), "RpcConvoyDonationMessage", typeof(string));
            ContributeConvoyPrefab = OptionalField<ContributeToFaction, GameObject>("convoySelectPrefab");
            GameplayUIAircraftSelectionMenu = OptionalField<GameplayUI, GameObject>("aircraftSelectionMenu");
            GameplayUIAirbaseName = OptionalField<GameplayUI, TMP_Text>("airbaseName");
            GameplayUISelectAircraftButton = OptionalField<GameplayUI, Button>("selectAircraftButton");
            SelectionCameraTarget = OptionalField<CameraSelectionState, Transform>("target");
            SelectionCameraFixedTarget = OptionalField<CameraSelectionState, bool>("fixedTarget");
            SelectionCameraAllowMoveToDropFocus = OptionalField<CameraSelectionState, bool>("allowMoveToDropFocus");
            SelectionCameraFollowProxy = OptionalField<CameraSelectionState, Transform>("followProxy");
            SelectionCameraDistance = OptionalField<CameraSelectionState, float>("cameraDistance");
            SelectionCameraViewDistance = OptionalField<CameraSelectionState, float>("viewDistance");
            SelectionCameraOrbitalAngle = OptionalField<CameraSelectionState, float>("orbitalAngle");
            if (!VehicleShopAvailable) ModLog.Warning("Vehicle purchase disabled: game members for it were not found");

            return _missing.Count == 0;
        }

        private static AccessTools.FieldRef<T, F> Field<T, F>(string name)
        {
            try
            {
                var fieldRef = AccessTools.FieldRefAccess<T, F>(name);
                if (fieldRef != null) return fieldRef;
            }
            catch (Exception e)
            {
                _missing.Add(typeof(T).Name + "." + name + " (" + e.Message + ")");
                return null;
            }
            _missing.Add(typeof(T).Name + "." + name);
            return null;
        }

        private static AccessTools.FieldRef<T, F> OptionalField<T, F>(string name)
        {
            try
            {
                return AccessTools.FieldRefAccess<T, F>(name);
            }
            catch (Exception e)
            {
                ModLog.Warning("Optional game member not found: " + typeof(T).Name + "." + name + " (" + e.Message + ")");
                return null;
            }
        }

        private static Action<T, int> OptionalEnumSetter<T>(string name)
        {
            FieldInfo field = AccessTools.Field(typeof(T), name);
            if (field == null || !field.FieldType.IsEnum)
            {
                ModLog.Warning("Optional game member not found: " + typeof(T).Name + "." + name + " (enum)");
                return null;
            }
            return (instance, value) => field.SetValue(instance, Enum.ToObject(field.FieldType, value));
        }

        private static D OptionalMethod<D>(Type type, string name, params Type[] parameters) where D : Delegate
        {
            try
            {
                var method = AccessTools.Method(type, name, parameters);
                if (method != null) return AccessTools.MethodDelegate<D>(method);
            }
            catch (Exception e)
            {
                ModLog.Warning("Optional game member not found: " + type.Name + "." + name + "() (" + e.Message + ")");
                return null;
            }
            ModLog.Warning("Optional game member not found: " + type.Name + "." + name + "()");
            return null;
        }

        private static D Method<D>(Type type, string name, params Type[] parameters) where D : Delegate
        {
            try
            {
                var method = AccessTools.Method(type, name, parameters);
                if (method != null) return AccessTools.MethodDelegate<D>(method);
            }
            catch (Exception e)
            {
                _missing.Add(type.Name + "." + name + "() (" + e.Message + ")");
                return null;
            }
            _missing.Add(type.Name + "." + name + "()");
            return null;
        }
    }
}
