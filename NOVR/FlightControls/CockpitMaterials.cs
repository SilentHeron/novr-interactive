using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOVR.MotionControls;

internal static class CockpitMaterials
{
    // The game's stripped shader collection contains Particles/Unlit, but not
    // necessarily URP/Unlit. Keep solid controls in the depth-writing pass.
    internal static Material Opaque(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ??
                     Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
        if (shader == null)
            throw new InvalidOperationException("No supported opaque cockpit-control shader is available.");
        var material = new Material(shader) { name = name, enableInstancing = true };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Back);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Geometry;
        return material;
    }
}
