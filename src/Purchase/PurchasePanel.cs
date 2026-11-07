using System.Collections.Generic;
using NuclearOption.Networking;
using TMPro;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class PurchasePanel
    {
        private static readonly Vector2 Size = new Vector2(560f, 660f);
        private const float ListTop = 56f;
        private const float ListBottom = 175f;

        private sealed class Entry
        {
            public VehicleDefinition Definition;
            public float Price;
            public VanillaWindow.Row Row;
        }

        private static VanillaWindow _window;
        private static TMP_Text _funds;
        private static TMP_Text _total;
        private static TMP_Text _remaining;
        private static readonly List<Entry> _entries = new List<Entry>();
        private static Entry _selected;
        private static PurchasePoint _point;
        private static bool _fromSpawnScreen;

        public static bool IsOpen => _window != null && _window.IsOpen;

        public static void Open(PurchasePoint point)
        {
            if ((_window == null || !_window.Alive) && !Build())
            {
                ModLog.Notify("Vehicle purchase is not available: game menu not found");
                return;
            }
            _point = point;
            _selected = null;
            _fromSpawnScreen = SpawnScreen.Active;
            foreach (Entry entry in _entries) entry.Row.State = -1;
            if (DynamicMap.mapMaximized) { DynamicMap single = SceneSingleton<DynamicMap>.i; if (single != null) single.Minimize(); }
            _window.Show();
            Tick();
        }

        public static void Close()
        {
            bool wasOpen = IsOpen;
            _window?.Hide();
            _point = null;
            _selected = null;
            if (wasOpen && _fromSpawnScreen && !VehicleSession.IsActive) { DynamicMap single = SceneSingleton<DynamicMap>.i; if (single != null) single.Maximize(); }
            _fromSpawnScreen = false;
        }

        public static void Reset()
        {
            _window = null;
            _funds = null;
            _total = null;
            _remaining = null;
            _entries.Clear();
            _selected = null;
            _point = null;
            _fromSpawnScreen = false;
        }

        public static void Tick()
        {
            if (!IsOpen) return;

            if (!GameManager.GetLocalPlayer(out Player player) || !VehicleShop.IsOwn(_point, player.HQ))
            {
                Close();
                return;
            }

            float funds = player.Allocation;
            bool allowed = !player.HQ.preventDonation || (_point != null && _point.IsAirbase);

            foreach (Entry entry in _entries)
            {
                float cooldown = PurchaseClient.CooldownRemaining(entry.Definition);
                bool canSpawn = allowed && cooldown <= 0f;
                VehicleShop.Result restriction = VehicleShop.Restriction(player, entry.Definition);
                bool restricted = restriction != VehicleShop.Result.Purchased;
                bool available = canSpawn && !restricted;
                bool affordable = funds >= entry.Price;
                bool interactable = available && affordable && !PurchaseClient.Pending;
                VanillaWindow.Row row = entry.Row;

                int state = (interactable ? 1 : 0) | (affordable ? 2 : 0) | (canSpawn ? 4 : 0) | ((int)restriction << 3);
                if (state != row.State)
                {
                    row.State = state;
                    string reason = restricted ? VehicleShop.RestrictionText(restriction, entry.Definition) : "Unavailable";
                    VanillaWindow.SetRowState(row, interactable, available ? null : " ( " + reason + " )");
                    row.Hover?.SetText(available
                        ? (affordable ? "Spawn : " : "Insufficient funds for : ") + "\n1x " + entry.Definition.unitName
                        : reason);
                }
                if (!canSpawn && !restricted && row.MaskText != null)
                {
                    row.MaskText.text = allowed ? string.Format(" ( Unavailable for {0:F0}s )", Mathf.Ceil(cooldown)) : " ( Unavailable )";
                }
            }

            float price = _selected != null ? _selected.Price : 0f;
            _funds.text = UnitConverter.ValueReading(funds);
            _total.text = "-" + UnitConverter.ValueReading(price);
            _remaining.text = UnitConverter.ValueReading(funds - price);
            _window.Confirm.interactable = _selected != null && (_selected.Row.State & 1) != 0;
        }

        private static void Select(Entry entry)
        {
            _selected = entry;
            Tick();
        }

        private static void Confirm()
        {
            if (_selected == null || _point == null) return;
            PurchaseClient.Request(_point, _selected.Definition);
            Close();
        }

        private static bool Build()
        {
            _window = VanillaWindow.Create("PurchasePanel", "GROUND FORCES", Size, new Vector2(0.5f, 0.5f), Vector2.zero,
                ListTop, ListBottom, true);
            if (_window == null) return false;

            _funds = _window.AddLine("Current funds:", 145f);
            _total = _window.AddLine("Total value:", 117f);
            _remaining = _window.AddLine("Remaining funds:", 89f);
            _window.Confirm.onClick.AddListener(Confirm);
            _window.Cancel.onClick.AddListener(Close);

            _entries.Clear();
            List<VehicleDefinition> catalog = VehicleShop.Catalog;
            var definitions = new List<VehicleDefinition>();
            for (int i = 0; catalog != null && i < catalog.Count; i++)
            {
                if (VehicleShop.TryGet(i, out VehicleDefinition definition)) definitions.Add(definition);
            }
            definitions.Sort((a, b) => VehicleShop.Price(a).CompareTo(VehicleShop.Price(b)));

            foreach (VehicleDefinition definition in definitions)
            {
                var entry = new Entry { Definition = definition, Price = VehicleShop.Price(definition) };
                Sprite icon = definition.friendlyIcon != null ? definition.friendlyIcon : definition.mapIcon;
                entry.Row = _window.AddRow(definition.unitName + " (" + UnitConverter.ValueReading(entry.Price) + ")", icon, () => Select(entry));
                if (entry.Row != null) _entries.Add(entry);
            }
            if (_entries.Count == 0)
            {
                _window.Destroy();
                _window = null;
                return false;
            }
            return true;
        }
    }
}
