using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlayableVehicles
{
    internal static class MeshSplit
    {
        internal sealed class Result
        {
            public Mesh[] Parts;
            public Vector3[] Centers;
        }

        private static readonly Dictionary<Mesh, Result> Cache = new Dictionary<Mesh, Result>();
        private static readonly Dictionary<Mesh, string> Failed = new Dictionary<Mesh, string>();

        public static Result Get(Mesh mesh, bool requireBoth, out string error)
        {
            return Get(mesh, center => center.x < 0f, requireBoth, out error);
        }

        public static Result Get(Mesh mesh, Func<Vector3, bool> first, bool requireBoth, out string error)
        {
            error = null;
            if (Cache.TryGetValue(mesh, out Result cached))
            {
                if (requireBoth && (cached.Parts[0] == null || cached.Parts[1] == null))
                {
                    error = "one side is empty";
                    return null;
                }
                return cached;
            }
            if (Failed.TryGetValue(mesh, out error)) return null;
            try
            {
                Result result = Split(mesh, first);
                Cache[mesh] = result;
                ModLog.Info("Mesh split: '" + mesh.name + "' (" + mesh.vertexCount + " vertices) into sides");
                if (requireBoth && (result.Parts[0] == null || result.Parts[1] == null))
                {
                    error = "one side is empty";
                    return null;
                }
                return result;
            }
            catch (Exception e)
            {
                error = e.Message;
                Failed[mesh] = error;
                return null;
            }
        }

        private static Result Split(Mesh mesh, Func<Vector3, bool> first)
        {
            VertexAttributeDescriptor[] attributes = mesh.GetVertexAttributes();
            int vertexCount = mesh.vertexCount;
            int streams = 0;
            foreach (VertexAttributeDescriptor a in attributes) streams = Mathf.Max(streams, a.stream + 1);

            var streamData = new byte[streams][];
            for (int s = 0; s < streams; s++)
            {
                int stride = mesh.GetVertexBufferStride(s);
                if (stride == 0) continue;
                using (GraphicsBuffer buffer = mesh.GetVertexBuffer(s))
                {
                    streamData[s] = new byte[vertexCount * stride];
                    buffer.GetData(streamData[s]);
                }
            }

            int posStream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
            int posOffset = mesh.GetVertexAttributeOffset(VertexAttribute.Position);
            if (posStream < 0 || mesh.GetVertexAttributeFormat(VertexAttribute.Position) != VertexAttributeFormat.Float32)
            {
                throw new InvalidOperationException("unsupported position format " + mesh.GetVertexAttributeFormat(VertexAttribute.Position));
            }
            int posStride = mesh.GetVertexBufferStride(posStream);
            var positions = new Vector3[vertexCount];
            byte[] posData = streamData[posStream];
            for (int v = 0; v < vertexCount; v++)
            {
                int at = v * posStride + posOffset;
                positions[v] = new Vector3(BitConverter.ToSingle(posData, at), BitConverter.ToSingle(posData, at + 4), BitConverter.ToSingle(posData, at + 8));
            }

            bool wide = mesh.indexFormat == IndexFormat.UInt32;
            int indexTotal = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                SubMeshDescriptor d = mesh.GetSubMesh(sub);
                indexTotal = Mathf.Max(indexTotal, d.indexStart + d.indexCount);
            }
            var indexBytes = new byte[indexTotal * (wide ? 4 : 2)];
            using (GraphicsBuffer buffer = mesh.GetIndexBuffer())
            {
                buffer.GetData(indexBytes);
            }

            var result = new Result { Parts = new Mesh[2], Centers = new Vector3[2] };
            int nonEmpty = 0;
            for (int side = 0; side < 2; side++)
            {
                var kept = new List<uint>(indexTotal / 2);
                var descriptors = new SubMeshDescriptor[mesh.subMeshCount];
                bool any = false;
                var bounds = new Bounds();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    SubMeshDescriptor d = mesh.GetSubMesh(sub);
                    int start = kept.Count;
                    for (int i = d.indexStart; i + 2 < d.indexStart + d.indexCount; i += 3)
                    {
                        uint a = Index(indexBytes, i, wide), b = Index(indexBytes, i + 1, wide), c = Index(indexBytes, i + 2, wide);
                        Vector3 pa = positions[a + d.baseVertex], pb = positions[b + d.baseVertex], pc = positions[c + d.baseVertex];
                        if (first((pa + pb + pc) * (1f / 3f)) != (side == 0)) continue;
                        kept.Add(a);
                        kept.Add(b);
                        kept.Add(c);
                        if (!any)
                        {
                            bounds = new Bounds(pa, Vector3.zero);
                            any = true;
                        }
                        bounds.Encapsulate(pa);
                        bounds.Encapsulate(pb);
                        bounds.Encapsulate(pc);
                    }
                    descriptors[sub] = new SubMeshDescriptor(start, kept.Count - start, MeshTopology.Triangles) { baseVertex = d.baseVertex };
                }
                if (kept.Count == 0) continue;
                nonEmpty++;

                var part = new Mesh { name = mesh.name + (side == 0 ? "_PvLeft" : "_PvRight"), indexFormat = mesh.indexFormat };
                part.SetVertexBufferParams(vertexCount, attributes);
                for (int s = 0; s < streams; s++)
                {
                    if (streamData[s] != null) part.SetVertexBufferData(streamData[s], 0, 0, streamData[s].Length, s, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
                }
                part.SetIndexBufferParams(kept.Count, mesh.indexFormat);
                if (wide)
                {
                    part.SetIndexBufferData(kept, 0, 0, kept.Count, MeshUpdateFlags.DontRecalculateBounds);
                }
                else
                {
                    var narrow = new ushort[kept.Count];
                    for (int i = 0; i < narrow.Length; i++) narrow[i] = (ushort)kept[i];
                    part.SetIndexBufferData(narrow, 0, 0, narrow.Length, MeshUpdateFlags.DontRecalculateBounds);
                }
                part.subMeshCount = descriptors.Length;
                for (int sub = 0; sub < descriptors.Length; sub++) part.SetSubMesh(sub, descriptors[sub], MeshUpdateFlags.DontRecalculateBounds);
                part.bounds = mesh.bounds;
                part.UploadMeshData(true);
                result.Parts[side] = part;
                result.Centers[side] = bounds.center;
            }
            if (nonEmpty == 0) throw new InvalidOperationException("no triangles");
            return result;
        }

        private static uint Index(byte[] data, int i, bool wide)
        {
            return wide ? BitConverter.ToUInt32(data, i * 4) : BitConverter.ToUInt16(data, i * 2);
        }
    }
}
