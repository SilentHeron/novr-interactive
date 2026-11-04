using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace NOVR.MotionControls;

// Read back an instance for mirrored handles and shaft fades; never edit source assets.
internal static class NativeCockpitMesh
{
    internal static Mesh ReadableCopy(Mesh original)
    {
        if (original.isReadable) return Object.Instantiate(original);
        var copy = new Mesh { name = original.name + " (interactive instance)", indexFormat = original.indexFormat };
        try
        {
            copy.SetVertexBufferParams(original.vertexCount,original.GetVertexAttributes());
            for (var stream = 0; stream < original.vertexBufferCount; stream++)
            {
                using var buffer = original.GetVertexBuffer(stream);
                if (buffer == null || (long)buffer.count * buffer.stride > 16 * 1024 * 1024) throw new InvalidOperationException("Cockpit vertex buffer unavailable.");
                var bytes = new byte[buffer.count * buffer.stride]; buffer.GetData(bytes);
                copy.SetVertexBufferData(bytes,0,0,bytes.Length,stream,MeshUpdateFlags.DontRecalculateBounds);
            }
            using var indices = original.GetIndexBuffer();
            if (indices == null || (long)indices.count * indices.stride > 16 * 1024 * 1024) throw new InvalidOperationException("Cockpit index buffer unavailable.");
            var data = new byte[indices.count * indices.stride]; indices.GetData(data);
            copy.SetIndexBufferParams(data.Length / (original.indexFormat == IndexFormat.UInt16 ? 2 : 4),original.indexFormat);
            copy.SetIndexBufferData(data,0,0,data.Length,MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);
            copy.subMeshCount = original.subMeshCount;
            for (var s = 0; s < original.subMeshCount; s++) copy.SetSubMesh(s,original.GetSubMesh(s),MeshUpdateFlags.DontRecalculateBounds);
            copy.bounds = original.bounds; return copy;
        }
        catch { Object.Destroy(copy); throw; }
    }
}
