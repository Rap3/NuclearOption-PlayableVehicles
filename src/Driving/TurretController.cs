using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class TurretController
    {
        private readonly List<Turret> _turrets = new List<Turret>();
        private readonly List<bool> _turretWasEnabled = new List<bool>();
        private readonly TargetLock _lock = new TargetLock();
        private GroundVehicle _vehicle;
        private bool _fireHeld;
        private WeaponStation _weaponTargetStation;

        public bool Silent { get; set; }
        public bool FireHeld => _fireHeld;
        public System.Action<PvNoticeKind> RemoteNotice { get; set; }

        internal const string NoRadarText = "No radar link: a friendly radar or fire control vehicle must be within 300 m";
        private const float NoRadarNoticeInterval = 3f;

        public byte FirePresses { get; private set; }

        private const float RemoteFireTimeout = 0.5f;
        private const int MaxPressesPerPacket = 16;
        private const int MaxPendingShots = 3;
        private const float PendingShotLifetime = 0.5f;
        private bool _remoteFire;
        private float _remoteInputTime;
        private bool _remotePressesKnown;
        private byte _remotePresses;
        private int _pendingShots;
        private float _pendingShotsUntil;

        private const float MaxAimDistance = 6000f;
        private const float NoHitAimDistance = 4000f;
        private const float SolveInterval = 0.1f;

        private Vector3 _crosshairOffset;
        private bool _crosshairReachable = true;
        private Vector3 _leadVector;
        private Vector3 _interceptOffset;
        private bool _interceptReachable = true;
        private bool _hasGunSolution;
        private float _nextSolveTime;
        private int _solvedStationIndex = -1;
        private Unit _solvedTarget;

        private static bool Zoomed => VehicleSession.IsActive && VehicleSession.ViewCamera != null && VehicleSession.ViewCamera.IsZoomed;
        private static float TurretAimTauActive => Zoomed ? TurretAimTau * 0.4f : TurretAimTau;

        private sealed class GunState
        {
            public float Yaw;
            public float Pitch;
            public int Frame;
        }

        private readonly Dictionary<Turret, GunState> _gunState = new Dictionary<Turret, GunState>();
        private readonly List<Turret> _heldTurrets = new List<Turret>();
        private readonly List<Vector3> _heldDirections = new List<Vector3>();

        public Turret PrimaryTurret => _turrets.Count > 0 ? _turrets[0] : null;
        public Transform SelectedBarrel => StationBarrel(SelectedStation);
        public int SelectedStationIndex { get; private set; }
        public Vector3 AimRayDir => _aimRayDir;
        public bool AimRayValid => _aimRayValid;
        public Vector3 CurrentAimPoint { get; private set; }
        public bool HasAimHit { get; private set; }

        public Vector3 TurretForward { get; private set; }

        public Unit LockedTarget => _lock.Target;
        public bool IsTracking => _lock.IsLocked && ModConfig.LockAutoTrack.Value;
        public Vector3 LeadPoint { get; private set; }
        public Vector3 AimTargetPoint { get; private set; }
        public Vector3 StationAimPoint { get; private set; }
        private bool AimTargetKnown => IsTracking || HasAimHit;

        public enum RangeState
        {
            Unknown,
            InRange,
            TooClose,
            BeyondMaxRange,
            OutOfReach,
        }

        public RangeState Range { get; private set; }
        public float AimDistance { get; private set; }
        public float MinRange { get; private set; }
        public float MaxRange { get; private set; }

        public Unit MissionTarget { get; private set; }
        public FireMode MissionMode { get; private set; }
        public ArtillerySolver.Solution MissionSolution { get; private set; }
        public bool MissionActive => MissionTarget != null && MissionMode != FireMode.None;

        private const float MissionSolveInterval = 0.5f;
        private float _nextNoRadarNotice;
        private float _nextMissionSolve;
        private Unit _missionSolvedTarget;
        private FireMode _missionSolvedMode;

        public bool HasArtillery => ArtilleryTurret(SelectedStation) != null;
        public FireMissionKind MissionKind => FireMissions.KindOf(SelectedStation);

        public void SetMission(Unit target, FireMode mode)
        {
            MissionTarget = target;
            MissionMode = target != null ? mode : FireMode.None;
        }

        public void ClearMission(string reason)
        {
            if (!MissionActive) return;
            MissionTarget = null;
            MissionMode = FireMode.None;
            if (reason != null && !Silent) ModLog.Notify(reason);
        }

        public Turret ArtilleryTurret(WeaponStation station)
        {
            if (station == null || station.Turrets == null) return null;
            foreach (Turret turret in station.Turrets)
            {
                if (ArtillerySolver.IsArtillery(turret)) return turret;
            }
            return null;
        }

        public WeaponStation SelectedStation
        {
            get
            {
                List<WeaponStation> stations = _vehicle != null ? _vehicle.weaponStations : null;
                return stations != null && SelectedStationIndex >= 0 && SelectedStationIndex < stations.Count ? stations[SelectedStationIndex] : null;
            }
        }

        public void Attach(GroundVehicle vehicle)
        {
            _vehicle = vehicle;
            RestoreTraverseRates();
            _turrets.Clear();
            _turretWasEnabled.Clear();
            _lock.Clear();
            MissionTarget = null;
            MissionMode = FireMode.None;
            _solvedStationIndex = -1;
            _weaponTargetStation = null;
            _fireHeld = false;
            _remoteFire = false;
            _remotePressesKnown = false;
            _pendingShots = 0;
            _aimDirSmooth.Clear();
            _aimDirCmd.Clear();
            _aimRayValid = false;
            _aimDistanceValid = false;
            _heldTurrets.Clear();
            _heldDirections.Clear();
            _gunState.Clear();

            foreach (Turret turret in vehicle.GetComponentsInChildren<Turret>(true))
            {
                if (turret == null) continue;
                _turrets.Add(turret);
                _turretWasEnabled.Add(turret.enabled);
                turret.enabled = true;
            }

            ScaleTraverseRates();
            SelectedStationIndex = 0;
        }

        private readonly List<float> _baseTraverseRates = new List<float>();
        private readonly List<float> _baseElevationRates = new List<float>();

        private void ScaleTraverseRates()
        {
            _baseTraverseRates.Clear();
            _baseElevationRates.Clear();
            bool available = GameAccess.TurretTraverseRate != null && GameAccess.TurretElevationRate != null;
            float scale = Mathf.Clamp(ModConfig.TurretTraverseScale.Value, 0.25f, 3f);
            bool apply = available && !Mathf.Approximately(scale, 1f);
            for (int i = 0; i < _turrets.Count; i++)
            {
                Turret turret = _turrets[i];
                float traverse = available && turret != null ? GameAccess.TurretTraverseRate(turret) : 0f;
                float elevation = available && turret != null ? GameAccess.TurretElevationRate(turret) : 0f;
                _baseTraverseRates.Add(traverse);
                _baseElevationRates.Add(elevation);
                if (!apply || turret == null) continue;
                GameAccess.TurretTraverseRate(turret) = traverse * scale;
                GameAccess.TurretElevationRate(turret) = elevation * scale;
            }
        }

        private void RestoreTraverseRates()
        {
            bool available = GameAccess.TurretTraverseRate != null && GameAccess.TurretElevationRate != null;
            if (available)
            {
                int count = Mathf.Min(_turrets.Count, _baseTraverseRates.Count);
                for (int i = 0; i < count; i++)
                {
                    Turret turret = _turrets[i];
                    if (turret == null) continue;
                    GameAccess.TurretTraverseRate(turret) = _baseTraverseRates[i];
                    GameAccess.TurretElevationRate(turret) = _baseElevationRates[i];
                }
            }
            _baseTraverseRates.Clear();
            _baseElevationRates.Clear();
        }

        public void Release()
        {
            RestoreTraverseRates();
            for (int i = 0; i < _turrets.Count; i++)
            {
                if (_turrets[i] != null) _turrets[i].enabled = _turretWasEnabled[i];
            }
            PointDefence.Unmark(_turrets);
            _turrets.Clear();
            _turretWasEnabled.Clear();
            _lock.Clear();
            MissionTarget = null;
            MissionMode = FireMode.None;
            SetWeaponTarget(null, null);
            _heldTurrets.Clear();
            _heldDirections.Clear();
            _gunState.Clear();
            _fireHeld = false;
            _remoteFire = false;
            _pendingShots = 0;
            RemoteNotice = null;
            _vehicle = null;
        }

        public void AimTurrets()
        {
            _heldTurrets.Clear();
            _heldDirections.Clear();
            _lock.Validate(_vehicle, !Silent);
            WeaponStation station = SelectedStation;
            _lock.TargetAllowed = unit => !SamOnlyAir(_vehicle) || unit is Aircraft || unit is Missile;
            bool hasGun = Ballistics.TryGetGunProfile(station, StationBarrel(station), out GunProfile profile);

            UpdateSolutions(hasGun, profile);
            UpdateAimTarget(hasGun);
            UpdateRange(station, hasGun);

            bool applyBallistics = hasGun && ModConfig.BallisticComputer.Value && AimTargetKnown;
            Vector3 ballisticOffset = IsTracking ? _interceptOffset : _crosshairOffset;
            StationAimPoint = applyBallistics ? AimTargetPoint + ballisticOffset : AimTargetPoint;
            bool mission = UpdateMission(station, hasGun, profile);

            if (VerticalLaunchVehicle(_vehicle) && LauncherArmPose.AppliesTo(_vehicle))
            {
                LauncherArmPose.Tick(_vehicle);
                if (_turrets.Count > 0 && _turrets[0] != null) TurretForward = _turrets[0].transform.forward;
            }

            for (int i = 0; i < _turrets.Count; i++)
            {
                Turret turret = _turrets[i];
                if (turret == null || !turret.gameObject.activeInHierarchy) continue;
                if (PointDefence.Mark(turret, PointDefence.IsAutonomous(_vehicle, station, turret)) && PointDefence.RunsVanilla(turret)) continue;

                bool stationTurret = station != null && station.Turrets != null && station.Turrets.Contains(turret);
                if (VerticalLaunchVehicle(_vehicle)) continue;
                if (mission && stationTurret && ArtillerySolver.IsArtillery(turret))
                {
                    GameAccess.TurretAimTurret(turret, MissionSolution.Direction);
                    HoldBarrel(turret, MissionSolution.Direction);
                    if (i == 0) TurretForward = turret.transform.forward;
                    continue;
                }
                Vector3 target = stationTurret ? StationAimPoint : CurrentAimPoint;
                Transform elevationAxis = GameAccess.TurretElevationTransform != null ? GameAccess.TurretElevationTransform(turret) : null;
                Vector3 aimDir = target - (elevationAxis != null ? elevationAxis.position : turret.transform.position);
                if (!IsTracking && _aimRayValid && !ModConfig.AimAtCrosshairPoint.Value)
                {
                    Vector3 corr = stationTurret && applyBallistics ? ballisticOffset : Vector3.zero;
                    aimDir = _aimRayDir * aimDir.magnitude + corr;
                }
                if (_aimDirSmooth.TryGetValue(turret, out Vector3 prevDir))
                {
                    aimDir = Vector3.Lerp(prevDir, aimDir,
                        1f - Mathf.Exp(-Time.fixedDeltaTime / TurretAimTauActive));
                }
                _aimDirSmooth[turret] = aimDir;
                if (_aimDirCmd.TryGetValue(turret, out Vector3 lastCmd)
                    && Vector3.Angle(aimDir, lastCmd) < TurretDeadzoneDegrees)
                {
                    aimDir = lastCmd;
                }
                else
                {
                    _aimDirCmd[turret] = aimDir;
                }
                GameAccess.TurretAimTurret(turret, aimDir);
                HoldBarrel(turret, aimDir);

                if (i == 0) TurretForward = turret.transform.forward;
            }
        }

        private void HoldBarrel(Turret turret, Vector3 aimDirection)
        {
            if (aimDirection.sqrMagnitude < 1e-6f) return;
            _heldTurrets.Add(turret);
            _heldDirections.Add(aimDirection.normalized);
        }

        public void StabilizeTurrets(float dt)
        {
            if (_heldTurrets.Count == 0 || dt <= 0f) return;
            for (int i = 0; i < _heldTurrets.Count; i++)
            {
                Turret turret = _heldTurrets[i];
                if (turret == null) continue;
                Transform turretTransform = turret.transform;
                Transform hull = turretTransform.parent;
                if (hull == null) continue;
                Transform elevation = GameAccess.TurretElevationTransform != null ? GameAccess.TurretElevationTransform(turret) : null;

                Vector3 inHull = hull.InverseTransformDirection(_heldDirections[i]);
                float targetYaw = Mathf.Atan2(inHull.x, inHull.z) * Mathf.Rad2Deg;
                float targetPitch = -Mathf.Asin(Mathf.Clamp(inHull.y, -1f, 1f)) * Mathf.Rad2Deg;

                GunState state;
                int frame = Time.frameCount;
                if (!_gunState.TryGetValue(turret, out state) || state.Frame != frame - 1)
                {
                    state = new GunState
                    {
                        Yaw = turretTransform.localEulerAngles.y,
                        Pitch = elevation != null ? elevation.localEulerAngles.x : 0f,
                        Frame = frame
                    };
                    _gunState[turret] = state;
                }
                else
                {
                    state.Frame = frame;
                }

                float traverseRate = GameAccess.TurretTraverseRate != null ? GameAccess.TurretTraverseRate(turret) : 38f;
                float elevationRate = GameAccess.TurretElevationRate != null ? GameAccess.TurretElevationRate(turret) : 30f;
                state.Yaw = Mathf.MoveTowardsAngle(state.Yaw, targetYaw, Mathf.Max(traverseRate, 0f) * dt);
                float range = GameAccess.TurretTraverseRange != null ? GameAccess.TurretTraverseRange(turret) : 360f;
                if (range < 360f) state.Yaw = Mathf.Clamp(state.Yaw, -range, range);
                turretTransform.localEulerAngles = new Vector3(0f, state.Yaw, 0f);

                if (elevation != null)
                {
                    state.Pitch = Mathf.MoveTowards(state.Pitch, targetPitch, Mathf.Max(elevationRate, 0f) * dt);
                    float maxElevation = GameAccess.TurretMaxElevation != null ? GameAccess.TurretMaxElevation(turret) : 0f;
                    float minElevation = GameAccess.TurretMinElevation != null ? GameAccess.TurretMinElevation(turret) : 0f;
                    state.Pitch = Mathf.Clamp(state.Pitch, -maxElevation, -minElevation);
                    elevation.localEulerAngles = new Vector3(state.Pitch, 0f, 0f);
                    if (GameAccess.TurretElevationAngle != null) GameAccess.TurretElevationAngle(turret) = state.Pitch;
                }

                if (GameAccess.TurretTraverseAngle != null) GameAccess.TurretTraverseAngle(turret) = state.Yaw;
            }
        }

        private static bool SamOnlyAir(GroundVehicle vehicle)
        {
            VehicleDefinition def = vehicle != null ? vehicle.definition as VehicleDefinition : null;
            return def != null && (def.vehicleType == VehicleType.R_SAM || def.vehicleType == VehicleType.IR_SAM);
        }

        private bool UpdateMission(WeaponStation station, bool hasGun, in GunProfile profile)
        {
            if (!MissionActive) return false;
            if (MissionTarget == null || MissionTarget.disabled)
            {
                ClearMission("Fire mission complete: target destroyed");
                return false;
            }
            if (!DeployedForMission(_vehicle))
            {
                ClearMission("Fire mission cancelled: vehicle stowed");
                return false;
            }
            FireMissionKind kind = FireMissions.KindOf(station);
            if (kind == FireMissionKind.None || (kind == FireMissionKind.Artillery) != (MissionMode != FireMode.Missile))
            {
                ClearMission("Fire mission cancelled: weapon changed");
                return false;
            }
            if (kind != FireMissionKind.Artillery)
            {
                StationAimPoint = TargetLock.AimPoint(MissionTarget);
                return false;
            }
            Turret artillery = ArtilleryTurret(station);

            Transform barrel = StationBarrel(station);
            Vector3 muzzle = hasGun && profile.Muzzle != null ? profile.Muzzle.position : (barrel != null ? barrel.position : artillery.transform.position);
            bool due = Time.time >= _nextMissionSolve || _missionSolvedTarget != MissionTarget || _missionSolvedMode != MissionMode;
            if (due)
            {
                _nextMissionSolve = Time.time + MissionSolveInterval;
                _missionSolvedTarget = MissionTarget;
                _missionSolvedMode = MissionMode;
                MissionSolution = SolveArtillery(artillery, station.WeaponInfo, hasGun, profile, muzzle, MissionTarget, MissionMode);
            }
            ArtillerySolver.Solution solution = MissionSolution;
            if (solution.Status != ArtillerySolver.Status.Ready) return false;

            StationAimPoint = muzzle + solution.Direction.normalized * 1000f;
            return true;
        }

        public static bool DeployedForMission(GroundVehicle vehicle)
        {
            return vehicle != null && (VehicleDeploy.IsDeployed(vehicle) || !VehicleDeploy.CanDeploy(vehicle) || VehicleDriver.IsStatic(vehicle));
        }

        public ArtillerySolver.Solution PreviewMission(Unit target, FireMode mode)
        {
            WeaponStation station = SelectedStation;
            FireMissionKind kind = FireMissions.KindOf(station);
            if (kind == FireMissionKind.Ballistic || kind == FireMissionKind.AirDefense) return FireMissions.Check(kind, _vehicle, station, target);
            Turret artillery = ArtilleryTurret(station);
            if (artillery == null || target == null) return new ArtillerySolver.Solution { Status = ArtillerySolver.Status.OutOfRange };

            Transform barrel = StationBarrel(station);
            bool hasGun = Ballistics.TryGetGunProfile(station, barrel, out GunProfile profile);
            Vector3 muzzle = hasGun ? profile.Muzzle.position : (barrel != null ? barrel.position : artillery.transform.position);
            return SolveArtillery(artillery, station.WeaponInfo, hasGun, profile, muzzle, target, mode);
        }

        public static ArtillerySolver.Solution SolveArtillery(Turret artillery, WeaponInfo info, bool hasGun, in GunProfile profile,
            Vector3 muzzle, Unit target, FireMode mode)
        {
            Vector3 point = TargetLock.AimPoint(target);
            if (!hasGun) return ArtillerySolver.Solve(artillery, info, muzzle, point, mode);

            ArtillerySolver.Solution solution = ArtillerySolver.SolveSimulated(profile, artillery, point, mode);
            Vector3 velocity = TargetLock.Velocity(target);
            if (solution.Status == ArtillerySolver.Status.Ready && velocity.sqrMagnitude > 1f)
            {
                solution = ArtillerySolver.SolveSimulated(profile, artillery, point + velocity * solution.TimeOfFlight, mode);
            }
            return solution;
        }

        private void UpdateSolutions(bool hasGun, in GunProfile profile)
        {
            Unit target = _lock.Target;
            bool due = Time.time >= _nextSolveTime || _solvedStationIndex != SelectedStationIndex || _solvedTarget != target;
            if (!due) return;

            _nextSolveTime = Time.time + SolveInterval;
            _solvedStationIndex = SelectedStationIndex;
            _solvedTarget = target;
            _hasGunSolution = hasGun;

            _crosshairOffset = Vector3.zero;
            _crosshairReachable = true;
            _leadVector = Vector3.zero;
            _interceptOffset = Vector3.zero;
            _interceptReachable = true;
            if (!hasGun) return;

            if (HasAimHit)
            {
                _crosshairReachable = Ballistics.Solve(profile, CurrentAimPoint, out _crosshairOffset, out _);
            }
            if (target != null)
            {
                Vector3 targetPoint = TargetLock.AimPoint(target);
                _interceptReachable = Ballistics.SolveIntercept(profile, targetPoint, TargetLock.Velocity(target),
                    out Vector3 intercept, out _interceptOffset);
                _leadVector = intercept - targetPoint;
            }
        }

        private void UpdateAimTarget(bool hasGun)
        {
            Unit target = _lock.Target;
            LeadPoint = target != null ? TargetLock.AimPoint(target) + (hasGun ? _leadVector : Vector3.zero) : CurrentAimPoint;
            AimTargetPoint = IsTracking ? LeadPoint : CurrentAimPoint;
        }

        private void UpdateRange(WeaponStation station, bool hasGun)
        {
            WeaponInfo info = station != null ? station.WeaponInfo : null;
            MinRange = info != null ? info.targetRequirements.minRange : 0f;
            MaxRange = info != null ? info.targetRequirements.maxRange : 0f;

            Transform barrel = StationBarrel(station);
            Vector3 origin = barrel != null ? barrel.position : (_vehicle != null ? _vehicle.transform.position : AimTargetPoint);
            AimDistance = Vector3.Distance(origin, AimTargetPoint);

            bool reachable = !hasGun || !_hasGunSolution || (IsTracking ? _interceptReachable : _crosshairReachable);
            if (!AimTargetKnown) Range = RangeState.Unknown;
            else if (!reachable) Range = RangeState.OutOfReach;
            else if (MaxRange > 0f && AimDistance > MaxRange) Range = RangeState.BeyondMaxRange;
            else if (MinRange > 0f && AimDistance < MinRange) Range = RangeState.TooClose;
            else Range = RangeState.InRange;
        }

        private Transform StationBarrel(WeaponStation station)
        {
            Turret turret = station != null && station.Turrets != null && station.Turrets.Count > 0 && station.Turrets[0] != null
                ? station.Turrets[0]
                : PrimaryTurret;
            if (turret == null) return null;
            Transform barrel = GameAccess.TurretElevationTransform(turret);
            return barrel != null ? barrel : turret.transform;
        }

        private static bool VerticalLaunchVehicle(GroundVehicle vehicle)
        {
            if (vehicle == null) return false;
            string name = vehicle.unitName;
            string prefab = vehicle.name;
            return name == "MSV R9 Stratolance Launcher"
                || name == "MSV Ballistic Missile Launcher"
                || name == "MSV Nuclear Ballistic Missile Launcher"
                || name == "HLT Hypersonic Missile Launcher" || prefab == "Aryx_HLT_HSM"
                || name == "HLT Hypersonic Nuclear Missile Launcher" || prefab == "Aryx_HLT_HSM_Nuclear"
                || name == "HLT High Altitude Intercept Launcher" || prefab == "Aryx_HLT_HAIL";
        }

        public void ReadLocalInput()
        {
            if (InputGate.ActionClicked(GameActions.NextWeapon)) SwitchWeapon(1);
            else if (InputGate.ActionClicked(GameActions.PreviousWeapon)) SwitchWeapon(-1);
            else if (InputGate.KeyDown(KeyCode.Alpha1)) SetWeaponSlot(0);
            else if (InputGate.KeyDown(KeyCode.Alpha2)) SetWeaponSlot(1);
            else if (InputGate.KeyDown(KeyCode.Alpha3)) SetWeaponSlot(2);

            if (ProjectileCam.IsActive)
            {
                SetLocalFire(false);
                return;
            }

            UpdateAimPoint();

            if (InputGate.ActionClicked(GameActions.TargetSelect)) _lock.Select(_vehicle, Camera.main);
            if (InputGate.ActionClicked(GameActions.TargetCancel))
            {
                _lock.Release();
                ClearMission("Fire mission cancelled");
            }

            SetLocalFire(InputGate.ActionHeld(GameActions.Fire));
        }

        private void SetLocalFire(bool held)
        {
            if (held && !_fireHeld)
            {
                FirePresses++;
                if (NetBridge.IsClientOnly) ProjectileCam.RecordClientPrediction(SelectedStation);
            }
            _fireHeld = held;
        }

        public void ApplyRemoteInput(Vector3 aimPoint, Vector3 aimDir, bool hasAimHit, int stationIndex, Unit lockTarget, bool fire, byte firePresses,
            Unit missionTarget, FireMode missionMode)
        {
            CurrentAimPoint = aimPoint;
            HasAimHit = hasAimHit;
            _aimRayDir = aimDir.sqrMagnitude > 1e-4f ? aimDir.normalized : Vector3.forward;
            _aimRayValid = true;
            SetWeaponSlot(stationIndex);
            _lock.Set(_vehicle, lockTarget);
            ApplyRemoteFire(fire, firePresses);
            bool sameTarget = missionTarget != null && missionTarget == MissionTarget;
            if (missionTarget != null && !(sameTarget
                ? !missionTarget.disabled && missionTarget.NetworkHQ != _vehicle.NetworkHQ
                : TargetLock.IsKnownForeignTarget(missionTarget, _vehicle)))
            {
                missionTarget = null;
            }
            SetMission(missionTarget, missionMode);
        }

        private void ApplyRemoteFire(bool fire, byte firePresses)
        {
            _remoteFire = true;
            _remoteInputTime = Time.unscaledTime;
            _fireHeld = fire;

            int presses = _remotePressesKnown ? (byte)(firePresses - _remotePresses) : 0;
            _remotePressesKnown = true;
            _remotePresses = firePresses;
            if (presses > MaxPressesPerPacket) presses = 0;
            if (fire && presses > 0) presses--;
            if (presses <= 0) return;
            _pendingShots = Mathf.Min(_pendingShots + presses, MaxPendingShots);
            _pendingShotsUntil = Time.unscaledTime + PendingShotLifetime;
        }

        public void ClearFire()
        {
            _fireHeld = false;
            _pendingShots = 0;
        }

        public void TryFire()
        {
            if (_remoteFire && Time.unscaledTime - _remoteInputTime > RemoteFireTimeout) ClearFire();
            if (_pendingShots > 0 && Time.unscaledTime > _pendingShotsUntil) _pendingShots = 0;
            if (!_fireHeld && _pendingShots == 0) return;
            if (FireSelectedWeapon() && _pendingShots > 0) _pendingShots--;
        }

        private void UpdateAimPoint()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Ray ray = VehicleCamera.ViewRay(mainCam);
            _aimRayDir = ray.direction;
            _aimRayValid = true;
            float distance;
            if (AimRaycast.Cast(ray.origin, ray.direction, MaxAimDistance, _vehicle.transform, out RaycastHit hit))
            {
                distance = hit.distance;
                HasAimHit = true;
            }
            else
            {
                distance = NoHitAimDistance;
                HasAimHit = false;
            }

            if (!_aimDistanceValid)
            {
                _aimDistanceSmooth = distance;
                _aimDistanceValid = true;
            }
            else
            {
                _aimDistanceSmooth = Mathf.MoveTowards(_aimDistanceSmooth, distance, AimDistanceRate * Time.deltaTime);
            }
            CurrentAimPoint = ray.origin + ray.direction * _aimDistanceSmooth;
        }

        private const float MarkerInterval = 0.05f;
        private const float MarkerIdleInterval = 0.25f;
        private const float MarkerProfileLifetime = 1f;
        private const float MarkerStillMoveSqr = 1e-4f;
        private const float MarkerStillAngle = 0.02f;
        private WeaponStation _markerStation;
        private bool _markerHasGun;
        private GunProfile _markerProfile;
        private float _markerProfileAt = float.NegativeInfinity;
        private Transform _markerMuzzle;
        private bool _markerValid;
        private float _markerAt;
        private Vector3 _markerOrigin;
        private Quaternion _markerRotation;
        private Vector3 _markerPoint;
        private bool _markerHit;
        private const float MarkerSmoothTau = 0.12f;
        private Vector3 _markerLocalOffset;
        private Vector3 _markerStage1;
        private Vector3 _markerStage2;
        private Transform _markerSmoothMuzzle;
        private bool _markerSmoothValid;
        private const float AimDistanceRate = 1200f;
        private float _aimDistanceSmooth;
        private bool _aimDistanceValid;
        private Vector3 _aimRayDir;
        private bool _aimRayValid;
        private const float TurretAimTau = 0.08f;
        private readonly Dictionary<Turret, Vector3> _aimDirSmooth = new Dictionary<Turret, Vector3>();
        private const float TurretDeadzoneDegrees = 0.05f;
        private readonly Dictionary<Turret, Vector3> _aimDirCmd = new Dictionary<Turret, Vector3>();

        public bool ImpactHit => _markerHit;

        private bool MarkerProfile(WeaponStation station, Transform barrel, float now, out GunProfile profile)
        {
            bool stale = station != _markerStation || now - _markerProfileAt >= MarkerProfileLifetime
                || (_markerHasGun && _markerProfile.Muzzle == null);
            if (stale)
            {
                _markerStation = station;
                _markerProfileAt = now;
                _markerHasGun = Ballistics.TryGetGunProfile(station, barrel, out _markerProfile);
            }
            profile = _markerProfile;
            return _markerHasGun;
        }

        public bool TryGetGunMarker(float toleranceDegrees, out Vector3 point, out bool onTarget)
        {
            WeaponStation station = SelectedStation;
            Transform barrel = StationBarrel(station);
            Transform ignore = _vehicle != null ? _vehicle.transform : null;
            float now = Time.unscaledTime;
            onTarget = false;

            bool hasGun = MarkerProfile(station, barrel, now, out GunProfile profile);
            Transform muzzle = hasGun ? profile.Muzzle : barrel;
            if (muzzle == null)
            {
                _markerValid = false;
                point = Vector3.zero;
                return false;
            }

            Vector3 origin = muzzle.position;
            Quaternion rotation = muzzle.rotation;
            bool reuse = false;
            point = default;
            if (_markerValid && muzzle == _markerMuzzle && station == _markerStation)
            {
                bool still = (origin - _markerOrigin).sqrMagnitude < MarkerStillMoveSqr
                    && Quaternion.Angle(rotation, _markerRotation) < MarkerStillAngle;
                if (now - _markerAt < (still ? MarkerIdleInterval : MarkerInterval))
                {
                    point = origin + rotation * Quaternion.Inverse(_markerRotation) * (_markerPoint - _markerOrigin);
                    reuse = true;
                }
            }

            if (!reuse)
            {
                if (hasGun)
                {
                    _markerHit = Ballistics.PredictImpact(profile, ignore, out point, out _);
                }
                else
                {
                    _markerHit = AimRaycast.Cast(origin, barrel.forward, MaxAimDistance, ignore, out RaycastHit hit);
                    point = _markerHit ? hit.point : origin + barrel.forward * NoHitAimDistance;
                }
                _markerValid = true;
                _markerMuzzle = muzzle;
                _markerAt = now;
                _markerOrigin = origin;
                _markerRotation = rotation;
                _markerPoint = point;
            }

            if (point.sqrMagnitude > 0.0001f)
            {
                Vector3 local = Quaternion.Inverse(rotation) * (point - origin);
                bool jump = (_markerLocalOffset - local).sqrMagnitude > 2500f;
                if (_markerSmoothValid && muzzle == _markerSmoothMuzzle && !jump)
                {
                    float a = 1f - Mathf.Exp(-Time.unscaledDeltaTime / MarkerSmoothTau);
                    Vector3 previous = _markerLocalOffset;
                    _markerStage1 = Vector3.Lerp(_markerStage1, local, a);
                    _markerStage2 = Vector3.Lerp(_markerStage2, _markerStage1, a);
                    Vector3 extrapolated = 2f * _markerStage1 - _markerStage2;
                    _markerLocalOffset = new Vector3(
                        Mathf.Clamp(extrapolated.x, Mathf.Min(previous.x, local.x), Mathf.Max(previous.x, local.x)),
                        Mathf.Clamp(extrapolated.y, Mathf.Min(previous.y, local.y), Mathf.Max(previous.y, local.y)),
                        Mathf.Clamp(extrapolated.z, Mathf.Min(previous.z, local.z), Mathf.Max(previous.z, local.z)));
                }
                else
                {
                    _markerLocalOffset = local;
                    _markerStage1 = local;
                    _markerStage2 = local;
                    _markerSmoothMuzzle = muzzle;
                    _markerSmoothValid = true;
                }
                point = origin + rotation * _markerLocalOffset;
            }
            else
            {
                _markerSmoothValid = false;
            }

            Vector3 toAim = AimTargetPoint - origin;
            Vector3 toImpact = point - origin;
            onTarget = AimTargetKnown && toAim.sqrMagnitude > 1f && toImpact.sqrMagnitude > 1f
                && Vector3.Angle(toImpact, toAim) <= toleranceDegrees;
            return true;
        }

        private void SwitchWeapon(int delta)
        {
            int count = _vehicle.weaponStations?.Count ?? 0;
            if (count <= 1) return;
            SelectedStationIndex = (SelectedStationIndex + delta + count) % count;
        }

        public void SetWeaponSlot(int index)
        {
            if (_vehicle.weaponStations == null || index < 0 || index >= _vehicle.weaponStations.Count) return;
            SelectedStationIndex = index;
        }

        private static bool StationIntact(WeaponStation station)
        {
            if (station == null || station.Weapons == null) return true;
            for (int i = 0; i < station.Weapons.Count; i++)
            {
                Weapon weapon = station.Weapons[i];
                if (weapon == null) return false;
                UnitPart part = weapon.GetComponentInParent<UnitPart>();
                if (part == null) continue;
                if (part.IsDetached() || part.hitPoints <= 0f) return false;
            }
            return true;
        }

        private bool FireSelectedWeapon()
        {
            if (_vehicle.weaponStations == null || _vehicle.weaponStations.Count == 0) return false;

            if (SelectedStationIndex >= _vehicle.weaponStations.Count)
            {
                SelectedStationIndex = 0;
            }

            WeaponStation station = _vehicle.weaponStations[SelectedStationIndex];
            if (station == null || !station.Ready()) return false;
            if (!StationIntact(station))
            {
                if (Time.time >= _nextNoRadarNotice)
                {
                    _nextNoRadarNotice = Time.time + NoRadarNoticeInterval;
                    if (!Silent) ModLog.Notify("Weapon destroyed");
                }
                return false;
            }
            if (FireMissions.NeedsRadar(station) && !FireMissions.HasRadar(_vehicle))
            {
                if (Time.time >= _nextNoRadarNotice)
                {
                    _nextNoRadarNotice = Time.time + NoRadarNoticeInterval;
                    if (!Silent) ModLog.Notify(NoRadarText);
                    else RemoteNotice?.Invoke(PvNoticeKind.NoRadar);
                }
                return false;
            }
            if (VerticalLaunchVehicle(_vehicle)
                && (!VehicleDeploy.IsDeployed(_vehicle) || LauncherArmPose.InProgress(_vehicle)))
            {
                if (Time.time >= _nextNoRadarNotice)
                {
                    _nextNoRadarNotice = Time.time + NoRadarNoticeInterval;
                    if (!Silent) ModLog.Notify("Deploy " + _vehicle.unitName + " to fire");
                }
                return false;
            }

            if (station.Weapons != null)
            {
                for (int w = 0; w < station.Weapons.Count; w++)
                {
                    if (station.Weapons[w] != null) station.Weapons[w].Safety = false;
                }
            }

            Unit target = MissionActive ? MissionTarget : _lock.Target;
            if (target == null && HasProximityFuse(station)) target = FuseTarget(station);
            SetWeaponTarget(station, target);
            station.Fire(_vehicle, target);
            return true;
        }

        private const float FuseConeDegrees = 15f;
        private const float FuseRetargetInterval = 0.2f;
        private WeaponStation _fuseStation;
        private Unit _fuseTarget;
        private float _nextFuseSearch;

        internal static bool HasProximityFuse(WeaponStation station)
        {
            if (station == null || station.Weapons == null || GameAccess.GunProximityTimer == null) return false;
            foreach (Weapon weapon in station.Weapons)
            {
                if (weapon is Gun gun && GameAccess.GunProximityTimer(gun)) return true;
            }
            return false;
        }

        private Unit FuseTarget(WeaponStation station)
        {
            if (station == _fuseStation && Time.time < _nextFuseSearch && _fuseTarget != null && !_fuseTarget.disabled) return _fuseTarget;
            _fuseStation = station;
            _nextFuseSearch = Time.time + FuseRetargetInterval;
            _fuseTarget = null;

            FactionHQ hq = _vehicle.NetworkHQ;
            Transform barrel = StationBarrel(station);
            if (hq == null || barrel == null) return null;
            float maxRange = station.WeaponInfo != null && station.WeaponInfo.targetRequirements.maxRange > 0f
                ? station.WeaponInfo.targetRequirements.maxRange
                : 5000f;
            Vector3 origin = barrel.position;
            Vector3 forward = barrel.forward;
            float best = FuseConeDegrees;
            foreach (KeyValuePair<PersistentID, TrackingInfo> pair in hq.trackingDatabase)
            {
                if (pair.Value == null || !pair.Value.TryGetUnit(out Unit unit)) continue;
                if (unit == null || unit.disabled || unit.NetworkHQ == hq || !(unit is Aircraft || unit is Missile)) continue;
                Vector3 toTarget = unit.transform.position - origin;
                if (toTarget.sqrMagnitude > maxRange * maxRange) continue;
                float angle = Vector3.Angle(forward, toTarget);
                if (angle < best)
                {
                    best = angle;
                    _fuseTarget = unit;
                }
            }
            return _fuseTarget;
        }

        private void SetWeaponTarget(WeaponStation station, Unit target)
        {
            if (_weaponTargetStation != null && _weaponTargetStation != station) ApplyWeaponTarget(_weaponTargetStation, TurretTarget(_weaponTargetStation));
            if (station != null) ApplyWeaponTarget(station, target);
            _weaponTargetStation = station;
        }

        private static Unit TurretTarget(WeaponStation station)
        {
            if (station.Turrets == null) return null;
            foreach (Turret turret in station.Turrets)
            {
                Unit target = turret != null ? turret.GetTarget() : null;
                if (target != null) return target;
            }
            return null;
        }

        private static void ApplyWeaponTarget(WeaponStation station, Unit target)
        {
            if (station.Weapons == null) return;
            for (int w = 0; w < station.Weapons.Count; w++)
            {
                if (station.Weapons[w] != null) station.Weapons[w].SetTarget(target);
            }
        }
    }
}
