using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class CameraRig
    {
        private const float MinPivotHeight = 2.8f;
        private const float RoofClearance = 1.2f;
        private const float DefaultPitch = 6f;
        private const float MaxPitch = 85f;
        private const float MinDistance = 3.5f;
        private const float MinCollisionDistance = 0.8f;
        private const float GroundClearance = 0.6f;
        private const float HullMargin = 0.4f;
        private const float LoweredPivotClearance = 0.6f;
        private const float PivotDropSpeed = 14f;
        private const float ZoomSensitivityMultiplier = 0.12f;

        private static readonly RaycastHit[] ObstacleHits = new RaycastHit[24];

        private GroundVehicle _vehicle;
        private Rigidbody _rb;
        private Camera _cam;
        private Bounds _hull;
        private float _bodyRadius = 3f;
        private float _pivotHeight = MinPivotHeight;

        private float _yaw;
        private float _pitch = DefaultPitch;
        private float _distance;
        private float _sightZoom;
        private float _sightZoomDefault;
        private float _zoomSpeed;
        private bool _zoomed;

        private Vector3 _lookDir = Vector3.forward;
        private bool _hasLook;
        private Vector3 _pivotSmooth;
        private Vector3 _pivotVel;
        private float _heightSmooth;
        private float _heightVel;
        private bool _hasPivot;
        private float _distanceMult = 1f;
        private float _pivotDrop;
        private Vector3 _datum;

        public float DefaultFov { get; private set; } = 60f;
        public bool ZoomForced { get; set; }
        public bool IsZoomed => ZoomForced || _zoomed;
        public float BodyRadius => _bodyRadius;
        public float CollisionMultiplier => _distanceMult;
        public Vector3 LookDirection => _lookDir;

        private float MinThirdPersonDistance => Mathf.Max(MinDistance, _bodyRadius + 2f);
        private float MaxThirdPersonDistance => Mathf.Max(ModConfig.CameraMaxDistance.Value, MinThirdPersonDistance);
        public float SightZoom => _sightZoom;
        private float SightZoomMin => Mathf.Clamp(ModConfig.SightZoomDefault.Value, 1f, SightZoomMax);
        private float SightZoomMax => Mathf.Max(ModConfig.SightZoomMax.Value, 1f);

        public void ResetSightZoom()
        {
            _sightZoom = _sightZoomDefault;
        }

        public void SetSightZoom(float magnification)
        {
            _sightZoomDefault = Mathf.Clamp(magnification, 1f, SightZoomMax);
            _sightZoom = _sightZoomDefault;
        }

        public void Attach(GroundVehicle vehicle, Camera camera)
        {
            _vehicle = vehicle;
            _cam = camera;
            _rb = vehicle.GetComponent<Rigidbody>();
            if (_cam != null) DefaultFov = _cam.fieldOfView;

            UnitDefinition definition = vehicle.definition;
            if (definition != null)
            {
                _bodyRadius = Mathf.Max(0.5f * Mathf.Sqrt(definition.length * definition.length + definition.width * definition.width
                    + definition.height * definition.height), 2f);
            }
            float centerOfMass = _rb != null ? _rb.centerOfMass.y : 0f;
            Bounds hull = VehicleGeometry.LocalBounds(vehicle);
            _pivotHeight = Mathf.Max(MinPivotHeight, hull.max.y - centerOfMass + RoofClearance);
            hull.Expand(HullMargin * 2f);
            _hull = hull;

            _yaw = vehicle.transform.eulerAngles.y;
            _pitch = DefaultPitch;
            _zoomed = false;
            ZoomForced = false;
            _zoomSpeed = 0f;
            _distance = MinThirdPersonDistance;
            _sightZoomDefault = Mathf.Clamp(ModConfig.SightZoomDefault.Value, 1f, SightZoomMax);
            _sightZoom = _sightZoomDefault;
            _distanceMult = 1f;
            _pivotDrop = 0f;
            _hasPivot = false;
            _hasLook = false;
            _pivotSmooth = Vector3.zero;
            _pivotVel = Vector3.zero;
            _heightSmooth = 0f;
            _heightVel = 0f;
            _datum = Datum.originPosition;
        }

        public void Detach()
        {
            _vehicle = null;
            _rb = null;
            _cam = null;
            _hasLook = false;
            _hasPivot = false;
        }

        public void ResetPivot()
        {
            _hasPivot = false;
            _hasLook = false;
        }

        public void SetLook(float yaw, float pitch)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, -ModConfig.CameraMaxLookUp.Value, MaxPitch);
        }

        public void SyncLookTo(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-4f) return;
            Vector3 euler = Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        public Vector3 WishDirection => Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;

        public Vector3 SmoothLook(Vector3 wishDir, float tau, float dt)
        {
            if (!_hasLook || _lookDir.sqrMagnitude < 1e-6f)
            {
                _lookDir = wishDir;
                _hasLook = true;
                return _lookDir;
            }
            Quaternion from = Quaternion.LookRotation(_lookDir, Vector3.up);
            Quaternion to = Quaternion.LookRotation(wishDir, Vector3.up);
            float a = 1f - Mathf.Exp(-dt / Mathf.Max(tau, 1e-4f));
            _lookDir = (Quaternion.Slerp(from, to, a) * Vector3.forward).normalized;
            return _lookDir;
        }

        public Vector3 Pivot(float dt)
        {
            Transform vt = _vehicle.transform;
            Vector3 now = (_rb != null ? vt.TransformPoint(_rb.centerOfMass) : vt.position) + Vector3.up * _pivotHeight;
            if (!_hasPivot)
            {
                _pivotSmooth = now;
                _pivotVel = Vector3.zero;
                _heightSmooth = now.y;
                _heightVel = 0f;
                _hasPivot = true;
                return _pivotSmooth;
            }

            _heightSmooth = Mathf.SmoothDamp(_heightSmooth, now.y, ref _heightVel,
                Mathf.Max(ModConfig.CameraAnimTau.Value, 1e-3f), Mathf.Infinity, dt);
            Vector3 target = new Vector3(now.x, _heightSmooth, now.z);
            _pivotSmooth = Vector3.SmoothDamp(_pivotSmooth, target, ref _pivotVel,
                Mathf.Max(ModConfig.CameraTau.Value, 1e-3f), Mathf.Infinity, dt);
            return _pivotSmooth;
        }

        public Vector3 ThirdPersonPosition(Vector3 pivot, Vector3 forward, Vector3 up)
        {
            float distance = _distance * Mathf.Max(ModConfig.VehicleOffsetMultiplier.Value, 0.1f);
            Vector3 cameraOffset = -forward * distance;
            Vector3 pivotOffset = up * 0.25f;
            float wishMult = 1f;
            if (distance > 0.01f)
            {
                float radius = ModConfig.CameraCollisionRadius.Value;
                if (SphereHit(pivot, pivot + pivotOffset, radius, out float pivotAlong))
                    pivotOffset = pivotOffset.normalized * Mathf.Max(pivotAlong - radius, 0f);
                Vector3 armStart = pivot + pivotOffset;
                if (SphereHit(armStart, armStart + cameraOffset, radius, out float hitAlong))
                {
                    float usable = Mathf.Max(hitAlong - radius, 0f);
                    wishMult = Mathf.Clamp01(usable / Mathf.Max(cameraOffset.magnitude, 1e-4f));
                }
            }
            float speed = wishMult > _distanceMult ? ModConfig.CameraMoveOutSpeed.Value : ModConfig.CameraMoveInSpeed.Value;
            _distanceMult = MoveTo(_distanceMult, wishMult, speed, Time.deltaTime);
            Vector3 wishPos = pivot + pivotOffset + cameraOffset * _distanceMult;

            Transform vt = _vehicle.transform;
            if (InsideHull(vt, wishPos))
            {
                Vector3 loweredPivot = pivot;
                loweredPivot.y = Mathf.Min(pivot.y,
                    vt.TransformPoint(new Vector3(0f, _hull.max.y - HullMargin, 0f)).y + LoweredPivotClearance);
                float drop = pivot.y - loweredPivot.y;
                if (drop > 0.05f)
                {
                    Vector3 lowered = loweredPivot + cameraOffset * _distanceMult;
                    if (!InsideHull(vt, lowered))
                    {
                        _pivotDrop = Mathf.Lerp(_pivotDrop, drop, 1f - Mathf.Exp(-PivotDropSpeed * Time.deltaTime));
                        wishPos = lowered;
                    }
                    else
                    {
                        wishPos = pivot + cameraOffset * (MinCollisionDistance / Mathf.Max(distance, 0.01f));
                    }
                }
                else
                {
                    wishPos = pivot + cameraOffset * (MinCollisionDistance / Mathf.Max(distance, 0.01f));
                }
            }
            else
            {
                _pivotDrop = Mathf.Lerp(_pivotDrop, 0f, 1f - Mathf.Exp(-PivotDropSpeed * Time.deltaTime));
            }
            wishPos.y -= _pivotDrop;
            return KeepAboveGround(wishPos);
        }

        public void FollowOriginShift(out Vector3 shift)
        {
            Vector3 datum = Datum.originPosition;
            shift = datum - _datum;
            _datum = datum;
            if (shift.sqrMagnitude < 1e-6f) return;
            _pivotSmooth += shift;
        }

        public void ReadInput()
        {
            if (ModConfig.GameViewControls.Value)
            {
                ReadGameActions();
                return;
            }

            _zoomed = Input.GetMouseButton(1);

            float sens = _zoomed ? ZoomSensitivityMultiplier : 1f;
            _yaw += Input.GetAxis("Mouse X") * ModConfig.MouseSensitivityX.Value * sens;
            _pitch -= Input.GetAxis("Mouse Y") * ModConfig.MouseSensitivityY.Value * sens;
            _pitch = Mathf.Clamp(_pitch, -ModConfig.CameraMaxLookUp.Value, MaxPitch);

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                if (_zoomed)
                {
                    _sightZoom = Mathf.Clamp(_sightZoom * Mathf.Exp(scroll * 1.5f), SightZoomMin, SightZoomMax);
                }
                else
                {
                    _distance = Mathf.Clamp(_distance - scroll * 10f, MinThirdPersonDistance, MaxThirdPersonDistance);
                }
            }
        }

        private void ReadGameActions()
        {
            _zoomed = InputGate.ActionHeld(GameActions.FreeLook);

            float dt = Time.unscaledDeltaTime;
            float fov = _cam != null ? _cam.fieldOfView : DefaultFov;
            float rate = Mathf.Min(fov / 20f, 1f) * 90f * PlayerSettings.viewSensitivity * dt;
            _yaw += InputGate.ActionAxis(GameActions.PanView) * rate;
            _pitch += InputGate.ActionAxis(GameActions.TiltView) * rate * (PlayerSettings.viewInvertPitch ? -1f : 1f);
            _pitch = Mathf.Clamp(_pitch, -ModConfig.CameraMaxLookUp.Value, MaxPitch);

            _zoomSpeed -= InputGate.ActionAxis(GameActions.ZoomView) * 60f * dt;
            _zoomSpeed = Mathf.Clamp(Mathf.Lerp(_zoomSpeed, 0f, 4f * dt), -10f, 10f);
            if (_zoomed)
            {
                _sightZoom = Mathf.Clamp(_sightZoom * Mathf.Exp(-_zoomSpeed * dt * 0.5f), SightZoomMin, SightZoomMax);
            }
            else
            {
                _distance = Mathf.Clamp(_distance + _zoomSpeed * dt * _bodyRadius, MinThirdPersonDistance, MaxThirdPersonDistance);
            }
        }

        private bool SphereHit(Vector3 from, Vector3 to, float radius, out float along)
        {
            along = float.MaxValue;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-4f) return false;
            int count = Physics.SphereCastNonAlloc(from, radius, delta / length, ObstacleHits, length,
                ~PhysicsLayers.ExclusionZonesMask, QueryTriggerInteraction.Ignore);
            Transform own = _vehicle.transform;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = ObstacleHits[i];
                Collider collider = hit.collider;
                if (collider == null || collider.transform.IsChildOf(own)) continue;
                Rigidbody body = collider.attachedRigidbody;
                if (body != null && !body.isKinematic) continue;
                if (hit.distance <= 0f) continue;
                if (hit.distance < along) along = hit.distance;
            }
            return along < float.MaxValue;
        }

        private bool InsideHull(Transform vehicleTransform, Vector3 worldPoint)
        {
            return _hull.Contains(vehicleTransform.InverseTransformPoint(worldPoint));
        }

        internal static Vector3 KeepAboveGround(Vector3 position)
        {
            const float probeHeight = 50f;
            Vector3 origin = position + Vector3.up * probeHeight;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ground, probeHeight + 20f,
                    PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore))
            {
                float minY = ground.point.y + GroundClearance;
                if (position.y < minY) position.y = minY;
            }
            return position;
        }

        private static float MoveTo(float from, float to, float speed, float dt)
        {
            return Mathf.MoveTowards(from, to, Mathf.Max(speed, 0f) * dt);
        }
    }
}
