using System;
using System.Collections.Generic;
using Mirage;
using NuclearOption.Networking;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class RemoteDrivers
    {
        internal sealed class Session
        {
            public INetworkPlayer Connection;
            public GroundVehicle Vehicle;
            public VehicleDriver Driver;
            public Unit LastBulletUnit;
            public float LastBulletTime;
            public float LastInputTime;
            public bool HasInputSeq;
            public ushort LastInputSeq;
            public float NextDamageDirectionTime;
            public float NextEchoTime;
            public float NextInputAt;
            public float NextDeployAt;
            public float NextLogisticsAt;
            public float NextIncomingHitTime;
            public PvStationAmmo[] AmmoSent;
            public PvStationAmmo[] AmmoBuild;
            public int AmmoSentCount;
            public bool AmmoSentRearm;
            public float NextAmmoTime;
            public float AmmoKeepAliveTime;
        }

        private const float HitDedupWindow = 0.1f;
        private const float MinInputInterval = 0.03f;
        private const float MinDeployInterval = 0.25f;
        internal const float MinLogisticsInterval = 0.25f;
        private const float MinEnterInterval = 0.4f;
        private static readonly Dictionary<INetworkPlayer, float> _nextEnterAt = new Dictionary<INetworkPlayer, float>();
        private const float InputTimeout = 10f;
        private const float DamageDirectionInterval = 0.1f;
        private const float EchoInterval = 0.2f;
        private const float IncomingHitInterval = 1f / 30f;
        private const byte NoStation = 255;

        private static readonly List<Session> _sessions = new List<Session>();

        public static IReadOnlyList<Session> Sessions => _sessions;

        public static void OnEnter(INetworkPlayer connection, PvEnterRequest message)
        {
            Session existing = Find(connection);
            if (existing != null && existing.Vehicle != null && !existing.Vehicle.disabled && existing.Driver != null
                && existing.Vehicle.persistentID.Id == message.VehicleId)
            {
                existing.LastInputTime = Time.unscaledTime;
                Reply(connection, message.VehicleId, PvReply.Granted);
                LogisticsLink.SendState(connection, existing.Vehicle);
                return;
            }

            float now = Time.unscaledTime;
            if (_nextEnterAt.TryGetValue(connection, out float nextEnter) && now < nextEnter)
            {
                Reply(connection, message.VehicleId, PvReply.Busy);
                return;
            }
            _nextEnterAt[connection] = now + MinEnterInterval;

            Player player = connection.Identity != null ? connection.Identity.GetComponent<Player>() : null;
            PvReply reply = Validate(message.VehicleId, player, out GroundVehicle vehicle);
            if (reply != PvReply.Granted)
            {
                Reply(connection, message.VehicleId, reply);
                return;
            }

            Session previous = Find(connection);
            if (previous != null) End(previous, false);

            VehicleDriver driver = vehicle.gameObject.GetComponent<VehicleDriver>() ?? vehicle.gameObject.AddComponent<VehicleDriver>();
            driver.enabled = true;
            try
            {
                driver.Init(vehicle, VehicleDriver.DriveMode.ServerForClient);
            }
            catch (Exception e)
            {
                ModLog.Warning("Multiplayer: driver init failed on " + vehicle.unitName + ": " + e);
                try { driver.Release(); } catch { }
                driver.enabled = false;
                Reply(connection, message.VehicleId, PvReply.Destroyed);
                return;
            }
            driver.Turrets.RemoteNotice = kind => connection.Send(new PvNotice { VehicleId = vehicle.persistentID.Id, Kind = kind }, Channel.Reliable);
            KillCredit.Begin(vehicle, player);

            _sessions.Add(new Session { Connection = connection, Vehicle = vehicle, Driver = driver, LastInputTime = Time.unscaledTime });
            Reply(connection, message.VehicleId, PvReply.Granted);
            LogisticsLink.SendState(connection, vehicle);
            ModLog.Info("Multiplayer: " + player + " drives " + vehicle.unitName);
        }

        public static void OnExit(INetworkPlayer connection, PvExitRequest message)
        {
            Session session = Find(connection);
            if (session != null && session.Vehicle != null && session.Vehicle.persistentID.Id == message.VehicleId)
            {
                End(session, false);
            }
        }

        public static void OnInput(INetworkPlayer connection, PvInput message)
        {
            Session session = Find(connection);
            if (session == null || session.Vehicle == null || session.Vehicle.persistentID.Id != message.VehicleId) return;
            session.LastInputTime = Time.unscaledTime;
            if (Time.unscaledTime < session.NextInputAt) return;
            session.NextInputAt = Time.unscaledTime + MinInputInterval;
            if (session.HasInputSeq)
            {
                int delta = unchecked((short)(message.Seq - session.LastInputSeq));
                if (delta <= 0) return;
            }
            session.HasInputSeq = true;
            session.LastInputSeq = message.Seq;
            session.Driver.ApplyRemoteInput(message);

            if (Time.unscaledTime >= session.NextEchoTime)
            {
                session.NextEchoTime = Time.unscaledTime + EchoInterval;
                connection.Send(new PvInputEcho { ClientTime = message.ClientTime }, Channel.Unreliable);
            }
        }

        public static void OnDeploy(INetworkPlayer connection, PvDeploy message)
        {
            Session session = Find(connection);
            if (session == null || session.Vehicle == null || session.Vehicle.persistentID.Id != message.VehicleId) return;
            if (Time.unscaledTime < session.NextDeployAt) return;
            session.NextDeployAt = Time.unscaledTime + MinDeployInterval;
            if (!VehicleDeploy.CanDeploy(session.Vehicle)) return;
            if (message.Deploy && session.Driver.BodyVelocity.magnitude > VehicleDeploy.MaxDeploySpeed * 2f) return;
            VehicleDeploy.Set(session.Vehicle, message.Deploy);
        }

        public static void OnDisconnected(INetworkPlayer connection)
        {
            Session session = Find(connection);
            if (session != null) End(session, false);
            _nextEnterAt.Remove(connection);
        }

        public static void Tick()
        {
            for (int i = _sessions.Count - 1; i >= 0; i--)
            {
                Session session = _sessions[i];
                if (session.Vehicle == null || session.Vehicle.disabled || session.Driver == null) End(session, true);
                else if (Time.unscaledTime - session.LastInputTime > InputTimeout)
                {
                    ModLog.Warning("Multiplayer: no input from the driver of " + session.Vehicle.unitName + " for " + InputTimeout + " s, session ended");
                    End(session, true);
                }
                else
                {
                    AmmoSync.ServerTick(session);
                }
            }
        }

        public static void EndAll()
        {
            for (int i = _sessions.Count - 1; i >= 0; i--)
            {
                End(_sessions[i], false);
            }
            _nextEnterAt.Clear();
        }

        private static PvReply Validate(uint vehicleId, Player player, out GroundVehicle vehicle)
        {
            vehicle = null;
            if (!ModConfig.Enabled.Value) return PvReply.Disabled;
            if (!UnitRegistry.TryGetUnit(new PersistentID?(new PersistentID { Id = vehicleId }), out Unit unit)) return PvReply.NotFound;
            vehicle = unit as GroundVehicle;
            if (vehicle == null) return PvReply.NotFound;
            if (vehicle.disabled) return PvReply.Destroyed;
            if (player == null || vehicle.NetworkHQ == null || vehicle.NetworkHQ != player.HQ) return PvReply.WrongFaction;
            if (ControlRegistry.IsControlled(vehicle)) return PvReply.Busy;
            if (vehicle.IsSlung()) return PvReply.Busy;
            if (!VehicleAccess.CanDrive(player, vehicle, false)) return PvReply.NotOwned;
            return PvReply.Granted;
        }

        private static void End(Session session, bool notifyClient)
        {
            _sessions.Remove(session);
            try
            {
                Towing.Release(session.Vehicle, true);
            }
            catch (Exception e)
            {
                ModLog.Error("Multiplayer: trailer release failed: " + e);
            }
            try
            {
                if (session.Driver != null)
                {
                    session.Driver.Turrets.RemoteNotice = null;
                    session.Driver.Release();
                    session.Driver.enabled = false;
                }
            }
            catch (Exception e)
            {
                ModLog.Error("Multiplayer: driver release failed: " + e);
            }
            ControlRegistry.Forget(session.Vehicle);
            KillCredit.End(session.Vehicle);

            if (notifyClient && session.Connection != null && (object)session.Vehicle != null)
            {
                Reply(session.Connection, session.Vehicle.persistentID.Id, PvReply.Ended);
            }
        }

        public static void OnBulletHit(Unit shooter, Unit hitUnit, Vector3 localPoint)
        {
            if (_sessions.Count == 0) return;
            Session session = FindByVehicle(shooter);
            if (session == null || !HitFeedback.IsHitOnForeignUnit(session.Vehicle, hitUnit)) return;

            session.LastBulletUnit = hitUnit;
            session.LastBulletTime = Time.time;
            SendHit(session, hitUnit, localPoint, true);
        }

        public static void OnDamage(Unit damaged, PersistentID dealer)
        {
            if (_sessions.Count == 0) return;
            Session session = null;
            foreach (Session candidate in _sessions)
            {
                if (candidate.Vehicle != null && candidate.Vehicle.persistentID == dealer) session = candidate;
            }
            if (session == null || !HitFeedback.IsHitOnForeignUnit(session.Vehicle, damaged)) return;
            if (damaged == session.LastBulletUnit && Time.time - session.LastBulletTime < HitDedupWindow) return;

            SendHit(session, damaged, Vector3.zero, false);
        }

        public static void OnVehicleDamaged(Unit damaged, PersistentID dealer)
        {
            if (_sessions.Count == 0) return;
            Session session = FindByVehicle(damaged);
            if (session == null || session.Connection == null || dealer == session.Vehicle.persistentID) return;
            if (Time.unscaledTime < session.NextDamageDirectionTime) return;
            if (!UnitRegistry.TryGetUnit(new PersistentID?(dealer), out Unit attacker) || attacker == null) return;

            Vector3 direction = attacker.transform.position - session.Vehicle.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            direction.Normalize();
            session.NextDamageDirectionTime = Time.unscaledTime + DamageDirectionInterval;
            session.Connection.Send(new PvDamageDirection
            {
                VehicleId = session.Vehicle.persistentID.Id,
                DirX = direction.x,
                DirZ = direction.z,
            }, Channel.Unreliable);
        }

        public static void OnIncomingBullet(Unit shooter, Unit hitUnit, Vector3 localPoint, Vector3 bulletVelocity, WeaponInfo weaponInfo)
        {
            if (_sessions.Count == 0) return;
            Session session = FindByVehicle(hitUnit);
            if (session == null || session.Connection == null || shooter == null || ReferenceEquals(shooter, hitUnit)) return;
            if (Time.unscaledTime < session.NextIncomingHitTime) return;
            if (bulletVelocity.sqrMagnitude < 0.01f) return;
            session.NextIncomingHitTime = Time.unscaledTime + IncomingHitInterval;

            byte station = NoStation;
            for (int i = 0; i < shooter.weaponStations.Count && i < NoStation; i++)
            {
                if (shooter.weaponStations[i].WeaponInfo == weaponInfo)
                {
                    station = (byte)i;
                    break;
                }
            }
            Vector3 direction = bulletVelocity.normalized;
            session.Connection.Send(new PvIncomingHit
            {
                VehicleId = session.Vehicle.persistentID.Id,
                ShooterId = shooter.persistentID.Id,
                Station = station,
                X = localPoint.x,
                Y = localPoint.y,
                Z = localPoint.z,
                DirX = direction.x,
                DirY = direction.y,
                DirZ = direction.z,
            }, Channel.Unreliable);
        }

        private static void SendHit(Session session, Unit target, Vector3 localPoint, bool hasPoint)
        {
            session.Connection.Send(new PvHit
            {
                TargetId = target.persistentID.Id,
                HasPoint = hasPoint,
                X = localPoint.x,
                Y = localPoint.y,
                Z = localPoint.z,
            }, Channel.Unreliable);
        }

        internal static Session FindByVehicle(Unit vehicle)
        {
            if ((object)vehicle == null) return null;
            foreach (Session session in _sessions)
            {
                if (ReferenceEquals(session.Vehicle, vehicle)) return session;
            }
            return null;
        }

        internal static Session Find(INetworkPlayer connection)
        {
            foreach (Session session in _sessions)
            {
                if (session.Connection == connection) return session;
            }
            return null;
        }

        private static void Reply(INetworkPlayer connection, uint vehicleId, PvReply reply)
        {
            connection.Send(new PvSessionReply { VehicleId = vehicleId, Reply = reply }, Channel.Reliable);
        }
    }
}
