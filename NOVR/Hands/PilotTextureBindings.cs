using UnityEngine;

namespace NOVR.MotionControls;

internal static class PilotTextureBindings
{
    private static readonly string[] Albedo = { "_BaseMap", "_MainTex" }, Normal = { "_BumpMap" };
    internal static void Replace(Material material, Texture original, Texture replacement, bool normal)
    {
        // A cropped atlas must retain its sampler transform, strength and
        // unrelated maps. Never replace a slot another owner has since changed.
        foreach(var property in normal ? Normal : Albedo)
            if(material.HasProperty(property) && material.GetTexture(property)==original)
                material.SetTexture(property,replacement);
    }
}
