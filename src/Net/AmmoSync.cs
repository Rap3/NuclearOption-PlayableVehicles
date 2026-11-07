using Mirage;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class AmmoSync
    {
        public const float MinInterval = 0.1f;
        public const float MaxInterval = 1f;
        private const float StaleAfter = 3f;

        public struct View
        {
            public int Loaded;
            public int Total;
            public int Ammo;
            public bool Reloading;
            public float ReloadProgress;
        }

        public static void ServerTick(RemoteDrivers.Session session)
        {
            GroundVehicle vehicle = session.Vehicle;
            if (vehicle == null || session.Connection == null || vehicle.weaponStations == null) return;
            float now = Time.unscaledTime;
            if (now < session.NextAmmoTime) return;

            int count = Mathf.Min(vehicle.weaponStations.Count, 16);
            if (session.AmmoSent == null || session.AmmoSent.Length != count) session.AmmoSent = new PvStationAmmo[count];
            if (session.AmmoBuild == null || session.AmmoBuild.Length != count) session.AmmoBuild = new PvStationAmmo[count];
            bool changed = session.AmmoSentRearm != vehicle.HasRequestedRearm || session.AmmoSentCount != count;
            PvStationAmmo[] stations = session.AmmoBuild;
            for (int i = 0; i < count; i++)
            {
                View v = Local(vehicle.weaponStations[i]);
                stations[i] = new PvStationAmmo
                {
                    Loaded = Clamp16(v.Loaded),
                    Total = Clamp16(v.Total),
                    Ammo = Clamp16(v.Ammo),
                    Reloading = v.Reloading,
                    ReloadProgress = (byte)Mathf.Clamp(Mathf.RoundToInt(v.ReloadProgress * 255f), 0, 255),
                };
                PvStationAmmo old = session.AmmoSent[i];
                if (old.Loaded != stations[i].Loaded || old.Total != stations[i].Total || old.Ammo != stations[i].Ammo
                    || old.Reloading != stations[i].Reloading || old.ReloadProgress != stations[i].ReloadProgress) changed = true;
            }
            if (!changed && now < session.AmmoKeepAliveTime) return;

            session.AmmoBuild = session.AmmoSent;
            session.AmmoSent = stations;
            session.AmmoSentCount = count;
            session.AmmoSentRearm = vehicle.HasRequestedRearm;
            session.NextAmmoTime = now + MinInterval;
            session.AmmoKeepAliveTime = now + MaxInterval;
            session.Connection.Send(new PvAmmoState
            {
                VehicleId = vehicle.persistentID.Id,
                RearmRequested = vehicle.HasRequestedRearm,
                Stations = stations,
            }, Channel.Unreliable);
        }

        private static ushort Clamp16(int value)
        {
            return (ushort)Mathf.Clamp(value, 0, ushort.MaxValue);
        }

        private static uint _vehicleId;
        private static PvStationAmmo[] _stations;
        private static bool _rearmRequested;
        private static float _time = float.NegativeInfinity;

        public static void OnAmmoState(INetworkPlayer _, PvAmmoState message)
        {
            GroundVehicle vehicle = VehicleSession.Vehicle;
            if (!VehicleSession.IsClientDriver || (object)vehicle == null || vehicle.persistentID.Id != message.VehicleId) return;
            _vehicleId = message.VehicleId;
            _stations = message.Stations;
            _rearmRequested = message.RearmRequested;
            _time = Time.unscaledTime;
        }

        public static void Clear()
        {
            _vehicleId = 0;
            _stations = null;
            _rearmRequested = false;
            _time = float.NegativeInfinity;
        }

        private static bool HasRemote(GroundVehicle vehicle)
        {
            return VehicleSession.IsClientDriver && vehicle != null && _stations != null
                && vehicle.persistentID.Id == _vehicleId && Time.unscaledTime - _time < StaleAfter;
        }

        public static bool RearmRequested(GroundVehicle vehicle)
        {
            if (vehicle == null) return false;
            return HasRemote(vehicle) ? _rearmRequested : vehicle.HasRequestedRearm;
        }

        public static View Of(GroundVehicle vehicle, WeaponStation station)
        {
            if (station != null && HasRemote(vehicle))
            {
                int index = vehicle.weaponStations.IndexOf(station);
                if (index >= 0 && index < _stations.Length)
                {
                    PvStationAmmo s = _stations[index];
                    return new View
                    {
                        Loaded = s.Loaded,
                        Total = s.Total,
                        Ammo = s.Ammo,
                        Reloading = s.Reloading,
                        ReloadProgress = s.ReloadProgress / 255f,
                    };
                }
            }
            return Local(station);
        }

        public static View Local(WeaponStation station)
        {
            var view = new View();
            if (station == null) return view;
            view.Ammo = station.Ammo;
            view.Reloading = station.Reloading;
            foreach (Weapon weapon in station.Weapons)
            {
                if (weapon == null) continue;
                view.Loaded += weapon.GetAmmoLoaded();
                view.Total += weapon.GetAmmoTotal();
                view.ReloadProgress = Mathf.Max(view.ReloadProgress, weapon.GetReloadProgress());
            }
            return view;
        }

        public static string ReadoutOf(GroundVehicle vehicle, WeaponStation station)
        {
            return HasRemote(vehicle) ? Readout(Of(vehicle, station)) : station.GetAmmoReadout();
        }

        public static string Readout(View view)
        {
            return view.Total != view.Loaded ? view.Loaded + " / " + view.Ammo : view.Loaded.ToString();
        }
    }
}
