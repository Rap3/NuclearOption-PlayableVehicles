using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class WeaponListWidget : HudWidget
    {
        private const float Margin = 24f;
        private const float Spacing = 4f;
        private const float InactiveAlpha = 0.45f;
        private const float FallbackRowHeight = 26f;
        private const int SlotKeys = 3;

        private sealed class Row
        {
            public WeaponStation Station;
            public GameHudParts.WeaponPanel Panel;
            public CanvasGroup Group;
            public TextMeshProUGUI Text;
            public string Key = "";
            public string AmmoText;
            public int AmmoLoaded;
            public int AmmoTotal;
            public int AmmoCount;
            public bool AmmoReloading;
            public string LineText;
            public string LineAmmo;
            public bool LineSelected;
        }

        private readonly List<Row> _rows = new List<Row>();

        public override void Build(RectTransform root, HudContext context)
        {
            List<WeaponStation> stations = context.Vehicle.weaponStations;
            if (stations == null) return;

            float y = Margin;
            for (int i = stations.Count - 1; i >= 0; i--)
            {
                WeaponStation station = stations[i];
                if (station == null || station.WeaponInfo == null) continue;
                var row = new Row { Station = station, Panel = GameHudParts.Weapon(root) };
                if (stations.Count > 1 && i < SlotKeys) row.Key = "[" + (i + 1) + "]";
                RectTransform rect;
                float height;
                if (row.Panel != null)
                {
                    rect = row.Panel.Root;
                    height = (row.Panel.Size.y > 1f ? row.Panel.Size.y : rect.sizeDelta.y) * rect.localScale.y;
                    if (row.Panel.Icon != null)
                    {
                        Sprite icon = IconOf(station.WeaponInfo);
                        row.Panel.Icon.sprite = icon;
                        row.Panel.Icon.enabled = icon != null;
                    }
                    if (row.Panel.Name == null) row.Panel.Name = NameLine(row.Panel);
                    row.Panel.Name.text = station.Cargo ? "Cargo (" + station.WeaponInfo.weaponName + ")" : station.WeaponInfo.weaponName;
                    if (row.Key.Length > 0) KeyLabel(row.Panel, row.Key);
                }
                else
                {
                    row.Text = HudStyle.CreateText("Weapon" + i, root, Vector2.zero, Vector2.zero, new Vector2(420f, FallbackRowHeight),
                        20f, TextAlignmentOptions.Right);
                    rect = row.Text.rectTransform;
                    height = FallbackRowHeight;
                }
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.anchoredPosition = new Vector2(-Margin, y);
                row.Group = rect.gameObject.AddComponent<CanvasGroup>();
                row.Group.interactable = false;
                row.Group.blocksRaycasts = false;
                y += height + Spacing;
                _rows.Add(row);
            }
        }

        internal static Sprite IconOf(WeaponInfo info)
        {
            if (info.weaponIcon != null || !info.gun) return info.weaponIcon;
            if (!_gunIconSearched)
            {
                _gunIconSearched = true;
                _gunIcon = FindGunIcon();
            }
            return _gunIcon;
        }

        private static Sprite _gunIcon;
        private static bool _gunIconSearched;

        private static Sprite FindGunIcon()
        {
            List<WeaponMount> mounts = Encyclopedia.i != null ? Encyclopedia.i.weaponMounts : null;
            if (mounts == null) return null;
            WeaponInfo anyGun = null;
            foreach (WeaponMount mount in mounts)
            {
                WeaponInfo info = mount != null ? mount.info : null;
                if (info == null || !info.gun || info.weaponIcon == null) continue;
                if (info.weaponName != null && info.weaponName.Contains("57mm"))
                {
                    ModLog.Info("Gun icon for vehicle cannons: " + info.weaponName + " (" + mount.name + ")");
                    return info.weaponIcon;
                }
                if (anyGun == null) anyGun = info;
            }
            if (anyGun != null) ModLog.Info("Gun icon for vehicle cannons: no 57mm aircraft gun, using " + anyGun.weaponName);
            return anyGun != null ? anyGun.weaponIcon : null;
        }

        private static TextMeshProUGUI NameLine(GameHudParts.WeaponPanel panel)
        {
            TextMeshProUGUI name = GameHudParts.CopyText(panel.Ammo, panel.Root);
            RectTransform rect = name.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(-12f, panel.Ammo.rectTransform.rect.height);
            rect.anchoredPosition = new Vector2(6f, -4f);
            name.alignment = TextAlignmentOptions.TopLeft;
            name.enableWordWrapping = false;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.fontSize = panel.Ammo.fontSize * 0.75f;
            return name;
        }

        private static void KeyLabel(GameHudParts.WeaponPanel panel, string key)
        {
            TextMeshProUGUI label = GameHudParts.CopyText(panel.Ammo, panel.Root);
            if (label == null) return;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(60f, panel.Ammo.rectTransform.rect.height);
            rect.anchoredPosition = new Vector2(-6f, -4f);
            label.alignment = TextAlignmentOptions.TopRight;
            label.enableWordWrapping = false;
            label.fontSize = panel.Ammo.fontSize * 0.75f;
            label.text = key;
            label.color = HudStyle.Normal;
        }

        private static string AmmoReadout(Row row, in AmmoSync.View view, bool remote)
        {
            if (row.AmmoText == null || view.Loaded != row.AmmoLoaded || view.Total != row.AmmoTotal || view.Ammo != row.AmmoCount
                || view.Reloading != row.AmmoReloading)
            {
                row.AmmoLoaded = view.Loaded;
                row.AmmoTotal = view.Total;
                row.AmmoCount = view.Ammo;
                row.AmmoReloading = view.Reloading;
                row.AmmoText = remote ? AmmoSync.Readout(view) : row.Station.GetAmmoReadout();
            }
            return row.AmmoText;
        }

        public override void Refresh(HudContext context)
        {
            WeaponStation selected = context.Turrets.SelectedStation;
            bool remote = VehicleSession.IsClientDriver;
            foreach (Row row in _rows)
            {
                WeaponStation station = row.Station;
                float alpha = station == selected ? 1f : InactiveAlpha;
                if (!Mathf.Approximately(row.Group.alpha, alpha)) row.Group.alpha = alpha;

                AmmoSync.View view = AmmoSync.Of(context.Vehicle, station);
                Color baseColor = view.Ammo > 0 ? HudStyle.Normal : HudStyle.Alert;
                string ammo = AmmoReadout(row, view, remote);
                if (row.Panel == null)
                {
                    bool isSelected = station == selected;
                    if (row.LineText == null || isSelected != row.LineSelected || !ReferenceEquals(ammo, row.LineAmmo))
                    {
                        row.LineSelected = isSelected;
                        row.LineAmmo = ammo;
                        row.LineText = (isSelected ? "> " : "") + (row.Key.Length > 0 ? row.Key + " " : "") + station.WeaponInfo.weaponName + "  " + ammo;
                        row.Text.text = row.LineText;
                    }
                    row.Text.color = baseColor;
                    continue;
                }

                GameHudParts.WeaponPanel panel = row.Panel;
                if (panel.Ammo.text != ammo) panel.Ammo.text = ammo;
                panel.Name.color = baseColor;
                panel.Ammo.color = baseColor;
                if (panel.Icon != null && panel.Icon.enabled) panel.Icon.color = view.Reloading ? HudStyle.Normal * 0.5f + HudStyle.Alert : baseColor;

                if (panel.ReloadImage == null || panel.Reload == null || panel.Panel == null) continue;
                bool reloading = view.Reloading;
                if (panel.ReloadImage.enabled != reloading) panel.ReloadImage.enabled = reloading;
                if (reloading)
                {
                    float width = (panel.Panel.sizeDelta.x - 10f) * view.ReloadProgress;
                    panel.Reload.sizeDelta = new Vector2(width, panel.Reload.sizeDelta.y);
                }
            }
        }
    }
}
