using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class VehicleCamera
    {
        private const float MinPivotHeight = 2.8f;
        private const float RoofClearance = 1.2f;
        private const float DefaultPitch = 6f;
        private const float MinDistance = 3.5f;
        private const float MaxPitch = 85f;
        private const float MinCollisionDistance = 0.8f;
        private const float GroundClearance = 0.6f;
        private const float HullMargin = 0.4f;
        private const float LoweredPivotClearance = 0.6f;
        private const float PivotDropSpeed = 14f;
        private const float ZoomSensitivityMultiplier = 0.12f;
        private const float TauIncDecay = 0.4f;
        private const float RecoilMaxDegrees = 15f;
        private const float ProjectileDistance = 0.25f;
        private const float ProjectileLookTau = 0.06f;

        private GroundVehicle _vehicle;
        private Rigidbody _rb;
        private Camera _cam;

        private float _yaw;
        private float _pitch = DefaultPitch;
        private float _targetDistance;
        private float _defaultFov = 60f;
        private float _zoomSpeed;
        private float _bodyRadius = 3f;
        private Bounds _hullBounds;
        private float _pivotDrop;

        private float _distanceMult = 1f;
        private Vector3 _pivotSmooth;
        private bool _hasPivot;
        private Vector3 _aimOrigin;
        private bool _hasAimOrigin;
        private Vector3 _datum;
        private float _tauInc;
        private Vector3 _lookDir = Vector3.forward;
        private bool _hasLook;
        private bool _wasZoomed;
        private float _magnification = 1f;
        private float _fovSmooth = 60f;
        private float _trauma;
        private float _shakeTime;
        private Vector3 _recoil;
        private Vector3 _recoilVel;
        private Vector3 _recoilImpulse;
        private bool _projectileMode;

        private int[] _ammo = new int[0];

        public bool IsZoomed { get; private set; }

        public Ray AimRay { get; private set; }
        public bool HasAimRay { get; private set; }

        public Camera CurrentCamera => _cam;

        public static Ray ViewRay(Camera camera)
        {
            VehicleCamera view = VehicleSession.ViewCamera;
            if (VehicleSession.IsActive && view != null && view.HasAimRay) return view.AimRay;
            return camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        public void AddShake(float lowFreqShake, float highFreqShake)
        {
            float add = Mathf.Max(lowFreqShake, highFreqShake) * ModConfig.ShakeScale.Value;
            if (add <= 0f) return;
            _trauma = Mathf.Clamp01(_trauma + add);
        }

        private float MinThirdPersonDistance => Mathf.Max(MinDistance, _bodyRadius + 2f);

        public void Attach(GroundVehicle vehicle)
        {
            _vehicle = vehicle;
            _rb = vehicle.GetComponent<Rigidbody>();
            _cam = ResolveCamera();
            if (_cam != null) _defaultFov = _cam.fieldOfView;

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
            _hullBounds = hull;
            _pivotDrop = 0f;
            _hasAimOrigin = false;
            _targetDistance = Mathf.Max(ModConfig.CameraDistance.Value, MinThirdPersonDistance);

            _yaw = vehicle.transform.eulerAngles.y;
            _pitch = DefaultPitch;
            IsZoomed = false;
            _zoomSpeed = 0f;
            _distanceMult = 1f;
            _hasPivot = false;
            _hasLook = false;
            _pivotSmooth = Vector3.zero;
            _datum = Datum.originPosition;
            _tauInc = 0f;
            _wasZoomed = false;
            _magnification = 1f;
            _fovSmooth = _defaultFov;
            _trauma = 0f;
            _shakeTime = 0f;
            _recoil = Vector3.zero;
            _recoilVel = Vector3.zero;
            _recoilImpulse = Vector3.zero;
            _projectileMode = false;
            HasAimRay = false;
            RememberAmmo();
        }

        public void ProjectileStarted(Vector3 initialVelocity)
        {
            _projectileMode = true;
            _hasPivot = false;
            _hasAimOrigin = false;
            if (initialVelocity.sqrMagnitude < 1f) return;
            Vector3 euler = Quaternion.LookRotation(initialVelocity.normalized, Vector3.up).eulerAngles;
            _yaw = euler.y;
            _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            _hasLook = false;
        }

        public void ProjectileEnded()
        {
            if (!_projectileMode) return;
            _projectileMode = false;
            _hasPivot = false;
            _hasAimOrigin = false;
        }

        private float _pivotHeight = MinPivotHeight;

        public void Detach()
        {
            if (_cam != null) _cam.fieldOfView = _defaultFov;

            CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
            if (csm != null) csm.desiredFOV = _defaultFov;

            _vehicle = null;
            _rb = null;
            _cam = null;
            IsZoomed = false;
            HasAimRay = false;
        }

        private static Camera ResolveCamera()
        {
            CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
            if (csm != null && csm.mainCamera != null) return csm.mainCamera;
            return Camera.main;
        }

        public void UpdateCamera()
        {
            if (_vehicle == null) return;
            if (_cam == null)
            {
                _cam = ResolveCamera();
                if (_cam == null) return;
            }

            if (InputGate.MouseLookAllowed && !VehicleRadialMenu.IsOpen) ReadMouse();
            WatchFire();

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            FollowOriginShift();

            bool projectile = false;
            Vector3 projectilePos = default;
            Vector3 projectileVel = default;
            if (_projectileMode)
            {
                projectile = ProjectileCam.TryUpdate(dt, out projectilePos, out projectileVel);
                if (!projectile) _projectileMode = false;
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 wishDir = rotation * Vector3.forward;
            if (projectile && projectileVel.sqrMagnitude > 1f)
            {
                wishDir = projectileVel.normalized;
                Vector3 euler = Quaternion.LookRotation(wishDir, Vector3.up).eulerAngles;
                _yaw = euler.y;
                _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
            }
            if (!_hasLook)
            {
                _lookDir = wishDir;
                _hasLook = true;
            }
            float lookTau = projectile
                ? ProjectileLookTau
                : Mathf.Max(ModConfig.CameraLookTau.Value + _tauInc, 1e-4f);
            _lookDir = Approach(_lookDir, wishDir, lookTau, dt).normalized;
            Vector3 forward = _lookDir;
            Quaternion lookRotation = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 stableUp = Vector3.up;
            Vector3 right = Vector3.Cross(stableUp, forward);
            if (right.sqrMagnitude < 1e-6f) right = rotation * Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(forward, right).normalized;

            Transform vt = _vehicle.transform;
            Vector3 pivotNow = projectile
                ? projectilePos
                : (_rb != null ? vt.TransformPoint(_rb.centerOfMass) : vt.position) + Vector3.up * _pivotHeight;
            if (!_hasPivot)
            {
                _pivotSmooth = pivotNow;
                _hasPivot = true;
            }
            _pivotSmooth = Approach(_pivotSmooth, pivotNow, ModConfig.CameraTau.Value, dt);
            Vector3 pivot = _pivotSmooth;

            if (IsZoomed != _wasZoomed)
            {
                _tauInc = Mathf.Max(_tauInc, ModConfig.CameraZoomTauInc.Value);
                _wasZoomed = IsZoomed;
            }
            float zoomFov = Mathf.Clamp(ModConfig.ZoomFov.Value, 1f, _defaultFov - 1f);
            _fovSmooth = Approach(_fovSmooth, IsZoomed ? zoomFov : _defaultFov, ModConfig.CameraZoomTau.Value, dt);
            _cam.fieldOfView = _fovSmooth;
            _magnification = Mathf.Tan(_defaultFov * Mathf.Deg2Rad * 0.5f) / Mathf.Max(Mathf.Tan(_fovSmooth * Mathf.Deg2Rad * 0.5f), 1e-4f);

            float distance = IsZoomed
                ? -(_bodyRadius + 0.5f)
                : projectile
                    ? ProjectileDistance
                    : _targetDistance * Mathf.Max(ModConfig.VehicleOffsetMultiplier.Value, 0.1f);
            Vector3 cameraOffset = -forward * distance;
            Vector3 pivotOffset = up * 0.25f;
            float wishMult = 1f;
            if (!IsZoomed && distance > 0.01f && !projectile)
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
            float multSpeed = wishMult > _distanceMult ? ModConfig.CameraMoveOutSpeed.Value : ModConfig.CameraMoveInSpeed.Value;
            _distanceMult = MoveTo(_distanceMult, wishMult, multSpeed, dt);
            Vector3 wishPos = pivot + pivotOffset + cameraOffset * _distanceMult;

            if (!projectile && !IsZoomed && InsideHull(vt, wishPos))
            {
                Vector3 loweredPivot = pivot;
                loweredPivot.y = Mathf.Min(pivot.y,
                    vt.TransformPoint(new Vector3(0f, _hullBounds.max.y - HullMargin, 0f)).y + LoweredPivotClearance);
                float drop = pivot.y - loweredPivot.y;
                if (drop > 0.05f)
                {
                    Vector3 lowered = loweredPivot + cameraOffset * _distanceMult;
                    if (!InsideHull(vt, lowered))
                    {
                        _pivotDrop = Mathf.Lerp(_pivotDrop, drop, 1f - Mathf.Exp(-PivotDropSpeed * dt));
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
                _pivotDrop = Mathf.Lerp(_pivotDrop, 0f, 1f - Mathf.Exp(-PivotDropSpeed * dt));
            }
            wishPos.y -= _pivotDrop;
            wishPos = KeepAboveGround(wishPos);

            _tauInc = MoveTo(_tauInc, 0f, TauIncDecay, dt);
            Vector3 position = wishPos;

            float shakeDeg = ModConfig.ShakeMaxDegrees.Value;
            float shake = _trauma * _trauma;
            Vector3 shakeAngles = Vector3.zero;
            if (shake > 0.0001f)
            {
                float t = _shakeTime;
                shakeAngles = new Vector3(
                    2f * shakeDeg * shake * (Mathf.PerlinNoise1D(t) - 0.5f),
                    2f * shakeDeg * shake * (Mathf.PerlinNoise1D(t + 1000f) - 0.5f),
                    shakeDeg * shake * (Mathf.PerlinNoise1D(t + 2000f) - 0.5f));
            }
            _trauma = Mathf.Clamp01(_trauma - ModConfig.ShakeFadeout.Value * dt);
            _shakeTime += ModConfig.ShakeSpeed.Value * dt;
            if (_shakeTime > 1e5f) _shakeTime = 0f;

            Vector3 recoilAngles = UpdateRecoil(dt);

            if (!_hasAimOrigin)
            {
                _aimOrigin = position;
                _hasAimOrigin = true;
            }
            _aimOrigin = Approach(_aimOrigin, position, ModConfig.CameraAimTau.Value, dt);
            AimRay = new Ray(_aimOrigin, forward);
            HasAimRay = true;
            float shakeScale = _defaultFov > 1f ? Mathf.Sqrt(Mathf.Clamp(_fovSmooth / _defaultFov, 0.01f, 1f)) : 1f;
            ApplyPose(position, lookRotation * Quaternion.Euler((shakeAngles + recoilAngles * Mathf.Rad2Deg) * shakeScale));
        }

        private Vector3 UpdateRecoil(float dt)
        {
            float k = Mathf.Max(ModConfig.RecoilStiffness.Value, 1f);
            float c = Mathf.Max(ModConfig.RecoilViscosity.Value, 0f);
            _recoilVel += (-k * _recoil - c * _recoilVel) * dt;
            _recoilVel += _recoilImpulse;
            _recoilImpulse = Vector3.zero;
            _recoil += _recoilVel * dt;
            float maxRad = RecoilMaxDegrees * Mathf.Deg2Rad;
            if (_recoil.sqrMagnitude > maxRad * maxRad)
            {
                _recoil = _recoil.normalized * maxRad;
                _recoilVel = Vector3.zero;
            }
            return _recoil;
        }

        private static float Approach(float from, float to, float tau, float dt)
        {
            return Mathf.Lerp(from, to, 1f - Mathf.Exp(-dt / Mathf.Max(tau, 1e-4f)));
        }

        private void FollowOriginShift()
        {
            Vector3 datum = Datum.originPosition;
            Vector3 shift = datum - _datum;
            _datum = datum;
            if (shift.sqrMagnitude < 1e-6f) return;
            _pivotSmooth += shift;
            _aimOrigin += shift;
            ProjectileCam.FollowOriginShift(shift);
        }

        private static Vector3 Approach(Vector3 from, Vector3 to, float tau, float dt)
        {
            return Vector3.Lerp(from, to, 1f - Mathf.Exp(-dt / Mathf.Max(tau, 1e-4f)));
        }

        private static float MoveTo(float from, float to, float speed, float dt)
        {
            return Mathf.MoveTowards(from, to, Mathf.Max(speed, 0f) * dt);
        }

        private static readonly RaycastHit[] _obstacleHits = new RaycastHit[24];

        private bool SphereHit(Vector3 from, Vector3 to, float radius, out float along)
        {
            along = float.MaxValue;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-4f) return false;
            int count = Physics.SphereCastNonAlloc(from, radius, delta / length, _obstacleHits, length,
                ~PhysicsLayers.ExclusionZonesMask, QueryTriggerInteraction.Ignore);
            Transform own = _vehicle.transform;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _obstacleHits[i];
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
            return _hullBounds.Contains(vehicleTransform.InverseTransformPoint(worldPoint));
        }

        private void WatchFire()
        {
            var stations = _vehicle.weaponStations;
            if (stations == null) return;
            if (_ammo.Length != stations.Count) RememberAmmo();
            for (int i = 0; i < stations.Count; i++)
            {
                WeaponStation station = stations[i];
                int ammo = station != null ? station.Ammo : 0;
                if (ammo < _ammo[i] && station != null) ShakeForShot(station.WeaponInfo, _ammo[i] - ammo);
                _ammo[i] = ammo;
            }
        }

        private void RememberAmmo()
        {
            var stations = _vehicle != null ? _vehicle.weaponStations : null;
            _ammo = new int[stations != null ? stations.Count : 0];
            for (int i = 0; i < _ammo.Length; i++) _ammo[i] = stations[i] != null ? stations[i].Ammo : 0;
        }

        private void ShakeForShot(WeaponInfo info, int shots)
        {
            if (info == null) return;
            float strength;
            if (info.missile || info.bomb) strength = 0.12f;
            else
            {
                float energy = 0.5f * Mathf.Max(info.massPerRound, 0f) * info.muzzleVelocity * info.muzzleVelocity;
                strength = Mathf.Clamp(energy / 6e6f, 0.02f, 0.8f) * Mathf.Clamp01(info.fireInterval / 0.25f);
            }
            strength *= 1f + 0.35f * (Mathf.Min(shots, 6) - 1);
            strength *= ModConfig.ShakeScale.Value;
            if (strength <= 0f) return;
            float omega = Mathf.Sqrt(Mathf.Max(ModConfig.RecoilStiffness.Value, 1f));
            float peakRad = strength * ModConfig.RecoilImpulse.Value * Mathf.Deg2Rad;
            _recoilImpulse += new Vector3(-peakRad * omega, 0f, 0f);
        }

        private void ReadMouse()
        {
            if (ModConfig.GameViewControls.Value)
            {
                ReadViewActions();
                return;
            }

            IsZoomed = Input.GetMouseButton(1);

            float sens = IsZoomed ? ZoomSensitivityMultiplier : 1f;
            _yaw += Input.GetAxis("Mouse X") * ModConfig.MouseSensitivityX.Value * sens;
            _pitch -= Input.GetAxis("Mouse Y") * ModConfig.MouseSensitivityY.Value * sens;
            _pitch = Mathf.Clamp(_pitch, -ModConfig.CameraMaxLookUp.Value, MaxPitch);

            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f && !IsZoomed)
            {
                _targetDistance = Mathf.Clamp(_targetDistance - scroll * 10f, MinThirdPersonDistance,
                    Mathf.Max(ModConfig.CameraMaxDistance.Value, MinThirdPersonDistance));
            }
        }

        private void ReadViewActions()
        {
            IsZoomed = InputGate.ActionHeld(GameActions.FreeLook);

            float dt = Time.unscaledDeltaTime;
            float rate = Mathf.Min(_cam.fieldOfView / 20f, 1f) * 90f * PlayerSettings.viewSensitivity * dt;
            _yaw += InputGate.ActionAxis(GameActions.PanView) * rate;
            _pitch += InputGate.ActionAxis(GameActions.TiltView) * rate * (PlayerSettings.viewInvertPitch ? -1f : 1f);
            _pitch = Mathf.Clamp(_pitch, -ModConfig.CameraMaxLookUp.Value, MaxPitch);

            _zoomSpeed -= InputGate.ActionAxis(GameActions.ZoomView) * 60f * dt;
            _zoomSpeed = Mathf.Clamp(Mathf.Lerp(_zoomSpeed, 0f, 4f * dt), -10f, 10f);
            if (!IsZoomed)
            {
                _targetDistance = Mathf.Clamp(_targetDistance + _zoomSpeed * dt * _bodyRadius, MinThirdPersonDistance,
                    Mathf.Max(ModConfig.CameraMaxDistance.Value, MinThirdPersonDistance));
            }
            else _zoomSpeed = 0f;
        }

        private static Vector3 KeepAboveGround(Vector3 position)
        {
            const float probeHeight = 50f;
            Vector3 origin = position + Vector3.up * probeHeight;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit ground, probeHeight + 20f, PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore))
            {
                float minY = ground.point.y + GroundClearance;
                if (position.y < minY) position.y = minY;
            }
            return position;
        }

        private void ApplyPose(Vector3 position, Quaternion rotation)
        {
            Transform camT = _cam.transform;
            CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
            Transform rigT = csm != null ? csm.transform : null;
            bool hasSeparateRig = rigT != null && rigT != camT;

            if (hasSeparateRig && !rigT.IsChildOf(camT)) rigT.SetPositionAndRotation(position, rotation);
            camT.SetPositionAndRotation(position, rotation);
            if (hasSeparateRig && rigT.IsChildOf(camT)) rigT.SetPositionAndRotation(position, rotation);
        }
    }
}
