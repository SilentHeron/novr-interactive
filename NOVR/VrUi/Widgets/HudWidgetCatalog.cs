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
        string preview, string idle = "", float scale = .65f)
    {
        Id = id;
        Label = label;
        Size = size;
        DefaultPose = new HudWidgetPose(position, Vector3.zero, scale);
        Preview = preview;
        Idle = idle;
    }
}

// Stable IDs are persisted in aircraft profiles. Add a widget here, then wire
// its native source separately; source ownership/restoration is not UI metadata.
internal static class HudWidgetCatalog
{
    internal static readonly IReadOnlyList<HudWidgetDefinition> All = Array.AsReadOnly(new[]
    {
        new HudWidgetDefinition("weapons", "Weapons", new Vector2(420, 170), new Vector3(.28f, -.40f, .28f),
            "Selected weapon\nAmmunition · readiness", "WEAPONS\nNo readout"),
        new HudWidgetDefinition("countermeasures", "Countermeasures", new Vector2(260, 135), new Vector3(-.34f, -.21f, .38f),
            "Countermeasures\nCount · jammer charge", "COUNTERMEASURES\nNo readout"),
        new HudWidgetDefinition("power", "Capacitor", new Vector2(420, 80), new Vector3(0, -.43f, .28f),
            "Capacitor / charge", "CAPACITOR\nNo readout"),
        new HudWidgetDefinition("threats", "Incoming missiles", new Vector2(460, 190), new Vector3(-.17f, -.06f, .44f),
            "Incoming missiles\nBearing · distance · notch"),
        new HudWidgetDefinition("ew", "Radar / EW", new Vector2(260, 260), new Vector3(-.28f, -.40f, .28f),
            "Electronic warnings\nRadar / jamming bearings"),
        new HudWidgetDefinition("feed", "Notifications", new Vector2(460, 270), new Vector3(.17f, -.06f, .44f),
            "Kill feed\nMission notifications", scale: .60f),
        new HudWidgetDefinition("map", "Map", new Vector2(300, 300), new Vector3(.34f, -.21f, .38f),
            "Tactical map\nTerrain and units", "MAP\nNo readout"),
        new HudWidgetDefinition("damage", "Aircraft damage", new Vector2(300, 300), new Vector3(.12f, -.28f, .32f),
            "Aircraft damage\nParts and system status"),
        new HudWidgetDefinition("objectives", "Instructions", new Vector2(420, 150), new Vector3(-.12f, -.28f, .32f),
            "Instructions\nTaxi guidance and clearances")
    });

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
