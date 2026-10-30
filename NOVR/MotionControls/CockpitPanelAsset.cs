using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace NOVR.MotionControls;

// Unity's JsonUtility can omit fields from runtime mod DTOs. Use the game's
// managed JSON reader and validate the embedded asset before creating objects.
internal sealed class CockpitPanelAsset
{
    internal sealed class Surface { public string name = ""; public float[] color = Array.Empty<float>(); }
    internal sealed class Group { public string name = ""; public float[] position = Array.Empty<float>(); }
    internal sealed class Part
    {
        public string name = "", group = "";
        public int material = 0;
        public float[] vertices = Array.Empty<float>();
        public int[] triangles = Array.Empty<int>();
    }
    public Surface[] materials = Array.Empty<Surface>();
    public Group[] groups = Array.Empty<Group>();
    public Part[] parts = Array.Empty<Part>();
    internal static CockpitPanelAsset Load(string json)
    {
        var asset = JsonConvert.DeserializeObject<CockpitPanelAsset>(json);
        if (asset == null || asset.materials == null || asset.groups == null || asset.parts == null ||
            asset.materials.Length == 0 || asset.groups.Length == 0 || asset.parts.Length == 0)
            throw new InvalidDataException("The cockpit console mesh asset is empty or incomplete.");
        foreach (var material in asset.materials)
            if (material == null || material.color == null || material.color.Length != 4 || !material.color.All(FlightControlMath.IsFinite))
                throw new InvalidDataException("Invalid cockpit console material.");
        foreach (var group in asset.groups)
            if (group == null || string.IsNullOrEmpty(group.name) || group.position == null || group.position.Length != 3 || !group.position.All(FlightControlMath.IsFinite))
                throw new InvalidDataException("Invalid cockpit console pivot.");
        var names = asset.groups.Select(g => g.name).ToArray();
        if (names.Distinct().Count() != names.Length) throw new InvalidDataException("Duplicate cockpit console pivot.");
        foreach (var part in asset.parts)
            if (part == null || !names.Contains(part.group) || part.material < 0 || part.material >= asset.materials.Length ||
                part.vertices == null || part.vertices.Length == 0 || part.vertices.Length % 3 != 0 || !part.vertices.All(FlightControlMath.IsFinite) ||
                part.triangles == null || part.triangles.Length == 0 || part.triangles.Length % 3 != 0 ||
                part.triangles.Any(i => i < 0 || i >= part.vertices.Length / 3))
                throw new InvalidDataException("Invalid cockpit console mesh geometry.");
        return asset;
    }
}
