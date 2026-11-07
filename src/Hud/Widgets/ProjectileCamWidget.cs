using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class ProjectileCamWidget : HudWidget
    {
        private const float Bottom = 8f;
        private const float Width = 360f;
        private const float Height = 26f;

        private RectTransform _panel;
        private TextMeshProUGUI _title;
        private ProjectileCam.Kind _kind = ProjectileCam.Kind.None;

        public override bool VisibleInProjectileCam => true;

        public override void Build(RectTransform root, HudContext context)
        {
            _panel = HudStyle.CreateRect("ProjectileCam", root, new Vector2(0.5f, 0f), new Vector2(0f, Bottom), new Vector2(Width, Height));
            HudStyle.CreateImage("Backing", _panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height), HudStyle.Panel);
            _title = HudStyle.CreateText("Title", _panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(Width, Height),
                15f, TextAlignmentOptions.Center);
            _panel.gameObject.SetActive(false);
        }

        public override void Refresh(HudContext context)
        {
            if (!ProjectileCam.IsActive)
            {
                if (_panel.gameObject.activeSelf) _panel.gameObject.SetActive(false);
                _kind = ProjectileCam.Kind.None;
                return;
            }
            if (!_panel.gameObject.activeSelf) _panel.gameObject.SetActive(true);

            if (ProjectileCam.Active != _kind)
            {
                _kind = ProjectileCam.Active;
                _title.text = (_kind == ProjectileCam.Kind.Shell ? "SHELL CAM" : "MISSILE CAM")
                    + "  <size=70%>[" + ModConfig.ProjectileCamKey.Value + " to exit]</size>";
            }
        }
    }
}
