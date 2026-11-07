using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class EngineSound
    {
        private const float BlendRate = 3f;
        private const float BlendFallRate = 0.8f;
        private const float LoadFallRate = 0.7f;
        private const float IdlePitchRise = 0.3f;
        private const float IdleVolume = 1f;

        private AudioSource _idle;
        private AudioSource _drive;
        private float _minPitch;
        private float _maxPitch;
        private float _idlePitch;
        private float _blend;
        private float _load;

        public void Attach(GroundVehicle vehicle)
        {
            Detach();
            if (GameAccess.GroundVehicleEngineIdleSound == null || GameAccess.GroundVehicleEngineDriveSound == null) return;
            _idle = GameAccess.GroundVehicleEngineIdleSound(vehicle);
            _drive = GameAccess.GroundVehicleEngineDriveSound(vehicle);
            if (_idle == null || _drive == null)
            {
                _idle = _drive = null;
                return;
            }
            _minPitch = GameAccess.GroundVehicleEngineMinPitch != null ? GameAccess.GroundVehicleEngineMinPitch(vehicle) : 0.5f;
            _maxPitch = GameAccess.GroundVehicleEngineMaxPitch != null ? GameAccess.GroundVehicleEngineMaxPitch(vehicle) : 1f;
            _idlePitch = _idle.pitch;
            _blend = 0f;
            _load = 0f;
        }

        public void Tick(GroundVehicle vehicle, IGroundDrive drive)
        {
            Tick(vehicle, drive != null ? drive.Engine : null);
        }

        public void Tick(GroundVehicle vehicle, Powertrain engine)
        {
            if (_idle == null || _drive == null || engine == null) return;
            if (vehicle.disabled || vehicle.displayDetail < 1f) return;

            float rpm = Mathf.InverseLerp(engine.IdleRpm, engine.MaxRpm, engine.Rpm);
            float dt = Time.deltaTime;
            _load = engine.Load > _load ? engine.Load : Mathf.MoveTowards(_load, engine.Load, LoadFallRate * dt);
            float working = Mathf.Clamp01(Mathf.Max(_load, rpm * 1.5f));
            _blend = Mathf.MoveTowards(_blend, working, (working > _blend ? BlendRate : BlendFallRate) * dt);

            _drive.pitch = Mathf.Lerp(_minPitch, _maxPitch, rpm);
            float driveVolume = _blend * (0.5f + 0.5f * Mathf.Max(_load, rpm));
            float idleVolume = IdleVolume * (1f - _blend);
            Set(_drive, driveVolume);
            Set(_idle, idleVolume);
            _idle.pitch = _idlePitch * (1f + IdlePitchRise * rpm);
        }

        private static void Set(AudioSource source, float volume)
        {
            source.volume = volume;
            if (volume > 0.01f)
            {
                if (!source.isPlaying)
                {
                    source.Play();
                    if (source.clip != null) source.time = Random.Range(0f, source.clip.length);
                }
            }
            else if (source.isPlaying)
            {
                source.Stop();
            }
        }

        public Dictionary<string, object> Describe()
        {
            return new Dictionary<string, object>
            {
                ["blend"] = _blend,
                ["idle"] = Describe(_idle),
                ["drive"] = Describe(_drive),
                ["same"] = _idle != null && _idle == _drive,
            };
        }

        private static Dictionary<string, object> Describe(AudioSource source)
        {
            if (source == null) return null;
            return new Dictionary<string, object>
            {
                ["clip"] = source.clip != null ? source.clip.name : null,
                ["volume"] = source.volume,
                ["pitch"] = source.pitch,
                ["playing"] = source.isPlaying,
                ["loop"] = source.loop,
                ["enabled"] = source.enabled && source.gameObject.activeInHierarchy,
                ["mute"] = source.mute,
            };
        }

        public void Detach()
        {
            if (_idle != null) _idle.pitch = _idlePitch;
            _idle = null;
            _drive = null;
        }
    }
}
