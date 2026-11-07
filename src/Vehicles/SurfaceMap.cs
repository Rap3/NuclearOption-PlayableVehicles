using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlayableVehicles
{
    internal enum Surface : byte
    {
        Paved,
        Sand,
        Fields,
        Grass,
        Lush,
        Rock,
    }

    internal static class SurfaceMap
    {
        private const int MaskSize = 2048;
        private const byte Covered = 128;

        private static readonly (Surface surface, string[] keys)[] Layers =
        {
            (Surface.Fields, new[] { "fields" }),
            (Surface.Grass, new[] { "grassy", "grass" }),
            (Surface.Lush, new[] { "lush" }),
            (Surface.Rock, new[] { "rocky", "rock" }),
        };

        private static MapSettings _map;
        private static bool _tried;
        private static Vector2 _mapSize;
        private static byte[][] _masks;

        public static bool Available => _masks != null;

        public static Surface At(Vector3 point, Collider collider)
        {
            if (collider != null && GameAssets.i != null && collider.sharedMaterial != GameAssets.i.terrainMaterial) return Surface.Paved;
            return Terrain(point.ToGlobalPosition());
        }

        public static Surface Terrain(GlobalPosition position)
        {
            if (!EnsureLoaded()) return Surface.Grass;
            float u = (position.x + _mapSize.x * 0.5f) / _mapSize.x;
            float v = (position.z + _mapSize.y * 0.5f) / _mapSize.y;
            if (u < 0f || u >= 1f || v < 0f || v >= 1f) return Surface.Sand;
            int index = (int)(v * MaskSize) * MaskSize + (int)(u * MaskSize);
            Surface result = Surface.Sand;
            for (int i = 0; i < Layers.Length; i++)
            {
                byte[] mask = _masks[i];
                if (mask != null && mask[index] >= Covered) result = Layers[i].surface;
            }
            return result;
        }

        public static void Reset()
        {
            _map = null;
            _masks = null;
            _tried = false;
        }

        private static bool EnsureLoaded()
        {
            LevelInfo level = NetworkSceneSingleton<LevelInfo>.i;
            MapSettings map = level != null ? level.LoadedMapSettings : null;
            if (map == null) return false;
            if (map != _map)
            {
                _map = map;
                _masks = null;
                _tried = false;
            }
            if (_masks != null) return true;
            if (_tried) return false;
            _tried = true;
            try
            {
                Load(map);
            }
            catch (Exception e)
            {
                _masks = null;
                ModLog.Error("Surface map: failed to read terrain layers: " + e.Message);
            }
            return _masks != null;
        }

        private static void Load(MapSettings map)
        {
            _mapSize = map.MapSize;
            Material terrain = FindTerrainMaterial();
            if (terrain == null)
            {
                ModLog.Warning("Surface map: terrain material not found - all terrain counts as grass");
                return;
            }
            var masks = new byte[Layers.Length][];
            var found = new List<string>();
            foreach (string property in terrain.GetTexturePropertyNames())
            {
                Texture texture = terrain.GetTexture(property);
                if (texture == null || texture.name.IndexOf("splat", StringComparison.OrdinalIgnoreCase) < 0) continue;
                string name = texture.name.ToLowerInvariant();
                for (int i = 0; i < Layers.Length; i++)
                {
                    if (masks[i] != null || !Matches(name, Layers[i].keys)) continue;
                    masks[i] = ReadMask(texture);
                    int covered = 0;
                    foreach (byte value in masks[i])
                    {
                        if (value >= Covered) covered++;
                    }
                    found.Add(Layers[i].surface + "=" + texture.name + " " + (100f * covered / masks[i].Length).ToString("0") + "%");
                    break;
                }
            }
            _masks = masks;
            ModLog.Info("Surface map: " + map.name + " " + _mapSize.x.ToString("0") + "x" + _mapSize.y.ToString("0") + " m, layers "
                + (found.Count > 0 ? string.Join(", ", found) : "none"));
        }

        private static bool Matches(string name, string[] keys)
        {
            foreach (string key in keys)
            {
                int at = name.LastIndexOf(key, StringComparison.Ordinal);
                if (at >= 0 && (at + key.Length == name.Length || !char.IsLetter(name[at + key.Length]))) return true;
            }
            return false;
        }

        private static Material FindTerrainMaterial()
        {
            foreach (MeshRenderer renderer in Object.FindObjectsOfType<MeshRenderer>())
            {
                Material material = renderer.sharedMaterial;
                if (material == null || !material.name.StartsWith("Terrain", StringComparison.Ordinal)) continue;
                foreach (string property in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(property);
                    if (texture != null && texture.name.IndexOf("splat", StringComparison.OrdinalIgnoreCase) >= 0) return material;
                }
            }
            return null;
        }

        private static byte[] ReadMask(Texture texture)
        {
            RenderTexture rt = RenderTexture.GetTemporary(MaskSize, MaskSize, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            var copy = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false, true);
            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                copy.ReadPixels(new Rect(0, 0, MaskSize, MaskSize), 0, 0, false);
                copy.Apply(false);
                Color32[] pixels = copy.GetPixels32();
                var mask = new byte[pixels.Length];
                for (int i = 0; i < pixels.Length; i++) mask[i] = pixels[i].r;
                return mask;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(copy);
            }
        }
    }
}
