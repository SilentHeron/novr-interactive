using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NOVR.MotionControls;

internal static class CockpitMaterials
{
    internal static void Fs20Finish(Material material, Material? cockpit, Color tint)
    {
        if (cockpit == null || !cockpit.HasProperty("_BaseMap") || !material.HasProperty("_BaseMap")) return;
        var paint = cockpit.GetTexture("_BaseMap");
        // Only use the known local FS-20 atlas. This unlettered paint region
        // keeps existing cockpit legends/warnings off the new keycaps. Shared
        // native textures are referenced, never copied/read back or modified.
        if (paint == null || !paint.name.StartsWith("smallFighter1_cockpit", StringComparison.OrdinalIgnoreCase)) return;
        material.SetTexture("_BaseMap", paint);
        material.SetTextureOffset("_BaseMap",new Vector2(.185f,.43f));
        material.SetTextureScale("_BaseMap",new Vector2(.06f,.12f));
        material.SetColor("_BaseColor",tint);
        if (cockpit.HasProperty("_BumpMap") && material.HasProperty("_BumpMap") && cockpit.GetTexture("_BumpMap") != null)
        {
            material.SetTexture("_BumpMap",cockpit.GetTexture("_BumpMap"));
            material.SetFloat("_BumpScale",.2f); material.EnableKeyword("_NORMALMAP");
        }
        material.SetFloat("_Smoothness",.2f); // Restrained highlights behind the white legends.
    }
    internal static Material Hardware(string name, Color color, float metallic = .15f)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return Opaque(name, color);
        var material = new Material(shader) { name = name, enableInstancing = true };
        material.SetColor("_BaseColor", color); material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        material.SetFloat("_Metallic", metallic); material.SetFloat("_Smoothness", .32f);
        material.SetFloat("_Surface", 0); material.SetFloat("_ZWrite", 1);
        material.renderQueue = (int)RenderQueue.Geometry;
        return material;
    }
    // The game's stripped shader collection contains Particles/Unlit, but not
    // necessarily URP/Unlit. Keep solid controls in the depth-writing pass.
    internal static Material Opaque(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit") ??
                     Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");
        if (shader == null) throw new InvalidOperationException("No supported opaque cockpit-control shader is available.");
        var material = new Material(shader) { name = name, enableInstancing = true };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", Texture2D.whiteTexture);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.One);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.Zero);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Back);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Geometry;
        return material;
    }
}
