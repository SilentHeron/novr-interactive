using NOVR.Comfort;
using UnityEngine;

internal static class MaterialChecks
{
    internal static int Run()
    {
        var passed = 0;
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            passed++;
        }
        bool Eligible(Material material) => SeatMaterialPolicy.AreEligible(new[] { material }, 1);
        var native = new Material();
        Check(Eligible(native), "Separate native Seats/URP Lit opaque material remains supported.");
        Check(native.shader!.name == "Universal Render Pipeline/Lit" && native.renderQueue == 2000 &&
            native.Floats["_Surface"] == 0 && native.Keywords.Count == 0,
            "Eligibility never alters the native shader, queue, properties or keywords.");
        foreach (var keyword in new[] { "_SURFACE_TYPE_TRANSPARENT", "_ALPHABLEND_ON", "_ALPHAPREMULTIPLY_ON" })
        {
            var material = new Material();
            material.Keywords.Add(keyword);
            Check(!Eligible(material), "Transparent keyword rejects a nominally opaque seat: " + keyword);
        }
        foreach (var name in new[] { "Glass", "canopy_int", "window_L", "windscreen", "Windshield", "visor", "TransparentSeat" })
        {
            Check(!Eligible(new Material { name = name }), "Window material cannot inherit a seat object's name: " + name);
            Check(!Eligible(new Material { shader = new Shader { name = "Shader Graphs/" + name } }),
                "Window shader cannot qualify through a misleading _Surface=0 property: " + name);
        }
        foreach (var value in new[] { 1f, .5f, float.NaN, float.PositiveInfinity })
        {
            var material = new Material();
            material.Floats["_Surface"] = value;
            Check(!Eligible(material), "Only a finite opaque surface value qualifies.");
        }
        Check(!Eligible(new Material { renderQueue = 2950 }), "Ibis glass queue fails closed even without glass naming.");
        Check(!Eligible(new Material { RenderType = "Transparent" }), "Transparent shader tag vetoes otherwise opaque metadata.");
        Check(!Eligible(new Material { RenderType = "" }), "Unknown render type remains untouched.");
        Check(!Eligible(new Material { shader = null }), "Missing shader remains untouched.");
        Check(!SeatMaterialPolicy.AreEligible(new Material[] { null! }, 1), "Missing material remains untouched.");
        Check(!SeatMaterialPolicy.AreEligible(Array.Empty<Material>(), 1), "A mesh with no material mapping stays intact.");
        Check(!SeatMaterialPolicy.AreEligible(new[] { native }, 2), "An underspecified submesh mapping stays intact.");
        Check(!SeatMaterialPolicy.AreEligible(new[] { native, native }, 1), "An extra redraw material cannot hide an unknown pass.");
        Check(SeatMaterialPolicy.AreEligible(new[] { native, native }, 2), "Two independently opaque seat submeshes qualify.");
        var glass = new Material { name = "UtilityHelo1_glass_int", shader = new Shader { name = "Shader Graphs/canopy_int" }, renderQueue = 2950 };
        Check(!SeatMaterialPolicy.AreEligible(new[] { native, glass }, 2), "Mixed seat and glass submeshes leave the whole renderer untouched.");
        Check(!SeatMaterialPolicy.AreEligible(new[] { glass, native }, 2), "Material order cannot bypass mixed-mesh protection.");
        return passed;
    }
}
