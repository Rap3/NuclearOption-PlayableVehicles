using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class Powertrain
    {
        private const float DriveRadius = 0.35f;
        private const float DriveEfficiency = 0.85f;
        private const float ShiftTime = 0.25f;
        private const float ShiftTorque = 0.5f;
        private const float UpshiftRpm = 0.95f;
        private const float LightUpshiftRpm = 0.6f;
        private const float DownshiftRpm = 0.45f;
        private const float KickdownDemand = 0.6f;
        private const float KickdownForceGain = 1.15f;
        private const float KickdownMaxRpm = 0.92f;
        private const float UpshiftForceShare = 0.95f;
        private const float MinGearTime = 1.2f;
        private const float RpmRiseRate = 1.5f;
        private const float RpmFallRate = 1.2f;
        private const float StallRpm = 0.65f;
        private const float ConverterMultiplication = 1.8f;
        private const float PowerFactor = 1.815f;

        private DriveSpec _spec;
        private float _finalDrive;
        private float _reverseRatio;
        private float _peakTorque;
        private float _shiftUntil;
        private float _shaftRpm;
        private float _gearSince;

        public float Rpm { get; private set; }
        public float Load { get; private set; }
        public int Gear { get; private set; } = 1;
        public float IdleRpm => _spec.IdleRpm;
        public float MaxRpm => _spec.MaxRpm;
        public float TopSpeed => _spec.TopKmh / 3.6f;

        public void Init(DriveSpec spec)
        {
            _spec = spec;
            float omegaMax = spec.MaxRpm * Mathf.PI / 30f;
            float topGear = spec.Gears[spec.Gears.Length - 1];
            _finalDrive = omegaMax * DriveRadius / (spec.TopKmh / 3.6f * topGear);
            _reverseRatio = spec.ReverseKmh > 0f
                ? omegaMax * DriveRadius / (spec.ReverseKmh / 3.6f * _finalDrive)
                : 1f;
            _peakTorque = spec.PowerKw * 1000f / (0.85f * omegaMax);
            Gear = StartGear();
            _shiftUntil = 0f;
            _gearSince = 0f;
            Rpm = spec.IdleRpm;
            Load = 0f;
        }

        public void Update(float throttle, bool pivot, float demand, float speed)
        {
            float now = Time.time;
            if (pivot)
            {
                if (Gear != StartGear() && Mathf.Abs(speed) < 0.5f) Shift(StartGear(), now);
            }
            else if (throttle < -0.1f && speed < 0.5f)
            {
                if (Gear != -1) Shift(-1, now);
            }
            else if (throttle > 0.1f && speed > -0.5f && Gear < 1)
            {
                Shift(StartGear(), now);
            }

            float hullSpeed = Mathf.Abs(speed);
            _shaftRpm = RpmAt(hullSpeed, Gear);
            float stall = _spec.MaxRpm * StallRpm;
            float target = Mathf.Max(_spec.IdleRpm, demand > 0.05f ? Mathf.Max(_shaftRpm, stall * demand) : _shaftRpm);
            float rate = _spec.MaxRpm * (target > Rpm ? RpmRiseRate : RpmFallRate);
            Rpm = Mathf.MoveTowards(Rpm, Mathf.Min(target, _spec.MaxRpm), rate * Time.fixedDeltaTime);

            if (Gear >= 1 && now >= _shiftUntil)
            {
                float upshift = _spec.MaxRpm * Mathf.Lerp(LightUpshiftRpm, UpshiftRpm, demand);
                bool settled = now >= _gearSince + MinGearTime;
                bool coasting = demand < 0.05f;
                bool loaded = demand > KickdownDemand;
                float force = loaded ? ForceAt(Gear, hullSpeed) : 0f;
                bool upAllowed = !loaded || ForceAt(Gear + 1, hullSpeed) >= force * UpshiftForceShare;
                bool kickdown = loaded && Gear > 1 && RpmAt(hullSpeed, Gear - 1) < _spec.MaxRpm * KickdownMaxRpm
                    && ForceAt(Gear - 1, hullSpeed) > force * KickdownForceGain;
                if (Gear < _spec.Gears.Length && (_shaftRpm > _spec.MaxRpm * 0.98f || settled && !coasting && upAllowed && _shaftRpm > upshift)) Shift(Gear + 1, now);
                else if (Gear > 1 && settled && (kickdown || _shaftRpm < _spec.MaxRpm * DownshiftRpm && RpmAt(hullSpeed, Gear - 1) < upshift * 0.9f)) Shift(Gear - 1, now);
            }
            if (Gear > StartGear() && hullSpeed < 0.5f && now >= _shiftUntil) Shift(StartGear(), now);

            Load = now < _shiftUntil ? demand * 0.3f : demand;
        }

        public float Force(float demand)
        {
            if (demand <= 0f) return 0f;
            if (Time.time < _shiftUntil) demand *= ShiftTorque;
            float rpm = Rpm;
            if (rpm >= _spec.MaxRpm) return 0f;
            float torque = _peakTorque * PowerFactor * TorqueShape(rpm / _spec.MaxRpm) * demand * Converter(_shaftRpm);
            return torque * Ratio(Gear) * _finalDrive * DriveEfficiency / DriveRadius;
        }

        private int StartGear() => _spec.Gears.Length > 4 ? 2 : 1;

        private void Shift(int gear, float now)
        {
            Gear = gear;
            _shiftUntil = now + ShiftTime;
            _gearSince = now;
        }

        private float Ratio(int gear) => gear < 0 ? _reverseRatio : _spec.Gears[Mathf.Clamp(gear, 1, _spec.Gears.Length) - 1];

        private float RpmAt(float speed, int gear)
        {
            return speed / DriveRadius * Ratio(gear) * _finalDrive * 30f / Mathf.PI;
        }

        private float TorqueShape(float x)
        {
            return x < 0.6f ? Mathf.Lerp(0.75f, 1f, Mathf.InverseLerp(_spec.IdleRpm / _spec.MaxRpm, 0.6f, x)) : Mathf.Lerp(1f, 0.85f, (x - 0.6f) / 0.4f);
        }

        private float Converter(float shaftRpm)
        {
            return Mathf.Max(1f, Mathf.Lerp(ConverterMultiplication, 1f, shaftRpm / (_spec.MaxRpm * StallRpm)));
        }

        private float ForceAt(int gear, float hullSpeed)
        {
            if (gear < 1 || gear > _spec.Gears.Length) return 0f;
            float shaft = RpmAt(hullSpeed, gear);
            if (shaft >= _spec.MaxRpm * 0.98f) return 0f;
            float rpm = Mathf.Max(shaft, _spec.MaxRpm * StallRpm);
            return TorqueShape(rpm / _spec.MaxRpm) * Converter(shaft) * Ratio(gear);
        }
    }
}
