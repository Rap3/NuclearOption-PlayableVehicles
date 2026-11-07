using UnityEngine;

namespace PlayableVehicles
{
    internal static class TrailerLegs
    {
        private const float LegSideX = 1.4f;
        private const float LegBelowY = -0.7f;

        private static bool IsLeg(Vector3 center)
        {
            return Mathf.Abs(center.x) > LegSideX && center.y < LegBelowY;
        }

        public static void SetHidden(GroundVehicle trailer, bool hidden)
        {
            if (trailer == null) return;
            TrailerLegsVisual visual = trailer.GetComponentInChildren<TrailerLegsVisual>(true);
            if (hidden)
            {
                if (visual == null && !TryCreate(trailer, out visual)) return;
                visual.RootRenderer.enabled = false;
                visual.BodyObject.SetActive(true);
                visual.LegsObject.SetActive(false);
            }
            else if (visual != null)
            {
                visual.RootRenderer.enabled = true;
                visual.BodyObject.SetActive(false);
                visual.LegsObject.SetActive(false);
            }
        }

        private static bool TryCreate(GroundVehicle trailer, out TrailerLegsVisual visual)
        {
            visual = null;
            MeshRenderer root = trailer.GetComponent<MeshRenderer>();
            MeshFilter filter = trailer.GetComponent<MeshFilter>();
            if (root == null || filter == null || filter.sharedMesh == null)
            {
                ModLog.Info("TrailerLegs: " + trailer.unitName + ": no root mesh renderer - legs stay visible");
                return false;
            }
            MeshSplit.Result split = MeshSplit.Get(filter.sharedMesh, IsLeg, true, out string error);
            if (split == null)
            {
                ModLog.Warning("TrailerLegs: " + trailer.unitName + ": mesh split failed: " + error);
                return false;
            }

            GameObject holder = new GameObject("PvTrailerLegs");
            holder.transform.SetParent(trailer.transform, false);
            GameObject body = CopyPart(holder.transform, "PvBody", split.Parts[1], root);
            GameObject legs = CopyPart(holder.transform, "PvLegs", split.Parts[0], root);
            body.SetActive(false);
            legs.SetActive(false);

            visual = holder.AddComponent<TrailerLegsVisual>();
            visual.RootRenderer = root;
            visual.BodyObject = body;
            visual.LegsObject = legs;
            ModLog.Info("TrailerLegs: " + trailer.unitName + ": legs split off (" + split.Parts[0].name + ")");
            return true;
        }

        private static GameObject CopyPart(Transform parent, string name, Mesh mesh, MeshRenderer source)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MeshFilter f = go.AddComponent<MeshFilter>();
            f.sharedMesh = mesh;
            MeshRenderer r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = source.sharedMaterials;
            return go;
        }
    }

    internal sealed class TrailerLegsVisual : MonoBehaviour
    {
        public MeshRenderer RootRenderer;
        public GameObject BodyObject;
        public GameObject LegsObject;
    }
}
