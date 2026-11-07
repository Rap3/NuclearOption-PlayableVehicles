using System.Collections.Generic;
using UnityEngine;

namespace PlayableVehicles
{
    internal interface IWheelSteer
    {
        WheeledSpec Wheeled { get; }
        float WheelSteerDeg(int axle, int side);
        float WheelLift(int axle, int side);
    }

    internal sealed class WheelAnimator
    {
        private struct SteeredWheel
        {
            public Transform Pivot;
            public Vector3 Rest;
            public float Lift;
            public int Axle;
            public int Side;
        }

        private readonly List<SteeredWheel> _wheels = new List<SteeredWheel>();
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private readonly List<Object> _created = new List<Object>();
        private IWheelSteer _drive;
        private float _roll;
        private bool _suspension;
        private const float DropSpeed = 0.6f;

        public void Attach(GroundVehicle vehicle, IWheelSteer drive, bool suspension = false)
        {
            Detach();
            WheeledSpec spec = drive != null ? drive.Wheeled : null;
            if (spec == null) return;
            GameObject[] axles = GameAccess.GroundVehicleWheels != null ? GameAccess.GroundVehicleWheels(vehicle) : null;
            if (axles == null) return;
            _drive = drive;
            _suspension = suspension;
            Transform root = vehicle.transform;

            foreach (GameObject axle in axles)
            {
                if (axle == null) continue;
                int index = NearestAxle(spec, root.InverseTransformPoint(axle.transform.position).z);
                if (index >= spec.SteeredAxles && !suspension) continue;
                var filter = axle.GetComponent<MeshFilter>();
                var renderer = axle.GetComponent<MeshRenderer>();
                if (filter == null || renderer == null || filter.sharedMesh == null) continue;
                MeshSplit.Result split = MeshSplit.Get(filter.sharedMesh, false, out string error);
                if (split == null)
                {
                    ModLog.Warning(vehicle.unitName + ": wheel mesh '" + filter.sharedMesh.name + "' could not be split - these wheels do not steer or travel: " + error);
                    continue;
                }

                for (int half = 0; half < 2; half++)
                {
                    if (split.Parts[half] == null) continue;
                    Vector3 center = axle.transform.TransformPoint(split.Centers[half]);
                    var pivot = new GameObject(axle.name + (half == 0 ? "_PvSteerA" : "_PvSteerB"));
                    pivot.layer = axle.layer;
                    pivot.transform.SetParent(root, false);
                    pivot.transform.SetPositionAndRotation(center, root.rotation);

                    var wheel = new GameObject(axle.name + "_PvWheel");
                    wheel.layer = axle.layer;
                    wheel.transform.SetParent(pivot.transform, false);
                    wheel.transform.SetPositionAndRotation(axle.transform.position, axle.transform.rotation);
                    wheel.transform.localScale = Divide(axle.transform.lossyScale, root.lossyScale);
                    wheel.AddComponent<MeshFilter>().sharedMesh = split.Parts[half];
                    var copy = wheel.AddComponent<MeshRenderer>();
                    copy.sharedMaterials = renderer.sharedMaterials;
                    copy.shadowCastingMode = renderer.shadowCastingMode;
                    copy.receiveShadows = renderer.receiveShadows;

                    _created.Add(pivot);
                    _wheels.Add(new SteeredWheel
                    {
                        Pivot = pivot.transform,
                        Rest = pivot.transform.localPosition,
                        Axle = index,
                        Side = root.InverseTransformPoint(center).x < 0f ? 0 : 1,
                    });
                }
                renderer.enabled = false;
                _hidden.Add(renderer);
            }
        }

        public void Tick(float speed)
        {
            if (_wheels.Count == 0 || _drive == null) return;
            float radius = Mathf.Max(_drive.Wheeled.WheelRadius, 0.1f);
            _roll = Mathf.Repeat(_roll + speed * Mathf.Rad2Deg * Time.deltaTime / radius, 360f);
            Quaternion roll = Quaternion.AngleAxis(_roll, Vector3.right);
            for (int i = 0; i < _wheels.Count; i++)
            {
                SteeredWheel wheel = _wheels[i];
                if (wheel.Pivot == null) continue;
                wheel.Pivot.localRotation = Quaternion.AngleAxis(_drive.WheelSteerDeg(wheel.Axle, wheel.Side), Vector3.up) * roll;
                if (!_suspension) continue;
                float target = _drive.WheelLift(wheel.Axle, wheel.Side);
                wheel.Lift = target >= wheel.Lift ? target : Mathf.Max(target, wheel.Lift - DropSpeed * Time.deltaTime);
                wheel.Pivot.localPosition = wheel.Rest + Vector3.up * wheel.Lift;
                _wheels[i] = wheel;
            }
        }

        public void Detach()
        {
            foreach (Renderer renderer in _hidden)
            {
                if (renderer != null) renderer.enabled = true;
            }
            _hidden.Clear();
            foreach (Object created in _created)
            {
                if (created != null) Object.Destroy(created);
            }
            _created.Clear();
            _wheels.Clear();
            _drive = null;
            _roll = 0f;
            _suspension = false;
        }

        private static int NearestAxle(WheeledSpec spec, float z)
        {
            int best = 0;
            for (int i = 1; i < spec.AxleZ.Length; i++)
            {
                if (Mathf.Abs(spec.AxleZ[i] - z) < Mathf.Abs(spec.AxleZ[best] - z)) best = i;
            }
            return best;
        }

        private static Vector3 Divide(Vector3 a, Vector3 b)
        {
            return new Vector3(b.x != 0f ? a.x / b.x : a.x, b.y != 0f ? a.y / b.y : a.y, b.z != 0f ? a.z / b.z : a.z);
        }
    }
}
