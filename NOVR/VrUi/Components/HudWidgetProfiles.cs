using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Coordinates use the original pilot viewpoint. Moving the VR seat must never
// be included in these values: the widgets remain fixed in the aircraft.
internal struct HudWidgetPose
{
    internal Vector3 Position, Euler;
    internal float Scale, Opacity, Background;
    internal bool Visible;
    internal HudWidgetPose(Vector3 position, Vector3 euler, float scale = 1, float opacity = 1, float background = 0, bool visible = true)
    { Position = position; Euler = euler; Scale = scale; Opacity = opacity; Background = background; Visible = visible; }
}

internal sealed class HudWidgetProfiles
{
    internal const float MinScale = .35f, MaxScale = 3f, MinOpacity = .25f;
    internal static readonly string[] Ids = { "weapons", "countermeasures", "power", "threats", "ew", "feed" };
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private readonly ConfigFile _config;
    private readonly Dictionary<string, ConfigEntry<string>> _defaults;
    private readonly Dictionary<string, AircraftProfile> _aircraft = new(StringComparer.Ordinal);
    private sealed class AircraftProfile
    {
        internal ConfigEntry<bool> Override = null!;
        internal Dictionary<string, ConfigEntry<string>> Poses = null!;
    }

    internal HudWidgetProfiles(ConfigFile config)
    {
        _config = config;
        _defaults = BindPoses("VR HUD Widgets - Defaults");
    }
    private static readonly Dictionary<string, HudWidgetPose> Factory = new(StringComparer.Ordinal)
    {
        ["weapons"] = new(new Vector3(.34f, -.34f, .75f), new Vector3(0, 12, 0)),
        ["countermeasures"] = new(new Vector3(-.34f, -.32f, .75f), new Vector3(0, -12, 0)),
        ["power"] = new(new Vector3(-.34f, -.43f, .75f), new Vector3(0, -12, 0)),
        ["threats"] = new(new Vector3(-.27f, -.12f, .85f), new Vector3(0, -12, 0)),
        ["ew"] = new(new Vector3(-.54f, -.31f, .85f), new Vector3(0, -20, 0)),
        ["feed"] = new(new Vector3(.27f, -.12f, .85f), new Vector3(0, 12, 0))
    };
    internal static Dictionary<string, HudWidgetPose> FactoryDefaults => new(Factory, StringComparer.Ordinal);
    internal static string Label(string id) => id switch
    {
        "weapons" => "Weapons", "countermeasures" => "Countermeasures", "power" => "Capacitor",
        "threats" => "Incoming missiles", "ew" => "Radar / EW", "feed" => "Notifications", _ => "HUD widget"
    };
    internal Dictionary<string, HudWidgetPose> Load(string? aircraftKey)
    {
        var shared = Read(_defaults, FactoryDefaults);
        if (string.IsNullOrEmpty(aircraftKey)) return shared;
        var aircraft = Aircraft(aircraftKey!);
        return aircraft.Override.Value ? Read(aircraft.Poses, shared) : shared;
    }
    internal void Save(string aircraftKey, Dictionary<string, HudWidgetPose> poses)
    {
        if (string.IsNullOrEmpty(aircraftKey)) return;
        var snapshot = Merge(Load(aircraftKey), poses);
        var aircraft = Aircraft(aircraftKey);
        Store(aircraft.Poses, snapshot);
        aircraft.Override.Value = true;
        _config.Save();
    }
    internal void SaveDefault(Dictionary<string, HudWidgetPose> poses)
    {
        Store(_defaults, Merge(Load(null), poses));
        _config.Save();
    }
    internal void ResetAircraft(string aircraftKey)
    {
        if (string.IsNullOrEmpty(aircraftKey)) return;
        Aircraft(aircraftKey).Override.Value = false;
        _config.Save();
    }
    private AircraftProfile Aircraft(string key)
    {
        if (_aircraft.TryGetValue(key, out var profile)) return profile;
        var section = Section(key);
        profile = new AircraftProfile
        {
            Override = _config.Bind(section, "Use aircraft layout", false,
                "True uses this aircraft's saved HUD positions. False inherits the shared HUD layout."),
            Poses = BindPoses(section)
        };
        _aircraft.Add(key, profile);
        return profile;
    }
    private Dictionary<string, ConfigEntry<string>> BindPoses(string section)
    {
        var entries = new Dictionary<string, ConfigEntry<string>>(StringComparer.Ordinal);
        var factory = FactoryDefaults;
        foreach (var id in Ids)
            entries.Add(id, _config.Bind(section, id, Encode(factory[id]),
                Label(id) + ": original cockpit x/y/z, pitch/yaw/roll, scale, opacity, background, visible. Managed by View > HUD layout."));
        return entries;
    }
    private static Dictionary<string, HudWidgetPose> Read(Dictionary<string, ConfigEntry<string>> entries, Dictionary<string, HudWidgetPose> fallback)
    {
        var result = new Dictionary<string, HudWidgetPose>(StringComparer.Ordinal);
        foreach (var id in Ids) result.Add(id, Decode(id, entries[id].Value, fallback[id]));
        return result;
    }
    private static Dictionary<string, HudWidgetPose> Merge(Dictionary<string, HudWidgetPose> current, Dictionary<string, HudWidgetPose> changed)
    {
        foreach (var id in Ids)
            if (changed.TryGetValue(id, out var pose)) current[id] = Sanitize(id, pose);
        return current;
    }
    private static void Store(Dictionary<string, ConfigEntry<string>> entries, Dictionary<string, HudWidgetPose> poses)
    { foreach (var id in Ids) entries[id].Value = Encode(Sanitize(id, poses[id])); }
    internal static HudWidgetPose Sanitize(string id, HudWidgetPose pose)
    {
        var fallback = Factory.TryGetValue(id, out var factory) ? factory : new HudWidgetPose(new Vector3(0, -.2f, .8f), Vector3.zero);
        pose.Position = new Vector3(Bound(pose.Position.x, -3, 3, fallback.Position.x),
            Bound(pose.Position.y, -3, 3, fallback.Position.y), Bound(pose.Position.z, -.5f, 5, fallback.Position.z));
        pose.Euler = new Vector3(Angle(pose.Euler.x, fallback.Euler.x), Angle(pose.Euler.y, fallback.Euler.y), Angle(pose.Euler.z, fallback.Euler.z));
        pose.Scale = Bound(pose.Scale, MinScale, MaxScale, fallback.Scale);
        pose.Opacity = Bound(pose.Opacity, MinOpacity, 1, fallback.Opacity);
        pose.Background = Bound(pose.Background, 0, 1, fallback.Background);
        return pose;
    }
    private static float Bound(float value, float min, float max, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value));
    private static float Angle(float value, float fallback) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : (float)Math.IEEERemainder(value, 360);
    private static string Encode(HudWidgetPose p) => string.Join("|", new[] {
        p.Position.x.ToString("R", Invariant), p.Position.y.ToString("R", Invariant), p.Position.z.ToString("R", Invariant),
        p.Euler.x.ToString("R", Invariant), p.Euler.y.ToString("R", Invariant), p.Euler.z.ToString("R", Invariant),
        p.Scale.ToString("R", Invariant), p.Opacity.ToString("R", Invariant), p.Background.ToString("R", Invariant), p.Visible ? "1" : "0" });
    private static HudWidgetPose Decode(string id, string text, HudWidgetPose fallback)
    {
        var parts = (text ?? "").Split('|');
        if (parts.Length != 10) return fallback;
        var values = new float[9];
        for (var i = 0; i < values.Length; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, Invariant, out values[i])) return fallback;
        if (parts[9] != "0" && parts[9] != "1") return fallback;
        return Sanitize(id, new HudWidgetPose(new Vector3(values[0], values[1], values[2]), new Vector3(values[3], values[4], values[5]),
            values[6], values[7], values[8], parts[9] == "1"));
    }
    private static string Section(string key)
    {
        using var hash = SHA256.Create();
        return "VR HUD Widgets " + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").Substring(0, 32);
    }
}
