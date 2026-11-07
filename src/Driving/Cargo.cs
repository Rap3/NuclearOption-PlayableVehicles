using System.Collections.Generic;
using Mirage;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class Cargo
    {
        private const float MaxLoadSpeed = VehicleDeploy.MaxDeploySpeed * 2f;

        private const float LoadGap = 3f;
        private const float SearchRadius = 30f;
        private const float SizeTolerance = 0.3f;

        public static bool IsFlatbed(GroundVehicle vehicle)
        {
            return vehicle != null && vehicle.definition != null
                && vehicle.definition.unitName.IndexOf("Flatbed", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static CargoMount LoadOf(GroundVehicle truck)
        {
            if (truck == null) return null;
            foreach (CargoMount mount in CargoMount.Active)
            {
                if (mount != null && mount.Truck == truck) return mount;
            }
            return null;
        }

        public static void Toggle(GroundVehicle truck, VehicleDriver driver)
        {
            if (NetBridge.IsClientOnly)
            {
                if (driver.BodyVelocity.magnitude > MaxLoadSpeed)
                {
                    ModLog.Notify(LogisticsLink.Describe(PvLogisticsEvent.StopToLoad, truck, null));
                    return;
                }
                ClientLoad load = LogisticsLink.ClientLoadOf(truck);
                LogisticsLink.Request(truck, !(load != null && load.Active));
                return;
            }
            Execute(truck, driver.BodyVelocity.magnitude);
        }

        public static void Execute(GroundVehicle truck, float speed)
        {
            if (speed > MaxLoadSpeed)
            {
                LogisticsLink.Report(truck, PvLogisticsEvent.StopToLoad, null);
                return;
            }
            CargoMount loaded = LoadOf(truck);
            if (loaded != null)
            {
                loaded.Unload();
                return;
            }

            if (!FlatbedBed.TryGet(truck, out FlatbedBed bed))
            {
                LogisticsLink.Report(truck, PvLogisticsEvent.NoBed, null);
                return;
            }
            var rejected = new List<string>();
            GroundVehicle best = FindContainer(truck, bed, out GroundVehicle tooBig, rejected);
            if (best == null && rejected.Count > 0) ModLog.Info("Cargo: no container for " + truck.unitName + " - " + string.Join("; ", rejected));
            if (best == null)
            {
                if (tooBig != null) LogisticsLink.Report(truck, PvLogisticsEvent.TooBig, tooBig);
                else LogisticsLink.Report(truck, PvLogisticsEvent.NoContainer, null);
                return;
            }

            best.gameObject.AddComponent<CargoMount>().Load(best, truck, bed);
            LogisticsLink.Report(truck, PvLogisticsEvent.Loaded, best);
        }

        public static FlatbedBed BedOf(GroundVehicle truck)
        {
            return FlatbedBed.TryGet(truck, out FlatbedBed bed) ? bed : null;
        }

        public static GroundVehicle FindContainer(GroundVehicle truck, FlatbedBed bed, out GroundVehicle tooBig, List<string> rejected)
        {
            Vector3 bedCenter = truck.transform.TransformPoint(bed.Center);
            GroundVehicle best = null;
            float bestGap = LoadGap;
            tooBig = null;
            List<Unit> units = UnitRegistry.allUnits;
            for (int i = 0; i < units.Count; i++)
            {
                GroundVehicle candidate = units[i] as GroundVehicle;
                if (candidate == null || candidate == truck || candidate.disabled) continue;
                if ((candidate.transform.position - bedCenter).sqrMagnitude > (SearchRadius + 20f) * (SearchRadius + 20f)) continue;
                if (!VehicleDriver.IsStatic(candidate)) continue;
                Bounds size = VehicleGeometry.CachedBounds(candidate);
                Vector3 center = candidate.transform.TransformPoint(size.center);
                Vector3 offset = center - bedCenter;
                float distance = new Vector2(offset.x, offset.z).magnitude;
                if (distance > SearchRadius) continue;
                float gap = distance - size.size.z * 0.5f - bed.Length * 0.5f;
                string reason = !VehicleGeometry.IsContainer(candidate) ? "not a container"
                    : candidate.NetworkHQ != truck.NetworkHQ ? "other faction"
                    : VehicleGeometry.IsBusy(candidate) ? "busy"
                    : gap > LoadGap ? "too far (" + gap.ToString("F1") + " m)"
                    : null;
                if (reason == null && (size.size.z > bed.Length + SizeTolerance || size.size.x > bed.Width + SizeTolerance))
                {
                    tooBig = candidate;
                    reason = "too big (" + size.size.z.ToString("F1") + " x " + size.size.x.ToString("F1") + " m)";
                }
                if (reason != null)
                {
                    rejected?.Add(candidate.unitName + ": " + reason);
                    continue;
                }
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = candidate;
                }
            }
            return best;
        }

        private static readonly Dictionary<int, float[]> _savedPoses = new Dictionary<int, float[]>();
        private const int MaxSavedPoses = 512;

        private static void SaveLauncherPoses(GroundVehicle vehicle)
        {
            if (_savedPoses.Count > MaxSavedPoses) _savedPoses.Clear();
            var stations = vehicle.weaponStations;
            if (stations == null) return;
            foreach (WeaponStation station in stations)
            {
                if (station == null || station.Turrets == null) continue;
                foreach (Turret turret in station.Turrets)
                {
                    if (turret == null) continue;
                    int id = turret.GetInstanceID();
                    if (_savedPoses.ContainsKey(id)) continue;
                    if (GameAccess.TurretElevationTransform(turret) == null) continue;
                    _savedPoses[id] = new[] { GameAccess.TurretTraverseAngle(turret), GameAccess.TurretElevationAngle(turret) };
                }
            }
        }

        private static void RestoreLauncherPoses(GroundVehicle vehicle)
        {
            var stations = vehicle.weaponStations;
            if (stations == null) return;
            foreach (WeaponStation station in stations)
            {
                if (station == null || station.Turrets == null) continue;
                foreach (Turret turret in station.Turrets)
                {
                    if (turret == null) continue;
                    int id = turret.GetInstanceID();
                    if (!_savedPoses.TryGetValue(id, out float[] pose)) continue;
                    _savedPoses.Remove(id);
                    GameAccess.TurretTraverseAngle(turret) = pose[0];
                    GameAccess.TurretElevationAngle(turret) = pose[1];
                    Transform elevation = GameAccess.TurretElevationTransform(turret);
                    if (elevation != null) elevation.localEulerAngles = new Vector3(pose[1], 0f, 0f);
                    turret.transform.localEulerAngles = new Vector3(0f, pose[0], 0f);
                }
            }
        }

        public static void ApplyStow(GroundVehicle vehicle, bool stow)
        {
            if (vehicle == null || vehicle.disabled) return;
            if (stow) SaveLauncherPoses(vehicle);
            vehicle.StowTurrets(stow);
            if (!stow) RestoreLauncherPoses(vehicle);
            if (vehicle.radar is Radar radar)
            {
                radar.enabled = !stow;
                if (stow) radar.ResetRotators();
            }
        }

        public static void StowBroadcast(GroundVehicle vehicle, bool stow)
        {
            ApplyStow(vehicle, stow);
            if (NetBridge.HasModClients)
            {
                NetBridge.SendToModClients(new PvCargoStow { VehicleId = vehicle.persistentID.Id, Stow = stow }, Channel.Reliable);
            }
        }

        public static void OnStow(INetworkPlayer _, PvCargoStow message)
        {
            if (message.VehicleId == 0) return;
            if (UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = message.VehicleId }), out Unit unit))
            {
                ApplyStow(unit as GroundVehicle, message.Stow);
            }
        }
    }

    internal sealed class FlatbedBed
    {
        public Vector3 Center;
        public float Length;
        public float Width;
        public float Height;

        private const float Step = 0.25f;
        private const float RiseLimit = 0.35f;
        private static readonly Dictionary<VehicleDefinition, FlatbedBed> _cache = new Dictionary<VehicleDefinition, FlatbedBed>();

        public static bool TryGet(GroundVehicle truck, out FlatbedBed bed)
        {
            VehicleDefinition definition = truck.definition as VehicleDefinition;
            if (definition != null && _cache.TryGetValue(definition, out bed)) return bed != null;
            bed = Measure(truck);
            if (definition != null) _cache[definition] = bed;
            if (bed != null)
            {
                ModLog.Info("Cargo bed of " + truck.unitName + ": length " + bed.Length.ToString("F2") + " m, width " + bed.Width.ToString("F2")
                    + " m, floor " + bed.Height.ToString("F2") + " m");
            }
            return bed != null;
        }

        private static FlatbedBed Measure(GroundVehicle truck)
        {
            Transform root = truck.transform;
            Bounds bounds = VehicleGeometry.LocalBounds(truck);
            Collider[] colliders = truck.GetComponentsInChildren<Collider>();
            float top = bounds.max.y + 2f;

            float SurfaceAt(float z)
            {
                Vector3 origin = root.TransformPoint(new Vector3(0f, top, z));
                var ray = new Ray(origin, -root.up);
                float best = float.MaxValue;
                foreach (Collider collider in colliders)
                {
                    if (collider == null || collider.isTrigger || !collider.enabled) continue;
                    if (collider.Raycast(ray, out RaycastHit hit, top - bounds.min.y + 2f) && hit.distance < best) best = hit.distance;
                }
                return best < float.MaxValue ? top - best : float.NaN;
            }

            float rear = bounds.min.z + 0.1f;
            float floor = float.NaN;
            float bedStart = rear;
            float z = rear;
            for (; z < bounds.max.z; z += Step)
            {
                floor = SurfaceAt(z);
                if (!float.IsNaN(floor))
                {
                    bedStart = z;
                    break;
                }
            }
            if (float.IsNaN(floor)) return null;

            float bedEnd = bedStart;
            for (z = bedStart + Step; z < bounds.max.z; z += Step)
            {
                float height = SurfaceAt(z);
                if (float.IsNaN(height) || height > floor + RiseLimit || height < floor - RiseLimit) break;
                bedEnd = z;
            }
            float length = bedEnd - bedStart + Step;
            if (length < 1.5f) return null;

            return new FlatbedBed
            {
                Length = length,
                Width = bounds.size.x,
                Height = floor,
                Center = new Vector3(0f, floor, (bedStart + bedEnd) * 0.5f),
            };
        }
    }

    internal sealed class CargoMount : MonoBehaviour
    {
        public static readonly HashSet<CargoMount> Active = new HashSet<CargoMount>();

        public GroundVehicle Container { get; private set; }
        public GroundVehicle Truck { get; private set; }

        private Rigidbody _rb;
        private FixedJoint _joint;
        private FlatbedBed _bed;
        private RigidbodyInterpolation _interpolation;
        private bool _unloaded;

        public void Load(GroundVehicle container, GroundVehicle truck, FlatbedBed bed)
        {
            Container = container;
            Truck = truck;
            Active.Add(this);
            _bed = bed;
            _rb = container.rb;

            VehicleDeploy.Set(container, false);
            container.enabled = false;
            Cargo.StowBroadcast(container, true);
            _interpolation = _rb.interpolation;
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (container.definition != null && container.definition.mass > 0f) _rb.mass = container.definition.mass;

            Bounds size = VehicleGeometry.LocalBounds(container);
            Transform t = truck.transform;
            Quaternion rotation = t.rotation;
            Vector3 bottomCenter = new Vector3(size.center.x, size.min.y, size.center.z);
            Vector3 position = t.TransformPoint(bed.Center + Vector3.up * 0.05f) - rotation * bottomCenter;
            _rb.velocity = truck.rb != null ? truck.rb.velocity : Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = position;
            _rb.rotation = rotation;
            container.transform.SetPositionAndRotation(position, rotation);

            _joint = _rb.gameObject.AddComponent<FixedJoint>();
            _joint.connectedBody = truck.rb;
            _joint.enableCollision = false;
            _rb.WakeUp();
        }

        public void Unload()
        {
            if (_unloaded) return;
            GroundVehicle container = Container;
            GroundVehicle truck = Truck;
            Transform t = truck != null ? truck.transform : null;
            Release();
            if (container == null || container.disabled || t == null)
            {
                LogisticsLink.Report(truck, PvLogisticsEvent.State, null);
                return;
            }

            Bounds truckBounds = VehicleGeometry.LocalBounds(truck);
            Bounds size = VehicleGeometry.LocalBounds(container);
            Vector3 behind = t.TransformPoint(new Vector3(0f, 0f, truckBounds.min.z - 1f - (size.max.z - size.center.z)));
            Vector3 ground = behind;
            if (Physics.Raycast(behind + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 60f, PhysicsLayers.StaticsMask, QueryTriggerInteraction.Ignore))
            {
                ground = hit.point;
            }
            Quaternion rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(t.forward, Vector3.up), Vector3.up);
            Vector3 bottomCenter = new Vector3(size.center.x, size.min.y, size.center.z);
            Vector3 position = ground + Vector3.up * 0.1f - rotation * bottomCenter;
            container.rb.velocity = Vector3.zero;
            container.rb.angularVelocity = Vector3.zero;
            container.rb.position = position;
            container.rb.rotation = rotation;
            container.transform.SetPositionAndRotation(position, rotation);
            LogisticsLink.Report(truck, PvLogisticsEvent.Unloaded, container);
        }

        private void Release()
        {
            if (_unloaded) return;
            _unloaded = true;
            Active.Remove(this);
            if (_joint != null) Destroy(_joint);
            if (Container != null && !Container.disabled)
            {
                Container.enabled = true;
                GameAccess.GroundVehicleResetStationary(Container) = true;
                if (_rb != null) _rb.interpolation = _interpolation;
                SettleStationary.Begin(Container);
                CargoUnstow.Begin(Container);
            }
            Truck = null;
            Destroy(this);
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }

        private void FixedUpdate()
        {
            if (_unloaded) return;
            if (Container == null || Container.disabled || Truck == null || Truck.disabled || _joint == null)
            {
                GroundVehicle truck = Truck;
                GroundVehicle container = Container;
                Release();
                LogisticsLink.Report(truck, container != null && !container.disabled ? PvLogisticsEvent.FellOff : PvLogisticsEvent.State, container);
                return;
            }
            Container.speed = Truck.speed;
        }
    }

    internal sealed class CargoUnstow : MonoBehaviour
    {
        private const float GiveUpTime = 60f;
        private GroundVehicle _vehicle;
        private float _started;

        public static void Begin(GroundVehicle vehicle)
        {
            if (vehicle == null) return;
            CargoUnstow existing = vehicle.GetComponent<CargoUnstow>();
            if (existing != null)
            {
                existing._started = Time.time;
                return;
            }
            var unstow = vehicle.gameObject.AddComponent<CargoUnstow>();
            unstow._vehicle = vehicle;
            unstow._started = Time.time;
        }

        private void FixedUpdate()
        {
            GroundVehicle vehicle = _vehicle;
            if (vehicle == null || vehicle.disabled)
            {
                Destroy(this);
                return;
            }
            if (VehicleDeploy.IsDeployed(vehicle))
            {
                Cargo.StowBroadcast(vehicle, false);
                Destroy(this);
                return;
            }
            if (Time.time - _started > GiveUpTime)
            {
                if (!VehicleGeometry.IsBusy(vehicle)) Cargo.StowBroadcast(vehicle, false);
                Destroy(this);
            }
        }
    }
}
