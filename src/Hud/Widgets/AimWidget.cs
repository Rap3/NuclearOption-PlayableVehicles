using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class AimWidget : HudWidget
    {
        private const float OnTargetToleranceDegrees = 0.75f;
        private const float BlockedMargin = 15f;
        private const float BlockedFraction = 0.05f;
        private const float BlockedAngleDegrees = 4f;
        private static readonly Vector2 LabelOffset = new Vector2(0f, -38f);
        private const float CenterScale = 0.6f;

        private const float CenterAlpha = 0.55f;

        private RectTransform _center;
        private GameHudParts.TurretCrosshair _gun;
        private RawImage _fallbackMarker;
        private Texture2D _ringTexture;
        private TMPro.TextMeshProUGUI _label;
        private int _labelKey = int.MinValue;

        public override void Build(RectTransform root, HudContext context)
        {
            GameHudParts.TurretCrosshair center = GameHudParts.Crosshair(root);
            if (center != null)
            {
                if (center.Circle != null) center.Circle.enabled = false;
                if (center.Readiness != null) center.Readiness.enabled = false;
                center.Cross.color = HudStyle.WithAlpha(HudStyle.Normal, CenterAlpha);
                _center = center.Root;
                _center.localScale *= CenterScale;
            }
            else
            {
                _center = HudStyle.CreateRect("Crosshair", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
                HudStyle.CreateImage("Up", _center, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(2f, 10f), HudStyle.Normal);
                HudStyle.CreateImage("Down", _center, new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(2f, 10f), HudStyle.Normal);
                HudStyle.CreateImage("Left", _center, new Vector2(0.5f, 0.5f), new Vector2(-12f, 0f), new Vector2(10f, 2f), HudStyle.Normal);
                HudStyle.CreateImage("Right", _center, new Vector2(0.5f, 0.5f), new Vector2(12f, 0f), new Vector2(10f, 2f), HudStyle.Normal);
                HudStyle.CreateImage("Dot", _center, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 2f), HudStyle.Normal);
            }
            _center.anchoredPosition = Vector2.zero;

            _gun = GameHudParts.Crosshair(root);
            if (_gun != null)
            {
                _gun.Root.localScale *= ModConfig.GunMarkerScale.Value;
            }
            else
            {
                _ringTexture = HudStyle.CreateRingTexture(64, 3f);
                float size = 34f * ModConfig.GunMarkerScale.Value;
                _fallbackMarker = HudStyle.CreateRawImage("GunMarker", root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size),
                    _ringTexture, HudStyle.Normal);
            }

            Transform markerRoot = _gun != null ? (Transform)_gun.Root : _fallbackMarker.transform;
            _label = HudStyle.CreateText("ImpactLabel", markerRoot, new Vector2(0.5f, 0.5f), LabelOffset, new Vector2(260f, 24f),
                16f, TMPro.TextAlignmentOptions.Center);
            _label.raycastTarget = false;
            _label.gameObject.SetActive(false);
        }

        private enum Impact
        {
            OnCourse,
            Sky,
            Blocked,
            OutOfReach,
        }

        private static Impact Classify(HudContext context, TurretController turrets, Vector3 point, out float impactDistance)
        {
            Vector3 from = context.Vehicle.transform.position;
            impactDistance = Vector3.Distance(from, point);
            if (!turrets.ImpactHit || turrets.Range == TurretController.RangeState.OutOfReach
                || turrets.Range == TurretController.RangeState.BeyondMaxRange)
            {
                return Impact.OutOfReach;
            }
            if (!turrets.HasAimHit && !turrets.IsTracking) return Impact.Sky;
            float aim = Vector3.Distance(from, turrets.AimTargetPoint);
            if (impactDistance >= aim - Mathf.Max(BlockedMargin, aim * BlockedFraction)) return Impact.OnCourse;
            float angle = Vector3.Angle(point - from, turrets.AimTargetPoint - from);
            return angle <= BlockedAngleDegrees ? Impact.Blocked : Impact.OnCourse;
        }

        private void UpdateLabel(Impact impact, float distance)
        {
            bool show = impact != Impact.OnCourse;
            if (_label.gameObject.activeSelf != show) _label.gameObject.SetActive(show);
            if (!show) return;

            int key = (int)impact * 1000000 + Mathf.RoundToInt(distance);
            if (key == _labelKey) return;
            _labelKey = key;
            switch (impact)
            {
                case Impact.OutOfReach:
                    _label.text = "OUT OF REACH";
                    _label.color = HudStyle.Alert;
                    break;
                case Impact.Blocked:
                    _label.text = "BLOCKED " + UnitConverter.DistanceReading(distance);
                    _label.color = HudStyle.Warning;
                    break;
                default:
                    _label.text = UnitConverter.DistanceReading(distance);
                    _label.color = HudStyle.Normal;
                    break;
            }
        }

        public override void Refresh(HudContext context)
        {
            if (ProjectileCam.IsActive)
            {
                if (_center.gameObject.activeSelf) _center.gameObject.SetActive(false);
                GameObject camMarker = _gun != null ? _gun.Root.gameObject : _fallbackMarker.gameObject;
                if (camMarker.activeSelf) camMarker.SetActive(false);
                return;
            }

            TurretController turrets = context.Turrets;

            bool zoomed = context.View.IsZoomed;
            if (_center.gameObject.activeSelf == zoomed) _center.gameObject.SetActive(!zoomed);

            GameObject marker = _gun != null ? _gun.Root.gameObject : _fallbackMarker.gameObject;
            if (!turrets.TryGetGunMarker(OnTargetToleranceDegrees, out Vector3 markerPoint, out bool onTarget))
            {
                if (marker.activeSelf) marker.SetActive(false);
                return;
            }

            Vector3 screen = context.Camera.WorldToScreenPoint(markerPoint);
            bool visible = screen.z > 0f;
            if (marker.activeSelf != visible) marker.SetActive(visible);
            if (!visible) return;

            marker.transform.position = new Vector3(screen.x, screen.y, 0f);
            Impact impact = Classify(context, turrets, markerPoint, out float impactDistance);
            UpdateLabel(impact, impactDistance);
            bool outOfRange = impact == Impact.OutOfReach;
            bool blocked = impact == Impact.Blocked;

            if (_gun == null)
            {
                _fallbackMarker.color = outOfRange ? HudStyle.Alert : blocked ? HudStyle.Warning : onTarget ? HudStyle.Normal : HudStyle.Warning;
                return;
            }

            float reload = AmmoSync.Of(context.Vehicle, turrets.SelectedStation).ReloadProgress;
            bool reloading = reload > 0f;
            if (_gun.Readiness != null)
            {
                if (_gun.Readiness.enabled != reloading) _gun.Readiness.enabled = reloading;
                if (reloading) _gun.Readiness.fillAmount = reload;
            }
            if (_gun.Circle != null)
            {
                bool ready = onTarget && !reloading && !outOfRange;
                if (_gun.Circle.enabled != ready) _gun.Circle.enabled = ready;
            }
            _gun.Cross.color = outOfRange ? HudStyle.Alert
                : blocked ? HudStyle.Warning
                : reloading ? HudStyle.Alert + HudStyle.Normal * 0.5f
                : HudStyle.Normal;
        }

        public override void Dispose()
        {
            if (_ringTexture != null) Object.Destroy(_ringTexture);
        }
    }
}
