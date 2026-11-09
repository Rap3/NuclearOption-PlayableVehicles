using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class DeployParts
    {
        private static readonly Type PartType = typeof(GroundVehicle).GetNestedType("DeployablePart", BindingFlags.NonPublic);
        private static readonly FieldInfo AmountField =
            PartType?.GetField("deployedAmount", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly AccessTools.FieldRef<object, float> Amount =
            AmountField != null ? AccessTools.FieldRefAccess<object, float>(AmountField) : null;

        public static bool Available => Amount != null && GameAccess.GroundVehicleDeployableParts != null;

        private static readonly FieldInfo DeployingField =
            PartType?.GetField("deploying", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo StaysField =
            PartType?.GetField("staysDeployed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TransformField =
            PartType?.GetField("transform", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo RateField =
            PartType?.GetField("deployRate", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo AngleStowedField =
            PartType?.GetField("angleStowed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo AngleDeployedField =
            PartType?.GetField("angleDeployed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo PosStowedField =
            PartType?.GetField("positionStowed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo PosDeployedField =
            PartType?.GetField("positionDeployed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo DisableWhenStowedField =
            PartType?.GetField("disableWhenStowed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo PartRadarField =
            PartType?.GetField("radar", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo PartColliderField =
            PartType?.GetField("collider", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TurretField =
            PartType?.GetField("turret", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TurretStowed = typeof(Turret).GetField("stowed", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TurretTraverse = typeof(Turret).GetField("traverseAngle", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TurretElevation = typeof(Turret).GetField("elevationAngle", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TurretTarget = typeof(Turret).GetField("target", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TurretElevTransform = typeof(Turret).GetField("elevationTransform", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Dictionary<string, object> TurretDump(object part)
        {
            var d = new Dictionary<string, object>();
            if (TurretField?.GetValue(part) is not Turret t) return d;
            d["turretEnabled"] = t.enabled;
            d["turretActive"] = t.gameObject.activeInHierarchy;
            d["turretSelfActive"] = t.gameObject.activeSelf;
            d["manual"] = typeof(Turret).GetField("manual", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(t) is true;
            d["turretStowed"] = TurretStowed != null && (bool)TurretStowed.GetValue(t);
            d["traverse"] = TurretTraverse != null ? (float)TurretTraverse.GetValue(t) : -999f;
            d["elevation"] = TurretElevation != null ? (float)TurretElevation.GetValue(t) : -999f;
            d["hasTarget"] = TurretTarget != null && TurretTarget.GetValue(t) != null;
            if (TurretElevTransform?.GetValue(t) is Transform et)
                d["elevEuler"] = Mathf.Round(et.localEulerAngles.x) + "," + Mathf.Round(et.localEulerAngles.y) + "," + Mathf.Round(et.localEulerAngles.z);
            return d;
        }

        private static string PathOf(Transform t)
        {
            var sb = new System.Text.StringBuilder();
            while (t != null && sb.Length < 200)
            {
                if (sb.Length > 0) sb.Insert(0, "/");
                sb.Insert(0, t.name);
                t = t.parent;
            }
            return sb.ToString();
        }

        public static IEnumerable<object> DebugDump(Array parts)
        {
            var dump = new List<object>();
            if (parts == null) return dump;
            foreach (object part in parts)
            {
                if (part == null) continue;
                var entry = new Dictionary<string, object>
                {
                    ["name"] = TransformField?.GetValue(part) is Transform t ? t.name : "?",
                    ["amount"] = Amount != null ? Amount(part) : -1f,
                    ["deploying"] = DeployingField != null && (bool)DeployingField.GetValue(part),
                    ["stays"] = StaysField != null && (bool)StaysField.GetValue(part),
                    ["active"] = TransformField?.GetValue(part) is Transform tr ? tr.gameObject.activeInHierarchy : (object)null,
                    ["euler"] = TransformField?.GetValue(part) is Transform tt
                        ? (object)(Mathf.Round(tt.localEulerAngles.x) + "," + Mathf.Round(tt.localEulerAngles.y) + "," + Mathf.Round(tt.localEulerAngles.z))
                        : null,
                    ["rate"] = RateField != null ? (object)(float)RateField.GetValue(part) : null,
                    ["angStowed"] = AngleStowedField?.GetValue(part)?.ToString(),
                    ["angDeployed"] = AngleDeployedField?.GetValue(part)?.ToString(),
                    ["posStowed"] = PosStowedField?.GetValue(part)?.ToString(),
                    ["posDeployed"] = PosDeployedField?.GetValue(part)?.ToString(),
                    ["disableWhenStowed"] = DisableWhenStowedField != null && (bool)DisableWhenStowedField.GetValue(part),
                    ["partRadar"] = PartRadarField?.GetValue(part) != null,
                    ["partCollider"] = PartColliderField?.GetValue(part) != null,
                    ["partTurret"] = TurretField?.GetValue(part) is Turret pt ? pt.name : null,
                    ["path"] = TransformField?.GetValue(part) is Transform tp ? PathOf(tp) : null,
                };
                foreach (var kv in TurretDump(part)) entry[kv.Key] = kv.Value;
                dump.Add(entry);
            }
            return dump;
        }

        public static bool AnyMoving(GroundVehicle vehicle)
        {
            if (vehicle == null) return false;
            if (LauncherArmPose.InProgress(vehicle)) return true;
            if (!Available) return false;
            Array parts = GameAccess.GroundVehicleDeployableParts(vehicle);
            if (parts == null || parts.Length == 0) return false;
            for (int i = 0; i < parts.Length; i++)
            {
                object part = parts.GetValue(i);
                if (part == null) continue;
                float amount = Amount(part);
                if (amount > 0f && amount < 1f) return true;
            }
            return false;
        }
    }
}
