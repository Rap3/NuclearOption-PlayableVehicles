using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class HudOverrides
    {
        private sealed class Override
        {
            public bool Original;
            public bool ModValue;
            public bool Restore;
        }

        private static readonly Dictionary<GameObject, Override> _overridden = new Dictionary<GameObject, Override>();

        public static void Apply(GroundVehicle vehicle)
        {
            ShowMinimapOnly();
            CleanCombatHud();
            ClearCombatMarkers();
        }

        public static void Tick()
        {
            ShowMinimapOnly();
            CleanCombatHud();
        }

        public static void Restore()
        {
            foreach (KeyValuePair<GameObject, Override> pair in _overridden)
            {
                GameObject go = pair.Key;
                Override state = pair.Value;
                if (go == null || !state.Restore) continue;
                if (go.activeSelf != state.ModValue) continue;
                if (go.activeSelf != state.Original) go.SetActive(state.Original);
            }
            _overridden.Clear();

            RestoreCombatMarkers();
        }

        public static void Reset()
        {
            _overridden.Clear();
            _chainAnchor = null;
            _chainRoot = null;
            _chain.Clear();
        }

        private static Transform _chainAnchor, _chainRoot;
        private static readonly List<Transform> _chain = new List<Transform>();

        private static List<Transform> ChainFrom(Transform anchor, Transform flightRoot)
        {
            if (_chainAnchor == anchor && _chainRoot == flightRoot) return _chain;
            _chainAnchor = anchor;
            _chainRoot = flightRoot;
            _chain.Clear();
            for (Transform node = anchor; node != null; node = node.parent)
            {
                _chain.Add(node);
                if (node == flightRoot) break;
            }
            return _chain;
        }

        private static void ShowMinimapOnly()
        {
            FlightHud flightHud = SceneSingleton<FlightHud>.i;
            Canvas flightCanvas = flightHud != null ? GameAccess.FlightHudCanvas(flightHud) : null;
            Transform flightRoot = flightCanvas != null ? flightCanvas.transform : null;

            DynamicMap map = SceneSingleton<DynamicMap>.i;
            Transform anchor = map != null ? map.hudMapAnchor : null;
            if (anchor == null)
            {
                if (flightRoot != null) SetActiveTemporarily(flightRoot.gameObject, false);
                return;
            }

            bool mapOnFlightCanvas = flightRoot != null && anchor.IsChildOf(flightRoot);
            if (flightRoot != null && !mapOnFlightCanvas) SetActiveTemporarily(flightRoot.gameObject, false);

            if (DynamicMap.mapMaximized)
            {
                for (Transform node = anchor; node != null && node != flightRoot; node = node.parent)
                {
                    if (node.GetComponent<UnityEngine.UI.Image>() != null)
                    {
                        SetActiveTemporarily(node.gameObject, false);
                        break;
                    }
                }
                return;
            }

            List<Transform> chain = ChainFrom(anchor, flightRoot);
            for (int c = 0; c < chain.Count; c++)
            {
                Transform node = chain[c];
                SetActiveTemporarily(node.gameObject, true);
                if (node == flightRoot) break;
                if (!mapOnFlightCanvas && node.TryGetComponent(out Canvas canvas) && canvas.isRootCanvas) break;

                Transform parent = node.parent;
                if (mapOnFlightCanvas && parent != null)
                {
                    for (int i = 0; i < parent.childCount; i++)
                    {
                        Transform sibling = parent.GetChild(i);
                        if (sibling != node && !VehicleRadialMenu.IsMenuBranch(sibling)) SetActiveTemporarily(sibling.gameObject, false);
                    }
                }
            }

            SetActiveTemporarily(map.gameObject, true);
        }

        private static void CleanCombatHud()
        {
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null) return;

            Hide(GameAccess.CombatHudCountermeasureBackground(combatHud));
            Hide(GameAccess.CombatHudTopRightPanel(combatHud));
            Hide(GameAccess.CombatHudTargetText(combatHud));
            Hide(GameAccess.CombatHudTargetInfo(combatHud));

            UnitDebug unitDebug = ModConfig.ShowOverlay.Value ? GameUiLinks.FindUnitDebug() : null;
            if (unitDebug != null) SetActiveTemporarily(unitDebug.gameObject, false, false);
        }

        private static void ClearCombatMarkers()
        {
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud != null) combatHud.ClearIcons();
        }

        private static void RestoreCombatMarkers()
        {
            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            if (combatHud == null || combatHud.aircraft == null) return;

            FactionHQ hq = combatHud.aircraft.NetworkHQ;
            if (hq == null) return;

            combatHud.ClearIcons();
            for (int i = 0; i < hq.factionUnits.Count; i++)
            {
                combatHud.CreateMarker(hq.factionUnits[i]);
            }
            foreach (var pair in hq.trackingDatabase)
            {
                combatHud.CreateMarker(pair.Key);
            }
        }

        private static void Hide(Component component)
        {
            if (component != null) SetActiveTemporarily(component.gameObject, false);
        }

        private static void Hide(GameObject go)
        {
            SetActiveTemporarily(go, false);
        }

        private static void SetActiveTemporarily(GameObject go, bool active, bool restore = true)
        {
            if (go == null) return;
            if (!_overridden.TryGetValue(go, out Override state))
            {
                state = new Override { Original = go.activeSelf, Restore = restore };
                _overridden.Add(go, state);
            }
            else if (go.activeSelf != state.ModValue)
            {
                state.Original = go.activeSelf;
            }
            state.ModValue = active;
            if (go.activeSelf != active) go.SetActive(active);
        }
    }
}
