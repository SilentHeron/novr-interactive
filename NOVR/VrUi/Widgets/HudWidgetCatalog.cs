using System;
using System.Collections.Generic;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

internal readonly struct HudWidgetDefinition
{
    internal readonly string Id;
    internal readonly string Label;
    internal readonly Vector2 Size;
    internal readonly HudWidgetPose DefaultPose;
    internal readonly string Preview;
    internal readonly string Idle;

    internal HudWidgetDefinition(string id, string label, Vector2 size, Vector3 position,
        string preview, string idle = "", float scale = .65f, Vector3 rotation = default)
    {
        Id = id;
        Label = label;
        Size = size;
        DefaultPose = new HudWidgetPose(position, rotation, scale);
        Preview = preview;
        Idle = idle;
    }
}

// Stable IDs are persisted in aircraft profiles. Add a widget here, then wire
// its native source separately; source ownership/restoration is not UI metadata.
internal static class HudWidgetCatalog
{
    internal const float MetresPerPixel = .0007f;
    internal static readonly IReadOnlyList<HudWidgetDefinition> All = Array.AsReadOnly(ThreePlaneDefaults());

    private static HudWidgetDefinition[] ThreePlaneDefaults()
    {
        var widgets = new List<HudWidgetDefinition>();
        var rowTops = new float[3];
        // Three inward-facing planes below the seated user's eye line. Stacking
        // uses the rendered height, so changing a default size keeps row gaps.
        void Add(int column, string id, string label, Vector2 size, float scale, string preview, string idle = "")
        {
            var rotation = new Vector3(30, column * 35, 0);
            var height = size.y * MetresPerPixel * scale;
            var top = rowTops[column + 1];
            var origin = new Vector3(column * .31f, -.08f, column == 0 ? .45f : .37f);
            var position = origin + Quaternion.Euler(rotation) * new Vector3(0, -top - height * .5f, 0);
            widgets.Add(new HudWidgetDefinition(id, label, size, position, preview, idle, scale, rotation));
            rowTops[column + 1] = top + height + .018f;
        }

        // Left: threats and defensive systems.
        Add(-1, "threats", "Incoming missiles", new Vector2(460, 190), .70f,
            "Incoming missiles\nBearing · distance · notch");
        Add(-1, "ew", "Radar / EW", new Vector2(260, 260), .75f,
            "Electronic warnings\nRadar / jamming bearings");
        Add(-1, "countermeasures", "Countermeasures", new Vector2(260, 135), .80f,
            "Countermeasures\nCount · jammer charge", "COUNTERMEASURES\nNo readout");
        // Center: immediate instructions, weapons and aircraft controls.
        Add(0, "objectives", "Instructions", new Vector2(420, 150), .75f,
            "Instructions\nTaxi guidance and clearances");
        Add(0, "weapons", "Weapons", new Vector2(420, 170), .75f,
            "Selected weapon\nAmmunition · readiness", "WEAPONS\nNo readout");
        Add(0, "auxiliary", "Auxiliary controls", new Vector2(360, 220), .85f,
            "Wing sweep / tilt · nozzle angle\nNative fan control gauge");
        Add(0, "power", "Capacitor", new Vector2(420, 80), .70f,
            "Capacitor / charge", "CAPACITOR\nNo readout");
        // Right: reports, navigation and aircraft condition.
        Add(1, "feed", "Notifications", new Vector2(460, 270), .65f,
            "Kill feed\nMission notifications");
        Add(1, "map", "Map", new Vector2(300, 300), .85f,
            "Tactical map\nTerrain and units", "MAP\nNo readout");
        Add(1, "damage", "Aircraft damage", new Vector2(300, 300), .60f,
            "Aircraft damage\nParts and system status");
        return widgets.ToArray();
    }

    private static readonly Dictionary<string, HudWidgetDefinition> ById = Index();
    private static readonly HudWidgetDefinition Unknown = new("", "HUD widget", new Vector2(460, 270),
        new Vector3(0, -.2f, .8f), "Kill feed\nMission notifications", scale: 1f);

    private static Dictionary<string, HudWidgetDefinition> Index()
    {
        var result = new Dictionary<string, HudWidgetDefinition>(StringComparer.Ordinal);
        foreach (var definition in All)
            result.Add(definition.Id, definition);
        return result;
    }

    internal static HudWidgetDefinition Get(string id) => ById[id];

    // Keep the historical presentation fallback for callers with an unknown ID.
    // Registered widgets always have a complete definition in All.
    internal static HudWidgetDefinition Presentation(string id) => id != null && ById.TryGetValue(id, out var definition)
        ? definition : Unknown;

    internal static string[] CreateIds()
    {
        var ids = new string[All.Count];
        for (var index = 0; index < ids.Length; index++)
            ids[index] = All[index].Id;
        return ids;
    }

    internal static Dictionary<string, HudWidgetPose> CreateDefaults()
    {
        var result = new Dictionary<string, HudWidgetPose>(StringComparer.Ordinal);
        foreach (var definition in All)
            result.Add(definition.Id, definition.DefaultPose);
        return result;
    }

    internal static string Label(string id) => Presentation(id).Label;
}
