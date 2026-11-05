using System;
using System.Collections.Generic;
using UnityEngine;

namespace NOVR.Comfort;

// Names establish seat ownership; materials can only veto it. In particular,
// native canopy shaders report _Surface = 0 despite rendering in a glass queue.
internal static class SeatMaterialPolicy
{
    internal static bool AreEligible(IReadOnlyList<Material> materials, int subMeshes)
    {
        // Extra material slots redraw the last submesh. Missing slots and mixed
        // seat/window renderers are ambiguous, so leave their entire mesh intact.
        if (subMeshes < 1 || materials.Count != subMeshes)
            return false;
        for (var i = 0; i < materials.Count; i++)
        {
            var material = materials[i];
            if (material == null || material.shader == null ||
                IsWindow(material.name) || IsWindow(material.shader.name) ||
                material.renderQueue < 0 || material.renderQueue > 2500 ||
                material.GetTag("RenderType", false, "") != "Opaque" ||
                material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") ||
                material.IsKeywordEnabled("_ALPHABLEND_ON") ||
                material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") ||
                (material.HasProperty("_Surface") && material.GetFloat("_Surface") != 0))
                return false;
        }
        return true;
    }

    private static bool IsWindow(string name) =>
        name.IndexOf("glass", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("canopy", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("window", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("windscreen", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("windshield", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("visor", StringComparison.OrdinalIgnoreCase) >= 0 ||
        name.IndexOf("transparent", StringComparison.OrdinalIgnoreCase) >= 0;
}
