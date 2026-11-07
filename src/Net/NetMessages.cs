using Mirage.Serialization;

namespace PlayableVehicles
{
    internal struct PvEnterRequest
    {
        public uint VehicleId;
    }

    internal struct PvExitRequest
    {
        public uint VehicleId;
    }

    internal enum PvReply : byte
    {
        Granted,
        NotFound,
        Destroyed,
        WrongFaction,
        Busy,
        Ended,
        NotOwned,
        Disabled,
    }

    internal struct PvSessionReply
    {
        public uint VehicleId;
        public PvReply Reply;
    }

    internal static class PvProtocol
    {
        public const byte Version = 10;
        public const byte Legacy = 1;
        public const byte FirstVersioned = 2;
    }

    internal struct PvHello
    {
        public byte Version;
    }

    internal struct PvVersion
    {
        public byte Version;
    }

    internal struct PvInput
    {
        public uint VehicleId;
        public float Throttle;
        public float Steer;
        public float AimX;
        public float AimY;
        public float AimZ;
        public float AimDirX;
        public float AimDirY;
        public float AimDirZ;
        public bool HasAimHit;
        public bool Fire;
        public byte Station;
        public uint LockId;
        public float PosX;
        public float PosY;
        public float PosZ;
        public float RotX;
        public float RotY;
        public float RotZ;
        public float RotW;
        public float VelX;
        public float VelY;
        public float VelZ;
        public uint MissionTargetId;
        public FireMode MissionMode;
        public ushort Seq;
        public byte FirePresses;
        public float ClientTime;
        public ushort RttMs;
        public byte RadarAltDm;
    }

    internal struct PvStationAmmo
    {
        public ushort Loaded;
        public ushort Total;
        public ushort Ammo;
        public bool Reloading;
        public byte ReloadProgress;
    }

    internal struct PvAmmoState
    {
        public uint VehicleId;
        public bool RearmRequested;
        public PvStationAmmo[] Stations;
    }

    internal struct PvInputEcho
    {
        public float ClientTime;
    }

    internal struct PvIncomingHit
    {
        public uint VehicleId;
        public uint ShooterId;
        public byte Station;
        public float X;
        public float Y;
        public float Z;
        public float DirX;
        public float DirY;
        public float DirZ;
    }

    internal struct PvStateRequest
    {
    }

    internal enum PvNoticeKind : byte
    {
        NoRadar,
    }

    internal struct PvNotice
    {
        public uint VehicleId;
        public PvNoticeKind Kind;
    }

    internal struct PvDamageDirection
    {
        public uint VehicleId;
        public float DirX;
        public float DirZ;
    }

    internal struct PvTurretAim
    {
        public uint VehicleId;
        public byte Station;
        public float StationAimX;
        public float StationAimY;
        public float StationAimZ;
        public float FreeAimX;
        public float FreeAimY;
        public float FreeAimZ;
    }

    internal struct PvHit
    {
        public uint TargetId;
        public bool HasPoint;
        public float X;
        public float Y;
        public float Z;
    }

    internal struct PvDriver
    {
        public uint VehicleId;
        public uint PlayerNetId;
        public bool Driving;
    }

    internal struct PvCargoStow
    {
        public uint VehicleId;
        public bool Stow;
    }

    internal struct PvPurchaseRequest
    {
        public PurchasePoint.PointKind PointKind;
        public uint PointId;
        public ushort VehicleIndex;
        public bool Deploy;
    }

    internal struct PvPurchaseReply
    {
        public VehicleShop.Result Result;
        public ushort VehicleIndex;
        public uint VehicleId;
        public float Wait;
    }

    internal struct PvSellRequest
    {
        public uint VehicleId;
        public PurchasePoint.PointKind PointKind;
        public uint PointId;
    }

    internal struct PvSellReply
    {
        public VehicleShop.Result Result;
        public uint VehicleId;
        public float Refund;
    }

    internal struct PvServerRules
    {
        public VehicleAccessMode Access;
    }

    internal struct PvDeploy
    {
        public uint VehicleId;
        public bool Deploy;
    }

    internal struct PvLogisticsRequest
    {
        public uint VehicleId;
        public bool Attach;
    }

    internal enum PvLogisticsEvent : byte
    {
        State,
        Hitched,
        Unhitched,
        HitchLost,
        HitchBroke,
        HitchSlung,
        NoTrailer,
        Misaligned,
        TowBarLow,
        TowBarHigh,
        StopToHitch,
        Loaded,
        Unloaded,
        FellOff,
        NoContainer,
        TooBig,
        NoBed,
        StopToLoad,
    }

    internal struct PvLogistics
    {
        public uint VehicleId;
        public PvLogisticsEvent Event;
        public uint SubjectId;
        public uint LoadId;
        public float LoadMass;
    }

    internal static class NetSerializers
    {
        private static bool _registered;
        private const int MaxAmmoStations = 16;

        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            Writer<PvEnterRequest>.Write = (w, m) => w.WriteUInt32(m.VehicleId);
            Reader<PvEnterRequest>.Read = r => new PvEnterRequest { VehicleId = r.ReadUInt32() };

            Writer<PvExitRequest>.Write = (w, m) => w.WriteUInt32(m.VehicleId);
            Reader<PvExitRequest>.Read = r => new PvExitRequest { VehicleId = r.ReadUInt32() };

            Writer<PvSessionReply>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteByte((byte)m.Reply);
            };
            Reader<PvSessionReply>.Read = r => new PvSessionReply { VehicleId = r.ReadUInt32(), Reply = (PvReply)r.ReadByte() };

            Writer<PvInput>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteSingle(m.Throttle);
                w.WriteSingle(m.Steer);
                w.WriteSingle(m.AimX);
                w.WriteSingle(m.AimY);
                w.WriteSingle(m.AimZ);
                w.WriteSingle(m.AimDirX);
                w.WriteSingle(m.AimDirY);
                w.WriteSingle(m.AimDirZ);
                w.WriteBoolean(m.HasAimHit);
                w.WriteBoolean(m.Fire);
                w.WriteByte(m.Station);
                w.WriteUInt32(m.LockId);
                w.WriteSingle(m.PosX);
                w.WriteSingle(m.PosY);
                w.WriteSingle(m.PosZ);
                w.WriteSingle(m.RotX);
                w.WriteSingle(m.RotY);
                w.WriteSingle(m.RotZ);
                w.WriteSingle(m.RotW);
                w.WriteSingle(m.VelX);
                w.WriteSingle(m.VelY);
                w.WriteSingle(m.VelZ);
                w.WriteUInt32(m.MissionTargetId);
                w.WriteByte((byte)m.MissionMode);
                w.WriteUInt16(m.Seq);
                w.WriteByte(m.FirePresses);
                w.WriteSingle(m.ClientTime);
                w.WriteUInt16(m.RttMs);
                w.WriteByte(m.RadarAltDm);
            };
            Reader<PvInput>.Read = r => new PvInput
            {
                VehicleId = r.ReadUInt32(),
                Throttle = r.ReadSingle(),
                Steer = r.ReadSingle(),
                AimX = r.ReadSingle(),
                AimY = r.ReadSingle(),
                AimZ = r.ReadSingle(),
                AimDirX = r.ReadSingle(),
                AimDirY = r.ReadSingle(),
                AimDirZ = r.ReadSingle(),
                HasAimHit = r.ReadBoolean(),
                Fire = r.ReadBoolean(),
                Station = r.ReadByte(),
                LockId = r.ReadUInt32(),
                PosX = r.ReadSingle(),
                PosY = r.ReadSingle(),
                PosZ = r.ReadSingle(),
                RotX = r.ReadSingle(),
                RotY = r.ReadSingle(),
                RotZ = r.ReadSingle(),
                RotW = r.ReadSingle(),
                VelX = r.ReadSingle(),
                VelY = r.ReadSingle(),
                VelZ = r.ReadSingle(),
                MissionTargetId = r.ReadUInt32(),
                MissionMode = (FireMode)r.ReadByte(),
                Seq = r.ReadUInt16(),
                FirePresses = r.ReadByte(),
                ClientTime = r.ReadSingle(),
                RttMs = r.ReadUInt16(),
                RadarAltDm = r.ReadByte(),
            };

            Writer<PvAmmoState>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteBoolean(m.RearmRequested);
                int count = m.Stations != null ? System.Math.Min(m.Stations.Length, MaxAmmoStations) : 0;
                w.WriteByte((byte)count);
                for (int i = 0; i < count; i++)
                {
                    PvStationAmmo s = m.Stations[i];
                    w.WriteUInt16(s.Loaded);
                    w.WriteUInt16(s.Total);
                    w.WriteUInt16(s.Ammo);
                    w.WriteBoolean(s.Reloading);
                    w.WriteByte(s.ReloadProgress);
                }
            };
            Reader<PvAmmoState>.Read = r =>
            {
                var m = new PvAmmoState { VehicleId = r.ReadUInt32(), RearmRequested = r.ReadBoolean() };
                int count = System.Math.Min((int)r.ReadByte(), MaxAmmoStations);
                m.Stations = new PvStationAmmo[count];
                for (int i = 0; i < count; i++)
                {
                    m.Stations[i] = new PvStationAmmo
                    {
                        Loaded = r.ReadUInt16(),
                        Total = r.ReadUInt16(),
                        Ammo = r.ReadUInt16(),
                        Reloading = r.ReadBoolean(),
                        ReloadProgress = r.ReadByte(),
                    };
                }
                return m;
            };

            Writer<PvInputEcho>.Write = (w, m) => w.WriteSingle(m.ClientTime);
            Reader<PvInputEcho>.Read = r => new PvInputEcho { ClientTime = r.ReadSingle() };

            Writer<PvIncomingHit>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteUInt32(m.ShooterId);
                w.WriteByte(m.Station);
                w.WriteSingle(m.X);
                w.WriteSingle(m.Y);
                w.WriteSingle(m.Z);
                w.WriteSingle(m.DirX);
                w.WriteSingle(m.DirY);
                w.WriteSingle(m.DirZ);
            };
            Reader<PvIncomingHit>.Read = r => new PvIncomingHit
            {
                VehicleId = r.ReadUInt32(),
                ShooterId = r.ReadUInt32(),
                Station = r.ReadByte(),
                X = r.ReadSingle(),
                Y = r.ReadSingle(),
                Z = r.ReadSingle(),
                DirX = r.ReadSingle(),
                DirY = r.ReadSingle(),
                DirZ = r.ReadSingle(),
            };

            Writer<PvStateRequest>.Write = (w, m) => { };
            Reader<PvStateRequest>.Read = r => default;

            Writer<PvNotice>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteByte((byte)m.Kind);
            };
            Reader<PvNotice>.Read = r => new PvNotice { VehicleId = r.ReadUInt32(), Kind = (PvNoticeKind)r.ReadByte() };

            Writer<PvDamageDirection>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteSingle(m.DirX);
                w.WriteSingle(m.DirZ);
            };
            Reader<PvDamageDirection>.Read = r => new PvDamageDirection { VehicleId = r.ReadUInt32(), DirX = r.ReadSingle(), DirZ = r.ReadSingle() };

            Writer<PvHit>.Write = (w, m) =>
            {
                w.WriteUInt32(m.TargetId);
                w.WriteBoolean(m.HasPoint);
                w.WriteSingle(m.X);
                w.WriteSingle(m.Y);
                w.WriteSingle(m.Z);
            };
            Reader<PvHit>.Read = r => new PvHit
            {
                TargetId = r.ReadUInt32(),
                HasPoint = r.ReadBoolean(),
                X = r.ReadSingle(),
                Y = r.ReadSingle(),
                Z = r.ReadSingle(),
            };

            Writer<PvDriver>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteUInt32(m.PlayerNetId);
                w.WriteBoolean(m.Driving);
            };
            Reader<PvDriver>.Read = r => new PvDriver { VehicleId = r.ReadUInt32(), PlayerNetId = r.ReadUInt32(), Driving = r.ReadBoolean() };

            Writer<PvPurchaseRequest>.Write = (w, m) =>
            {
                w.WriteByte((byte)m.PointKind);
                w.WriteUInt32(m.PointId);
                w.WriteUInt16(m.VehicleIndex);
                w.WriteBoolean(m.Deploy);
            };
            Reader<PvPurchaseRequest>.Read = r => new PvPurchaseRequest
            {
                PointKind = (PurchasePoint.PointKind)r.ReadByte(),
                PointId = r.ReadUInt32(),
                VehicleIndex = r.ReadUInt16(),
                Deploy = r.ReadBoolean(),
            };

            Writer<PvSellRequest>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteByte((byte)m.PointKind);
                w.WriteUInt32(m.PointId);
            };
            Reader<PvSellRequest>.Read = r => new PvSellRequest
            {
                VehicleId = r.ReadUInt32(),
                PointKind = (PurchasePoint.PointKind)r.ReadByte(),
                PointId = r.ReadUInt32(),
            };

            Writer<PvSellReply>.Write = (w, m) =>
            {
                w.WriteByte((byte)m.Result);
                w.WriteUInt32(m.VehicleId);
                w.WriteSingle(m.Refund);
            };
            Reader<PvSellReply>.Read = r => new PvSellReply
            {
                Result = (VehicleShop.Result)r.ReadByte(),
                VehicleId = r.ReadUInt32(),
                Refund = r.ReadSingle(),
            };

            Writer<PvPurchaseReply>.Write = (w, m) =>
            {
                w.WriteByte((byte)m.Result);
                w.WriteUInt16(m.VehicleIndex);
                w.WriteUInt32(m.VehicleId);
                w.WriteSingle(m.Wait);
            };
            Reader<PvPurchaseReply>.Read = r => new PvPurchaseReply
            {
                Result = (VehicleShop.Result)r.ReadByte(),
                VehicleIndex = r.ReadUInt16(),
                VehicleId = r.ReadUInt32(),
                Wait = r.ReadSingle(),
            };

            Writer<PvServerRules>.Write = (w, m) => w.WriteByte((byte)m.Access);
            Reader<PvServerRules>.Read = r => new PvServerRules { Access = (VehicleAccessMode)r.ReadByte() };

            Writer<PvDeploy>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteBoolean(m.Deploy);
            };
            Reader<PvDeploy>.Read = r => new PvDeploy { VehicleId = r.ReadUInt32(), Deploy = r.ReadBoolean() };

            Writer<PvLogisticsRequest>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteBoolean(m.Attach);
            };
            Reader<PvLogisticsRequest>.Read = r => new PvLogisticsRequest { VehicleId = r.ReadUInt32(), Attach = r.ReadBoolean() };

            Writer<PvLogistics>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteByte((byte)m.Event);
                w.WriteUInt32(m.SubjectId);
                w.WriteUInt32(m.LoadId);
                w.WriteSingle(m.LoadMass);
            };
            Reader<PvLogistics>.Read = r => new PvLogistics
            {
                VehicleId = r.ReadUInt32(),
                Event = (PvLogisticsEvent)r.ReadByte(),
                SubjectId = r.ReadUInt32(),
                LoadId = r.ReadUInt32(),
                LoadMass = r.ReadSingle(),
            };

            Writer<PvCargoStow>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteBoolean(m.Stow);
            };
            Reader<PvCargoStow>.Read = r => new PvCargoStow { VehicleId = r.ReadUInt32(), Stow = r.ReadBoolean() };

            Writer<PvHello>.Write = (w, m) => w.WriteByte(m.Version);
            Reader<PvHello>.Read = r => new PvHello { Version = r.ReadByte() };

            Writer<PvVersion>.Write = (w, m) => w.WriteByte(m.Version);
            Reader<PvVersion>.Read = r => new PvVersion { Version = r.ReadByte() };

            Writer<PvTurretAim>.Write = (w, m) =>
            {
                w.WriteUInt32(m.VehicleId);
                w.WriteByte(m.Station);
                w.WriteSingle(m.StationAimX);
                w.WriteSingle(m.StationAimY);
                w.WriteSingle(m.StationAimZ);
                w.WriteSingle(m.FreeAimX);
                w.WriteSingle(m.FreeAimY);
                w.WriteSingle(m.FreeAimZ);
            };
            Reader<PvTurretAim>.Read = r => new PvTurretAim
            {
                VehicleId = r.ReadUInt32(),
                Station = r.ReadByte(),
                StationAimX = r.ReadSingle(),
                StationAimY = r.ReadSingle(),
                StationAimZ = r.ReadSingle(),
                FreeAimX = r.ReadSingle(),
                FreeAimY = r.ReadSingle(),
                FreeAimZ = r.ReadSingle(),
            };

            MessagePacker.RegisterMessage<PvEnterRequest>();
            MessagePacker.RegisterMessage<PvExitRequest>();
            MessagePacker.RegisterMessage<PvSessionReply>();
            MessagePacker.RegisterMessage<PvInput>();
            MessagePacker.RegisterMessage<PvHello>();
            MessagePacker.RegisterMessage<PvVersion>();
            MessagePacker.RegisterMessage<PvTurretAim>();
            MessagePacker.RegisterMessage<PvHit>();
            MessagePacker.RegisterMessage<PvDriver>();
            MessagePacker.RegisterMessage<PvPurchaseRequest>();
            MessagePacker.RegisterMessage<PvPurchaseReply>();
            MessagePacker.RegisterMessage<PvSellRequest>();
            MessagePacker.RegisterMessage<PvSellReply>();
            MessagePacker.RegisterMessage<PvServerRules>();
            MessagePacker.RegisterMessage<PvDeploy>();
            MessagePacker.RegisterMessage<PvStateRequest>();
            MessagePacker.RegisterMessage<PvNotice>();
            MessagePacker.RegisterMessage<PvDamageDirection>();
            MessagePacker.RegisterMessage<PvLogisticsRequest>();
            MessagePacker.RegisterMessage<PvLogistics>();
            MessagePacker.RegisterMessage<PvCargoStow>();
            MessagePacker.RegisterMessage<PvInputEcho>();
            MessagePacker.RegisterMessage<PvIncomingHit>();
            MessagePacker.RegisterMessage<PvAmmoState>();
        }

        public static string Describe(PvReply reply)
        {
            switch (reply)
            {
                case PvReply.NotFound: return "Host: vehicle not found";
                case PvReply.Destroyed: return "Host: vehicle is destroyed";
                case PvReply.WrongFaction: return "Host: vehicle belongs to another faction";
                case PvReply.Busy: return "Host: someone else is driving this vehicle";
                case PvReply.NotOwned: return "Host: " + VehicleAccess.DeniedText;
                case PvReply.Disabled: return "Host: vehicles are disabled on this server";
                case PvReply.Ended: return "Host ended the vehicle session";
                default: return "Host granted the vehicle";
            }
        }
    }
}
