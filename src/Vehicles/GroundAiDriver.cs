using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class GroundAiDriver : MonoBehaviour
    {
        private const float InputInterval = 0.2f;
        private const float StationaryDelay = 5f;
        private const float UnderwaterGlobalY = -2f;
        private const float FullSteerAngle = 20f;
        private const float WheelYawDamping = 0.5f;
        private const float PivotAngle = 60f;
        private const float ArcTurnSpeed = 2.5f;
        private const float KmhToMs = 1f / 3.6f;
        private const float TopSpeedShare = 0.95f;
        private const float MinGap = 8f;
        private const float FollowTime = 2f;
        private const float BrakeGain = 0.35f;
        private const float DeployingBlockTimeout = 60f;
        private bool _lastStationary;
        private float _stationaryChangeTime = -999f;
        private const float WheeledCornerAccel = 2f;
        private const float TrackedCornerAccel = 1.8f;
        private const float CornerChord = 40f;
        private const float MinCornerSpeed = 15f / 3.6f;
        private const float TurnHoldAngle = 150f;
        private const float PassedAheadCos = 0.35f;
        private const float PathLookahead = 250f;
        private const float PlanDecel = 3f;
        private const float MinPathTurn = 0.05f;
        private const float LaneMargin = 1f;
        private const float FollowRange = 80f;
        private const float BlockedDelay = 4f;

        private GroundVehicle _vehicle;
        private Rigidbody _rb;
        private IGroundDrive _drive;
        private readonly TrackAnimator _tracks = new TrackAnimator();
        private readonly WheelAnimator _wheelsAnim = new WheelAnimator();
        private readonly EngineSound _sound = new EngineSound();
        private bool _active;
        private bool _finished;

        private float _origMass;
        private Vector3 _origCenterOfMass;
        private float _origDrag;
        private float _origAngularDrag;

        private float _throttle;
        private float _steer;
        private float _nextInput;
        private float _stationaryTime;
        private float _stuckTimer;
        private float _reverseTimer;
        private float _bulldozeTimer;
        private float _blockedTime;
        private float _turnSign;

        public bool HasRoute { get; private set; }

        public static bool Enabled => ModConfig.ModAiDriving == null || ModConfig.ModAiDriving.Value;
        public bool Active => _active;

        private void Awake()
        {
            _vehicle = GetComponent<GroundVehicle>();
            _rb = GetComponent<Rigidbody>();
            _drive = DriveSpec.CreateDrive(_vehicle);
            _nextInput = Random.Range(0f, InputInterval);
        }

        public void Suspend()
        {
            if (!_active) return;
            _active = false;
            _tracks.Detach();
            _wheelsAnim.Detach();
            _sound.Detach();
            if (_rb != null)
            {
                _rb.mass = _origMass;
                _rb.centerOfMass = _origCenterOfMass;
                _rb.drag = _origDrag;
                _rb.angularDrag = _origAngularDrag;
            }
            if (_vehicle != null) _vehicle.speed = 0f;
        }

        public bool Wake()
        {
            if (!_active || _vehicle == null) return false;
            _stationaryTime = 0f;
            _vehicle.NetworknetworkStationary = false;
            return true;
        }

        private bool Take()
        {
            if (_drive == null || !_drive.Init(_vehicle, _rb, true)) return false;
            RemoteVehicleVisuals visuals = GetComponent<RemoteVehicleVisuals>();
            if (visuals != null) visuals.Suspend();
            _origMass = _rb.mass;
            _origCenterOfMass = _rb.centerOfMass;
            _origDrag = _rb.drag;
            _origAngularDrag = _rb.angularDrag;
            _vehicle.enabled = false;
            _drive.ApplyBody();
            if (_drive is TrackedDrive) _tracks.Attach(_vehicle);
            if (_drive is WheeledDrive wheeled) _wheelsAnim.Attach(_vehicle, wheeled);
            _sound.Attach(_vehicle);
            _throttle = 0f;
            _steer = 0f;
            _stationaryTime = 0f;
            _stuckTimer = 0f;
            _reverseTimer = 0f;
            _active = true;
            return true;
        }

        private bool CanDrive()
        {
            if (_finished || _vehicle == null || _rb == null || !NetBridge.IsServer) return false;
            if (!Enabled)
            {
                if (_active)
                {
                    Suspend();
                    if (!_vehicle.IsSlung() && !ControlRegistry.IsControlled(_vehicle)) _vehicle.enabled = true;
                    GameAccess.GroundVehicleResetStationary(_vehicle) = true;
                }
                return false;
            }
            if (_vehicle.disabled)
            {
                Suspend();
                if (!_vehicle.enabled && !_vehicle.IsSlung()) _vehicle.enabled = true;
                GameAccess.GroundVehicleResetStationary(_vehicle) = true;
                _finished = true;
                return false;
            }
            if (ControlRegistry.IsControlled(_vehicle) || _vehicle.IsSlung())
            {
                Suspend();
                return false;
            }
            return true;
        }

        private const float LodInterval = 1f;
        private float _nextLod;
        private bool _far;

        private void FixedUpdate()
        {
            if (!CanDrive()) return;
            if (Time.time >= _nextLod && !UpdateLod()) return;
            if (_far) return;
            if (!_active && !Take()) return;
            if (_vehicle.enabled) _vehicle.enabled = false;

            LauncherArmPose.Tick(_vehicle);

            float dt = Time.fixedDeltaTime;
            if (_vehicle.transform.position.GlobalY() < UnderwaterGlobalY)
            {
                _vehicle.Networkdisabled = true;
                return;
            }

            if (Time.time >= _nextInput)
            {
                _nextInput = Time.time + InputInterval;
                Decide();
            }

            bool anchored = GameAccess.GroundVehicleAnchored != null && GameAccess.GroundVehicleAnchored(_vehicle);
            if (_vehicle.NetworknetworkStationary != _lastStationary)
            {
                _lastStationary = _vehicle.NetworknetworkStationary;
                _stationaryChangeTime = Time.time;
            }
            if (_vehicle.NetworknetworkStationary)
            {
                _vehicle.speed = 0f;
                _vehicle.radarAlt = 0f;
                if ((_throttle != 0f || _steer != 0f) && !anchored)
                {
                    _stationaryTime = 0f;
                    _vehicle.NetworknetworkStationary = false;
                }
                return;
            }
            if (_rb.isKinematic) _rb.isKinematic = false;

            if (Time.time - _stationaryChangeTime < DeployingBlockTimeout && DeployParts.AnyMoving(_vehicle))
            {
                _throttle = 0f;
                _steer = 0f;
                _drive.Step(0f, 0f);
                _vehicle.speed = 0f;
                _vehicle.radarAlt = _drive.RadarAlt;
                _stationaryTime = 0f;
                return;
            }

            _drive.Step(_throttle, _steer);
            _vehicle.speed = _drive.Speed;
            _vehicle.radarAlt = _drive.RadarAlt;

            if (Mathf.Abs(_drive.Speed) < 0.3f && _drive.Grounded) _stationaryTime += dt;
            else _stationaryTime = 0f;
            if (!anchored && _stationaryTime > StationaryDelay && _throttle == 0f && _steer == 0f)
            {
                _vehicle.NetworknetworkStationary = true;
                return;
            }

            Transform t = _vehicle.transform;
            if (Mathf.Abs(_drive.Speed) < 1f && Vector3.Dot(t.up, Vector3.up) < 0.8f)
            {
                Vector3 local = t.InverseTransformDirection(_rb.angularVelocity);
                Vector3 axis = Vector3.Dot(t.right, Vector3.up) > 0f ? -t.forward : t.forward;
                _rb.AddTorque(axis * (0.3f - local.z), ForceMode.VelocityChange);
            }
        }

        private bool UpdateLod()
        {
            _nextLod = Time.time + LodInterval * Random.Range(0.8f, 1.2f);
            bool near = AiDriveLod.WantsOwnDrive(_vehicle.transform.position, !_far);
            if (near == !_far) return !_far;
            if (!near)
            {
                if (_active && !_drive.Grounded) return true;
                if (_active)
                {
                    Suspend();
                    if (!_vehicle.IsSlung()) _vehicle.enabled = true;
                    GameAccess.GroundVehicleResetStationary(_vehicle) = true;
                }
                _far = true;
                return false;
            }
            _far = false;
            return true;
        }

        private void Update()
        {
            if (!_active || _vehicle == null) return;
            GameAccess.GroundVehicleUpdate(_vehicle);
            if (!GameplayUI.GameIsPaused && _drive is TrackedDrive tracked) _tracks.Tick(tracked.TrackSpeedLeft, tracked.TrackSpeedRight);
            if (!GameplayUI.GameIsPaused) _wheelsAnim.Tick(_drive.Speed);
            _sound.Tick(_vehicle, _drive);
        }

        private void OnDestroy()
        {
            _tracks.Detach();
            _wheelsAnim.Detach();
            _sound.Detach();
        }

        private void Decide()
        {
            _throttle = 0f;
            _steer = 0f;
            Transform t = _vehicle.transform;
            float speed = _drive.Speed;
            float yawRate = Vector3.Dot(_rb.angularVelocity, t.up);

            PathfindingAgent pathfinder = GameAccess.GroundVehiclePathfinder != null ? GameAccess.GroundVehiclePathfinder(_vehicle) : null;
            SkipPassedWaypoints(pathfinder, t);
            SteeringInfo? info = pathfinder != null ? pathfinder.GetSteerpoint(t.GlobalPosition(), t.forward, speed, false) : null;
            HasRoute = info != null;
            if (info == null) return;

            SteeringInfo steering = info.Value;
            float lateral = _drive.CanPivot ? TrackedCornerAccel : WheeledCornerAccel;
            float top = _drive.TopSpeed * TopSpeedShare;
            float target = Mathf.Min(top, CornerSpeed(steering.nextWaypointAngle, CornerChord, lateral));
            float pathLimit = PathLimit(pathfinder, t.GlobalPosition(), lateral);
            target = Mathf.Min(target, pathLimit);
            Vector3 direction = steering.steerVector.normalized;
            float steerDistance = Mathf.Max(steering.steerVector.magnitude, 1f);

            AvoidObstacles(t, ref direction, out float inhibit);
            _bulldozeTimer = Mathf.Clamp(_bulldozeTimer + inhibit * 0.2f - 0.02f, 0f, 3f);

            float angle = Vector3.SignedAngle(Vector3.ProjectOnPlane(t.forward, t.up), Vector3.ProjectOnPlane(direction, t.up), t.up);
            if (_turnSign != 0f && Mathf.Abs(angle) > TurnHoldAngle && Mathf.Sign(angle) != _turnSign) angle = _turnSign * (360f - Mathf.Abs(angle));
            _turnSign = Mathf.Abs(angle) > PivotAngle ? Mathf.Sign(angle) : 0f;
            bool pivot = _drive.CanPivot && Mathf.Abs(angle) > PivotAngle && Mathf.Abs(speed) < ArcTurnSpeed;
            target = Mathf.Min(target, CornerSpeed(2f * Mathf.Abs(angle), steerDistance, lateral));
            if (pivot) target = 0f;

            if (_reverseTimer > 0f)
            {
                _reverseTimer -= InputInterval;
                _throttle = -1f;
                _steer = Mathf.Clamp(10f * Mathf.Sin(Time.timeSinceLevelLoad), -1f, 1f) * Mathf.Clamp01(Mathf.Abs(speed) * 0.05f) - yawRate * 0.2f;
                return;
            }

            bool wantsToMove = target > 1f || pivot;
            float follow = FollowLimit(t, speed, out bool blockedByStanding);
            target = Mathf.Min(target, follow);
            if (blockedByStanding && Mathf.Abs(speed) < 1f) _blockedTime += InputInterval;
            else _blockedTime = 0f;
            if (_blockedTime > BlockedDelay)
            {
                _blockedTime = 0f;
                _reverseTimer = 3f;
            }

            _throttle = SpeedControl(target, speed);
            if (_bulldozeTimer < 1f && _throttle > 0f) _throttle = Mathf.Max(_throttle - Mathf.Min(inhibit, 0.5f), 0f);
            float damping = _drive.CanPivot ? 0f : yawRate * WheelYawDamping;
            _steer = Mathf.Clamp(angle / FullSteerAngle - damping, -1f, 1f);

            bool turningInPlace = pivot && Mathf.Abs(yawRate) < 0.05f;
            if (wantsToMove && Mathf.Abs(speed) < 1f && (_throttle > 0.05f || turningInPlace)) _stuckTimer += InputInterval;
            else if (Mathf.Abs(speed) >= 1f || Mathf.Abs(yawRate) >= 0.05f) _stuckTimer = 0f;
            if (_stuckTimer > 2f)
            {
                _reverseTimer = 3f;
                _stuckTimer = 0f;
            }
        }

        private float FollowLimit(Transform t, float speed, out bool blockedByStanding)
        {
            blockedByStanding = false;
            List<Obstacle> obstacles = GameAccess.GroundVehicleObstacles != null ? GameAccess.GroundVehicleObstacles(_vehicle) : null;
            if (obstacles == null) return float.MaxValue;
            Vector3 position = t.position;
            Vector3 forward = t.forward;
            Vector3 right = t.right;
            float limit = float.MaxValue;
            for (int i = 0; i < obstacles.Count; i++)
            {
                Obstacle obstacle = obstacles[i];
                if (obstacle.Transform == null || obstacle.Radius == 0f || obstacle.Radius >= 8f) continue;
                Vector3 to = obstacle.Transform.position - position;
                float ahead = Vector3.Dot(to, forward);
                if (ahead <= 0f || ahead > FollowRange) continue;
                float otherRadius = obstacle.Radius / 1.4f;
                if (Mathf.Abs(Vector3.Dot(to, right)) > _vehicle.maxRadius * 0.6f + otherRadius * 0.6f + LaneMargin) continue;

                float gap = ahead - _vehicle.maxRadius - otherRadius;
                if (Vector3.Dot(obstacle.Transform.forward, forward) < 0f)
                {
                    if (gap >= MinGap) continue;
                    limit = Mathf.Min(limit, gap < MinGap * 0.5f ? 0f : 2f);
                    blockedByStanding = true;
                    continue;
                }

                Rigidbody other = obstacle.Transform.GetComponent<Rigidbody>();
                float otherSpeed = other != null && !other.isKinematic ? Mathf.Max(Vector3.Dot(other.velocity, forward), 0f) : 0f;
                float wanted = MinGap + FollowTime * Mathf.Max(speed, 0f);
                float allowed = Mathf.Max(otherSpeed + (gap - wanted) / FollowTime, 0f);
                if (allowed < limit)
                {
                    limit = allowed;
                    GroundAiDriver queued = obstacle.Transform.GetComponent<GroundAiDriver>();
                    blockedByStanding = otherSpeed < 0.5f && gap < MinGap * 1.5f && (queued == null || !queued.HasRoute);
                }
            }
            return limit;
        }

        private static float SpeedControl(float target, float speed)
        {
            float error = target - speed;
            if (error > -1f) return target <= 0f ? 0f : Mathf.Clamp(0.3f + error * 0.4f, 0f, 1f);
            return speed > 0.5f ? Mathf.Clamp(error * BrakeGain, -1f, -0.1f) : 0f;
        }

        private static void SkipPassedWaypoints(PathfindingAgent pathfinder, Transform vehicle)
        {
            if (pathfinder == null || GameAccess.PathfindingAgentWaypoints == null) return;
            List<GlobalPosition> waypoints = GameAccess.PathfindingAgentWaypoints(pathfinder);
            if (waypoints == null) return;
            GlobalPosition position = vehicle.GlobalPosition();
            Vector3 forward = vehicle.forward;
            forward.y = 0f;
            for (int guard = 0; guard < 8 && waypoints.Count >= 2; guard++)
            {
                Vector3 segment = waypoints[1] - waypoints[0];
                Vector3 toStart = waypoints[0] - position;
                segment.y = 0f;
                toStart.y = 0f;
                if (Vector3.Dot(segment, toStart) >= 0f) break;
                if (Vector3.Dot(forward, toStart.normalized) > PassedAheadCos) break;
                waypoints.RemoveAt(0);
            }
        }

        private readonly List<Vector3> _path = new List<Vector3>();

        private float PathLimit(PathfindingAgent pathfinder, GlobalPosition position, float lateral)
        {
            if (pathfinder == null || GameAccess.PathfindingAgentWaypoints == null) return float.MaxValue;
            List<GlobalPosition> waypoints = GameAccess.PathfindingAgentWaypoints(pathfinder);
            if (waypoints == null || waypoints.Count == 0) return float.MaxValue;

            _path.Clear();
            _path.Add(Vector3.zero);
            float length = 0f;
            foreach (GlobalPosition waypoint in waypoints)
            {
                if (!AddPathPoint(waypoint - position, ref length)) break;
            }
            List<RoadPathfinding.Node> nodes = GameAccess.PathfindingAgentNodes != null ? GameAccess.PathfindingAgentNodes(pathfinder) : null;
            if (nodes != null)
            {
                for (int i = 1; i < nodes.Count && length < PathLookahead; i++)
                {
                    if (nodes[i] != null && !AddPathPoint(nodes[i].position - position, ref length)) break;
                }
            }

            float limit = float.MaxValue;
            float distance = 0f;
            for (int i = 1; i + 1 < _path.Count; i++)
            {
                Vector3 into = _path[i] - _path[i - 1];
                Vector3 outOf = _path[i + 1] - _path[i];
                distance += into.magnitude;
                if (i == 1) continue;
                float turn = Vector3.Angle(into, outOf) * Mathf.Deg2Rad;
                if (turn < MinPathTurn) continue;
                float chord = Mathf.Min(CornerChord, 0.5f * (into.magnitude + outOf.magnitude));
                float corner = Mathf.Max(MinCornerSpeed, Mathf.Sqrt(lateral * chord / turn));
                float ahead = Mathf.Max(0f, distance - chord * 0.5f);
                limit = Mathf.Min(limit, Mathf.Sqrt(corner * corner + 2f * PlanDecel * ahead));
            }
            return limit;
        }

        private bool AddPathPoint(Vector3 offset, ref float length)
        {
            offset.y = 0f;
            float step = (offset - _path[_path.Count - 1]).magnitude;
            if (step < 0.5f) return true;
            _path.Add(offset);
            length += step;
            return length < PathLookahead;
        }

        private static float CornerSpeed(float turnDeg, float chord, float lateral)
        {
            float half = Mathf.Clamp(turnDeg, 0f, 179f) * 0.5f * Mathf.Deg2Rad;
            if (half < 0.01f) return float.MaxValue;
            float radius = chord / (2f * Mathf.Sin(half));
            return Mathf.Max(MinCornerSpeed, Mathf.Sqrt(lateral * radius));
        }

        private void AvoidObstacles(Transform t, ref Vector3 steer, out float inhibit)
        {
            inhibit = 0f;
            List<Obstacle> obstacles = GameAccess.GroundVehicleObstacles != null ? GameAccess.GroundVehicleObstacles(_vehicle) : null;
            if (obstacles == null) return;
            Vector3 position = t.position;
            int around = 0;
            Vector3 aroundSum = Vector3.zero;
            Vector3 push = Vector3.zero;
            for (int i = 0; i < obstacles.Count; i++)
            {
                Obstacle obstacle = obstacles[i];
                if (obstacle.Transform == null || obstacle.Radius == 0f) continue;
                Vector3 obstaclePosition = obstacle.Transform.position;
                Vector3 to = obstaclePosition - position;
                float sqr = to.sqrMagnitude;
                float reach = _vehicle.maxRadius + obstacle.Radius;
                if (sqr > (reach + 50f) * (reach + 50f)) continue;
                if (obstacle.Radius < 8f)
                {
                    push = -to.normalized;
                    float ahead = Mathf.Max(Vector3.Dot(-push, t.forward), 0f);
                    push *= Mathf.Min(100f * (1f + ahead) / Mathf.Max(sqr, 0.01f), 0.5f);
                    inhibit += ahead / Mathf.Max(sqr * 0.1f, 2f);
                }
                else
                {
                    if (Vector3.Dot(to, steer) <= 0f) continue;
                    Vector3 closest = position + Vector3.Project(to, steer);
                    float top = obstaclePosition.y + obstacle.Top;
                    if (closest.y < top && (closest - obstaclePosition).sqrMagnitude < reach * reach)
                    {
                        Vector3 edge = obstaclePosition + (closest - obstaclePosition).normalized * reach;
                        aroundSum += (edge - position).normalized;
                        around++;
                    }
                }
            }
            if (around > 0 && Vector3.Dot(aroundSum, steer) > 0f) steer = aroundSum;
            steer += push;
        }
    }
}
