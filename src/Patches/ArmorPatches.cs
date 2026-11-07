using System;
using HarmonyLib;
using UnityEngine;

namespace PlayableVehicles
{
    [OptionalPatch("zonal armor and module damage of ground vehicles")]
    [HarmonyPatch(typeof(DamageEffects), nameof(DamageEffects.ArmorPenetrate))]
    internal static class DamageEffects_ArmorPenetrate_Patch
    {
        private static readonly ErrorThrottle Errors = new ErrorThrottle("DamageEffects.ArmorPenetrate patch");

        private const float JetVelocity = 1000f;

        private static bool Prefix(Vector3 __0, Vector3 __1, float __2, float __3, float __4, PersistentID __5)
        {
            try
            {
                if (!ModConfig.ZonalArmor.Value) return true;
                Run(__0, __1, __2, __3, __4, __5);
                return false;
            }
            catch (Exception e)
            {
                Errors.Report(e);
                return true;
            }
        }

        private static void Run(Vector3 position, Vector3 velocity, float muzzleVelocity, float pierceDamage, float blastDamage, PersistentID dealerID)
        {
            if (blastDamage > 0f) DamageEffects.BlastFrag(blastDamage, position, dealerID, PersistentID.None);

            bool jet = blastDamage == 0f && muzzleVelocity == JetVelocity && Mathf.Approximately(velocity.magnitude, JetVelocity);
            Vector3 point = position;
            Vector3 v = velocity;
            for (int layer = 0; layer < 10; layer++)
            {
                if (v.sqrMagnitude <= muzzleVelocity * muzzleVelocity * 0.1f) return;
                if (!Physics.Linecast(point, point + v * 0.1f, out RaycastHit hit, ~PhysicsLayers.ExclusionZonesMask)) return;
                IDamageable damageable = hit.collider.gameObject.GetComponent<IDamageable>();
                if (damageable == null) return;

                if (ArmorModel.Handles(damageable))
                {
                    ArmorModel.Hit((UnitPart)damageable, hit, v, muzzleVelocity, pierceDamage, jet, dealerID);
                    return;
                }

                float pierce = Mathf.Max(Vector3.Dot(v.normalized, -hit.normal), 0.5f) * pierceDamage * (v.magnitude / muzzleVelocity);
                damageable.TakeDamage(pierce, 1f, 0f, 0f, 0f, dealerID);
                ArmorProperties armor = damageable.GetArmorProperties();
                if (pierce <= armor.pierceArmor) return;
                v *= (pierce - armor.pierceArmor * 2f) / pierce;
                point = hit.point + 0.1f * v.normalized;
            }
        }
    }
}
