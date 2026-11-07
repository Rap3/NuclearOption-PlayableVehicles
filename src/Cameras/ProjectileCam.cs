using UnityEngine;

namespace PlayableVehicles
{
    internal static class ProjectileCam
    {
        public enum Kind { None, Shell, Missile }

        public static Kind Active { get; private set; }
        public static bool IsActive => Active != Kind.None;
        public static bool IsWaiting { get; private set; }

        private const float Step = 0.02f;
        private const float DefaultLifetime = 5f;

        private static Vector3 _position;
        private static Vector3 _velocity;
        private static float _speed0Sqr;
        private static float _gravMult;
        private static float _dragK;
        private static float _life;
        private static float _stepAcc;
        private static Missile _missile;

        public static float Age { get; private set; }

        public static Vector3 Position => _position;
        public static Vector3 Velocity => _velocity;

        public static void Toggle()
        {
            if (!VehicleSession.IsActive) return;
            if (IsActive || IsWaiting)
            {
                Stop();
                return;
            }
            IsWaiting = true;
            ModLog.Notify("Projectile camera armed: the next shot from your vehicle takes the view");
        }

        public static void Stop()
        {
            IsWaiting = false;
            if (!IsActive) return;
            Active = Kind.None;
            _missile = null;
            VehicleSession.ViewCamera?.ProjectileEnded();
        }

        public static void Reset()
        {
            IsWaiting = false;
            Active = Kind.None;
            _missile = null;
        }

        public static void RecordShellShot(Gun gun)
        {
            if (!IsActive && !IsWaiting) return;
            if (IsActive) return;
            if (gun == null || !IsOwn(gun.attachedUnit)) return;
            if (GameAccess.GunGuidedProjectile != null && GameAccess.GunGuidedProjectile(gun) != null) return;
            Transform[] muzzles = GameAccess.GunMuzzles != null ? GameAccess.GunMuzzles(gun) : null;
            Transform muzzle = muzzles != null && muzzles.Length > 0 ? muzzles[0] : null;
            if (muzzle == null) return;
            float speed = GameAccess.GunMuzzleVelocity != null ? GameAccess.GunMuzzleVelocity(gun) : 0f;
            if (speed <= 0f) return;
            WeaponInfo info = gun.info;

            StartShell(muzzle.position,
                (gun.velocityInherit != null ? gun.velocityInherit.velocity : Vector3.zero) + muzzle.forward * speed,
                speed,
                info != null ? info.gravMult : 1f, info != null ? info.dragCoef : 0f,
                GameAccess.GunBulletSelfDestruct != null ? GameAccess.GunBulletSelfDestruct(gun) : DefaultLifetime);
        }

        public static void RecordClientPrediction(WeaponStation station)
        {
            if (!IsActive && !IsWaiting) return;
            if (IsActive) return;
            if (station == null || !Ballistics.TryGetGunProfile(station, null, out GunProfile profile)) return;
            StartShell(profile.Muzzle.position,
                profile.InheritedVelocity + profile.Muzzle.forward * profile.Speed,
                profile.Speed, profile.GravMult, profile.DragCoef, profile.Lifetime);
        }

        private static void StartShell(Vector3 position, Vector3 velocity, float speed, float gravMult, float dragCoef, float lifetime)
        {
            _position = position;
            _velocity = velocity;
            _speed0Sqr = speed * speed;
            _gravMult = gravMult;
            _dragK = dragCoef > 0f ? dragCoef / speed : 0f;
            _life = lifetime > 0f ? lifetime : DefaultLifetime;
            _stepAcc = 0f;
            _missile = null;
            Age = 0f;
            Active = Kind.Shell;
            IsWaiting = false;
            VehicleSession.ViewCamera?.ProjectileStarted(velocity);
        }

        public static void RecordMissile(Missile missile, Unit owner)
        {
            if (!IsActive && !IsWaiting) return;
            if (IsActive) return;
            if (missile == null || !IsOwn(owner)) return;
            _missile = missile;
            _position = missile.transform.position;
            _velocity = missile.rb != null ? missile.rb.velocity : missile.transform.forward * missile.speed;
            Age = 0f;
            Active = Kind.Missile;
            IsWaiting = false;
            VehicleSession.ViewCamera?.ProjectileStarted(_velocity);
        }

        private static bool IsOwn(Unit owner)
        {
            GroundVehicle mine = VehicleSession.Vehicle;
            return mine != null && owner != null && (object)owner == (object)mine;
        }

        public static void FollowOriginShift(Vector3 shift)
        {
            if (Active == Kind.Shell) _position += shift;
        }

        public static bool TryUpdate(float dt, out Vector3 position, out Vector3 velocity)
        {
            position = _position;
            velocity = _velocity;
            if (!IsActive) return false;

            if (Active == Kind.Missile)
            {
                if (_missile == null || _missile.disabled) { Stop(); return false; }
                _position = _missile.transform.position;
                _velocity = _missile.rb != null ? _missile.rb.velocity : _missile.transform.forward * _missile.speed;
                Age += dt;
                position = _position;
                velocity = _velocity;
                return true;
            }

            _stepAcc += dt;
            while (_stepAcc >= Step)
            {
                _stepAcc -= Step;
                if (_life <= 0f || _velocity.sqrMagnitude < _speed0Sqr * 0.02f) { Stop(); return false; }
                _velocity.y -= 9.81f * Step * _gravMult;
                if (_dragK > 0f) _velocity -= _velocity.magnitude * _velocity * (_dragK * Step);
                _position += _velocity * Step;
                _life -= Step;
                Age += Step;
            }
            position = _position;
            velocity = _velocity;
            return true;
        }
    }
}
