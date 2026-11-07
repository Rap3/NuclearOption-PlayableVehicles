using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal sealed class TrackAnimator
    {
        private const float ScrollPerMeter = -0.2f;

        private Renderer _original;
        private bool _originalEnabled;
        private readonly Renderer[] _sides = new Renderer[2];
        private readonly List<Object> _created = new List<Object>();
        private readonly Material[][] _sideMaterials = new Material[2][];

        public bool Active => _sides[0] != null;

        public void Attach(GroundVehicle vehicle)
        {
            Detach();
            Renderer tracks = GameAccess.GroundVehicleTracks != null ? GameAccess.GroundVehicleTracks(vehicle) : null;
            var filter = tracks != null ? tracks.GetComponent<MeshFilter>() : null;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (mesh == null || !(tracks is MeshRenderer)) return;

            MeshSplit.Result split = MeshSplit.Get(mesh, true, out string error);
            if (split == null)
            {
                ModLog.Warning(vehicle.unitName + ": track mesh '" + mesh.name + "' could not be split - tracks move together: " + error);
                return;
            }

            Material[] materials = tracks.sharedMaterials;
            for (int side = 0; side < 2; side++)
            {
                var go = new GameObject(tracks.name + (side == 0 ? "_PvLeft" : "_PvRight"));
                go.layer = tracks.gameObject.layer;
                go.transform.SetParent(tracks.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = split.Parts[side];
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = CopyMaterials(materials, _created);
                renderer.shadowCastingMode = tracks.shadowCastingMode;
                renderer.receiveShadows = tracks.receiveShadows;
                _sides[side] = renderer;
                _sideMaterials[side] = renderer.sharedMaterials;
                _created.Add(go);
            }

            Vector3 leftDir = vehicle.transform.InverseTransformDirection(tracks.transform.TransformDirection(Vector3.left));
            if (leftDir.x > 0f)
            {
                Renderer swap = _sides[0];
                _sides[0] = _sides[1];
                _sides[1] = swap;
                Material[] swapMats = _sideMaterials[0];
                _sideMaterials[0] = _sideMaterials[1];
                _sideMaterials[1] = swapMats;
            }

            _original = tracks;
            _originalEnabled = tracks.enabled;
            tracks.enabled = false;
        }

        internal static Material[] CopyMaterials(Material[] materials, List<Object> created)
        {
            var copies = new Material[materials.Length];
            for (int m = 0; m < materials.Length; m++)
            {
                copies[m] = materials[m] != null ? new Material(materials[m]) : null;
                if (copies[m] != null) created.Add(copies[m]);
            }
            return copies;
        }

        public void Tick(float left, float right)
        {
            if (!Active) return;
            float dt = Time.deltaTime;
            Scroll(_sideMaterials[0], left * ScrollPerMeter * dt);
            Scroll(_sideMaterials[1], right * ScrollPerMeter * dt);
        }

        private static void Scroll(Material[] materials, float delta)
        {
            if (materials == null) return;
            foreach (Material material in materials)
            {
                if (material != null) material.mainTextureOffset += new Vector2(0f, delta);
            }
        }

        public void Detach()
        {
            if (_original != null) _original.enabled = _originalEnabled;
            _original = null;
            foreach (Object created in _created)
            {
                if (created != null) Object.Destroy(created);
            }
            _created.Clear();
            _sides[0] = null;
            _sides[1] = null;
            _sideMaterials[0] = null;
            _sideMaterials[1] = null;
        }
    }
}
