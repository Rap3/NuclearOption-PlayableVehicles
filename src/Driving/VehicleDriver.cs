using UnityEngine;

namespace PlayableVehicles
{
    public class VehicleDriver : MonoBehaviour
    {
        public enum DriveMode
        {
            Local,
            ClientDriver,
            ServerForClient,
        }

        private const float RemoteInputTimeout = 1f;
        private const float PoseFollowGain = 5f;
        private const float PoseSnapDistance = 30f;
        private const float MaxPoseExtrapolation = 0.25f;
        private const float MaxPoseLead = 0.3f;
        private const float MaxPoseSpeed = 80f;
        private const float PoseSpeedFactor = 2f;
        private const float PoseTolerance = 20f;
        private const float MinPoseSpeedLimit = 15f;
        private const float KmhToMs = 0.277777f;
        private const float PoseWarningInterval = 5f;
        private const float MaxRemoteAimDistance = 20000f;
        private const float UnderwaterGlobalY = -2f;

        public DriveMode Mode { get; private set; }
        public bool Static { get; private set; }

        public static bool IsStatic(GroundVehicle vehicle)
        {
            return vehicle != null && GameAccess.GroundVehicleMobile != null && !GameAccess.GroundVehicleMobile(vehicle);
        }

        public static bool IsStaticDefinition(VehicleDefinition definition)
        {
            return definition != null && definition.unitPrefab != null && IsStatic(definition.unitPrefab.GetComponent<GroundVehicle>());
        }
        public float Throttle { get; private set; }
        public float Steer { get; private set; }

        private float _lastRemoteInputTime;
        private bool _hasPose;
        private GlobalPosition _posePosition;
        private Quaternion _poseRotation;
        private Vector3 _poseVelocity;
        private float _poseTime;
        private float _poseLead;
        private float _poseRadarAlt;
        private GlobalPosition _plausiblePosition;
        private float _poseBudget;
        private float _poseBudgetTime;
        private float _poseSpeedLimit = MinPoseSpeedLimit;
        private float _nextPoseWarning;

        private GroundVehicle _vehicle;
        private Rigidbody _rb;
        private float _currentSpeed;

        private float _maxForwardSpeed = 20f;
        private float _maxReverseSpeed = 8f;

        internal TurretController Turrets { get; } = new TurretController();
        private readonly GroundContact _ground = new GroundContact();
        private readonly VanillaDrive _vanilla = new VanillaDrive();
        private bool _useVanilla;
        public bool UsesVanillaPhysics => _useVanilla;
        internal VanillaDrive Vanilla => _vanilla;
        private IGroundDrive _own;
        private bool _useOwn;
        internal IGroundDrive Own => _useOwn ? _own : null;
        private Vector3 _origCenterOfMass;
        private readonly TrackAnimator _trackAnimator = new TrackAnimator();
        private readonly WheelAnimator _wheelAnimator = new WheelAnimator();
        private readonly EngineSound _engineSound = new EngineSound();
        internal EngineSound EngineSound => _engineSound;
        internal ClientLoad ClientLoad { get; } = new ClientLoad();

        private const float HeightResponse = 10f;
        private const float MaxVerticalSpeed = 15f;
        private const float AirborneMargin = 1f;
        private const float AlignResponse = 8f;

        private bool _origHoldPosition;
        private float _origDrag;
        private float _origAngularDrag;
        private float _origMass;
        private bool _origUseGravity;
        private RigidbodyInterpolation _origInterpolation;
        private bool _holdSaved;
        private bool _bodySaved;
        private bool _slungAtInit;
        private bool _slungDuringSession;

        private const float DeployingBlockTimeout = 60f;
        private bool _lastDeployed;
        private float _deployChangeTime = -999f;

        private static bool IsPureClient => NetBridge.IsClientOnly;

        public void Init(GroundVehicle vehicle, DriveMode mode)
        {
            Mode = mode;
            GroundAiDriver ai = vehicle.GetComponent<GroundAiDriver>();
            if (ai != null) ai.Suspend();
            _holdSaved = false;
            _bodySaved = false;
            _slungAtInit = vehicle.IsSlung();
            _slungDuringSession = false;
            _vehicle = vehicle;
            _rb = vehicle.GetComponent<Rigidbody>();
            _currentSpeed = 0f;
            Throttle = 0f;
            Steer = 0f;
            _hasPose = false;
            _poseLead = 0f;
            _poseRadarAlt = 0f;
            _lastRemoteInputTime = Time.time;
            _plausiblePosition = vehicle.transform.position.ToGlobalPosition();
            _poseBudget = PoseTolerance;
            _poseBudgetTime = Time.unscaledTime;
            ControlRegistry.Add(vehicle);
            ArtilleryAi.Pause(vehicle);
            Static = IsStatic(vehicle);
            if (mode == DriveMode.ClientDriver && !Static) ControlRegistry.AddClientSimulated(vehicle);
            Turrets.Silent = mode == DriveMode.ServerForClient;

            if (Static)
            {
                _vehicle.StowTurrets(false);
                Turrets.Attach(_vehicle);
                if (mode != DriveMode.ServerForClient)
                {
                    ModLog.Notify(_vehicle.unitName + ": static emplacement, turret control only"
                        + (_vehicle.definition != null && _vehicle.definition.CanSlingLoad ? ". Move it with a helicopter sling" : ""));
                }
                return;
            }

            float nativeTop = _vehicle.GetTopSpeed() * KmhToMs;
            if (nativeTop > 5f && nativeTop < 35f)
            {
                _maxForwardSpeed = nativeTop;
            }
            else
            {
                _maxForwardSpeed = !string.IsNullOrEmpty(_vehicle.unitName)
                    && (_vehicle.unitName.Contains("Tank") || _vehicle.unitName.Contains("Spearhead")) ? 17f : 21f;
            }
            _maxReverseSpeed = _maxForwardSpeed * ModConfig.ReverseSpeedFactor.Value;
            _poseSpeedLimit = Mathf.Clamp(Mathf.Max(_maxForwardSpeed, nativeTop), MinPoseSpeedLimit, MaxPoseSpeed);

            if (_rb != null)
            {
                _ground.Init(_vehicle, _rb);
                ModLog.Info("Ride height for " + _vehicle.unitName + ": " + _ground.RideHeight.ToString("F2") + " m");
            }

            _origHoldPosition = _vehicle.GetHoldPosition();
            _holdSaved = true;
            if (!IsPureClient)
            {
                try
                {
                    _vehicle.StopImmediately();
                    _vehicle.SetHoldPosition(true);
                }
                catch { }
                _vehicle.NetworknetworkStationary = false;
            }
            _vehicle.SetWheelsLocked(false);
            GameAccess.GroundVehicleResetStationary(_vehicle) = true;
            _vehicle.OnStationaryChanged(false);
            _lastDeployed = _vehicle.NetworknetworkStationary;
            _deployChangeTime = Time.time;

            _vehicle.enabled = false;

            if (_rb != null)
            {
                _origDrag = _rb.drag;
                _origAngularDrag = _rb.angularDrag;
                _origUseGravity = _rb.useGravity;
                _origInterpolation = _rb.interpolation;
                _origMass = _rb.mass;
                _origCenterOfMass = _rb.centerOfMass;
                _bodySaved = true;

                _rb.isKinematic = false;
                _rb.useGravity = true;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _own = ModConfig.VanillaPhysics.Value && Mode != DriveMode.ServerForClient ? DriveSpec.CreateDrive(_vehicle) : null;
                _useOwn = _own != null && _own.Init(_vehicle, _rb, false);
                _useVanilla = !_useOwn && ModConfig.VanillaPhysics.Value && Mode != DriveMode.ServerForClient && _vanilla.Init(_vehicle, _rb);
                if (_useOwn)
                {
                    _own.ApplyBody();
                    if (_own is TrackedDrive) _trackAnimator.Attach(_vehicle);
                    if (_own is WheeledDrive wheeled) _wheelAnimator.Attach(_vehicle, wheeled, true);
                    _engineSound.Attach(_vehicle);
                    ModLog.Info(_vehicle.unitName + ": " + (_own.CanPivot ? "tracked" : "wheeled") + " running gear (" + _own.Spec.Prototype + ")");
                }
                else if (_useVanilla)
                {
                    _rb.mass = _vanilla.Mass;
                }
                else
                {
                    _rb.drag = 0.5f;
                    _rb.angularDrag = 3.0f;
                    DriveSpec spec = DriveSpec.For(_vehicle);
                    if (Mode == DriveMode.ServerForClient && spec != null)
                    {
                        _rb.mass = spec.MassOf(_vehicle);
                    }
                    else if (Mode == DriveMode.ServerForClient && _vehicle.definition != null && _vehicle.definition.mass > 0f)
                    {
                        _rb.mass = _vehicle.definition.mass;
                    }
                }
                _rb.WakeUp();
            }

            _vehicle.StowTurrets(false);
            Turrets.Attach(_vehicle);
        }

        public void Release()
        {
            ClientLoad.Clear();
            ControlRegistry.Remove(_vehicle);
            ControlRegistry.RemoveClientSimulated(_vehicle);
            ArtilleryAi.Resume(_vehicle);

            if (_vehicle != null && !_vehicle.disabled && _vehicle.IsSlung())
            {
                _vehicle.SetWheelsLocked(true);
                _vehicle.StowTurrets(true);
            }

            if (Static)
            {
                Turrets.Release();
                Static = false;
                _vehicle = null;
                _rb = null;
                return;
            }

            if (_vehicle != null)
            {
                if (!_vehicle.disabled) _vehicle.enabled = !_vehicle.IsSlung();
                if (!IsPureClient && _holdSaved) _vehicle.SetHoldPosition(_origHoldPosition);
                GameAccess.GroundVehicleResetStationary(_vehicle) = true;
            }

            if (_rb != null && _bodySaved)
            {
                if (!_slungDuringSession && !_vehicle.IsSlung())
                {
                    _rb.drag = _origDrag;
                    _rb.angularDrag = _origAngularDrag;
                    _rb.useGravity = _origUseGravity;
                    _rb.interpolation = _rb.isKinematic ? RigidbodyInterpolation.None : _origInterpolation;
                }
                _rb.mass = _origMass;
                if (_useOwn) _rb.centerOfMass = _origCenterOfMass;
            }
            _slungDuringSession = false;
            _holdSaved = false;
            _bodySaved = false;
            _useVanilla = false;
            _useOwn = false;
            _trackAnimator.Detach();
            _wheelAnimator.Detach();
            _engineSound.Detach();

            Turrets.Release();
            _currentSpeed = 0f;
            _vehicle = null;
            _rb = null;
        }

        internal void ApplyRemoteInput(PvInput input)
        {
            if (Mode != DriveMode.ServerForClient) return;
            if (!IsFinite(input) || input.MissionMode > FireMode.Missile) return;

            Throttle = Mathf.Clamp(input.Throttle, -1f, 1f);
            Steer = Mathf.Clamp(input.Steer, -1f, 1f);
            Vector3 aimPoint = new GlobalPosition(input.AimX, input.AimY, input.AimZ).ToLocalPosition();
            Vector3 aimOffset = aimPoint - BodyPosition;
            if (aimOffset.sqrMagnitude > MaxRemoteAimDistance * MaxRemoteAimDistance)
            {
                aimPoint = BodyPosition + aimOffset.normalized * MaxRemoteAimDistance;
            }
            Unit lockTarget = input.LockId != 0 && UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = input.LockId }), out Unit unit)
                ? unit
                : null;
            Unit missionTarget = input.MissionTargetId != 0 && UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = input.MissionTargetId }), out Unit target)
                ? target
                : null;
            Vector3 aimDir = new Vector3(input.AimDirX, input.AimDirY, input.AimDirZ);
            if (aimDir.sqrMagnitude < 1e-4f) aimDir = (aimPoint - BodyPosition).normalized;
            else aimDir.Normalize();
            Turrets.ApplyRemoteInput(aimPoint, aimDir, input.HasAimHit, input.Station, lockTarget, input.Fire, input.FirePresses, missionTarget, input.MissionMode);

            Quaternion rotation = new Quaternion(input.RotX, input.RotY, input.RotZ, input.RotW);
            float rotationLengthSqr = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
            if (rotationLengthSqr > 0.5f && rotationLengthSqr < 1.5f)
            {
                GlobalPosition position = new GlobalPosition(input.PosX, input.PosY, input.PosZ);
                if (AcceptPose(position))
                {
                    _posePosition = position;
                    _poseRotation = Quaternion.Normalize(rotation);
                    float maxSpeed = Mathf.Min(MaxPoseSpeed, _poseSpeedLimit * PoseSpeedFactor);
                    _poseVelocity = Vector3.ClampMagnitude(new Vector3(input.VelX, input.VelY, input.VelZ), maxSpeed);
                    _poseTime = Time.time;
                    _poseLead = Mathf.Clamp(input.RttMs * 0.0005f, 0f, MaxPoseLead);
                    _poseRadarAlt = input.RadarAltDm * 0.1f;
                    _hasPose = true;
                }
                else
                {
                    _poseVelocity = Vector3.zero;
                }
            }
            _lastRemoteInputTime = Time.time;
        }

        private bool AcceptPose(GlobalPosition position)
        {
            float now = Time.unscaledTime;
            _poseBudget += _poseSpeedLimit * PoseSpeedFactor * Mathf.Max(0f, now - _poseBudgetTime);
            _poseBudgetTime = now;

            Vector3 delta = position - _plausiblePosition;
            float distance = new Vector2(delta.x, delta.z).magnitude + Mathf.Max(delta.y, 0f);
            if (distance <= _poseBudget)
            {
                _poseBudget = Mathf.Min(_poseBudget - distance, PoseTolerance);
                _plausiblePosition = position;
                return true;
            }

            if (now >= _nextPoseWarning)
            {
                _nextPoseWarning = now + PoseWarningInterval;
                ModLog.Warning("Multiplayer: implausible pose of " + (_vehicle != null ? _vehicle.unitName : "vehicle")
                    + " from its driver (" + distance.ToString("F0") + " m, allowed " + _poseBudget.ToString("F0") + " m) - ignored");
            }
            return false;
        }

        private static bool IsFinite(PvInput input)
        {
            return IsFinite(input.Throttle) && IsFinite(input.Steer)
                && IsFinite(input.AimX) && IsFinite(input.AimY) && IsFinite(input.AimZ)
                && IsFinite(input.AimDirX) && IsFinite(input.AimDirY) && IsFinite(input.AimDirZ)
                && IsFinite(input.PosX) && IsFinite(input.PosY) && IsFinite(input.PosZ)
                && IsFinite(input.RotX) && IsFinite(input.RotY) && IsFinite(input.RotZ) && IsFinite(input.RotW)
                && IsFinite(input.VelX) && IsFinite(input.VelY) && IsFinite(input.VelZ);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal float RemotePoseSpeed => _hasPose ? _poseVelocity.magnitude : 0f;

        internal void SetClientLoad(bool active, GroundVehicle load, float mass, bool towed)
        {
            if (Mode != DriveMode.ClientDriver || Static || _vehicle == null || _rb == null) return;
            ClientLoad.Set(_vehicle, active, load, mass, towed);
            if (_useVanilla) _rb.mass = _vanilla.Mass + ClientLoad.Mass;
            else if (_useOwn) _rb.mass = _own.Mass + ClientLoad.Mass;
        }

        public Vector3 BodyPosition => _rb != null ? _rb.position : _vehicle.transform.position;
        public Quaternion BodyRotation => _rb != null ? _rb.rotation : _vehicle.transform.rotation;
        public Vector3 BodyVelocity => _rb != null ? _rb.velocity : Vector3.zero;

        private void FixedUpdate()
        {
            if (_vehicle == null || _vehicle.disabled) return;

            if (_vehicle.IsSlung())
            {
                if (!_slungAtInit) _slungDuringSession = true;
                return;
            }

            if (Static)
            {
                Turrets.AimTurrets();
                return;
            }

            if (CheckUnderwater()) return;

            if (_vehicle.enabled) _vehicle.enabled = false;

            if (_rb != null)
            {
                bool deployed = VehicleDeploy.IsDeployed(_vehicle);
                if (deployed != _lastDeployed)
                {
                    _lastDeployed = deployed;
                    _deployChangeTime = Time.time;
                }
                if (deployed) HoldDeployed();
                else if (Time.time - _deployChangeTime < DeployingBlockTimeout && DeployParts.AnyMoving(_vehicle)) HoldDeploying();
                else if (Mode == DriveMode.ServerForClient) FollowClientPose();
                else DrivePhysics();
                if (Mode == DriveMode.ClientDriver) ClientLoad.FixedTick(_rb);
            }
            Turrets.AimTurrets();
        }

        private bool CheckUnderwater()
        {
            if (IsPureClient || _vehicle.transform.position.GlobalY() >= UnderwaterGlobalY) return false;
            ModLog.Info(_vehicle.unitName + " went under water: disabled");
            _vehicle.Networkdisabled = true;
            return true;
        }

        private void HoldDeployed()
        {
            _currentSpeed = 0f;
            _vehicle.speed = 0f;
            SetRadarAlt(0f);
        }

        private float _nextDeployingNotify;

        private void HoldDeploying()
        {
            float throttle = Throttle;
            float steer = Steer;
            Throttle = 0f;
            Steer = 0f;
            DrivePhysics();
            Throttle = throttle;
            Steer = steer;
            _vehicle.speed = 0f;
            if ((throttle != 0f || steer != 0f) && Time.unscaledTime >= _nextDeployingNotify)
            {
                _nextDeployingNotify = Time.unscaledTime + 2f;
                ModLog.Notify("Wait for the vehicle to stow");
            }
        }

        private void SetRadarAlt(float height)
        {
            _vehicle.radarAlt = Mathf.Max(height, 0f);
        }

        private void EnsureDynamicBody()
        {
            if (_rb.isKinematic) _rb.isKinematic = false;
            if (_rb.interpolation != RigidbodyInterpolation.Interpolate) _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void DrivePhysics()
        {
            EnsureDynamicBody();

            float throttle = Throttle;
            float steer = Steer;

            if (_useOwn)
            {
                _own.Step(throttle, steer);
                if (Mode == DriveMode.ClientDriver) ClientLoad.Step(_rb, _own.Brake);
                _currentSpeed = _own.Speed;
                _vehicle.speed = _own.Speed;
                SetRadarAlt(_own.RadarAlt);
                return;
            }

            if (_useVanilla)
            {
                _vanilla.Step(throttle, steer);
                if (Mode == DriveMode.ClientDriver && _vanilla.Supported) ClientLoad.Step(_rb, _vanilla.Brake);
                _currentSpeed = _vanilla.Speed;
                _vehicle.speed = _vanilla.Speed;
                SetRadarAlt(_vanilla.RadarAlt);
                return;
            }

            float targetSpeed = 0f;
            if (throttle > 0.05f) targetSpeed = throttle * _maxForwardSpeed;
            else if (throttle < -0.05f) targetSpeed = throttle * _maxReverseSpeed;

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, ModConfig.Acceleration.Value * Time.fixedDeltaTime);
            _vehicle.speed = _currentSpeed;

            if (!DriveOnGround(steer))
            {
                DriveWithoutGround(steer);
            }
        }

        private void FollowClientPose()
        {
            EnsureDynamicBody();

            bool stale = Time.time - _lastRemoteInputTime > RemoteInputTimeout;
            if (stale)
            {
                Throttle = 0f;
                Steer = 0f;
                Turrets.ClearFire();
            }

            if (!_hasPose || stale)
            {
                _rb.velocity = new Vector3(0f, _rb.velocity.y, 0f);
                _rb.angularVelocity = Vector3.zero;
                _vehicle.speed = 0f;
                return;
            }

            float age = Mathf.Min(Time.time - _poseTime, MaxPoseExtrapolation) + _poseLead;
            Vector3 target = _posePosition.ToLocalPosition() + _poseVelocity * age;
            Vector3 error = target - _rb.position;

            if (error.magnitude > PoseSnapDistance)
            {
                _rb.position = target;
                _rb.rotation = _poseRotation;
                _rb.velocity = _poseVelocity;
            }
            else
            {
                _rb.velocity = _poseVelocity + error * PoseFollowGain;
                _rb.MoveRotation(Quaternion.Slerp(_rb.rotation, _poseRotation, 0.5f));
            }
            _rb.angularVelocity = Vector3.zero;
            _vehicle.speed = Vector3.Dot(_poseVelocity, _vehicle.transform.forward);
            SetRadarAlt(_poseRadarAlt);
        }

        private bool DriveOnGround(float steer)
        {
            if (!_ground.Sample(_rb, out float groundHeight, out Vector3 groundNormal)) return false;

            float heightError = groundHeight + _ground.RideHeight - _rb.position.y;
            SetRadarAlt(-heightError);
            if (heightError < -AirborneMargin) return false;

            float dt = Time.fixedDeltaTime;

            Vector3 forward = Vector3.ProjectOnPlane(_rb.rotation * Vector3.forward, groundNormal);
            if (forward.sqrMagnitude < 0.0001f) return false;
            forward.Normalize();

            Quaternion aligned = Quaternion.Slerp(_rb.rotation, Quaternion.LookRotation(forward, groundNormal), AlignResponse * dt);
            Quaternion yaw = Quaternion.AngleAxis(steer * ModConfig.TurnRate.Value * dt, groundNormal);
            Quaternion newRotation = yaw * aligned;
            _rb.MoveRotation(newRotation);
            _rb.angularVelocity = Vector3.zero;

            forward = Vector3.ProjectOnPlane(newRotation * Vector3.forward, groundNormal).normalized;

            Vector3 velocity = forward * _currentSpeed;
            float verticalCorrection = heightError * HeightResponse - Physics.gravity.y * dt;
            velocity.y = Mathf.Clamp(velocity.y + verticalCorrection, -MaxVerticalSpeed, MaxVerticalSpeed);
            _rb.velocity = velocity;
            return true;
        }

        private void DriveWithoutGround(float steer)
        {
            Vector3 desiredVel = _vehicle.transform.forward * _currentSpeed;
            Vector2 horizontalVel = Vector2.ClampMagnitude(new Vector2(desiredVel.x, desiredVel.z), _maxForwardSpeed);
            _rb.velocity = new Vector3(horizontalVel.x, _rb.velocity.y, horizontalVel.y);

            if (steer != 0f)
            {
                Quaternion deltaRot = Quaternion.Euler(0f, steer * ModConfig.TurnRate.Value * Time.fixedDeltaTime, 0f);
                _rb.MoveRotation(_rb.rotation * deltaRot);
            }
            else
            {
                _rb.angularVelocity = new Vector3(_rb.angularVelocity.x * 0.4f, 0f, _rb.angularVelocity.z * 0.4f);
            }
        }

        private void Update()
        {
            if (_vehicle == null) return;

            if (_vehicle.IsSlung() && !_vehicle.disabled)
            {
                if (Mode != DriveMode.ServerForClient) ExitSlung();
                return;
            }

            if (!Static) GameAccess.GroundVehicleUpdate(_vehicle);
            if (_useOwn)
            {
                if (!GameplayUI.GameIsPaused && _own is TrackedDrive tracked) _trackAnimator.Tick(tracked.TrackSpeedLeft, tracked.TrackSpeedRight);
                if (!GameplayUI.GameIsPaused) _wheelAnimator.Tick(_own.Speed);
                _engineSound.Tick(_vehicle, _own);
            }

            if (_vehicle.disabled) return;

            if (Mode == DriveMode.ServerForClient)
            {
                Turrets.TryFire();
                return;
            }

            ReadLocalInput();
            if (InputGate.GameplayInputAllowed && InputGate.KeyDown(ModConfig.DeployKey.Value)) DeployAction();

            if (Mode == DriveMode.Local) Turrets.TryFire();
            else
            {
                ShotPrediction.Tick(_vehicle, Turrets);
                ClientLink.SendInput(_vehicle, this);
            }
        }

        public void DeployAction()
        {
            if (_vehicle == null || _vehicle.disabled) return;
            if (Static) ModLog.Notify(_vehicle.unitName + " deploys by itself while standing");
            else if (Towing.IsTractor(_vehicle)) Towing.Toggle(_vehicle, this);
            else if (Cargo.IsFlatbed(_vehicle)) Cargo.Toggle(_vehicle, this);
            else VehicleDeploy.Toggle(_vehicle, this);
        }

        private void ExitSlung()
        {
            if (!VehicleSession.IsActive || VehicleSession.Driver != this) return;
            VehicleSession.ExitWithMessage(_vehicle.unitName + " is on a helicopter sling: control released");
        }

        private void ReadLocalInput()
        {
            if (!InputGate.GameplayInputAllowed)
            {
                Throttle = 0f;
                Steer = 0f;
                Turrets.ClearFire();
                return;
            }

            float deadZone = ModConfig.StickDeadZone.Value;
            Throttle = Mathf.Clamp(InputGate.KeyAxis(ModConfig.ForwardKey.Value, ModConfig.ReverseKey.Value)
                + InputGate.JoystickAxis(GameActions.MoveLongitudinal, deadZone), -1f, 1f);
            Steer = Mathf.Clamp(InputGate.KeyAxis(ModConfig.RightKey.Value, ModConfig.LeftKey.Value)
                + InputGate.JoystickAxis(GameActions.MoveLateral, deadZone), -1f, 1f);
            Turrets.ReadLocalInput();
        }
    }
}
