using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PlayableVehicles
{
    internal static class WeaponIcons
    {
        private const string ResourcePrefix = "PlayableVehicles.Icons.";
        private const float CheckInterval = 5f;

        private static float _nextCheckAt;
        private static readonly HashSet<WeaponInfo> _applied = new HashSet<WeaponInfo>();
        private static Sprite _r9;
        private static Sprite _ram45;

        public static void Tick()
        {
            float now = Time.unscaledTime;
            if (now < _nextCheckAt) return;
            _nextCheckAt = now + CheckInterval;

            try
            {
                Apply();
            }
            catch (Exception e)
            {
                ModLog.Error("WeaponIcons.Tick failed: " + e.Message);
            }
        }

        private static void Apply()
        {
            WeaponInfo[] infos = Resources.FindObjectsOfTypeAll<WeaponInfo>();
            foreach (WeaponInfo info in infos)
            {
                if (info == null || _applied.Contains(info)) continue;
                Sprite replacement = ReplacementFor(info);
                if (replacement == null) continue;
                string oldName = info.weaponIcon != null ? info.weaponIcon.name : "null";
                info.weaponIcon = replacement;
                _applied.Add(info);
                ModLog.Info("Weapon icon: '" + info.weaponName + "' (" + oldName + ") -> " + replacement.name);
            }
        }

        private static Sprite ReplacementFor(WeaponInfo info)
        {
            string name = ((info.weaponName ?? "") + "|" + (info.shortName ?? "")).ToLowerInvariant();
            if (name.Contains("ram45") || name.Contains("ram-45")) return Ram45();
            if (name.Contains("stratolance")) return R9();
            return null;
        }

        private static Sprite R9()
        {
            return _r9 ?? (_r9 = LoadSprite("R9_Icon.png"));
        }

        private static Sprite Ram45()
        {
            return _ram45 ?? (_ram45 = LoadSprite("RAM-45_Icon.png"));
        }

        private static Sprite LoadSprite(string fileName)
        {
            Assembly assembly = typeof(WeaponIcons).Assembly;
            string resource = ResourcePrefix + fileName;
            using (Stream stream = assembly.GetManifestResourceStream(resource))
            {
                if (stream == null)
                {
                    ModLog.Error("Weapon icon resource missing: " + resource);
                    return null;
                }
                var bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length) read += stream.Read(bytes, read, bytes.Length - read);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(bytes))
                {
                    ModLog.Error("Weapon icon decode failed: " + resource);
                    return null;
                }
                var sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                sprite.name = Path.GetFileNameWithoutExtension(fileName);
                return sprite;
            }
        }
    }
}
