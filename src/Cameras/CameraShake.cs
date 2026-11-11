using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class CameraShake
    {
        private const float RecoilMaxDegrees = 15f;
        private const float PositionPerDegree = 0.012f;
        private const float MaxPositionOffset = 0.3f;

        private GroundVehicle _vehicle;
        private float _trauma;
        private float _time;
        private Vector3 _recoil;
        private Vector3 _recoilVel;
        private Vector3 _recoilImpulse;
        private int[] _ammo = new int[0];

        public float Trauma => _trauma;
        public float RecoilDegrees => _recoil.magnitude * Mathf.Rad2Deg;

        public void Attach(GroundVehicle vehicle)
        {
            _vehicle = vehicle;
            _trauma = 0f;
            _time = 0f;
            _recoil = Vector3.zero;
            _recoilVel = Vector3.zero;
            _recoilImpulse = Vector3.zero;
            RememberAmmo();
        }

        public void Detach()
        {
            _vehicle = null;
            _trauma = 0f;
            _recoil = Vector3.zero;
            _recoilVel = Vector3.zero;
            _recoilImpulse = Vector3.zero;
            _ammo = new int[0];
        }

        public void Add(float lowFreqShake, float highFreqShake)
        {
            float add = Mathf.Max(lowFreqShake, highFreqShake) * ModConfig.ShakeScale.Value;
            if (add <= 0f) return;
            _trauma = Mathf.Clamp01(_trauma + add);
        }

        public void Watch(GroundVehicle vehicle)
        {
            if (vehicle == null) return;
            var stations = vehicle.weaponStations;
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

        public void Tick(float dt, float fovRatio, out Quaternion rotation, out Vector3 position)
        {
            float shake = _trauma * _trauma;
            float shakeDeg = ModConfig.ShakeMaxDegrees.Value;
            Vector3 angles = Vector3.zero;
            Vector3 offset = Vector3.zero;
            if (shake > 0.0001f)
            {
                float t = _time;
                float nx = Mathf.PerlinNoise1D(t) - 0.5f;
                float ny = Mathf.PerlinNoise1D(t + 1000f) - 0.5f;
                float nz = Mathf.PerlinNoise1D(t + 2000f) - 0.5f;
                angles = new Vector3(2f * shakeDeg * shake * nx, 2f * shakeDeg * shake * ny, shakeDeg * shake * nz);
                float meters = Mathf.Min(shakeDeg * PositionPerDegree, MaxPositionOffset) * shake;
                offset = new Vector3(2f * meters * nx, 2f * meters * ny, 0f);
            }

            Vector3 recoil = UpdateRecoil(dt);
            float scale = Mathf.Sqrt(Mathf.Clamp(fovRatio, 0.01f, 1f));
            rotation = Quaternion.Euler((angles + recoil * Mathf.Rad2Deg) * scale);
            position = offset * scale;

            _trauma = Mathf.Clamp01(_trauma - ModConfig.ShakeFadeout.Value * dt);
            _time += ModConfig.ShakeSpeed.Value * dt;
            if (_time > 1e5f) _time = 0f;
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
    }
}
