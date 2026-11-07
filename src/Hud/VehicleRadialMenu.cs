using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal static class VehicleRadialMenu
    {
        private enum Wheel
        {
            Closed,
            Main,
            Weapons,
        }

        private sealed class Item
        {
            public RadialMenuAction Action;
            public Action Run;
            public int Station = -1;
            public Func<string> Label;
        }

        private const string RadialMenuButton = "Radial Menu";
        private const string WeaponWheelButton = "Weapon Wheel";
        private const string HorizontalAxis = "Radial Menu Horizontal";
        private const string VerticalAxis = "Radial Menu Vertical";
        private const float ItemRadius = 90f;

        private static Wheel _state;
        private static GroundVehicle _builtFor;
        private static RadialMenuMain _menu;
        private static GameObject _mainContainer;
        private static GameObject _weaponsContainer;
        private static readonly List<Item> _main = new List<Item>();
        private static readonly List<Item> _weapons = new List<Item>();
        private static readonly List<UnityEngine.Object> _created = new List<UnityEngine.Object>();
        private static float _degreesMain;
        private static float _degreesWeapons;
        private static Item _selected;
        private static int _currentIndex = -1;
        private static float _lastSelection;
        private static float _lastOpen;
        private static Vector3 _mouseDelta;
        private static readonly List<GameObject> _enabledParents = new List<GameObject>();

        private static RadialMenuAction _driveAction;

        public static bool IsDriveAction(RadialMenuAction action)
        {
            return (object)_driveAction != null && ReferenceEquals(action, _driveAction);
        }

        public static void AddDriveAction(RadialMenuMain menu)
        {
            if (!GameAccess.RadialMenuAvailable) return;
            RadialMenuAction[] actions = GameAccess.RadialMenuMainActions(menu);
            if (actions == null) return;
            if ((object)_driveAction != null && _driveAction != null && Array.IndexOf(actions, _driveAction) >= 0) return;

            if (_driveAction == null)
            {
                RadialMenuAction template = null;
                foreach (RadialMenuAction action in actions)
                {
                    if (action != null && GameAccess.RadialMenuActionKind(action) == RadialMenuAction.ActionType.Gear) template = action;
                }
                if (template == null) return;
                _driveAction = UnityEngine.Object.Instantiate(template);
                _driveAction.DisplayName = "Drive vehicle";
                Sprite icon = VehicleIcon();
                if (icon != null) GameAccess.RadialMenuActionIcon(_driveAction) = icon;
            }

            var extended = new RadialMenuAction[actions.Length + 1];
            Array.Copy(actions, extended, actions.Length);
            extended[actions.Length] = _driveAction;
            GameAccess.RadialMenuMainActions(menu) = extended;
        }

        private static Sprite VehicleIcon()
        {
            List<VehicleDefinition> vehicles = Encyclopedia.i != null ? Encyclopedia.i.vehicles : null;
            if (vehicles == null) return null;
            Sprite any = null;
            foreach (VehicleDefinition definition in vehicles)
            {
                if (definition == null || definition.mapIcon == null) continue;
                if (definition.unitName != null && definition.unitName.Contains("MBT")) return definition.mapIcon;
                if (any == null) any = definition.mapIcon;
            }
            return any;
        }

        public static bool IsMenuBranch(Transform node)
        {
            RadialMenuMain menu = SceneSingleton<RadialMenuMain>.i;
            if (menu == null || node == null || GameAccess.RadialMenuObject == null) return false;
            GameObject root = GameAccess.RadialMenuObject(menu);
            return root != null && root.transform.IsChildOf(node);
        }

        public static bool IsOpen => _state != Wheel.Closed;

        public static void Tick()
        {
            if (!VehicleSession.IsActive || VehicleSession.Vehicle == null || VehicleSession.Driver == null)
            {
                Release();
                return;
            }
            RadialMenuMain menu = SceneSingleton<RadialMenuMain>.i;
            if (menu == null || !GameAccess.RadialMenuAvailable)
            {
                return;
            }
            if (!ReferenceEquals(menu, _menu) || !ReferenceEquals(VehicleSession.Vehicle, _builtFor)) Build(menu, VehicleSession.Vehicle);

            Rewired.Player input = GameManager.playerInput;
            if (input == null) return;

            if (_state == Wheel.Closed)
            {
                if (InputGate.GameplayInputAllowed)
                {
                    if (input.GetButtonTimedPressDown(RadialMenuButton, PlayerSettings.pressDelay) && _main.Count > 0) Open(Wheel.Main);
                    else if (input.GetButtonTimedPressDown(WeaponWheelButton, PlayerSettings.pressDelay) && _weapons.Count > 1) Open(Wheel.Weapons);
                }
                if (_state == Wheel.Closed)
                {
                    FadeOut();
                    return;
                }
            }

            if (!InputGate.GameplayInputAllowed)
            {
                Close();
                return;
            }

            CanvasGroup group = GameAccess.RadialMenuCanvasGroup(_menu);
            if (Time.realtimeSinceStartup - _lastOpen > 0.05f && group.alpha < 1f) group.alpha += 10f * Time.deltaTime;
            GameObject root = GameAccess.RadialMenuObject(_menu);
            EnableParents(root.transform);
            if (!root.activeSelf) root.SetActive(true);

            bool weapons = _state == Wheel.Weapons;
            SetActive(_mainContainer, !weapons);
            SetActive(_weaponsContainer, weapons);
            SetActive(GameAccess.RadialMenuMainContainer(_menu), false);
            SetActive(GameAccess.RadialMenuWeaponsContainer(_menu), false);

            List<Item> items = weapons ? _weapons : _main;
            float degrees = weapons ? _degreesWeapons : _degreesMain;
            string button = weapons ? WeaponWheelButton : RadialMenuButton;
            if (PlayerSettings.radialControl < 2) ControlAxis(input, items, degrees, button);
            else ControlButtons(input, items, degrees, button);
        }

        private static void Open(Wheel wheel)
        {
            _state = wheel;
            _selected = null;
            _currentIndex = -1;
            _lastOpen = Time.realtimeSinceStartup;
            _mouseDelta = Vector3.zero;
            if (wheel == Wheel.Weapons) RefreshWeapons();
            else RefreshLabels();
        }

        private static void Release(Item item)
        {
            if (item == null) return;
            item.Action.UnHover();
            try
            {
                item.Run?.Invoke();
            }
            catch (Exception e)
            {
                ModLog.Error("Radial menu action failed: " + e);
            }
        }

        private static void Close()
        {
            _state = Wheel.Closed;
            _selected = null;
            _currentIndex = -1;
            if (_menu != null && GameAccess.RadialMenuSelectedText(_menu) != null) GameAccess.RadialMenuSelectedText(_menu).text = "";
        }

        private static void FadeOut()
        {
            if (_menu == null) return;
            CanvasGroup group = GameAccess.RadialMenuCanvasGroup(_menu);
            if (group.alpha > 0f)
            {
                group.alpha -= 5f * Time.deltaTime;
                return;
            }
            GameObject root = GameAccess.RadialMenuObject(_menu);
            if (root.activeSelf) root.SetActive(false);
            RestoreParents();
        }

        private static void EnableParents(Transform menuObject)
        {
            for (Transform t = menuObject.parent; t != null && t.GetComponent<Canvas>() == null; t = t.parent)
            {
                if (t.gameObject.activeSelf) continue;
                t.gameObject.SetActive(true);
                _enabledParents.Add(t.gameObject);
            }
        }

        private static void RestoreParents()
        {
            if (_enabledParents.Count == 0) return;
            foreach (GameObject parent in _enabledParents)
            {
                if (parent != null) parent.SetActive(false);
            }
            _enabledParents.Clear();
        }

        private static void ControlAxis(Rewired.Player input, List<Item> items, float degrees, string button)
        {
            if (!input.GetButton(button))
            {
                Item chosen = _selected;
                Close();
                Release(chosen);
                return;
            }
            _mouseDelta += (input.GetAxis("Pan View") * Vector3.right - input.GetAxis("Tilt View") * Vector3.up) * 0.5f;
            _mouseDelta = Vector3.Lerp(_mouseDelta, Vector3.zero, 0.05f);
            float x = PlayerSettings.radialControl == 0 ? input.GetAxis(HorizontalAxis) : _mouseDelta.x;
            float y = PlayerSettings.radialControl == 0 ? -input.GetAxis(VerticalAxis) : _mouseDelta.y;
            Vector2 direction = new Vector2(x, y);
            if (direction.sqrMagnitude > 0.1f)
            {
                _lastSelection = Time.realtimeSinceStartup;
                float angle = -Vector2.SignedAngle(Vector2.up, direction.normalized);
                if (angle < 0f) angle += 360f;
                angle = Mathf.Repeat(angle + degrees * 0.5f, 360f);
                int index = Mathf.Clamp(Mathf.FloorToInt(angle / degrees), 0, items.Count - 1);
                Select(items[index]);
            }
            else if (Time.realtimeSinceStartup - _lastSelection > 0.05f && _selected != null)
            {
                Unselect();
            }
        }

        private static void ControlButtons(Rewired.Player input, List<Item> items, float degrees, string button)
        {
            if (!input.GetButton(button))
            {
                Item chosen = _selected;
                Close();
                Release(chosen);
                return;
            }
            bool right = input.GetButtonDown(HorizontalAxis);
            bool left = input.GetNegativeButtonDown(HorizontalAxis);
            bool up = input.GetButtonDown(VerticalAxis);
            bool down = input.GetNegativeButtonDown(VerticalAxis);
            if (right || left || up || down)
            {
                _lastSelection = Time.realtimeSinceStartup;
                if (_currentIndex < 0)
                {
                    if (up) _currentIndex = 0;
                    else if (down) _currentIndex = Mathf.RoundToInt(0.5f * items.Count);
                    if (right) _currentIndex = Mathf.RoundToInt(0.25f * items.Count);
                    else if (left) _currentIndex = Mathf.RoundToInt(0.75f * items.Count);
                }
                float angle = _currentIndex * degrees;
                if (angle > 225f && angle < 315f)
                {
                    if (up) _currentIndex++;
                    else if (down) _currentIndex--;
                }
                else if (angle > 135f)
                {
                    if (right) _currentIndex--;
                    else if (left) _currentIndex++;
                }
                else if (angle > 45f)
                {
                    if (up) _currentIndex--;
                    else if (down) _currentIndex++;
                }
                else if (right) _currentIndex++;
                else if (left) _currentIndex--;
                if (_currentIndex < 0) _currentIndex = items.Count - 1;
                else if (_currentIndex > items.Count - 1) _currentIndex = 0;
                Select(items[_currentIndex]);
            }
            else if (Time.realtimeSinceStartup - _lastSelection > 0.5f && _selected != null)
            {
                _currentIndex = -1;
                Unselect();
            }
        }

        private static void Select(Item item)
        {
            if (item == _selected) return;
            _selected?.Action.UnHover();
            _selected = item;
            item.Action.Hover();
            GameAccess.RadialMenuSelectedText(_menu).text = item.Action.DisplayName;
        }

        private static void Unselect()
        {
            _selected.Action.UnHover();
            _selected = null;
            GameAccess.RadialMenuSelectedText(_menu).text = "";
        }

        private static void Build(RadialMenuMain menu, GroundVehicle vehicle)
        {
            Release();
            _menu = menu;
            _builtFor = vehicle;
            menu.CloseMenu();

            _mainContainer = Container(GameAccess.RadialMenuMainContainer(menu), "PV Vehicle Actions");
            _weaponsContainer = Container(GameAccess.RadialMenuWeaponsContainer(menu), "PV Vehicle Weapons");

            BuildMain(menu, vehicle);
            BuildWeapons(menu, vehicle);
            _degreesMain = Layout(_main, _mainContainer);
            _degreesWeapons = Layout(_weapons, _weaponsContainer);
        }

        private static GameObject Container(GameObject vanilla, string name)
        {
            var container = new GameObject(name, typeof(RectTransform));
            _created.Add(container);
            RectTransform source = (RectTransform)vanilla.transform;
            RectTransform rect = (RectTransform)container.transform;
            rect.SetParent(source.parent, false);
            rect.anchorMin = source.anchorMin;
            rect.anchorMax = source.anchorMax;
            rect.pivot = source.pivot;
            rect.sizeDelta = source.sizeDelta;
            rect.anchoredPosition = source.anchoredPosition;
            rect.localScale = source.localScale;
            rect.localRotation = source.localRotation;
            rect.SetSiblingIndex(source.GetSiblingIndex() + 1);
            container.SetActive(false);
            return container;
        }

        private static void BuildMain(RadialMenuMain menu, GroundVehicle vehicle)
        {
            RadialMenuAction[] templates = GameAccess.RadialMenuMainActions(menu);
            VehicleDriver driver = VehicleSession.Driver;

            AddMain(templates, RadialMenuAction.ActionType.Eject, () => "Exit vehicle", VehicleSession.Toggle);

            bool tractor = Towing.IsTractor(vehicle);
            bool flatbed = Cargo.IsFlatbed(vehicle);
            if (tractor || flatbed || VehicleDeploy.CanDeploy(vehicle))
            {
                Func<string> label = tractor ? (() => "Hitch / unhitch trailer")
                    : flatbed ? (() => "Load / unload container")
                    : (Func<string>)(() => VehicleDeploy.IsDeployed(vehicle) ? "Stow" : "Deploy");
                AddMain(templates, RadialMenuAction.ActionType.Gear, label, () => driver.DeployAction());
            }

            if (HasFireMissions(vehicle)) AddMain(templates, RadialMenuAction.ActionType.Radar, () => "Fire control computer", FireControlPanel.Toggle);
            AddMain(templates, RadialMenuAction.ActionType.Nightvis, () => "Night vision", NightVision.Toggle);
        }

        private static bool HasFireMissions(GroundVehicle vehicle)
        {
            if (vehicle.weaponStations == null) return false;
            foreach (WeaponStation station in vehicle.weaponStations)
            {
                if (FireMissions.KindOf(station) != FireMissionKind.None) return true;
            }
            return false;
        }

        private static void AddMain(RadialMenuAction[] templates, RadialMenuAction.ActionType kind, Func<string> label, Action run)
        {
            if (templates == null) return;
            foreach (RadialMenuAction template in templates)
            {
                if (template == null || GameAccess.RadialMenuActionKind(template) != kind) continue;
                RadialMenuAction action = UnityEngine.Object.Instantiate(template);
                _created.Add(action);
                action.DisplayName = label();
                _main.Add(new Item { Action = action, Run = run, Label = label });
                return;
            }
        }

        private static void BuildWeapons(RadialMenuMain menu, GroundVehicle vehicle)
        {
            RadialMenuAction prefab = GameAccess.RadialMenuWeaponAction(menu);
            if (prefab == null || vehicle.weaponStations == null) return;
            for (int i = 0; i < vehicle.weaponStations.Count; i++)
            {
                WeaponStation station = vehicle.weaponStations[i];
                if (station == null || station.WeaponInfo == null) continue;
                RadialMenuAction action = UnityEngine.Object.Instantiate(prefab);
                _created.Add(action);
                action.SetWeapon(station.WeaponInfo, i);
                GameAccess.RadialMenuActionIcon(action) = WeaponListWidget.IconOf(station.WeaponInfo);
                int index = i;
                _weapons.Add(new Item
                {
                    Action = action,
                    Station = i,
                    Run = () => VehicleSession.Driver?.Turrets.SetWeaponSlot(index),
                });
            }
        }

        private static float Layout(List<Item> items, GameObject container)
        {
            if (items.Count == 0) return 360f;
            float degrees = 360f / items.Count;
            GameObject sectorPrefab = GameAccess.RadialMenuSector(_menu);
            GameObject actionPrefab = GameAccess.RadialMenuActionPrefab(_menu);
            for (int j = 0; j < items.Count; j++)
            {
                GameObject sector = UnityEngine.Object.Instantiate(sectorPrefab, container.transform);
                Image background = sector.GetComponent<Image>();
                background.fillAmount = degrees / 360f;
                sector.transform.localEulerAngles = new Vector3(0f, 0f, -(j - 0.5f) * degrees);
                GameObject actionObject = UnityEngine.Object.Instantiate(actionPrefab, container.transform);
                Image icon = actionObject.GetComponent<Image>();
                Text text = actionObject.transform.Find("Text").GetComponent<Text>();
                items[j].Action.Setup(background, icon, text);
                float radians = j * degrees * Mathf.Deg2Rad;
                actionObject.transform.localPosition = ItemRadius * new Vector3(Mathf.Sin(radians), Mathf.Cos(radians), 0f);
            }
            return degrees;
        }

        private static void RefreshWeapons()
        {
            GroundVehicle vehicle = _builtFor;
            foreach (Item item in _weapons)
            {
                WeaponStation station = vehicle.weaponStations[item.Station];
                WeaponInfo info = station.WeaponInfo;
                if (info.sling || info.energy) continue;
                AmmoSync.View view = AmmoSync.Of(vehicle, station);
                float level = station.FullAmmo > 0 ? (float)view.Ammo / station.FullAmmo : 0f;
                item.Action.RefreshWeapon(AmmoSync.ReadoutOf(vehicle, station), level);
            }
        }

        private static void RefreshLabels()
        {
            foreach (Item item in _main)
            {
                if (item.Label != null) item.Action.DisplayName = item.Label();
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active) target.SetActive(active);
        }

        public static void Release()
        {
            if (_menu == null && _created.Count == 0) return;
            if (_menu != null && _state != Wheel.Closed)
            {
                Close();
                if (GameAccess.RadialMenuCanvasGroup != null) GameAccess.RadialMenuCanvasGroup(_menu).alpha = 0f;
                if (GameAccess.RadialMenuObject != null) SetActive(GameAccess.RadialMenuObject(_menu), false);
            }
            RestoreParents();
            _state = Wheel.Closed;
            _selected = null;
            foreach (UnityEngine.Object created in _created)
            {
                if (created != null) UnityEngine.Object.Destroy(created);
            }
            _created.Clear();
            _main.Clear();
            _weapons.Clear();
            _mainContainer = null;
            _weaponsContainer = null;
            _builtFor = null;
            _menu = null;
        }

        public static void Reset()
        {
            _created.Clear();
            _enabledParents.Clear();
            _main.Clear();
            _weapons.Clear();
            _state = Wheel.Closed;
            _selected = null;
            _mainContainer = null;
            _weaponsContainer = null;
            _builtFor = null;
            _menu = null;
        }
    }
}
