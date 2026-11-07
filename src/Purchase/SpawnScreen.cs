using NuclearOption.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal static class SpawnScreen
    {
        private const int SortingOrder = 1000;
        private static readonly Vector2 ButtonSize = new Vector2(200f, 44f);
        private const float ButtonsTop = 30f;
        private const float ButtonsGap = 150f;

        private const string SelectDepotText = "Select Vehicle Depot or Airbase...";
        private const string SelectVehicleText = "SELECT VEHICLE";

        private static readonly Color Selected = new Color32(0x40, 0x80, 0x40, 0xFF);
        private static readonly Color Unselected = new Color32(0x2E, 0x32, 0x38, 0xF0);

        public static bool GroundMode { get; private set; }

        private static GameObject _canvas;
        private static Button _air;
        private static Button _ground;
        private static PurchasePoint _point;
        private const float ApplyInterval = 0.25f;
        private static float _nextApply;
        private static bool _wasActive;
        private static PurchasePoint _appliedPoint;
        private static string _vanillaPrompt;
        private static string _vanillaButtonText;

        public static bool Active
        {
            get
            {
                if (!DynamicMap.mapMaximized || VehicleSession.IsActive || VanillaWindow.AnyOpen) return false;
                if (!GameManager.GetLocalPlayer(out Player player) || player.HQ == null || player.AircraftSpawnPending) return false;
                if (player.Aircraft != null && !player.Aircraft.disabled) return false;
                return !CursorManager.GetFlag(CursorFlags.SelectionMenu);
            }
        }

        public static void Tick()
        {
            bool active = VehicleShop.Enabled && ModConfig.Enabled.Value && Active;
            if (active && _canvas == null && !Build()) return;
            if (_canvas != null && _canvas.activeSelf != active) _canvas.SetActive(active);
            bool justShown = active && !_wasActive;
            _wasActive = active;
            if (!active) return;

            if (_point != null && (!GameManager.GetLocalHQ(out FactionHQ hq) || !_point.IsOwn(hq))) _point = null;
            if (GroundMode)
            {
                AdoptMapSelection();
                if (justShown || _point != _appliedPoint || Time.unscaledTime >= _nextApply) ApplyGround();
            }
        }

        public static void Reset()
        {
            GroundMode = false;
            _canvas = null;
            _air = null;
            _ground = null;
            _point = null;
            _appliedPoint = null;
            _wasActive = false;
            _nextApply = 0f;
            _vanillaPrompt = null;
            _vanillaButtonText = null;
        }

        public static void SelectDepot(VehicleDepot depot)
        {
            if (!GroundMode || !Active || depot == null) return;
            _point = PurchasePoint.Of(depot);
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map != null)
            {
                map.DeselectAllIcons();
                map.SelectIcon(depot);
            }
            ApplyGround();
        }

        public static bool SelectAirbase(Airbase airbase)
        {
            if (!GroundMode || !Active || airbase == null || !GameManager.GetLocalHQ(out FactionHQ hq)) return false;
            PurchasePoint point = PurchasePoint.Of(airbase);
            if (point == null || !point.IsOwn(hq)) return false;
            _point = point;
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map != null)
            {
                map.DeselectAllIcons();
                map.SelectIcon(airbase);
            }
            ApplyGround();
            return true;
        }

        private static void AdoptMapSelection()
        {
            DynamicMap map = SceneSingleton<DynamicMap>.i;
            if (map == null || map.selectedIcons == null || !GameManager.GetLocalHQ(out FactionHQ hq)) return;
            for (int i = map.selectedIcons.Count - 1; i >= 0; i--)
            {
                PurchasePoint point = map.selectedIcons[i] is UnitMapIcon icon && icon.unit is VehicleDepot depot ? PurchasePoint.Of(depot)
                    : map.selectedIcons[i] is AirbaseMapIcon airbaseIcon ? PurchasePoint.Of(airbaseIcon.airbase)
                    : null;
                if (point != null && point.IsOwn(hq))
                {
                    _point = point;
                    return;
                }
            }
        }

        public static bool TryOpenSelectedDepot()
        {
            if (!GroundMode) return false;
            if (_point == null) return true;
            if (VehicleSelectionMenu.Available) VehicleSelectionMenu.Open(_point);
            else PurchasePanel.Open(_point);
            return true;
        }

        private static void SetMode(bool ground)
        {
            if (GroundMode == ground) return;
            GroundMode = ground;
            _point = null;
            { DynamicMap single = SceneSingleton<DynamicMap>.i; if (single != null) single.DeselectAllIcons(); }

            if (!ground) RestoreAir();
            else ApplyGround();
            UpdateButtons();
        }

        private static void ApplyGround()
        {
            _nextApply = Time.unscaledTime + ApplyInterval;
            _appliedPoint = _point;

            GameplayUI ui = SceneSingleton<GameplayUI>.i;
            if (ui == null) return;
            TMP_Text prompt = GameAccess.GameplayUIAirbaseName != null ? GameAccess.GameplayUIAirbaseName(ui) : null;
            Button button = GameAccess.GameplayUISelectAircraftButton != null ? GameAccess.GameplayUISelectAircraftButton(ui) : null;

            if (prompt != null)
            {
                if (_vanillaPrompt == null) _vanillaPrompt = prompt.text;
                string text = _point != null ? _point.Name : SelectDepotText;
                if (prompt.text != text) prompt.text = text;
            }
            if (button != null)
            {
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                {
                    if (_vanillaButtonText == null) _vanillaButtonText = label.text;
                    if (label.text != SelectVehicleText) label.text = SelectVehicleText;
                }
                bool show = _point != null;
                if (button.gameObject.activeSelf != show) button.gameObject.SetActive(show);
            }
        }

        private static void RestoreAir()
        {
            { DynamicMap single = SceneSingleton<DynamicMap>.i; if (single != null) single.ShowAirbases(); }
            GameplayUI ui = SceneSingleton<GameplayUI>.i;
            if (ui == null) return;

            TMP_Text prompt = GameAccess.GameplayUIAirbaseName != null ? GameAccess.GameplayUIAirbaseName(ui) : null;
            if (prompt != null && _vanillaPrompt != null) prompt.text = _vanillaPrompt;
            Button button = GameAccess.GameplayUISelectAircraftButton != null ? GameAccess.GameplayUISelectAircraftButton(ui) : null;
            TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
            if (label != null && _vanillaButtonText != null) label.text = _vanillaButtonText;
            ui.ShowSelectAirbase();
        }

        private static void UpdateButtons()
        {
            Paint(_air, !GroundMode);
            Paint(_ground, GroundMode);
        }

        private static void Paint(Button button, bool selected)
        {
            if (button == null) return;
            ColorBlock colors = button.colors;
            colors.normalColor = selected ? Selected : Unselected;
            colors.selectedColor = colors.normalColor;
            button.colors = colors;
        }

        private static bool Build()
        {
            ContributeToFaction contribute = VanillaWindow.VanillaContribute();
            Transform source = contribute != null ? contribute.transform.Find("OptionsContainer/VehiclesButton") : null;
            if (source == null) return false;

            _canvas = VanillaWindow.CreateOverlayCanvas("SpawnModeCanvas", SortingOrder);
            if (_canvas == null) return false;

            _air = CreateButton(source, "AIR", -(ButtonSize.x + ButtonsGap) * 0.5f, () => SetMode(false));
            _ground = CreateButton(source, "GROUND", (ButtonSize.x + ButtonsGap) * 0.5f, () => SetMode(true));
            UpdateButtons();
            return true;
        }

        private static Button CreateButton(Transform source, string text, float x, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = Object.Instantiate(source.gameObject, _canvas.transform, false);
            go.SetActive(true);
            go.name = "PlayableVehicles_SpawnMode_" + text;
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -ButtonsTop);
            rect.sizeDelta = ButtonSize;

            TMP_Text label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
            Button button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(onClick);

            button.transition = Selectable.Transition.ColorTint;
            Behaviour animator = go.GetComponent("Animator") as Behaviour;
            if (animator != null) animator.enabled = false;
            CanvasGroup group = go.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = Color.white;
                button.targetGraphic.material = null;
            }
            return button;
        }
    }
}
