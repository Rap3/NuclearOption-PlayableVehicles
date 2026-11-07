using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class RemoteVehicleVisuals : MonoBehaviour, IWheelSteer
    {
        private const float Smoothing = 6f;
        private const float LoadAccel = 0.3f;
        private const float LoadYawRate = 0.1f;
        private const float ShowRange = 1500f;
        private const float HideRange = 1800f;

        private GroundVehicle _vehicle;
        private DriveSpec _spec;
        private WheeledSpec _wheeled;
        private TankSpec _tank;
        private readonly Powertrain _engine = new Powertrain();
        private readonly TrackAnimator _tracks = new TrackAnimator();
        private readonly WheelAnimator _wheels = new WheelAnimator();
        private readonly EngineSound _sound = new EngineSound();
        private bool _active;

        private GlobalPosition _lastPosition;
        private float _lastYaw;
        private bool _hasLast;
        private float _speed;
        private float _yawRate;
        private float _accel;
        private float _pivotZ;

        public WheeledSpec Wheeled => _wheeled;

        private void Awake()
        {
            _vehicle = GetComponent<GroundVehicle>();
            _spec = _vehicle != null ? DriveSpec.For(_vehicle) : null;
            _wheeled = _spec as WheeledSpec;
            _tank = _spec as TankSpec;
            if (_wheeled != null)
            {
                int steered = Mathf.Clamp(_wheeled.SteeredAxles, 1, Mathf.Max(1, _wheeled.AxleZ.Length - 1));
                float rear = 0f;
                for (int a = steered; a < _wheeled.AxleZ.Length; a++) rear += _wheeled.AxleZ[a];
                _pivotZ = rear / Mathf.Max(1, _wheeled.AxleZ.Length - steered);
            }
        }

        public void Suspend()
        {
            if (!_active) return;
            _active = false;
            _tracks.Detach();
            _wheels.Detach();
            _sound.Detach();
        }

        private bool ShouldShow()
        {
            if (_spec == null || _vehicle == null || _vehicle.disabled || _vehicle.IsSlung()) return false;
            GroundAiDriver ai = _vehicle.GetComponent<GroundAiDriver>();
            if (ai != null && ai.Active) return false;
            if (VehicleSession.IsActive && VehicleSession.Vehicle == _vehicle) return false;
            CameraStateManager camera = SceneSingleton<CameraStateManager>.i;
            if (camera == null) return false;
            float range = _active ? HideRange : ShowRange;
            return (camera.transform.position - _vehicle.transform.position).sqrMagnitude < range * range;
        }

        private void FixedUpdate()
        {
            if (!ShouldShow())
            {
                Suspend();
                _hasLast = false;
                return;
            }
            if (!_active)
            {
                _engine.Init(_spec);
                if (_tank != null) _tracks.Attach(_vehicle);
                if (_wheeled != null) _wheels.Attach(_vehicle, this);
                _sound.Attach(_vehicle);
                _active = true;
            }

            float dt = Time.fixedDeltaTime;
            Transform t = _vehicle.transform;
            GlobalPosition position = t.GlobalPosition();
            float yaw = t.eulerAngles.y;
            if (_hasLast && dt > 0f)
            {
                Vector3 moved = position - _lastPosition;
                float speed = Vector3.Dot(moved, t.forward) / dt;
                float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) * Mathf.Deg2Rad / dt;
                float k = 1f - Mathf.Exp(-Smoothing * dt);
                float previous = _speed;
                _speed = Mathf.Lerp(_speed, speed, k);
                _yawRate = Mathf.Lerp(_yawRate, yawRate, k);
                _accel = Mathf.Lerp(_accel, (_speed - previous) / dt, k);
            }
            _lastPosition = position;
            _lastYaw = yaw;
            _hasLast = true;

            float throttle = Mathf.Abs(_speed) > 0.3f && _accel * Mathf.Sign(_speed) > LoadAccel ? Mathf.Sign(_speed) : 0f;
            bool turning = _tank != null && Mathf.Abs(_yawRate) > LoadYawRate;
            float demand = Mathf.Max(Mathf.Abs(throttle), turning ? 0.6f : 0f);
            _engine.Update(throttle, turning && Mathf.Abs(_speed) < 0.5f, demand, _speed);
        }

        private void LateUpdate()
        {
            if (!_active || _vehicle == null) return;
            if (!GameplayUI.GameIsPaused)
            {
                if (_tank != null) _tracks.Tick(_speed + _yawRate * _tank.TrackX, _speed - _yawRate * _tank.TrackX);
                if (_wheeled != null) _wheels.Tick(_speed);
            }
            _sound.Tick(_vehicle, _engine);
        }

        private void OnDestroy()
        {
            Suspend();
        }

        public float WheelLift(int axle, int side)
        {
            return 0f;
        }

        public float WheelSteerDeg(int axle, int side)
        {
            if (_wheeled == null || axle >= _wheeled.SteeredAxles || axle >= _wheeled.AxleZ.Length) return 0f;
            if (Mathf.Abs(_speed) < 1f || Mathf.Abs(_yawRate) < 0.01f) return 0f;
            float radius = _speed / _yawRate;
            float x = side == 0 ? -_wheeled.TrackHalf : _wheeled.TrackHalf;
            float angle = Mathf.Atan((_wheeled.AxleZ[axle] - _pivotZ) / (radius - x)) * Mathf.Rad2Deg;
            return Mathf.Clamp(angle, -_wheeled.MaxSteerDeg * 1.3f, _wheeled.MaxSteerDeg * 1.3f);
        }
    }
}
