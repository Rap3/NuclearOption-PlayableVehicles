using Mirage;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class LogisticsLink
    {
        private const float RequestTimeout = 2f;

        private static uint _pendingVehicle;
        private static float _pendingSince;

        public static void Request(GroundVehicle vehicle, bool attach)
        {
            uint id = vehicle.persistentID.Id;
            if (_pendingVehicle == id && Time.unscaledTime - _pendingSince < RequestTimeout) return;
            if (!ClientLink.CanSendRequests()) return;
            _pendingVehicle = id;
            _pendingSince = Time.unscaledTime;
            NetBridge.SendToServer(new PvLogisticsRequest { VehicleId = id, Attach = attach }, Channel.Reliable);
        }

        public static void OnLogistics(INetworkPlayer _, PvLogistics message)
        {
            GroundVehicle vehicle = VehicleSession.Vehicle;
            VehicleDriver driver = VehicleSession.Driver;
            if (!VehicleSession.IsClientDriver || (object)vehicle == null || driver == null || vehicle.persistentID.Id != message.VehicleId) return;
            if (_pendingVehicle == message.VehicleId) _pendingVehicle = 0;

            driver.SetClientLoad(message.LoadId != 0, FindVehicle(message.LoadId), message.LoadMass, Towing.IsTractor(vehicle));
            string text = Describe(message.Event, vehicle, FindVehicle(message.SubjectId));
            if (text != null) ModLog.Notify(text);
        }

        public static ClientLoad ClientLoadOf(GroundVehicle vehicle)
        {
            if (!VehicleSession.IsClientDriver || VehicleSession.Driver == null || !ReferenceEquals(VehicleSession.Vehicle, vehicle)) return null;
            return VehicleSession.Driver.ClientLoad;
        }

        public static void ClientReset()
        {
            _pendingVehicle = 0;
        }

        public static void OnRequest(INetworkPlayer connection, PvLogisticsRequest message)
        {
            RemoteDrivers.Session session = RemoteDrivers.Find(connection);
            if (session == null || session.Vehicle == null || session.Vehicle.disabled || session.Driver == null
                || session.Vehicle.persistentID.Id != message.VehicleId) return;
            GroundVehicle vehicle = session.Vehicle;
            if (Time.unscaledTime < session.NextLogisticsAt) return;
            session.NextLogisticsAt = Time.unscaledTime + RemoteDrivers.MinLogisticsInterval;
            if (vehicle.IsSlung()) return;
            float speed = session.Driver.RemotePoseSpeed;

            if (Towing.IsTractor(vehicle))
            {
                if (message.Attach == (Towing.HitchOf(vehicle) != null)) SendState(connection, vehicle);
                else Towing.Execute(vehicle, speed);
            }
            else if (Cargo.IsFlatbed(vehicle))
            {
                if (message.Attach == (Cargo.LoadOf(vehicle) != null)) SendState(connection, vehicle);
                else Cargo.Execute(vehicle, speed);
            }
        }

        public static void SendState(INetworkPlayer connection, GroundVehicle vehicle)
        {
            if (connection == null || vehicle == null || !(Towing.IsTractor(vehicle) || Cargo.IsFlatbed(vehicle))) return;
            Send(connection, vehicle, PvLogisticsEvent.State, null);
        }

        public static void Report(GroundVehicle vehicle, PvLogisticsEvent e, GroundVehicle subject)
        {
            RemoteDrivers.Session session = (object)vehicle != null ? RemoteDrivers.FindByVehicle(vehicle) : null;
            if (session != null && session.Connection != null)
            {
                Send(session.Connection, vehicle, e, subject);
                return;
            }
            string text = Describe(e, vehicle, subject);
            if (text == null) return;
            if (ReferenceEquals(VehicleSession.Vehicle, vehicle)) ModLog.Notify(text);
            else ModLog.Info("[VEHICLE] " + text);
        }

        private static void Send(INetworkPlayer connection, GroundVehicle vehicle, PvLogisticsEvent e, GroundVehicle subject)
        {
            GroundVehicle load = CurrentLoad(vehicle);
            connection.Send(new PvLogistics
            {
                VehicleId = vehicle.persistentID.Id,
                Event = e,
                SubjectId = IdOf(subject),
                LoadId = IdOf(load),
                LoadMass = load != null && load.rb != null ? load.rb.mass : 0f,
            }, Channel.Reliable);
        }

        private static GroundVehicle CurrentLoad(GroundVehicle vehicle)
        {
            TowHitch hitch = Towing.HitchOf(vehicle);
            if (hitch != null && hitch.Trailer != null) return hitch.Trailer;
            CargoMount mount = Cargo.LoadOf(vehicle);
            return mount != null && mount.Container != null ? mount.Container : null;
        }

        private static uint IdOf(GroundVehicle vehicle)
        {
            return (object)vehicle != null ? vehicle.persistentID.Id : 0u;
        }

        private static GroundVehicle FindVehicle(uint id)
        {
            if (id == 0) return null;
            return UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = id }), out Unit unit) ? unit as GroundVehicle : null;
        }

        public static string Describe(PvLogisticsEvent e, GroundVehicle vehicle, GroundVehicle subject)
        {
            string key = "[" + ModConfig.DeployKey.Value + "]";
            string name = subject != null ? subject.unitName : null;
            switch (e)
            {
                case PvLogisticsEvent.Hitched: return name != null ? "Trailer hitched: " + name + ". " + key + " to unhitch" : null;
                case PvLogisticsEvent.Unhitched: return name != null ? "Trailer unhitched: " + name : null;
                case PvLogisticsEvent.HitchLost: return name != null ? "Trailer lost the hitch: " + name : null;
                case PvLogisticsEvent.HitchBroke: return name != null ? "Trailer hitch broke: " + name : null;
                case PvLogisticsEvent.HitchSlung: return name != null ? "Trailer unhitched: lifted by a helicopter (" + name + ")" : null;
                case PvLogisticsEvent.NoTrailer: return "No trailer behind: reverse up to a trailer's tow bar";
                case PvLogisticsEvent.Misaligned: return name != null ? "Line up with " + name + " to hitch it" : null;
                case PvLogisticsEvent.TowBarLow: return "Trailer kingpin is too low - find flatter ground";
                case PvLogisticsEvent.TowBarHigh: return "Trailer kingpin is too high - find flatter ground";
                case PvLogisticsEvent.StopToHitch: return "Stop the tractor to hitch a trailer";
                case PvLogisticsEvent.Loaded: return name != null ? "Loaded " + name + ". " + key + " to unload" : null;
                case PvLogisticsEvent.Unloaded: return name != null ? "Unloaded " + name : null;
                case PvLogisticsEvent.FellOff: return name != null ? name + " fell off the truck" : null;
                case PvLogisticsEvent.NoContainer: return "No container next to the truck";
                case PvLogisticsEvent.TooBig: return TooBigText(vehicle, subject);
                case PvLogisticsEvent.NoBed: return vehicle != null ? vehicle.unitName + ": cargo bed not found" : null;
                case PvLogisticsEvent.StopToLoad: return "Stop the truck to load or unload";
                default: return null;
            }
        }

        private static string TooBigText(GroundVehicle truck, GroundVehicle container)
        {
            if (truck == null || container == null) return null;
            Bounds size = VehicleGeometry.CachedBounds(container);
            string bed = FlatbedBed.TryGet(truck, out FlatbedBed measured) ? " on a " + UnitConverter.DimensionReading(measured.Length) + " bed" : "";
            return container.unitName + " is too long for " + truck.unitName + ": " + UnitConverter.DimensionReading(size.size.z) + bed;
        }
    }
}
