using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class TankSpec : DriveSpec
    {
        public float TrackX;
        public float TrackZ;
        public float TrackLength;
        public float TrackBottomY;
        public int RoadWheels = 7;
        public float WheelRadius = 0.35f;
        public float RoadInset = 0.75f;
        public float EndWheelRaise = 0.22f;
        public float RoadFrontZ = float.NaN;
        public float RoadRearZ = float.NaN;
        public float SprocketRaise = float.NaN;

        public static readonly Dictionary<string, TankSpec> ByPrefab = new Dictionary<string, TankSpec>
        {
            ["MBT1"] = new TankSpec
            {
                Prefab = "MBT1", Prototype = "Leopard 2A7", Mass = 64000f, PowerKw = 1103f, MaxRpm = 2600f, IdleRpm = 700f,
                Gears = new[] { 5.0f, 2.8f, 1.65f, 1.0f }, TopKmh = 80f, ReverseKmh = 31f,
                TrackX = 1.42f, TrackZ = -0.07f, TrackLength = 7.67f, TrackBottomY = -1.355f, CenterOfMassY = -0.35f,
            },
            ["MBT"] = new TankSpec
            {
                Prefab = "MBT", Prototype = "T-90M", Mass = 46500f, PowerKw = 831f, MaxRpm = 2000f, IdleRpm = 800f,
                Gears = new[] { 5.4f, 3.5f, 2.5f, 1.9f, 1.5f, 1.2f, 1.0f }, TopKmh = 85f, ReverseKmh = 5f,
                TrackX = 1.51f, TrackZ = -0.19f, TrackLength = 7.2f, TrackBottomY = -1.535f, CenterOfMassY = -0.45f,
            },
            ["Linebreaker_IFV"] = Bradley("Linebreaker_IFV", "M2A3 Bradley", 30400f, 447f),
            ["Linebreaker_APC"] = Bradley("Linebreaker_APC", "AMPV M1283", 36000f, 503f),
            ["Linebreaker_SAM"] = Bradley("Linebreaker_SAM", "M6 Linebacker", 30000f, 447f),
            ["SPAAG1"] = new TankSpec
            {
                Prefab = "SPAAG1", Prototype = "Gepard 1A2", Mass = 47500f, PowerKw = 610f, MaxRpm = 2200f, IdleRpm = 650f,
                Gears = new[] { 4.6f, 2.6f, 1.6f, 1.0f }, TopKmh = 75f, ReverseKmh = 25f,
                TrackX = 1.422f, TrackZ = -0.285f, TrackLength = 7.666f, TrackBottomY = -1.325f, CenterOfMassY = -0.3f,
            },
            ["SPAAG2"] = new TankSpec
            {
                Prefab = "SPAAG2", Prototype = "2S38 Derivatsiya-PVO", Mass = 20000f, PowerKw = 368f, MaxRpm = 2600f, IdleRpm = 800f,
                Gears = new[] { 4.4f, 2.5f, 1.6f, 1.0f }, TopKmh = 85f, ReverseKmh = 20f, RoadWheels = 6,
                TrackX = 1.41f, TrackZ = -0.059f, TrackLength = 6.644f, TrackBottomY = -1.291f, CenterOfMassY = -0.4f,
            },
            ["RadarSAM1"] = new TankSpec
            {
                Prefab = "RadarSAM1", Prototype = "Tor-M1", Mass = 34000f, PowerKw = 618f, MaxRpm = 2000f, IdleRpm = 800f,
                Gears = new[] { 4.6f, 2.6f, 1.6f, 1.0f }, TopKmh = 75f, ReverseKmh = 15f,
                TrackX = 1.422f, TrackZ = -0.285f, TrackLength = 7.666f, TrackBottomY = -1.529f, CenterOfMassY = -0.5f,
            },
            ["UGVDozer1"] = new TankSpec
            {
                Prefab = "UGVDozer1", Prototype = "DOK-ING MV-4", Mass = 6000f, PowerKw = 129f, MaxRpm = 2300f, IdleRpm = 900f,
                Gears = new[] { 1.0f }, TopKmh = 40f, ReverseKmh = 20f, RoadWheels = 4, WheelRadius = 0.15f,
                EndWheelRaise = 0.08f, RoadFrontZ = 1.2f, RoadRearZ = -0.8f, SprocketRaise = 0.3f,
                TrackX = 0.684f, TrackZ = 0.003f, TrackLength = 2.951f, TrackBottomY = -0.716f, CenterOfMassY = -0.35f,
            },
            ["Aryx_ADLT_01"] = new TankSpec
            {
                Prefab = "Aryx_ADLT_01", Prototype = "Light tank (27 t)", Mass = 27000f, PowerKw = 405f, MaxRpm = 2400f, IdleRpm = 700f,
                Gears = new[] { 5.0f, 2.8f, 1.65f, 1.0f }, TopKmh = 70f, ReverseKmh = 20f,
                TrackX = 1.422f, TrackZ = -0.285f, TrackLength = 7.666f, TrackBottomY = -1.325f, CenterOfMassY = -0.4f,
            },
            ["Aryx_CRVL_1_Recon"] = new TankSpec
            {
                Prefab = "Aryx_CRVL_1_Recon", Prototype = "Light tracked platform", Mass = 8000f, PowerKw = 200f, MaxRpm = 2600f, IdleRpm = 800f,
                Gears = new[] { 4.4f, 2.5f, 1.6f, 1.0f }, TopKmh = 87f, ReverseKmh = 20f, RoadWheels = 5,
                TrackX = 0.95f, TrackZ = 0.09f, TrackLength = 4.31f, TrackBottomY = -0.98f, CenterOfMassY = -0.4f,
            },
            ["Aryx_CRVL_1_Mortar"] = new TankSpec
            {
                Prefab = "Aryx_CRVL_1_Mortar", Prototype = "Light tracked platform", Mass = 7000f, PowerKw = 200f, MaxRpm = 2600f, IdleRpm = 800f,
                Gears = new[] { 4.4f, 2.5f, 1.6f, 1.0f }, TopKmh = 87f, ReverseKmh = 20f, RoadWheels = 5,
                TrackX = 0.95f, TrackZ = 0.09f, TrackLength = 4.31f, TrackBottomY = -0.98f, CenterOfMassY = -0.4f,
            },
            ["Aryx_FRCV_Artillery"] = new TankSpec
            {
                Prefab = "Aryx_FRCV_Artillery", Prototype = "M109-class SPH", Mass = 28000f, PowerKw = 331f, MaxRpm = 2300f, IdleRpm = 700f,
                Gears = new[] { 4.5f, 3.2f, 2.3f, 1.7f, 1.3f, 1.0f }, TopKmh = 70f, ReverseKmh = 15f,
                TrackX = 1.422f, TrackZ = -0.285f, TrackLength = 7.666f, TrackBottomY = -1.325f, CenterOfMassY = -0.4f,
            },
            ["Aryx_HeavyCEV"] = new TankSpec
            {
                Prefab = "Aryx_HeavyCEV", Prototype = "Heavy combat engineer vehicle", Mass = 53000f, PowerKw = 970f, MaxRpm = 2600f, IdleRpm = 700f,
                Gears = new[] { 5.0f, 2.8f, 1.65f, 1.0f }, TopKmh = 80f, ReverseKmh = 15f, RoadWheels = 6,
                TrackX = 1.85f, TrackZ = -0.47f, TrackLength = 8.6f, TrackBottomY = -1.445f, CenterOfMassY = -0.4f,
            },
        };

        private static TankSpec Bradley(string prefab, string prototype, float mass, float powerKw)
        {
            return new TankSpec
            {
                Prefab = prefab, Prototype = prototype, Mass = mass, PowerKw = powerKw, MaxRpm = 2600f, IdleRpm = 650f,
                Gears = new[] { 3.2f, 1.75f, 1.0f }, TopKmh = 80f, ReverseKmh = 20f, RoadWheels = 6,
                TrackX = 1.41f, TrackZ = -0.059f, TrackLength = 6.644f, TrackBottomY = -1.291f, CenterOfMassY = -0.4f,
            };
        }
    }

    internal sealed class TrackedDrive : IGroundDrive
    {
        private const float TravelUp = 0.25f;
        private const float TravelDown = 0.15f;
        private const float DampingRatio = 0.5f;
        private const float BumpShare = 0.5f;
        private const float ReboundShare = 1.5f;
        private const float MaxLoadG = 5f;
        private const float LongSlipScale = 0.25f;
        private const float LateralSlipScale = 0.25f;
        private const float EngineBrake = 0.3f;
        private const float ParkBrake = 0.6f;
        private const float ParkDamping = 0.85f;
        private const float PivotYawRate = 0.69f;
        private const float PivotForceBoost = 1.05f;
        private const float MaxLateralAccel = 5.5f;
        private const float SteerForce = 0.88f * 9.81f;
        private const float SteerYawSlack = 0.15f;
        private const float LateralGrip = 1.0f;
        private const float LateralGripMoving = 0.8f;
        private const float LateralGripSpeed = 10f;
        private const float DownhillFrom = 2f;
        private const float DownhillFull = 5f;
        private const float DownhillExtra = 18f / 3.6f;
        private const int AiRoadWheels = 4;

        private struct Wheel
        {
            public int Side;
            public Vector3 Top;
            public float Radius;
            public float Compression;
            public bool Contact;
            public Vector3 Point;
            public Vector3 Normal;
            public float Load;
            public Surface Surface;
            public Rigidbody Support;
        }

        private struct Grip
        {
            public float Mu;
            public float Rolling;

            public Grip(float mu, float rolling)
            {
                Mu = mu;
                Rolling = rolling;
            }
        }

        private static readonly Grip[] Grips =
        {
            new Grip(0.70f, 0.030f),
            new Grip(0.70f, 0.100f),
            new Grip(0.80f, 0.070f),
            new Grip(0.80f, 0.060f),
            new Grip(0.75f, 0.080f),
            new Grip(0.85f, 0.050f),
        };

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly float[] _drive = new float[2];
        private readonly float[] _brake = new float[2];
        private readonly float[] _sideLoad = new float[2];
        private readonly int[] _surfaceVotes = new int[6];
        private readonly Powertrain _engine = new Powertrain();
        private Wheel[] _wheels;
        private TankSpec _spec;
        private GroundVehicle _vehicle;
        private Rigidbody _rb;
        private float _mass;
        private float _springRate;
        private float _damping;
        private float _maxLoad;

        public DriveSpec Spec => _spec;
        public TankSpec Tank => _spec;
        public float Mass => _mass;
        public float TopSpeed => _engine.TopSpeed;
        public bool CanPivot => true;
        public Powertrain Engine => _engine;
        public float Speed { get; private set; }
        public bool Grounded { get; private set; }
        public bool OnPaved { get; private set; }
        public Surface Surface { get; private set; }
        public float Brake { get; private set; }
        public int Contacts { get; private set; }
        public float RadarAlt { get; private set; }
        public float TrackSpeedLeft { get; private set; }
        public float TrackSpeedRight { get; private set; }

        public bool Init(GroundVehicle vehicle, Rigidbody rb, bool ai)
        {
            _spec = DriveSpec.For(vehicle) as TankSpec;
            _vehicle = vehicle;
            _rb = rb;
            if (_spec == null || rb == null) return false;
            _mass = _spec.MassOf(vehicle);
            int count = ai ? Mathf.Min(AiRoadWheels, _spec.RoadWheels) : _spec.RoadWheels;

            var wheels = new List<Wheel>();
            float half = _spec.TrackLength * 0.5f;
            float r = _spec.WheelRadius;
            float frontEnd = _spec.TrackZ + half - r;
            float rearEnd = _spec.TrackZ - half + r;
            float firstRoad = float.IsNaN(_spec.RoadFrontZ) ? frontEnd - _spec.RoadInset : _spec.RoadFrontZ;
            float lastRoad = float.IsNaN(_spec.RoadRearZ) ? rearEnd + _spec.RoadInset : _spec.RoadRearZ;
            float raise = _spec.EndWheelRaise;
            float rearRaise = float.IsNaN(_spec.SprocketRaise) ? raise : _spec.SprocketRaise;
            for (int side = 0; side < 2; side++)
            {
                float x = side == 0 ? -_spec.TrackX : _spec.TrackX;
                float axleY = _spec.TrackBottomY + r;
                wheels.Add(MakeWheel(side, x, axleY + raise, frontEnd, r));
                for (int i = 0; i < count; i++)
                {
                    float z = Mathf.Lerp(firstRoad, lastRoad, i / (float)Mathf.Max(1, count - 1));
                    wheels.Add(MakeWheel(side, x, axleY, z, r));
                }
                wheels.Add(MakeWheel(side, x, axleY + rearRaise, rearEnd, r));
            }
            _wheels = wheels.ToArray();

            int support = 2 * count;
            _springRate = _mass * Physics.gravity.magnitude / (support * TravelDown);
            _damping = 2f * DampingRatio * Mathf.Sqrt(_springRate * _mass / support);
            _maxLoad = MaxLoadG * _mass * Physics.gravity.magnitude / support;

            _engine.Init(_spec);
            return true;
        }

        private Wheel MakeWheel(int side, float x, float axleY, float z, float radius)
        {
            return new Wheel { Side = side, Top = new Vector3(x, axleY + TravelUp, z), Radius = radius };
        }

        public void ApplyBody()
        {
            _rb.mass = _mass;
            _rb.centerOfMass = new Vector3(0f, _spec.CenterOfMassY, 0f);
            _rb.drag = 0f;
            _rb.angularDrag = 0.5f;
        }

        public void Step(float throttle, float steer)
        {
            float dt = Time.fixedDeltaTime;
            Transform t = _vehicle.transform;
            throttle = Mathf.Clamp(throttle, -1f, 1f);
            steer = Mathf.Clamp(steer, -1f, 1f);

            SampleContacts(t, dt);

            Vector3 forward = t.forward;
            Vector3 up = t.up;
            Speed = Vector3.Dot(_rb.velocity, forward);
            float yaw = Vector3.Dot(_rb.angularVelocity, up);
            TrackSpeedLeft = Speed + yaw * _spec.TrackX;
            TrackSpeedRight = Speed - yaw * _spec.TrackX;

            bool pivot = Mathf.Abs(throttle) < 0.1f && Mathf.Abs(steer) > 0.1f;
            bool parked = Mathf.Abs(throttle) < 0.05f && Mathf.Abs(steer) < 0.05f;
            float demand = Mathf.Max(Mathf.Abs(throttle), Mathf.Abs(steer));
            _engine.Update(throttle, pivot, demand, Speed);

            float[] drive = _drive;
            float[] brake = _brake;
            Array.Clear(drive, 0, drive.Length);
            Array.Clear(brake, 0, brake.Length);
            float engine = 0f;
            bool braking = false;
            if (parked)
            {
                brake[0] = brake[1] = ParkBrake;
            }
            else if (Mathf.Abs(throttle) < 0.05f)
            {
                if (!pivot) brake[0] = brake[1] = EngineBrake;
            }
            else if (Mathf.Abs(Speed) > 0.5f && Mathf.Sign(throttle) != Mathf.Sign(Speed))
            {
                brake[0] = brake[1] = Mathf.Abs(throttle);
                braking = true;
            }
            else
            {
                engine = Mathf.Sign(throttle) * _engine.Force(Mathf.Abs(throttle));
                drive[0] = drive[1] = engine * 0.5f;
            }

            float yawTarget = steer * MaxYawRate(Speed) * (Speed < -0.5f ? -1f : 1f);
            float steerForce = Mathf.Clamp((yawTarget - yaw) / SteerYawSlack, -1f, 1f) * SteerForce * _mass * 0.5f * (pivot ? PivotForceBoost : 1f);
            if (Mathf.Abs(steer) > 0.05f)
            {
                drive[0] += steerForce;
                drive[1] -= steerForce;
                if (!braking) brake[0] = brake[1] = 0f;
            }

            float downhill = Speed > 0f ? Mathf.Asin(Mathf.Clamp(-forward.y, -1f, 1f)) * Mathf.Rad2Deg : 0f;
            float speedLimit = TopSpeed + DownhillExtra * Mathf.Clamp01((downhill - DownhillFrom) / (DownhillFull - DownhillFrom));
            if (Speed > speedLimit)
            {
                float over = Mathf.Clamp01((Speed - speedLimit) / 2f);
                for (int s = 0; s < 2; s++)
                {
                    brake[s] = Mathf.Max(brake[s], over);
                    drive[s] -= engine * 0.5f;
                }
            }
            Brake = Mathf.Max(brake[0], brake[1]);

            float[] sideLoad = _sideLoad;
            Array.Clear(sideLoad, 0, sideLoad.Length);
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i].Contact) sideLoad[_wheels[i].Side] += _wheels[i].Load;
            }
            float totalLoad = sideLoad[0] + sideLoad[1];
            Vector3 gFlat = Vector3.ProjectOnPlane(Physics.gravity, Vector3.up);

            int contacts = 0, paved = 0;
            int[] surfaceVotes = _surfaceVotes;
            Array.Clear(surfaceVotes, 0, surfaceVotes.Length);
            float maxStopPerWheel = _mass / Mathf.Max(1, CountContacts()) / dt;
            float lateralGrip = Mathf.Lerp(LateralGrip, LateralGripMoving, Mathf.Abs(Speed) / LateralGripSpeed);
            for (int i = 0; i < _wheels.Length; i++)
            {
                ref Wheel w = ref _wheels[i];
                if (!w.Contact) continue;
                contacts++;
                surfaceVotes[(int)w.Surface]++;
                if (w.Surface == Surface.Paved) paved++;

                Vector3 normal = w.Normal;
                Vector3 fwd = Vector3.ProjectOnPlane(forward, normal).normalized;
                Vector3 side = Vector3.Cross(normal, fwd);
                Vector3 v = _rb.GetPointVelocity(w.Point);
                if (w.Support != null) v -= w.Support.GetPointVelocity(w.Point);
                float vLong = Vector3.Dot(v, fwd);
                float vLat = Vector3.Dot(v, side);

                Grip grip = Grips[(int)w.Surface];
                float limit = grip.Mu * w.Load;
                float share = sideLoad[w.Side] > 0f ? w.Load / sideLoad[w.Side] : 0f;

                float fLong = drive[w.Side] * share;
                float fLat = 0f;
                if (parked)
                {
                    Vector3 hold = -gFlat * (_mass * (totalLoad > 0f ? w.Load / totalLoad : 0f));
                    fLong += Vector3.Dot(hold, fwd);
                    fLat += Vector3.Dot(hold, side);
                    fLong -= ParkDamping * maxStopPerWheel * vLong;
                    fLat -= ParkDamping * maxStopPerWheel * vLat;
                }
                else
                {
                    fLong -= brake[w.Side] * limit * GroundProbe.Tanh(vLong / LongSlipScale);
                    fLat = -lateralGrip * grip.Mu * w.Load * GroundProbe.Tanh(vLat / LateralSlipScale);
                }
                fLong -= grip.Rolling * w.Load * GroundProbe.Tanh(vLong / 0.3f);

                if (!parked)
                {
                    float stopLong = maxStopPerWheel * Mathf.Abs(vLong);
                    float resist = fLong - drive[w.Side] * share;
                    if (Mathf.Abs(resist) > stopLong) fLong = drive[w.Side] * share + Mathf.Sign(resist) * stopLong;
                    float stopLat = maxStopPerWheel * Mathf.Abs(vLat);
                    if (Mathf.Abs(fLat) > stopLat) fLat = Mathf.Sign(fLat) * stopLat;
                }

                float total = Mathf.Sqrt(fLong * fLong + fLat * fLat);
                if (total > limit && total > 0f)
                {
                    float k = limit / total;
                    fLong *= k;
                    fLat *= k;
                }

                _rb.AddForceAtPosition(up * w.Load + fwd * fLong + side * fLat, w.Point);
            }

            Contacts = contacts;
            Grounded = contacts > 0;
            OnPaved = contacts > 0 && paved * 2 >= contacts;
            int best = 0;
            for (int s = 1; s < surfaceVotes.Length; s++)
            {
                if (surfaceVotes[s] > surfaceVotes[best]) best = s;
            }
            Surface = (Surface)best;
            RadarAlt = Grounded ? 0f : GroundProbe.Height(t, _rb, t.TransformPoint(new Vector3(0f, _spec.TrackBottomY, _spec.TrackZ)), 200f, _hits);
        }

        private static float MaxYawRate(float speed)
        {
            float v = Mathf.Abs(speed);
            return v < 0.1f ? PivotYawRate : Mathf.Min(PivotYawRate, MaxLateralAccel / v);
        }

        private int CountContacts()
        {
            int n = 0;
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i].Contact) n++;
            }
            return n;
        }

        private void SampleContacts(Transform t, float dt)
        {
            float travel = TravelUp + TravelDown;
            for (int i = 0; i < _wheels.Length; i++)
            {
                ref Wheel w = ref _wheels[i];
                float previous = w.Compression;
                bool hadContact = w.Contact;
                w.Contact = false;
                w.Load = 0f;
                w.Support = null;
                if (!GroundProbe.Cast(t, _rb, t.TransformPoint(w.Top), w.Radius, travel, _hits, out RaycastHit ground, out float distance))
                {
                    w.Compression = 0f;
                    continue;
                }
                w.Compression = travel - distance;
                float rate = hadContact && dt > 0f ? (w.Compression - previous) / dt : 0f;
                w.Load = GroundProbe.SpringLoad(w.Compression, travel, 0f, rate, _springRate, _damping * BumpShare, _damping * ReboundShare, _maxLoad);
                w.Contact = w.Load > 0f;
                w.Point = ground.point;
                w.Normal = ground.normal;
                Rigidbody body = ground.collider.attachedRigidbody;
                w.Support = body != null && body != _rb ? body : null;
                w.Surface = SurfaceMap.At(ground.point, ground.collider);
            }
        }
    }
}
