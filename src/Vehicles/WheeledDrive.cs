using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class WheeledSpec : DriveSpec
    {
        public float[] AxleZ;
        public int SteeredAxles = 1;
        public float TrackHalf;
        public float WheelRadius;
        public float WheelBottomY;
        public float MaxSteerDeg = 35f;

        public static readonly Dictionary<string, WheeledSpec> ByPrefab = new Dictionary<string, WheeledSpec>();

        static WheeledSpec()
        {
            foreach (string prefab in new[] { "AFV8_IFV", "AFV8_APC", "AFV8_SAM" })
            {
                Add(new WheeledSpec
                {
                    Prefab = prefab, Prototype = "K-17 Bumerang", PowerKw = 552f, MaxRpm = 2600f, IdleRpm = 750f,
                    Gears = Allison6, TopKmh = 100f, ReverseKmh = 20f, CenterOfMassY = -0.5f,
                    AxleZ = new[] { 2.51f, 1.09f, -0.80f, -2.23f }, SteeredAxles = 2, TrackHalf = 1.27f, WheelRadius = 0.634f, WheelBottomY = -1.635f,
                });
            }
            foreach (string prefab in new[] { "6x6_1_APC", "6x6_1_IFV", "6x6_1_AT", "6x6_1_AA" })
            {
                Add(new WheeledSpec
                {
                    Prefab = prefab, Prototype = "Patria 6x6", PowerKw = 336f, MaxRpm = 2100f, IdleRpm = 600f,
                    Gears = Allison6, TopKmh = 100f, ReverseKmh = 20f, CenterOfMassY = -0.45f,
                    AxleZ = new[] { 1.5f, -0.4f, -2.26f }, TrackHalf = 1.205f, WheelRadius = 0.605f, WheelBottomY = -1.479f,
                });
            }
            foreach (string prefab in new[] { "HLT-FC", "HLT-FT", "HLT-L", "HLT-M", "HLT-MArt", "HLT-R", "HLT-T" })
            {
                Add(new WheeledSpec
                {
                    Prefab = prefab, Prototype = "HEMTT A4", PowerKw = 373f, MaxRpm = 2100f, IdleRpm = 650f,
                    Gears = Allison6, TopKmh = 100f, ReverseKmh = 20f, CenterOfMassY = 0.1f,
                    AxleZ = new[] { 2.74f, 1.25f, -2.5f, -4.01f }, SteeredAxles = 2, TrackHalf = 1.04f, WheelRadius = 0.669f, WheelBottomY = -0.952f,
                });
            }
            foreach (string prefab in new[] { "Truck2-CRAM", "Truck2-FC", "Truck2-FT", "Truck2-L", "Truck2-LADS", "Truck2-M", "Truck2-R", "Truck2-TBM", "Truck2-TBM-N", "Truck2-MLRS", "Truck2-RSAM", "Truck2-T" })
            {
                bool three = prefab == "Truck2-MLRS" || prefab == "Truck2-RSAM" || prefab == "Truck2-T";
                Add(new WheeledSpec
                {
                    Prefab = prefab, Prototype = "KamAZ-6350", PowerKw = 294f, MaxRpm = 1900f, IdleRpm = 600f,
                    Gears = new[] { 4.5f, 3.2f, 2.3f, 1.7f, 1.3f, 1.0f, 0.85f, 0.75f }, TopKmh = 100f, ReverseKmh = 15f, CenterOfMassY = -0.1f,
                    AxleZ = three ? new[] { 3.57f, -0.84f, -2.27f } : new[] { 3.57f, -0.84f, -2.27f, -3.70f },
                    TrackHalf = 1.015f, WheelRadius = 0.584f, WheelBottomY = -1.216f,
                });
            }
            Add(new WheeledSpec
            {
                Prefab = "Truck2-MRAP", Prototype = "KamAZ Typhoon-K 4x4", PowerKw = 257f, MaxRpm = 2200f, IdleRpm = 600f,
                Gears = Allison6, TopKmh = 105f, ReverseKmh = 20f, CenterOfMassY = -0.7f,
                AxleZ = new[] { 2.24f, -2.16f }, TrackHalf = 1.015f, WheelRadius = 0.584f, WheelBottomY = -1.777f,
            });
            foreach (string prefab in new[] { "LightTruck1_AA", "LightTruck1_AT" })
            {
                Add(new WheeledSpec
                {
                    Prefab = prefab, Prototype = "HMMWV M1151", PowerKw = 140f, MaxRpm = 3400f, IdleRpm = 700f,
                    Gears = new[] { 2.48f, 1.48f, 1.0f, 0.75f }, TopKmh = 110f, ReverseKmh = 20f, CenterOfMassY = -0.6f,
                    AxleZ = new[] { 1.5f, -1.79f }, TrackHalf = 1.0f, WheelRadius = 0.449f, WheelBottomY = -1.215f,
                });
            }
            Add(new WheeledSpec
            {
                Prefab = "LCV45", Prototype = "Oshkosh JLTV", PowerKw = 254f, MaxRpm = 3200f, IdleRpm = 700f,
                Gears = Allison6, TopKmh = 110f, ReverseKmh = 20f, CenterOfMassY = -0.8f,
                AxleZ = new[] { 2.09f, -1.76f }, TrackHalf = 1.005f, WheelRadius = 0.573f, WheelBottomY = -1.574f,
            });
            foreach (string prefab in new[] { "UGV1_grenade", "UGV1_SAM" })
            {
                Add(new WheeledSpec
                {
                    Prefab = prefab, Prototype = "UGV 6x6 (electric)", PowerKw = 100f, MaxRpm = 3000f, IdleRpm = 800f,
                    Gears = new[] { 1.0f }, TopKmh = 70f, ReverseKmh = 20f, CenterOfMassY = -0.4f,
                    AxleZ = new[] { 0.79f, 0.08f, -0.65f }, TrackHalf = 0.65f, WheelRadius = 0.324f, WheelBottomY = -0.771f,
                });
            }
            foreach (string prefab in new[] { "Aryx_HLT_HSM", "Aryx_HLT_HAIL" })
            {
                Add(new WheeledSpec
                {
                    Prefab = prefab, Prototype = "MZKT-79221", PowerKw = 588f, MaxRpm = 2000f, IdleRpm = 700f,
                    Gears = new[] { 4.5f, 3.2f, 2.3f, 1.7f, 1.3f, 1.0f, 0.85f, 0.75f }, TopKmh = 100f, ReverseKmh = 15f, CenterOfMassY = 0.1f,
                    AxleZ = new[] { 3.479f, 1.99f, -1.83f, -3.3f, -4.81f }, SteeredAxles = 2, TrackHalf = 1.19f, WheelRadius = 0.616f, WheelBottomY = -0.9f,
                });
            }
            Add(new WheeledSpec
            {
                Prefab = "Aryx_HLT_HSM_Nuclear", Prototype = "MZKT-79221", PowerKw = 588f, MaxRpm = 2000f, IdleRpm = 700f,
                Gears = new[] { 4.5f, 3.2f, 2.3f, 1.7f, 1.3f, 1.0f, 0.85f, 0.75f }, TopKmh = 100f, ReverseKmh = 15f, CenterOfMassY = 0.1f,
                AxleZ = new[] { 3.479f, 1.99f, -1.83f, -3.3f, -4.81f }, SteeredAxles = 2, TrackHalf = 0.97f, WheelRadius = 0.616f, WheelBottomY = -0.9f,
            });
            Add(new WheeledSpec
            {
                Prefab = "Aryx_MC260_HLT-M", Prototype = "HEMTT A4", PowerKw = 373f, MaxRpm = 2100f, IdleRpm = 650f,
                Gears = Allison6, TopKmh = 100f, ReverseKmh = 20f, CenterOfMassY = 0.1f,
                AxleZ = new[] { 2.739f, 1.25f, -2.5f, -4.01f }, SteeredAxles = 2, TrackHalf = 1.04f, WheelRadius = 0.617f, WheelBottomY = -0.9f,
            });
        }

        private static float[] Allison6 => new[] { 3.5f, 1.9f, 1.44f, 1.0f, 0.74f, 0.64f };

        private static void Add(WheeledSpec spec)
        {
            ByPrefab[spec.Prefab] = spec;
        }
    }

    internal sealed class WheeledDrive : IGroundDrive, IWheelSteer
    {
        private const float TravelUp = 0.25f;
        private const float TravelDown = 0.15f;
        private const float StaticDeflection = 0.10f;
        private const float BumpRatio = 0.3f;
        private const float ReboundRatio = 0.9f;
        private const float AntiRoll = 0.4f;
        private const float MaxLoadG = 5f;
        private const float LongSlipScale = 0.25f;
        private const float LateralSlipScale = 0.25f;
        private const float PeakSlipTan = 0.14f;
        private const float ParkDamping = 0.85f;
        private const float EngineBrake = 0.15f;
        private const float ParkBrake = 0.8f;
        private const float SteerRate = 1.5f;
        private const float SteerFadeSpeed = 14f;
        private const float MaxLateralAccel = 6.5f;
        private const float RoughSpeed = 20f;
        private const float DownhillFrom = 2f;
        private const float DownhillFull = 5f;
        private const float DownhillExtra = 18f / 3.6f;

        private struct Wheel
        {
            public int Axle;
            public float X;
            public float Z;
            public Vector3 Top;
            public float Compression;
            public bool Contact;
            public Vector3 Point;
            public Vector3 Normal;
            public float Load;
            public Surface Surface;
            public Rigidbody Support;
            public float Steer;
            public float Spring;
            public float Bump;
            public float Rebound;
            public float MaxLoad;
        }

        private struct Grip
        {
            public float Mu;
            public float Rolling;
            public bool Rough;

            public Grip(float mu, float rolling, bool rough)
            {
                Mu = mu;
                Rolling = rolling;
                Rough = rough;
            }
        }

        private static readonly Grip[] Grips =
        {
            new Grip(0.85f, 0.015f, false),
            new Grip(0.70f, 0.100f, true),
            new Grip(0.75f, 0.065f, true),
            new Grip(0.78f, 0.050f, true),
            new Grip(0.75f, 0.065f, true),
            new Grip(0.80f, 0.030f, true),
        };

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private readonly Powertrain _engine = new Powertrain();
        private Wheel[] _wheels;
        private WheeledSpec _spec;
        private GroundVehicle _vehicle;
        private Rigidbody _rb;
        private float _mass;
        private float _pivotZ;
        private float _wheelbase;
        private float _steerAngle;
        private bool _towed;
        private float _towBrake;
        private Rigidbody _probeIgnore;

        public DriveSpec Spec => _spec;
        public float Mass => _mass;
        public float TopSpeed => _engine.TopSpeed;
        public bool CanPivot => false;
        public Powertrain Engine => _engine;
        public float Speed { get; private set; }
        public bool Grounded { get; private set; }
        public bool OnPaved { get; private set; }
        public Surface Surface { get; private set; }
        public float Brake { get; private set; }
        public int Contacts { get; private set; }
        public float RadarAlt { get; private set; }
        public float SteerAngle => _steerAngle * Mathf.Rad2Deg;
        public WheeledSpec Wheeled => _spec;

        public float WheelSteerDeg(int axle, int side)
        {
            int i = axle * 2 + side;
            return _wheels != null && i >= 0 && i < _wheels.Length ? _wheels[i].Steer * Mathf.Rad2Deg : 0f;
        }

        private const float VisualOverTravel = 0.2f;

        public float WheelLift(int axle, int side)
        {
            int i = axle * 2 + side;
            if (_wheels == null || i < 0 || i >= _wheels.Length || !_wheels[i].Contact) return -TravelDown;
            return Mathf.Clamp(_wheels[i].Compression - TravelDown, -TravelDown, TravelUp + VisualOverTravel);
        }
        public bool Init(GroundVehicle vehicle, Rigidbody rb, bool ai)
        {
            _towed = false;
            _towBrake = 0f;
            _spec = DriveSpec.For(vehicle) as WheeledSpec;
            _vehicle = vehicle;
            _rb = rb;
            if (_spec == null || rb == null) return false;
            _mass = _spec.MassOf(vehicle);
            BuildWheels();
            _steerAngle = 0f;
            _engine.Init(_spec);
            return true;
        }

        public bool InitTowed(GroundVehicle vehicle, Rigidbody rb, float standingHeight, Rigidbody ignore = null)
        {
            _towed = true;
            _towBrake = 0f;
            _probeIgnore = ignore;
            _vehicle = vehicle;
            _rb = rb;
            if (vehicle == null || rb == null) return false;
            GameObject[] axles = GameAccess.GroundVehicleWheels != null ? GameAccess.GroundVehicleWheels(vehicle) : null;
            if (axles == null) return false;
            Transform t = vehicle.transform;

            var locals = new List<Vector3>();
            var radii = new List<float>();
            foreach (GameObject axle in axles)
            {
                if (axle == null) continue;
                var filter = axle.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;
                MeshSplit.Result split = MeshSplit.Get(filter.sharedMesh, false, out string error);
                if (split == null)
                {
                    ModLog.Warning(vehicle.unitName + ": axle mesh not split, axle skipped: " + error);
                    continue;
                }
                float scaleY = Mathf.Abs(axle.transform.lossyScale.y) > 1e-4f ? Mathf.Abs(axle.transform.lossyScale.y) : 1f;
                for (int half = 0; half < 2; half++)
                {
                    if (split.Parts[half] == null) continue;
                    locals.Add(t.InverseTransformPoint(axle.transform.TransformPoint(split.Centers[half])));
                    radii.Add(Mathf.Clamp(split.Parts[half].bounds.extents.y * scaleY, 0.3f, 0.8f));
                }
            }
            if (locals.Count == 0) return false;
            float radius = 0f;
            foreach (float r in radii) radius += r;
            radius /= radii.Count;

            var axleZ = new List<float>();
            foreach (Vector3 p in locals)
            {
                int cluster = -1;
                for (int i = 0; i < axleZ.Count; i++)
                {
                    if (Mathf.Abs(axleZ[i] - p.z) < 0.6f) { cluster = i; break; }
                }
                if (cluster >= 0) axleZ[cluster] = (axleZ[cluster] + p.z) * 0.5f;
                else axleZ.Add(p.z);
            }
            axleZ.Sort((a, b) => b.CompareTo(a));
            float track = 0f;
            float bottom = float.MaxValue;
            for (int i = 0; i < locals.Count; i++)
            {
                track += Mathf.Abs(locals[i].x);
                bottom = Mathf.Min(bottom, locals[i].y - radii[i]);
            }
            track /= locals.Count;

            _mass = vehicle.definition != null && vehicle.definition.mass > 0f ? vehicle.definition.mass : rb.mass;
            _spec = new WheeledSpec
            {
                Prefab = vehicle.definition != null && vehicle.definition.unitPrefab != null ? vehicle.definition.unitPrefab.name : "trailer",
                Prototype = "trailer", Mass = _mass,
                AxleZ = axleZ.ToArray(), SteeredAxles = 0,
                TrackHalf = track, WheelRadius = radius, WheelBottomY = bottom,
            };
            BuildWheels();
            _steerAngle = 0f;
            ModLog.Info("Trailer drive " + _spec.Prefab + ": wheels " + locals.Count + ", axles " + axleZ.Count
                + " (z " + string.Join("/", axleZ.ConvertAll(z => z.ToString("0.0")).ToArray()) + "), track " + (track * 2f).ToString("0.0")
                + ", radius " + radius.ToString("0.00") + ", mass " + _mass.ToString("0"));
            _rb.drag = 0f;
            _rb.angularDrag = 0.5f;
            _rb.centerOfMass = new Vector3(0f, _spec.WheelBottomY + TowedCoMHeight, 0f);
            return true;
        }

        private const float TowedCoMHeight = 1.0f;

        public void SetTowedBrake(float brake)
        {
            _towBrake = Mathf.Clamp01(brake);
        }

        private void BuildWheels()
        {
            int axles = _spec.AxleZ.Length;
            int steered = Mathf.Clamp(_spec.SteeredAxles, 1, Mathf.Max(1, axles - 1));
            float rear = 0f;
            for (int a = steered; a < axles; a++) rear += _spec.AxleZ[a];
            _pivotZ = rear / Mathf.Max(1, axles - steered);
            _wheelbase = Mathf.Max(0.5f, _spec.AxleZ[0] - _pivotZ);

            var wheels = new List<Wheel>();
            float axleY = _spec.WheelBottomY + _spec.WheelRadius;
            for (int a = 0; a < axles; a++)
            {
                for (int side = 0; side < 2; side++)
                {
                    float x = side == 0 ? -_spec.TrackHalf : _spec.TrackHalf;
                    wheels.Add(new Wheel
                    {
                        Axle = a, X = x, Z = _spec.AxleZ[a],
                        Top = new Vector3(x, axleY + TravelUp, _spec.AxleZ[a]),
                    });
                }
            }
            _wheels = wheels.ToArray();

            float mean = 0f;
            for (int a = 0; a < axles; a++) mean += _spec.AxleZ[a];
            mean /= axles;
            float moment = 0f;
            for (int a = 0; a < axles; a++) moment += (_spec.AxleZ[a] - mean) * _spec.AxleZ[a];
            float slope = Mathf.Abs(moment) > 1e-4f ? -axles * mean / moment : 0f;
            float weight = _mass * Physics.gravity.magnitude;
            for (int i = 0; i < _wheels.Length; i++)
            {
                ref Wheel w = ref _wheels[i];
                float share = Mathf.Clamp(1f + slope * (w.Z - mean), 0.5f, 1.5f);
                float load = weight / _wheels.Length * share;
                w.Spring = load / StaticDeflection;
                float critical = 2f * Mathf.Sqrt(w.Spring * load / Physics.gravity.magnitude);
                w.Bump = BumpRatio * critical;
                w.Rebound = ReboundRatio * critical;
                w.MaxLoad = MaxLoadG * load;
            }
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
            if (_towed)
            {
                throttle = 0f;
                steer = 0f;
            }

            SampleContacts(t, dt);

            Vector3 forward = t.forward;
            Speed = Vector3.Dot(_rb.velocity, forward);

            float v = Mathf.Abs(Speed);
            float maxAngle = _spec.MaxSteerDeg * Mathf.Deg2Rad / (1f + v * v / (SteerFadeSpeed * SteerFadeSpeed));
            if (v > 1f) maxAngle = Mathf.Max(maxAngle, Mathf.Min(_spec.MaxSteerDeg * Mathf.Deg2Rad, Mathf.Atan(_wheelbase * MaxLateralAccel / (v * v))));
            _steerAngle = Mathf.MoveTowards(_steerAngle, steer * maxAngle, SteerRate * dt);
            SetWheelAngles();

            bool parked = !_towed && Mathf.Abs(throttle) < 0.05f && Mathf.Abs(Speed) < 1f;
            float demand = Mathf.Abs(throttle);
            if (!_towed) _engine.Update(throttle, false, demand, Speed);

            float drive = 0f;
            float brake = 0f;
            float engine = 0f;
            if (_towed)
            {
                brake = _towBrake;
            }
            else if (parked)
            {
                brake = ParkBrake;
            }
            else if (Mathf.Abs(throttle) < 0.05f)
            {
                brake = EngineBrake;
            }
            else if (Mathf.Abs(Speed) > 0.5f && Mathf.Sign(throttle) != Mathf.Sign(Speed))
            {
                brake = Mathf.Abs(throttle);
            }
            else
            {
                engine = Mathf.Sign(throttle) * _engine.Force(demand);
                drive = engine;
            }

            if (!_towed)
            {
                float downhill = Speed > 0f ? Mathf.Asin(Mathf.Clamp(-forward.y, -1f, 1f)) * Mathf.Rad2Deg : 0f;
                float speedLimit = TopSpeed + DownhillExtra * Mathf.Clamp01((downhill - DownhillFrom) / (DownhillFull - DownhillFrom));
                if (Speed > speedLimit)
                {
                    brake = Mathf.Max(brake, Mathf.Clamp01((Speed - speedLimit) / 2f));
                    drive -= engine;
                }
            }
            Brake = brake;

            float totalLoad = 0f;
            int touching = 0;
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (!_wheels[i].Contact) continue;
                totalLoad += _wheels[i].Load;
                touching++;
            }

            int paved = 0;
            int[] surfaceVotes = new int[6];
            float maxStopPerWheel = _mass / Mathf.Max(1, touching) / dt;
            Vector3 up = t.up;
            Vector3 gFlat = Vector3.ProjectOnPlane(Physics.gravity, Vector3.up);
            for (int i = 0; i < _wheels.Length; i++)
            {
                ref Wheel w = ref _wheels[i];
                if (!w.Contact) continue;
                surfaceVotes[(int)w.Surface]++;
                if (w.Surface == Surface.Paved) paved++;

                Vector3 normal = w.Normal;
                Vector3 heading = Quaternion.AngleAxis(w.Steer * Mathf.Rad2Deg, up) * forward;
                Vector3 fwd = Vector3.ProjectOnPlane(heading, normal).normalized;
                Vector3 side = Vector3.Cross(normal, fwd);
                Vector3 pv = _rb.GetPointVelocity(w.Point);
                if (w.Support != null) pv -= w.Support.GetPointVelocity(w.Point);
                float vLong = Vector3.Dot(pv, fwd);
                float vLat = Vector3.Dot(pv, side);

                Grip grip = Grips[(int)w.Surface];
                float limit = grip.Mu * w.Load;
                float share = totalLoad > 0f ? w.Load / totalLoad : 0f;
                float rolling = grip.Rolling * (grip.Rough ? 1f + vLong * vLong / (RoughSpeed * RoughSpeed) : 1f);

                float own = drive * share;
                float fLong = own;
                float fLat = 0f;
                if (parked)
                {
                    Vector3 hold = -gFlat * (_mass * share);
                    fLong += Vector3.Dot(hold, fwd);
                    fLat += Vector3.Dot(hold, side);
                    fLong -= ParkDamping * maxStopPerWheel * vLong;
                    fLat -= ParkDamping * maxStopPerWheel * vLat;
                }
                else
                {
                    fLong -= brake * limit * GroundProbe.Tanh(vLong / LongSlipScale);
                    fLat = -grip.Mu * w.Load * GroundProbe.Tanh(vLat / Mathf.Max(LateralSlipScale, Mathf.Abs(vLong) * PeakSlipTan));
                }
                fLong -= rolling * w.Load * GroundProbe.Tanh(vLong / 0.3f);

                if (!parked)
                {
                    float stopLong = maxStopPerWheel * Mathf.Abs(vLong);
                    float resist = fLong - own;
                    if (Mathf.Abs(resist) > stopLong) fLong = own + Mathf.Sign(resist) * stopLong;
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

            Contacts = touching;
            Grounded = touching > 0;
            OnPaved = touching > 0 && paved * 2 >= touching;
            int best = 0;
            for (int s = 1; s < surfaceVotes.Length; s++)
            {
                if (surfaceVotes[s] > surfaceVotes[best]) best = s;
            }
            Surface = (Surface)best;
            RadarAlt = Grounded ? 0f : GroundProbe.Height(t, _rb, t.TransformPoint(new Vector3(0f, _spec.WheelBottomY, 0f)), 200f, _hits);
        }

        private void SetWheelAngles()
        {
            float tan = Mathf.Tan(_steerAngle);
            for (int i = 0; i < _wheels.Length; i++)
            {
                ref Wheel w = ref _wheels[i];
                if (w.Axle >= _spec.SteeredAxles || Mathf.Abs(tan) < 1e-4f)
                {
                    w.Steer = 0f;
                    continue;
                }
                float radius = _wheelbase / tan;
                w.Steer = Mathf.Atan((w.Z - _pivotZ) / (radius - w.X));
            }
        }

        private void SampleContacts(Transform t, float dt)
        {
            float travel = TravelUp + TravelDown;
            float preload = TravelDown - StaticDeflection;
            for (int i = 0; i < _wheels.Length; i++)
            {
                ref Wheel w = ref _wheels[i];
                float previous = w.Compression;
                bool hadContact = w.Contact;
                w.Contact = false;
                w.Load = 0f;
                w.Support = null;
                if (!GroundProbe.Cast(t, _rb, t.TransformPoint(w.Top), _spec.WheelRadius, travel, _hits, out RaycastHit ground, out float distance, _probeIgnore))
                {
                    w.Compression = 0f;
                    continue;
                }
                w.Compression = travel - distance;
                float rate = hadContact && dt > 0f ? (w.Compression - previous) / dt : 0f;
                w.Load = GroundProbe.SpringLoad(w.Compression, travel, preload, rate, w.Spring, w.Bump, w.Rebound, w.MaxLoad);
                w.Contact = w.Load > 0f;
                w.Point = ground.point;
                w.Normal = ground.normal;
                Rigidbody body = ground.collider.attachedRigidbody;
                w.Support = body != null && body != _rb ? body : null;
                w.Surface = SurfaceMap.At(ground.point, ground.collider);
            }

            for (int i = 0; i + 1 < _wheels.Length; i += 2)
            {
                ref Wheel l = ref _wheels[i];
                ref Wheel r = ref _wheels[i + 1];
                if (!l.Contact || !r.Contact) continue;
                float force = AntiRoll * 0.5f * (l.Spring + r.Spring) * (Mathf.Min(l.Compression, travel) - Mathf.Min(r.Compression, travel));
                l.Load = Mathf.Clamp(l.Load + force, 0f, l.MaxLoad);
                r.Load = Mathf.Clamp(r.Load - force, 0f, r.MaxLoad);
            }
        }
    }
}
