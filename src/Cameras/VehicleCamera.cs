using System;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class VehicleCamera
    {
        private const float ProjectileDistance = 0.25f;
        private const float ProjectileLookTau = 0.06f;
        private const float SightLookTau = 0.015f;
        private const float TauIncDecay = 0.4f;

        private readonly CameraRig _rig = new CameraRig();
        private readonly CameraShake _shake = new CameraShake();
        private static readonly ErrorThrottle StabilizerErrors = new ErrorThrottle("TurretController.StabilizeTurrets");

        private GroundVehicle _vehicle;
        private Camera _cam;
        private float _defaultFov = 60f;
        private float _fovSmooth = 60f;
        private float _magnification = 1f;
        private bool _wasZoomed;
        private float _tauInc;
        private Vector3 _aimOrigin;
        private bool _hasAimOrigin;
        private bool _projectileMode;

        public bool IsZoomed { get; private set; }

        public Ray AimRay { get; private set; }
        public bool HasAimRay { get; private set; }

        public Camera CurrentCamera => _cam;

        public float SightMagnification => _rig.SightZoom;

        public void SetSightMagnification(float magnification)
        {
            _rig.SetSightZoom(magnification);
        }

        public void SetZoomForced(bool forced)
        {
            _rig.ZoomForced = forced;
        }

        public void AddShake(float lowFreqShake, float highFreqShake)
        {
            _shake.Add(lowFreqShake, highFreqShake);
        }

        public static Ray ViewRay(Camera camera)
        {
            VehicleCamera view = VehicleSession.ViewCamera;
            if (VehicleSession.IsActive && view != null && view.HasAimRay) return view.AimRay;
            return camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        public void Attach(GroundVehicle vehicle)
        {
            _vehicle = vehicle;
            _cam = ResolveCamera();
            if (_cam != null) _defaultFov = _cam.fieldOfView;
            _rig.Attach(vehicle, _cam);
            _shake.Attach(vehicle);

            IsZoomed = false;
            _fovSmooth = _defaultFov;
            _magnification = 1f;
            _wasZoomed = false;
            _tauInc = 0f;
            _hasAimOrigin = false;
            _projectileMode = false;
            HasAimRay = false;
        }

        public void ProjectileStarted(Vector3 initialVelocity)
        {
            _projectileMode = true;
            _rig.ResetPivot();
            _hasAimOrigin = false;
            if (initialVelocity.sqrMagnitude < 1f) return;
            _rig.SyncLookTo(initialVelocity);
        }

        public void ProjectileEnded()
        {
            if (!_projectileMode) return;
            _projectileMode = false;
            _rig.ResetPivot();
            _hasAimOrigin = false;
        }

        public void Detach()
        {
            if (_cam != null) _cam.fieldOfView = _defaultFov;

            CameraStateManager csm = SceneSingleton<CameraStateManager>.i;
            if (csm != null) csm.desiredFOV = _defaultFov;

            _shake.Detach();
            _rig.Detach();
            GunSight.RestoreOcclusion();
            GunSight.ResetCache();
            _vehicle = null;
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

            if (InputGate.MouseLookAllowed && !VehicleRadialMenu.IsOpen) _rig.ReadInput();
            IsZoomed = _rig.IsZoomed;
            _shake.Watch(_vehicle);

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);

            try
            {
                VehicleDriver driver = VehicleSession.Driver;
                if (driver != null) driver.Turrets.StabilizeTurrets(dt);
            }
            catch (Exception e)
            {
                StabilizerErrors.Report(e);
            }

            _rig.FollowOriginShift(out Vector3 shift);
            _aimOrigin += shift;

            bool projectile = false;
            Vector3 projectilePos = default;
            Vector3 projectileVel = default;
            if (_projectileMode)
            {
                projectile = ProjectileCam.TryUpdate(dt, out projectilePos, out projectileVel);
                if (!projectile) _projectileMode = false;
            }

            if (IsZoomed != _wasZoomed)
            {
                _tauInc = Mathf.Max(_tauInc, ModConfig.CameraZoomTauInc.Value);
                _wasZoomed = IsZoomed;
                if (IsZoomed)
                {
                    _rig.ResetSightZoom();
                    GunSight.ApplyOcclusion(_vehicle);
                }
                else GunSight.RestoreOcclusion();
            }
            _tauInc = Mathf.MoveTowards(_tauInc, 0f, TauIncDecay * dt);

            float zoomFov = SightFov(_rig.SightZoom);
            _fovSmooth = Approach(_fovSmooth, IsZoomed ? zoomFov : _defaultFov, ModConfig.CameraZoomTau.Value, dt);
            _cam.fieldOfView = _fovSmooth;
            _magnification = _defaultFov > 1f
                ? Mathf.Tan(_defaultFov * Mathf.Deg2Rad * 0.5f) / Mathf.Max(Mathf.Tan(_fovSmooth * Mathf.Deg2Rad * 0.5f), 1e-4f)
                : 1f;

            Vector3 wishDir = _rig.WishDirection;
            if (projectile && projectileVel.sqrMagnitude > 1f)
            {
                wishDir = projectileVel.normalized;
                _rig.SyncLookTo(wishDir);
            }
            float lookTau = projectile
                ? ProjectileLookTau
                : IsZoomed
                    ? SightLookTau
                    : Mathf.Max(ModConfig.CameraLookTau.Value + _tauInc, 1e-4f);
            Vector3 forward = _rig.SmoothLook(wishDir, lookTau, dt);
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 1e-6f) right = Vector3.right;
            right.Normalize();
            Vector3 up = Vector3.Cross(forward, right);

            Vector3 pivot = _rig.Pivot(dt);
            Vector3 position;
            Quaternion rotation;
            Vector3 aimOrigin;

            if (projectile)
            {
                position = projectilePos - forward * ProjectileDistance;
                rotation = Quaternion.LookRotation(forward, Vector3.up);
                aimOrigin = position;
                _hasAimOrigin = false;
            }
            else if (IsZoomed)
            {
                bool onOptic = GunSight.TryGetOpticPoint(_vehicle, out Vector3 sightPos);
                position = onOptic ? sightPos : _rig.ThirdPersonPosition(pivot, forward, up);
                rotation = Quaternion.LookRotation(forward, up);
                aimOrigin = position;
                _aimOrigin = position;
                _hasAimOrigin = true;
            }
            else
            {
                position = _rig.ThirdPersonPosition(pivot, forward, up);
                rotation = Quaternion.LookRotation(forward, up);
                if (!_hasAimOrigin)
                {
                    _aimOrigin = position;
                    _hasAimOrigin = true;
                }
                _aimOrigin = Approach(_aimOrigin, position, ModConfig.CameraAimTau.Value, dt);
                aimOrigin = _aimOrigin;
            }

            AimRay = new Ray(aimOrigin, forward);
            HasAimRay = true;

            float fovRatio = _defaultFov > 1f ? Mathf.Clamp(_fovSmooth / _defaultFov, 0.01f, 1f) : 1f;
            _shake.Tick(dt, fovRatio, out Quaternion shakeRotation, out Vector3 shakeOffset);
            ApplyPose(position + rotation * shakeOffset, rotation * shakeRotation);
        }

        private float SightFov(float magnification)
        {
            float mag = Mathf.Max(magnification, 1f);
            float half = _defaultFov * Mathf.Deg2Rad * 0.5f;
            float fov = 2f * Mathf.Atan(Mathf.Tan(half) / mag) * Mathf.Rad2Deg;
            return Mathf.Clamp(fov, 1f, _defaultFov - 1f);
        }

        private static float Approach(float from, float to, float tau, float dt)
        {
            return Mathf.Lerp(from, to, 1f - Mathf.Exp(-dt / Mathf.Max(tau, 1e-4f)));
        }

        private static Vector3 Approach(Vector3 from, Vector3 to, float tau, float dt)
        {
            return Vector3.Lerp(from, to, 1f - Mathf.Exp(-dt / Mathf.Max(tau, 1e-4f)));
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
