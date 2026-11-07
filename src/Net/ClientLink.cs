using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class ClientLink
    {
        private const float ReplyTimeout = 3f;
        private const float InputInterval = 0.05f;
        private const float RttSmoothing = 0.2f;
        private const float MaxRttSample = 5f;

        private const string NoModText = "Host did not answer: the host needs the PlayableVehicles mod";

        private static GroundVehicle _pending;
        private static float _pendingSince;
        private static float _nextInputTime;
        private static ushort _inputSeq;
        private static Player _stateRequestedFor;
        private static float _rtt;

        public static float Rtt => _rtt;

        public static void RequestEnter(GroundVehicle vehicle)
        {
            if (_pending != null || !CanSendRequests()) return;
            _pending = vehicle;
            _pendingSince = Time.unscaledTime;
            NetBridge.SendToServer(new PvEnterRequest { VehicleId = vehicle.persistentID.Id }, Channel.Reliable);
            ModLog.Info("Requesting " + vehicle.unitName + " from host...");
        }

        public static void SendExit(GroundVehicle vehicle)
        {
            if ((object)vehicle == null) return;
            SendExit(vehicle.persistentID.Id);
        }

        private static void SendExit(uint vehicleId)
        {
            NetBridge.SendToServer(new PvExitRequest { VehicleId = vehicleId }, Channel.Reliable);
        }

        public static void OnReply(INetworkPlayer _, PvSessionReply message)
        {
            if (_pending != null && _pending.persistentID.Id == message.VehicleId)
            {
                GroundVehicle vehicle = _pending;
                _pending = null;
                if (message.Reply == PvReply.Granted)
                {
                    VehicleSession.EnterAsClient(vehicle);
                    if (!(VehicleSession.IsActive && ReferenceEquals(VehicleSession.Vehicle, vehicle))) SendExit(message.VehicleId);
                }
                else ModLog.Notify(NetSerializers.Describe(message.Reply));
                return;
            }

            GroundVehicle current = VehicleSession.Vehicle;

            if (message.Reply == PvReply.Granted)
            {
                bool alreadyDriving = VehicleSession.IsActive && (object)current != null && current.persistentID.Id == message.VehicleId;
                if (!alreadyDriving)
                {
                    ModLog.Info("Late host reply for vehicle " + message.VehicleId + ", releasing it");
                    SendExit(message.VehicleId);
                }
                return;
            }

            if (VehicleSession.IsActive && (object)current != null
                && current.persistentID.Id == message.VehicleId)
            {
                VehicleSession.EndFromHost(NetSerializers.Describe(message.Reply));
            }
        }

        public static void OnNotice(INetworkPlayer _, PvNotice message)
        {
            GroundVehicle current = VehicleSession.Vehicle;
            if (!VehicleSession.IsClientDriver || (object)current == null || current.persistentID.Id != message.VehicleId) return;
            if (message.Kind == PvNoticeKind.NoRadar) ModLog.Notify(TurretController.NoRadarText);
        }

        public static void OnDisconnected()
        {
            _pending = null;
            _hostProtocol = 0;
            _mismatchShown = false;
            _stateRequestedFor = null;
            _rtt = 0f;
            if (VehicleSession.IsActive && VehicleSession.IsClientDriver) VehicleSession.EndFromHost("Disconnected from host");
        }

        public static void OnInputEcho(INetworkPlayer _, PvInputEcho message)
        {
            float sample = Time.realtimeSinceStartup - message.ClientTime;
            if (!(sample >= 0f && sample <= MaxRttSample)) return;
            _rtt = _rtt <= 0f ? sample : Mathf.Lerp(_rtt, sample, RttSmoothing);
        }

        public static void Tick()
        {
            if (_pending != null && Time.unscaledTime - _pendingSince > ReplyTimeout)
            {
                _pending = null;
                ModLog.Notify("Host did not answer in time, try again");
            }

            RequestStateOnMissionLoad();
        }

        private static void RequestStateOnMissionLoad()
        {
            if (!NetBridge.IsClientOnly || _hostProtocol != PvProtocol.Version) return;
            if (!GameManager.GetLocalPlayer(out Player player) || player == null || ReferenceEquals(player, _stateRequestedFor)) return;
            _stateRequestedFor = player;
            NetBridge.SendToServer(new PvStateRequest(), Channel.Reliable);
        }

        public static void SendHello()
        {
            _hostProtocol = 0;
            _mismatchShown = false;
            _stateRequestedFor = null;
            _rtt = 0f;
            if (!NetBridge.IsClientOnly) return;
            NetBridge.SendToServer(new PvHello { Version = PvProtocol.Version }, Channel.Reliable);
        }

        private static byte _hostProtocol;
        private static bool _mismatchShown;

        public static bool HostCompatible => !NetBridge.IsClientOnly || _hostProtocol == PvProtocol.Version;

        public static void OnVersion(INetworkPlayer _, PvVersion message)
        {
            _hostProtocol = message.Version;
            if (HostCompatible) ModLog.Info("Multiplayer: host has PlayableVehicles protocol " + message.Version);
            else ReportMismatch();
        }

        public static bool AcceptServerRules()
        {
            if (_hostProtocol == 0)
            {
                _hostProtocol = PvProtocol.Legacy;
                ReportMismatch();
            }
            return HostCompatible;
        }

        public static bool CanSendRequests()
        {
            if (HostCompatible) return true;
            ModLog.Notify(_hostProtocol == 0 ? NoModText : MismatchText());
            return false;
        }

        private static void ReportMismatch()
        {
            ModLog.Warning("Multiplayer: " + MismatchText());
            if (_mismatchShown) return;
            _mismatchShown = true;
            ModLog.Notify(MismatchText());
        }

        private static string MismatchText()
        {
            return "Host runs PlayableVehicles protocol " + _hostProtocol + ", you have " + PvProtocol.Version
                + (_hostProtocol > PvProtocol.Version ? " - update the mod" : " - the host needs to update the mod");
        }

        public static void SendInput(GroundVehicle vehicle, VehicleDriver driver)
        {
            if (Time.unscaledTime < _nextInputTime) return;
            _nextInputTime = Time.unscaledTime + InputInterval;

            TurretController turrets = driver.Turrets;
            GlobalPosition aim = turrets.CurrentAimPoint.ToGlobalPosition();
            GlobalPosition position = driver.BodyPosition.ToGlobalPosition();
            Quaternion rotation = driver.BodyRotation;
            Vector3 velocity = driver.BodyVelocity;
            Unit locked = turrets.LockedTarget;
            NetBridge.SendToServer(new PvInput
            {
                VehicleId = vehicle.persistentID.Id,
                Throttle = driver.Throttle,
                Steer = driver.Steer,
                AimX = aim.x,
                AimY = aim.y,
                AimZ = aim.z,
                AimDirX = turrets.AimRayDir.x,
                AimDirY = turrets.AimRayDir.y,
                AimDirZ = turrets.AimRayDir.z,
                HasAimHit = turrets.HasAimHit,
                Fire = turrets.FireHeld,
                Station = (byte)Mathf.Clamp(turrets.SelectedStationIndex, 0, 255),
                LockId = locked != null ? locked.persistentID.Id : 0u,
                PosX = position.x,
                PosY = position.y,
                PosZ = position.z,
                RotX = rotation.x,
                RotY = rotation.y,
                RotZ = rotation.z,
                RotW = rotation.w,
                VelX = velocity.x,
                VelY = velocity.y,
                VelZ = velocity.z,
                MissionTargetId = turrets.MissionActive ? turrets.MissionTarget.persistentID.Id : 0u,
                MissionMode = turrets.MissionActive ? turrets.MissionMode : FireMode.None,
                Seq = _inputSeq++,
                FirePresses = turrets.FirePresses,
                ClientTime = Time.realtimeSinceStartup,
                RttMs = (ushort)Mathf.Clamp(Mathf.RoundToInt(_rtt * 1000f), 0, ushort.MaxValue),
                RadarAltDm = (byte)Mathf.Clamp(Mathf.RoundToInt(vehicle.radarAlt * 10f), 0, 255),
            }, Channel.Unreliable);
        }
    }
}
