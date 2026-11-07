using UnityEngine;

namespace PlayableVehicles
{
    internal static class ShotPrediction
    {
        private const float SingleShotInterval = 0.2f;
        private const float PendingLifetime = 1f;

        private static GroundVehicle _vehicle;
        private static int[] _pending;
        private static float[] _pendingUntil;
        private static float[] _readyAt;
        private static byte _lastPresses;
        private static bool _firing;

        public static void Tick(GroundVehicle vehicle, TurretController turrets)
        {
            if (vehicle == null || vehicle.weaponStations == null || GameAccess.GunFireInterval == null || GameAccess.GunReloadTime == null) return;
            if (!ReferenceEquals(vehicle, _vehicle) || _pending == null || _pending.Length != vehicle.weaponStations.Count) Reset(vehicle, turrets);

            bool pressed = turrets.FirePresses != _lastPresses;
            _lastPresses = turrets.FirePresses;
            if (!turrets.FireHeld && !pressed) return;

            int index = turrets.SelectedStationIndex;
            if (index < 0 || index >= vehicle.weaponStations.Count) return;
            WeaponStation station = vehicle.weaponStations[index];
            Gun gun = SingleShotGun(station);
            if (gun == null || Time.unscaledTime < _readyAt[index]) return;

            AmmoSync.View view = AmmoSync.Of(vehicle, station);
            if (view.Ammo <= 0 || view.Loaded <= 0 || view.Reloading || view.ReloadProgress > 0f) return;

            foreach (Weapon weapon in station.Weapons)
            {
                if (weapon != null) weapon.Safety = false;
            }
            _firing = true;
            try
            {
                station.RemoteFireSingle(vehicle);
            }
            finally
            {
                _firing = false;
            }

            _pending[index]++;
            _pendingUntil[index] = Time.unscaledTime + PendingLifetime + ClientLink.Rtt;
            bool emptied = view.Loaded - station.Weapons.Count <= 0;
            _readyAt[index] = Time.unscaledTime + (emptied ? GameAccess.GunReloadTime(gun) : GameAccess.GunFireInterval(gun));
        }

        public static bool ConsumeEcho(Unit owner, WeaponStation station)
        {
            if (_firing || _pending == null || !VehicleSession.IsClientDriver || !ReferenceEquals(owner, _vehicle)) return false;
            int index = _vehicle.weaponStations.IndexOf(station);
            if (index < 0 || index >= _pending.Length || _pending[index] <= 0) return false;
            if (Time.unscaledTime > _pendingUntil[index])
            {
                _pending[index] = 0;
                return false;
            }
            _pending[index]--;
            return true;
        }

        private static Gun SingleShotGun(WeaponStation station)
        {
            if (station == null || station.Weapons == null || station.Weapons.Count == 0) return null;
            Gun gun = station.Weapons[0] as Gun;
            return gun != null && GameAccess.GunFireInterval(gun) > SingleShotInterval ? gun : null;
        }

        private static void Reset(GroundVehicle vehicle, TurretController turrets)
        {
            _vehicle = vehicle;
            int count = vehicle != null && vehicle.weaponStations != null ? vehicle.weaponStations.Count : 0;
            _pending = new int[count];
            _pendingUntil = new float[count];
            _readyAt = new float[count];
            _lastPresses = turrets != null ? turrets.FirePresses : (byte)0;
        }

        public static void Clear()
        {
            _vehicle = null;
            _pending = null;
            _pendingUntil = null;
            _readyAt = null;
        }
    }
}
