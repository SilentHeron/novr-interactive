using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOVR.MotionControls;

// Read only once when an aircraft's pilot is acquired. Never exported or bundled.
internal static class PilotMeshData
{
    internal static Vector2[] ReadUv(Mesh mesh)
    {
        if (mesh.isReadable) return mesh.uv;
        const VertexAttribute attribute = VertexAttribute.TexCoord0;
        if (!mesh.HasVertexAttribute(attribute) || mesh.GetVertexAttributeDimension(attribute) < 2) throw new InvalidOperationException("Pilot has no UV coordinates.");
        var format = mesh.GetVertexAttributeFormat(attribute);
        if (format != VertexAttributeFormat.Float32 && format != VertexAttributeFormat.Float16) throw new InvalidOperationException("Unsupported pilot UV format.");
        var stream = mesh.GetVertexAttributeStream(attribute); var stride = mesh.GetVertexBufferStride(stream);
        var offset = mesh.GetVertexAttributeOffset(attribute); var half = format == VertexAttributeFormat.Float16; var size = half ? 2 : 4;
        using var buffer = mesh.GetVertexBuffer(stream);
        if (buffer == null || (long)buffer.count * buffer.stride > 8 * 1024 * 1024) throw new InvalidOperationException("Pilot UV buffer exceeds budget.");
        var bytes = new byte[buffer.count * buffer.stride]; buffer.GetData(bytes);
        if ((long)(mesh.vertexCount - 1) * stride + offset + size * 2 > bytes.Length) throw new InvalidOperationException("Truncated pilot UV buffer.");
        var uv = new Vector2[mesh.vertexCount];
        for (var i = 0; i < uv.Length; i++)
        { var at = i * stride + offset; uv[i] = half ? new Vector2(Mathf.HalfToFloat(BitConverter.ToUInt16(bytes, at)), Mathf.HalfToFloat(BitConverter.ToUInt16(bytes, at + 2))) : new Vector2(BitConverter.ToSingle(bytes, at), BitConverter.ToSingle(bytes, at + 4)); }
        return uv;
    }
    internal static int[][] Indices(Mesh mesh)
    {
        var result = new int[mesh.subMeshCount][];
        byte[]? bytes = null;
        if (!mesh.isReadable)
        {
            using var buffer = mesh.GetIndexBuffer();
            if (buffer == null || (long)buffer.count * buffer.stride > 8 * 1024 * 1024) throw new InvalidOperationException("Pilot index buffer exceeds budget.");
            bytes = new byte[buffer.count * buffer.stride]; buffer.GetData(bytes);
        }
        for (var s = 0; s < result.Length; s++)
        {
            if (mesh.isReadable) { result[s] = mesh.GetTriangles(s); continue; }
            var sub = mesh.GetSubMesh(s); var size = mesh.indexFormat == IndexFormat.UInt16 ? 2 : 4;
            result[s] = new int[sub.topology == MeshTopology.Triangles ? sub.indexCount : 0];
            for (var i = 0; i < result[s].Length; i++)
            { var at = (sub.indexStart + i) * size; result[s][i] = (size == 2 ? BitConverter.ToUInt16(bytes!, at) : checked((int)BitConverter.ToUInt32(bytes!, at))) + sub.baseVertex; }
        }
        return result;
    }
}
