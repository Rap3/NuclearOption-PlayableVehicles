using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PlayableVehicles
{
    internal sealed class HudContext
    {
        public GroundVehicle Vehicle;
        public Rigidbody Body;
        public VehicleDriver Driver;
        public VehicleCamera View;
        public Camera Camera;
        public float ScaleFactor = 1f;
        public float SpeedBoxRight = float.NaN;

        public TurretController Turrets => Driver.Turrets;
    }

    internal abstract class HudWidget
    {
        public abstract void Build(RectTransform root, HudContext context);
        public abstract void Refresh(HudContext context);
        public virtual void Tick(HudContext context) { }
        public virtual void Dispose() { }
        public virtual bool VisibleInProjectileCam => false;
    }

    internal sealed class VehicleHud
    {
        private sealed class Entry
        {
            public HudWidget Widget;
            public GameObject[] Objects;
            public bool Failed;
            public bool HiddenByProjectileCam;
        }

        private readonly HudContext _context = new HudContext();
        private readonly List<Entry> _widgets = new List<Entry>();
        private GameObject _root;
        private Canvas _canvas;

        public void Show(GroundVehicle vehicle, VehicleDriver driver, VehicleCamera view)
        {
            Hide();

            _context.Vehicle = vehicle;
            _context.Body = vehicle.GetComponent<Rigidbody>();
            _context.Driver = driver;
            _context.View = view;

            HudStyle.Refresh();
            GameUiLinks.Reset();

            _root = new GameObject("PlayableVehiclesHud", typeof(RectTransform));
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 5;
            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var rootRect = (RectTransform)_root.transform;

            Add(new UnitMarkersWidget(), rootRect);
            Add(new HitMarkerWidget(), rootRect);
            Add(new HitDirectionWidget(), rootRect);
            Add(new OpticWidget(), rootRect);
            Add(new AimWidget(), rootRect);
            Add(new ProjectileCamWidget(), rootRect);
            Add(new LockWidget(), rootRect);
            Add(new GaugesWidget(), rootRect);
            Add(new CompassWidget(), rootRect);
            Add(new StatusWidget(), rootRect);
            Add(new WeaponListWidget(), rootRect);
            Add(new MissileWarningWidget(), rootRect);
            Add(new CaptureWidget(), rootRect);
            Add(new SupplyWidget(), rootRect);
        }

        private void Add(HudWidget widget, RectTransform root)
        {
            int firstChild = root.childCount;
            try
            {
                widget.Build(root, _context);
            }
            catch (Exception e)
            {
                ModLog.Warning("HUD: widget " + widget.GetType().Name + " failed to build, skipped: " + e);
                try
                {
                    widget.Dispose();
                }
                catch (Exception disposeError)
                {
                    ModLog.Warning("HUD: widget " + widget.GetType().Name + " failed to dispose: " + disposeError);
                }
                for (int i = root.childCount - 1; i >= firstChild; i--)
                {
                    Object.Destroy(root.GetChild(i).gameObject);
                }
                return;
            }

            var objects = new GameObject[root.childCount - firstChild];
            for (int i = 0; i < objects.Length; i++)
            {
                objects[i] = root.GetChild(firstChild + i).gameObject;
            }
            _widgets.Add(new Entry { Widget = widget, Objects = objects });
        }

        private static void Disable(Entry entry, string stage, Exception e)
        {
            entry.Failed = true;
            ModLog.Warning("HUD: widget " + entry.Widget.GetType().Name + " failed in " + stage + ", disabled until exit: " + e);
            foreach (GameObject obj in entry.Objects)
            {
                try
                {
                    if (obj != null) obj.SetActive(false);
                }
                catch { }
            }
        }

        public void Hide()
        {
            foreach (Entry entry in _widgets)
            {
                try
                {
                    entry.Widget.Dispose();
                }
                catch (Exception e)
                {
                    ModLog.Warning("HUD: widget " + entry.Widget.GetType().Name + " failed to dispose: " + e);
                }
            }
            _widgets.Clear();

            if (_root != null) Object.Destroy(_root);
            _root = null;
            _canvas = null;
            GameUiLinks.Reset();

            _context.Vehicle = null;
            _context.Body = null;
            _context.Driver = null;
            _context.View = null;
            _context.Camera = null;
        }

        public void Refresh(Camera camera)
        {
            if (_root == null || _context.Vehicle == null || _context.Driver == null) return;

            for (int i = 0; i < _widgets.Count; i++)
            {
                Entry entry = _widgets[i];
                if (entry.Failed) continue;
                try
                {
                    entry.Widget.Tick(_context);
                }
                catch (Exception e)
                {
                    Disable(entry, "Tick", e);
                }
            }

            bool visible = ModConfig.ShowOverlay.Value && !GameplayUI.GameIsPaused && !DynamicMap.mapMaximized && camera != null;
            if (_root.activeSelf != visible) _root.SetActive(visible);
            if (!visible) return;

            _context.Camera = camera;
            _context.ScaleFactor = _canvas.scaleFactor > 0.01f ? _canvas.scaleFactor : 1f;
            bool projectileCam = ProjectileCam.IsActive;
            for (int i = 0; i < _widgets.Count; i++)
            {
                Entry entry = _widgets[i];
                if (entry.Failed) continue;
                if (projectileCam && !entry.Widget.VisibleInProjectileCam)
                {
                    for (int o = 0; o < entry.Objects.Length; o++)
                    {
                        GameObject obj = entry.Objects[o];
                        if (obj != null && obj.activeSelf) obj.SetActive(false);
                    }
                    entry.HiddenByProjectileCam = true;
                    continue;
                }
                if (entry.HiddenByProjectileCam)
                {
                    entry.HiddenByProjectileCam = false;
                    for (int o = 0; o < entry.Objects.Length; o++)
                    {
                        GameObject obj = entry.Objects[o];
                        if (obj != null && !obj.activeSelf) obj.SetActive(true);
                    }
                }
                try
                {
                    entry.Widget.Refresh(_context);
                }
                catch (Exception e)
                {
                    Disable(entry, "Refresh", e);
                }
            }
        }
    }
}
