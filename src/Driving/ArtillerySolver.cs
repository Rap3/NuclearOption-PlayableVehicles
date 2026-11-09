using UnityEngine;

namespace PlayableVehicles
{
    internal enum FireMode : byte
    {
        None,
        Low,
        High,
        Missile,
    }

    internal static class ArtillerySolver
    {
        public enum Status
        {
            Ready,
            OutOfRange,
            ElevationLimit,
            TooClose,
            NotAirborne,
            AltitudeLimit,
            NoRadar,
            NoLineOfSight,
        }

        public struct Solution
        {
            public Vector3 Direction;
            public float Elevation;
            public float TimeOfFlight;
            public float Range;
            public Status Status;
        }

        public static bool IsArtillery(Turret turret)
        {
            if (turret == null) return false;
            if (GameAccess.TurretAimSolver != null && GameAccess.AimSolverArtillery != null)
            {
                AimSolver solver = GameAccess.TurretAimSolver(turret);
                if (solver != null && GameAccess.AimSolverArtillery(solver)) return true;
            }
            return turret.GetComponentInParent<MobileArtilleryAI>() != null;
        }

        public static Solution Solve(Turret turret, WeaponInfo info, Vector3 muzzle, Vector3 target, FireMode mode)
        {
            Vector3 toTarget = target - muzzle;
            var horizontal = new Vector3(toTarget.x, 0f, toTarget.z);
            float range = horizontal.magnitude;
            var solution = new Solution { Range = range, Status = Status.OutOfRange };
            if (info == null || range < 1f) return solution;

            float speed = info.GetMaxSpeed();
            float gravity = 9.81f * info.gravMult;
            if (speed <= 0f || gravity <= 0f) return solution;

            float maxRange = info.targetRequirements.maxRange;
            float ratio = maxRange > 0f ? range / maxRange : 0f;
            float drag = DragCoef(turret);
            float effective = ratio > 0.2f ? Mathf.Lerp(range * (1f + 0.5f * drag), range * (1f + 1.5f * drag), ratio) : range;

            float v2 = speed * speed;
            float discriminant = v2 * v2 - gravity * (gravity * effective * effective + 2f * toTarget.y * v2);
            if (discriminant < 0f) return solution;

            float root = Mathf.Sqrt(discriminant);
            float tan = (mode == FireMode.High ? v2 + root : v2 - root) / (gravity * effective);
            float angle = Mathf.Atan(tan);

            solution.Direction = horizontal.normalized + Vector3.up * tan;
            solution.Elevation = angle * Mathf.Rad2Deg;
            solution.TimeOfFlight = effective / (speed * Mathf.Cos(angle));
            float maxElevation = MaxElevation(turret);
            solution.Status = maxElevation > 0f && solution.Elevation > maxElevation ? Status.ElevationLimit : Status.Ready;
            return solution;
        }

        private const float ScanStep = 2f;
        private const float MinScanElevation = -10f;
        private const float MaxScanElevation = 86f;
        private const int MaxCells = 64;
        private const int RefineIterations = 12;
        private const int InterpolationIterations = 6;
        private const float MinBracket = ScanStep / 4096f;
        private const float RangeTolerance = 0.01f;
        private const float CoarseStep = 0.1f;
        private const int MaxWalk = 4;
        private const float CoarseMargin = 0.03f;
        private const float CoarseMarginMeters = 20f;
        private const float HintLifetime = 3f;
        private const float ResultLifetime = 0.4f;
        private const float ResultQuantum = 0.1f;
        private const int ResultSlots = 4;
        private const int CoarseUnreachable = -2;

        private enum Outcome
        {
            Found,
            ElevationLimit,
            OutOfRange,
            Fail,
        }

        private static GunProfile _gProfile;
        private static Vector3 _gOrigin;
        private static Vector3 _gInherited;
        private static Vector3 _gHeading;
        private static float _gTargetY;
        private static int _gCount = -1;
        private static readonly float[] _fine = new float[MaxCells];
        private static readonly float[] _fineTime = new float[MaxCells];
        private static readonly float[] _coarse = new float[MaxCells];

        private static int _hintLow = -1;
        private static int _hintHigh = -1;
        private static int _hintCount = -1;
        private static float _hintSpeed;
        private static float _hintGrav;
        private static float _hintDrag;
        private static float _hintLifetime;
        private static float _hintStamp = -100f;

        private struct ResultEntry
        {
            public bool Used;
            public float Stamp;
            public int Mode;
            public int Ox, Oy, Oz, Tx, Ty, Tz, Vx, Vy, Vz;
            public float Speed, Grav, Drag, Lifetime, MaxElevation;
            public Solution Solution;
        }

        private static readonly ResultEntry[] _results = new ResultEntry[ResultSlots];
        private static int _resultNext;

        public static Solution SolveSimulated(in GunProfile profile, Turret turret, Vector3 target, FireMode mode)
        {
            Vector3 origin = profile.Muzzle.position;
            Vector3 inherited = profile.InheritedVelocity;
            float maxElevation = MaxElevation(turret);
            float now = Time.realtimeSinceStartup;

            int modeKey = mode == FireMode.High ? 1 : 0;
            int ox = Quantize(origin.x), oy = Quantize(origin.y), oz = Quantize(origin.z);
            int tx = Quantize(target.x), ty = Quantize(target.y), tz = Quantize(target.z);
            int vx = Quantize(inherited.x), vy = Quantize(inherited.y), vz = Quantize(inherited.z);
            for (int i = 0; i < ResultSlots; i++)
            {
                ref ResultEntry e = ref _results[i];
                if (e.Used && now - e.Stamp <= ResultLifetime && e.Mode == modeKey
                    && e.Ox == ox && e.Oy == oy && e.Oz == oz && e.Tx == tx && e.Ty == ty && e.Tz == tz
                    && e.Vx == vx && e.Vy == vy && e.Vz == vz
                    && e.Speed == profile.Speed && e.Grav == profile.GravMult && e.Drag == profile.DragCoef
                    && e.Lifetime == profile.Lifetime && e.MaxElevation == maxElevation)
                {
                    return e.Solution;
                }
            }

            Solution solution = SolveCore(profile, origin, inherited, maxElevation, target, mode, now - _hintStamp <= HintLifetime);
            if (solution.Status == Status.Ready) _hintStamp = now;

            ref ResultEntry slot = ref _results[_resultNext];
            _resultNext = (_resultNext + 1) % ResultSlots;
            slot.Used = true;
            slot.Stamp = now;
            slot.Mode = modeKey;
            slot.Ox = ox; slot.Oy = oy; slot.Oz = oz;
            slot.Tx = tx; slot.Ty = ty; slot.Tz = tz;
            slot.Vx = vx; slot.Vy = vy; slot.Vz = vz;
            slot.Speed = profile.Speed; slot.Grav = profile.GravMult; slot.Drag = profile.DragCoef;
            slot.Lifetime = profile.Lifetime; slot.MaxElevation = maxElevation;
            slot.Solution = solution;
            return solution;
        }

        private static int Quantize(float value)
        {
            return Mathf.RoundToInt(value / ResultQuantum);
        }

        internal static Solution SolveCore(in GunProfile profile, Vector3 origin, Vector3 inherited, float maxElevation,
            Vector3 target, FireMode mode, bool useHint)
        {
            Vector3 toTarget = target - origin;
            var horizontal = new Vector3(toTarget.x, 0f, toTarget.z);
            float want = horizontal.magnitude;
            var solution = new Solution { Range = want, Status = Status.OutOfRange };
            if (want < 1f || profile.Speed <= 0f) return solution;
            Vector3 heading = horizontal / want;

            float gravity = 9.81f * profile.GravMult;
            float height = target.y - origin.y;
            float lowAngle = 0f, highAngle = 0f;
            bool vacuumAngles = false;
            if (gravity > 0f)
            {
                float fastest = profile.Speed + inherited.magnitude;
                float reach = want / 1.02f - 10f;
                float f2 = fastest * fastest;
                if (reach > 0f && f2 * f2 - gravity * (gravity * reach * reach + 2f * height * f2) < 0f) return solution;

                float v2 = profile.Speed * profile.Speed;
                float discriminant = v2 * v2 - gravity * (gravity * want * want + 2f * height * v2);
                if (discriminant >= 0f)
                {
                    float root = Mathf.Sqrt(discriminant);
                    lowAngle = Mathf.Atan((v2 - root) / (gravity * want)) * Mathf.Rad2Deg;
                    highAngle = Mathf.Atan((v2 + root) / (gravity * want)) * Mathf.Rad2Deg;
                    vacuumAngles = true;
                }
            }

            float top = maxElevation > 0f ? Mathf.Min(maxElevation, MaxScanElevation) : MaxScanElevation;
            int count = Mathf.Min(Mathf.FloorToInt((top - MinScanElevation) / ScanStep) + 1, MaxCells);
            if (count < 1) return solution;
            BeginGrid(profile, origin, inherited, heading, target.y, count);

            bool high = mode == FireMode.High;
            Outcome outcome = Outcome.Fail;
            int cell = -1;

            if (useHint && _hintCount == count && _hintSpeed == profile.Speed && _hintGrav == profile.GravMult
                && _hintDrag == profile.DragCoef && _hintLifetime == profile.Lifetime)
            {
                int hint = high ? _hintHigh : _hintLow;
                if (hint >= 0 && hint < count) outcome = Walk(high, hint, want, count, out cell);
            }

            if (outcome == Outcome.Fail)
            {
                int extreme = high ? count - 1 : 0;
                int start = extreme;
                if (vacuumAngles)
                {
                    start = high
                        ? Mathf.Clamp(Mathf.CeilToInt((highAngle - MinScanElevation) / ScanStep) + 1, 0, count - 1)
                        : Mathf.Clamp(Mathf.FloorToInt((lowAngle - MinScanElevation) / ScanStep) - 1, 0, count - 1);
                }

                int candidate = CoarseCandidate(high, want, count, start);
                if (candidate == -1 && start != extreme) candidate = CoarseCandidate(high, want, count, extreme);

                if (candidate == CoarseUnreachable) return solution;
                if (candidate >= 0) outcome = Walk(high, candidate, want, count, out cell);
            }

            if (outcome == Outcome.Fail) outcome = ExactScan(high, want, count, out cell);

            if (outcome == Outcome.OutOfRange) return solution;
            if (outcome == Outcome.ElevationLimit)
            {
                solution.Status = Status.ElevationLimit;
                return solution;
            }

            if (high) _hintHigh = cell;
            else _hintLow = cell;
            _hintCount = count;
            _hintSpeed = profile.Speed;
            _hintGrav = profile.GravMult;
            _hintDrag = profile.DragCoef;
            _hintLifetime = profile.Lifetime;

            return Refine(high, cell, want, solution);
        }

        private static void BeginGrid(in GunProfile profile, Vector3 origin, Vector3 inherited, Vector3 heading, float targetY, int count)
        {
            bool same = _gCount == count && _gTargetY == targetY && Same(_gOrigin, origin) && Same(_gInherited, inherited)
                && Same(_gHeading, heading) && _gProfile.Speed == profile.Speed && _gProfile.GravMult == profile.GravMult
                && _gProfile.DragCoef == profile.DragCoef && _gProfile.Lifetime == profile.Lifetime;
            if (same) return;

            _gProfile = profile;
            _gProfile.Muzzle = null;
            _gProfile.InheritVelocityFrom = null;
            _gOrigin = origin;
            _gInherited = inherited;
            _gHeading = heading;
            _gTargetY = targetY;
            _gCount = count;
            for (int i = 0; i < count; i++)
            {
                _fine[i] = float.NaN;
                _coarse[i] = float.NaN;
            }
        }

        private static bool Same(Vector3 a, Vector3 b)
        {
            return a.x == b.x && a.y == b.y && a.z == b.z;
        }

        private static float Elevation(int cell)
        {
            return MinScanElevation + cell * ScanStep;
        }

        private static float FineRange(int cell)
        {
            float cached = _fine[cell];
            if (!float.IsNaN(cached)) return cached;
            float range = RangeAt(Elevation(cell), out float time);
            _fine[cell] = range;
            _fineTime[cell] = time;
            return range;
        }

        private static float CoarseRange(int cell)
        {
            float cached = _coarse[cell];
            if (!float.IsNaN(cached)) return cached;
            float range = CoarseRangeAt(Elevation(cell));
            _coarse[cell] = range;
            return range;
        }

        private static int CoarseCandidate(bool high, float want, int count, int start)
        {
            float previous = -1f, best = -1f;
            if (high)
            {
                for (int i = start; i >= 0; i--)
                {
                    float range = CoarseRange(i);
                    if (range >= want) return i;
                    if (range >= 0f && previous >= 0f && range < previous) break;
                    if (range > best) best = range;
                    previous = range;
                }
            }
            else
            {
                for (int i = start; i < count; i++)
                {
                    float range = CoarseRange(i);
                    if (range >= want) return i;
                    if (range >= 0f && previous >= 0f && range < previous) break;
                    if (range > best) best = range;
                    previous = range;
                }
            }
            bool fromEdge = high ? start == count - 1 : start == 0;
            if (!fromEdge) return -1;
            return best >= 0f && best * (1f + CoarseMargin) + CoarseMarginMeters < want ? CoarseUnreachable : -1;
        }

        private static Outcome Walk(bool high, int start, float want, int count, out int cell)
        {
            cell = -1;
            int i = Mathf.Clamp(start, 0, count - 1);
            for (int step = 0; step <= MaxWalk; step++)
            {
                float range = FineRange(i);
                if (high)
                {
                    if (range >= want)
                    {
                        if (i == count - 1) return Outcome.ElevationLimit;
                        if (FineRange(i + 1) < want)
                        {
                            cell = i;
                            return Outcome.Found;
                        }
                        i++;
                    }
                    else
                    {
                        if (i == 0) return Outcome.Fail;
                        if (range >= 0f && FineRange(i - 1) <= range) return Outcome.Fail;
                        i--;
                    }
                }
                else
                {
                    if (range >= want)
                    {
                        if (i == 0) return Outcome.ElevationLimit;
                        if (FineRange(i - 1) < want)
                        {
                            cell = i;
                            return Outcome.Found;
                        }
                        i--;
                    }
                    else
                    {
                        if (i == count - 1) return Outcome.Fail;
                        if (range >= 0f && FineRange(i + 1) <= range) return Outcome.Fail;
                        i++;
                    }
                }
            }
            return Outcome.Fail;
        }

        private static Outcome ExactScan(bool high, float want, int count, out int cell)
        {
            cell = -1;
            int peak = -1;
            for (int i = 0; i < count; i++)
            {
                float range = FineRange(i);
                if (range >= 0f && (peak < 0 || range > FineRange(peak))) peak = i;
            }
            if (peak < 0 || FineRange(peak) < want) return Outcome.OutOfRange;

            if (high)
            {
                int i = peak;
                while (i + 1 < count && FineRange(i + 1) >= want) i++;
                if (i + 1 >= count) return Outcome.ElevationLimit;
                cell = i;
            }
            else
            {
                int i = 0;
                while (i < peak && FineRange(i) < want) i++;
                if (i == 0) return Outcome.ElevationLimit;
                cell = i;
            }
            return Outcome.Found;
        }

        private static Solution Refine(bool high, int cell, float want, Solution solution)
        {
            int satCell = cell;
            int unsatCell = high ? cell + 1 : cell - 1;
            float b = Elevation(satCell);
            float rb = FineRange(satCell);
            float tb = _fineTime[satCell];
            float a = Elevation(unsatCell);
            float ra = FineRange(unsatCell);

            float aim = want + RangeTolerance * 0.5f;
            float fa = ra - aim;
            float fb = rb - aim;
            int side = 0;
            for (int k = 0; k < InterpolationIterations + RefineIterations; k++)
            {
                if (rb - want <= RangeTolerance || Mathf.Abs(b - a) <= MinBracket) break;

                float x;
                if (k < InterpolationIterations && ra >= 0f && fb - fa > 1e-6f)
                {
                    x = (a * fb - b * fa) / (fb - fa);
                    float lo = Mathf.Min(a, b), hi = Mathf.Max(a, b), margin = (hi - lo) * 0.01f;
                    x = Mathf.Clamp(x, lo + margin, hi - margin);
                }
                else
                {
                    x = (a + b) * 0.5f;
                }

                float rx = RangeAt(x, out float tx);
                if (rx >= want)
                {
                    b = x;
                    rb = rx;
                    tb = tx;
                    fb = rx - aim;
                    if (side == 1) fa *= 0.5f;
                    side = 1;
                }
                else
                {
                    a = x;
                    ra = rx;
                    if (rx >= 0f) fa = rx - aim;
                    if (side == -1) fb *= 0.5f;
                    side = -1;
                }
            }

            solution.Elevation = b;
            solution.Direction = Direction(_gHeading, b);
            solution.TimeOfFlight = tb;
            solution.Status = Status.Ready;
            return solution;
        }

        private static float RangeAt(float elevation, out float time)
        {
            Vector3 velocity = _gInherited + Direction(_gHeading, elevation) * _gProfile.Speed;
            if (!Ballistics.CrossDescending(_gProfile, _gOrigin, velocity, _gTargetY, out Vector3 point, out time)) return -1f;
            point -= _gOrigin;
            point.y = 0f;
            return point.magnitude;
        }

        private static float CoarseRangeAt(float elevation)
        {
            Vector3 velocity = _gInherited + Direction(_gHeading, elevation) * _gProfile.Speed;
            Vector3 position = _gOrigin;
            int steps = Mathf.CeilToInt(_gProfile.Lifetime / CoarseStep);
            float minSpeedSqr = _gProfile.Speed * _gProfile.Speed * 0.02f;
            float gravityStep = 9.81f * CoarseStep * _gProfile.GravMult;
            float dragStep = _gProfile.DragCoef * CoarseStep / _gProfile.Speed;
            for (int i = 0; i < steps; i++)
            {
                if (velocity.sqrMagnitude < minSpeedSqr) break;
                Vector3 previous = position;
                velocity.y -= gravityStep;
                velocity -= velocity.magnitude * velocity * dragStep;
                position += velocity * CoarseStep;
                if (velocity.y < 0f && previous.y > _gTargetY && position.y <= _gTargetY)
                {
                    float t = (previous.y - _gTargetY) / Mathf.Max(previous.y - position.y, 1e-6f);
                    float dx = previous.x + (position.x - previous.x) * t - _gOrigin.x;
                    float dz = previous.z + (position.z - previous.z) * t - _gOrigin.z;
                    return Mathf.Sqrt(dx * dx + dz * dz);
                }
            }
            return -1f;
        }

        private static Vector3 Direction(Vector3 heading, float elevation)
        {
            float radians = elevation * Mathf.Deg2Rad;
            return heading * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians);
        }

        private static float DragCoef(Turret turret)
        {
            AimSolver solver = GameAccess.TurretAimSolver != null ? GameAccess.TurretAimSolver(turret) : null;
            return solver != null && GameAccess.AimSolverDragCoef != null ? GameAccess.AimSolverDragCoef(solver) : 1f;
        }

        private static float MaxElevation(Turret turret)
        {
            return turret != null && GameAccess.TurretMaxElevation != null ? GameAccess.TurretMaxElevation(turret) : 0f;
        }
    }
}
