using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlayableVehicles
{
    internal sealed class MissileWarningWidget : HudWidget
    {
        private const float CheckInterval = 0.2f;
        private const float DetectionRange = 5000f;
        private const float ListTop = -150f;
        private const float RowHeight = 24f;
        private const float MarkerSize = 36f;
        private const float EdgeFraction = 0.85f;

        public override bool VisibleInProjectileCam => true;

        private sealed class Threat
        {
            public Missile Missile;
            public GameObject Row;
            public Text Text;
            public Image Marker;
            public string Line;
            public string LineType;
            public long LineKey;
        }

        private sealed class Alarm
        {
            public AudioClip[] Clips;
            public AudioClip Alert;
            public AudioSource Source;
        }

        private readonly List<Threat> _threats = new List<Threat>();
        private readonly List<Missile> _unknown = new List<Missile>();
        private readonly Dictionary<string, Alarm> _alarms = new Dictionary<string, Alarm>();
        private RectTransform _list;
        private RectTransform _markers;
        private GameObject _rowPrefab;
        private Material _markerMaterial;
        private GameObject _audio;
        private float _nextCheck;

        public override void Build(RectTransform root, HudContext context)
        {
            _list = HudStyle.CreateRect("MissileThreats", root, new Vector2(0.5f, 1f), new Vector2(0f, ListTop), new Vector2(420f, 10f));
            _list.pivot = new Vector2(0.5f, 1f);
            _markers = HudStyle.CreateFill("MissileMarkers", root);

            CombatHUD combatHud = SceneSingleton<CombatHUD>.i;
            ThreatList threatList = combatHud != null && GameAccess.CombatHudThreatList != null ? GameAccess.CombatHudThreatList(combatHud) : null;
            _rowPrefab = threatList != null && GameAccess.ThreatListItemPrefab != null ? GameAccess.ThreatListItemPrefab(threatList) : null;
            GameObject markerPrefab = combatHud != null && GameAccess.CombatHudUnitMarkerPrefab != null ? GameAccess.CombatHudUnitMarkerPrefab(combatHud) : null;
            Image markerImage = markerPrefab != null ? markerPrefab.GetComponent<Image>() : null;
            _markerMaterial = markerImage != null ? markerImage.material : null;

            _audio = new GameObject("PlayableVehiclesMissileAlarm");
            foreach (GameAccess.ThreatAlarm type in GameAccess.ThreatAlarms(threatList))
            {
                if (string.IsNullOrEmpty(type.SeekerType) || type.Clips == null || type.Clips.Length == 0 || _alarms.ContainsKey(type.SeekerType)) continue;
                AudioSource source = _audio.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.clip = type.Clips[0];
                source.volume = 0.8f;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                if (SoundManager.i != null) source.outputAudioMixerGroup = SoundManager.i.MissileAlertMixer;
                _alarms[type.SeekerType] = new Alarm { Clips = type.Clips, Alert = type.Alert, Source = source };
            }
        }

        public override void Tick(HudContext context)
        {
            bool paused = GameplayUI.GameIsPaused;
            foreach (Alarm alarm in _alarms.Values)
            {
                if (paused && alarm.Source.isPlaying) alarm.Source.Pause();
                else if (!paused && !alarm.Source.isPlaying && alarm.Source.time > 0f) alarm.Source.UnPause();
            }
            if (paused || Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckInterval;

            GroundVehicle vehicle = context.Vehicle;
            DropFinished(vehicle);
            FindNew(vehicle);
            UpdateAlarms();
        }

        private void DropFinished(GroundVehicle vehicle)
        {
            for (int i = _threats.Count - 1; i >= 0; i--)
            {
                Missile missile = _threats[i].Missile;
                if (missile != null && !missile.disabled && missile.targetID == vehicle.persistentID) continue;
                Remove(_threats[i]);
                _threats.RemoveAt(i);
            }
            _unknown.RemoveAll(m => m == null || m.disabled || m.targetID != vehicle.persistentID);
        }

        private void FindNew(GroundVehicle vehicle)
        {
            List<Unit> units = UnitRegistry.allUnits;
            for (int i = 0; i < units.Count; i++)
            {
                if (!(units[i] is Missile missile) || missile.disabled || missile.targetID != vehicle.persistentID) continue;
                if (IsTracked(missile) || _unknown.Contains(missile)) continue;
                MissileSeeker seeker = GameAccess.MissileSeeker != null ? GameAccess.MissileSeeker(missile) : null;
                if (seeker != null && !seeker.triggerMissileWarning) continue;
                _unknown.Add(missile);
            }

            FactionHQ hq = vehicle.NetworkHQ;
            for (int i = _unknown.Count - 1; i >= 0; i--)
            {
                Missile missile = _unknown[i];
                bool seen = FastMath.InRange(missile.transform.position, vehicle.transform.position, DetectionRange)
                    && missile.LineOfSight(vehicle.transform.position, 1000f);
                bool announced = missile.seekerMode < Missile.SeekerMode.passive
                    && hq != null && (hq.GetTrackingData(missile.persistentID) != null || missile.NetworkHQ == hq);
                if (!seen && !announced) continue;
                _unknown.RemoveAt(i);
                Add(missile);
            }
        }

        private bool IsTracked(Missile missile)
        {
            foreach (Threat threat in _threats)
            {
                if (threat.Missile == missile) return true;
            }
            return false;
        }

        private void Add(Missile missile)
        {
            var threat = new Threat { Missile = missile };
            if (_rowPrefab != null && GameAccess.ThreatItemText != null)
            {
                threat.Row = Object.Instantiate(_rowPrefab, _list, false);
                ThreatItem item = threat.Row.GetComponent<ThreatItem>();
                threat.Text = item != null ? GameAccess.ThreatItemText(item) : threat.Row.GetComponentInChildren<Text>(true);
                if (item != null) Object.Destroy(item);
                threat.Row.SetActive(true);
                foreach (Graphic graphic in threat.Row.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            }
            Sprite sprite = GameAssets.i != null ? GameAssets.i.missileWarningSprite : null;
            if (sprite != null)
            {
                threat.Marker = HudStyle.CreateImage("MissileMarker", _markers, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(MarkerSize, MarkerSize), HudStyle.Alert);
                threat.Marker.sprite = sprite;
                threat.Marker.preserveAspect = true;
                if (_markerMaterial != null) threat.Marker.material = _markerMaterial;
            }
            _threats.Add(threat);
            { DynamicMap single = SceneSingleton<DynamicMap>.i; if (single != null) single.FlagIncomingMissile(missile); }

            if (_alarms.TryGetValue(SeekerType(missile), out Alarm alarm))
            {
                if (alarm.Alert != null) SoundManager.PlayInterfaceOneShot(alarm.Alert);
                alarm.Source.time = 0f;
            }
        }

        private void Remove(Threat threat)
        {
            if (threat.Row != null) Object.Destroy(threat.Row);
            if (threat.Marker != null) Object.Destroy(threat.Marker.gameObject);
            if (threat.Missile != null) { DynamicMap single = SceneSingleton<DynamicMap>.i; if (single != null) single.ClearIncomingMissile(threat.Missile); }
        }

        private void UpdateAlarms()
        {
            foreach (KeyValuePair<string, Alarm> pair in _alarms)
            {
                Alarm alarm = pair.Value;
                int index = int.MaxValue;
                foreach (Threat threat in _threats)
                {
                    if (SeekerType(threat.Missile) != pair.Key) continue;
                    index = Mathf.Min(index, (int)(threat.Missile.seekerMode - Missile.SeekerMode.activeLock));
                }
                if (index == int.MaxValue)
                {
                    if (alarm.Source.isPlaying) alarm.Source.Stop();
                    alarm.Source.time = 0f;
                    continue;
                }
                AudioClip clip = alarm.Clips[Mathf.Clamp(index, 0, alarm.Clips.Length - 1)];
                if (alarm.Source.clip != clip)
                {
                    alarm.Source.clip = clip;
                    alarm.Source.time = 0f;
                    alarm.Source.Play();
                }
                else if (!alarm.Source.isPlaying) alarm.Source.Play();
            }
        }

        private static string SeekerType(Missile missile)
        {
            try
            {
                return missile != null ? missile.GetSeekerType() ?? "" : "";
            }
            catch (System.NullReferenceException)
            {
                return "";
            }
        }

        public override void Refresh(HudContext context)
        {
            Camera camera = context.Camera;
            Vector3 vehiclePosition = context.Vehicle.transform.position;
            float y = 0f;
            foreach (Threat threat in _threats)
            {
                Missile missile = threat.Missile;
                if (missile == null) continue;
                bool searching = missile.seekerMode == Missile.SeekerMode.activeSearch;
                Color color = searching ? HudStyle.Warning : HudStyle.Alert + HudStyle.Normal * Mathf.Sin(Time.realtimeSinceStartup * 20f);

                if (threat.Text != null)
                {
                    float range = Vector3.Distance(vehiclePosition, missile.transform.position);
                    string type = SeekerType(missile);
                    long key = HudReadings.DistanceKey(range);
                    if (threat.Line == null || key != threat.LineKey || type != threat.LineType)
                    {
                        threat.LineKey = key;
                        threat.LineType = type;
                        threat.Line = "Missile [" + type + "] " + UnitConverter.DistanceReading(range);
                        threat.Text.text = threat.Line;
                    }
                    threat.Text.color = color;
                    var row = (RectTransform)threat.Row.transform;
                    row.anchorMin = row.anchorMax = new Vector2(0.5f, 1f);
                    row.pivot = new Vector2(0.5f, 1f);
                    row.anchoredPosition = new Vector2(0f, -y);
                    y += Mathf.Max(row.rect.height, RowHeight);
                }
                if (threat.Marker != null) PlaceMarker(threat.Marker, camera, missile.transform.position, color);
            }
        }

        private static void PlaceMarker(Image marker, Camera camera, Vector3 position, Color color)
        {
            marker.color = HudStyle.WithAlpha(color, 1f);
            Vector3 screen = camera.WorldToScreenPoint(position);
            bool onScreen = screen.z > 0f && screen.x >= 0f && screen.x <= Screen.width && screen.y >= 0f && screen.y <= Screen.height;
            if (!onScreen)
            {
                Vector3 local = camera.transform.InverseTransformPoint(position);
                var direction = new Vector2(local.x, local.y);
                if (direction.sqrMagnitude < 1e-4f) direction = Vector2.down;
                direction.Normalize();
                screen = new Vector3(Screen.width * 0.5f * (1f + direction.x * EdgeFraction), Screen.height * 0.5f * (1f + direction.y * EdgeFraction), 0f);
            }
            marker.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
        }

        public override void Dispose()
        {
            foreach (Threat threat in _threats) Remove(threat);
            _threats.Clear();
            _unknown.Clear();
            _alarms.Clear();
            if (_audio != null) Object.Destroy(_audio);
        }
    }
}
