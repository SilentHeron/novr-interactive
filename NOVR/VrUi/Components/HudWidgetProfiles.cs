using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Persisted coordinates use the original pilot viewpoint. Factory offsets are
// first centered on the tracked seated user; the result is then aircraft-fixed.
// Later seat adjustments and recentering never translate a saved layout.
internal struct HudWidgetPose
{
    internal Vector3 Position, Euler;
    internal float Scale, Opacity, Background, Aspect;
    internal bool Visible;
    internal HudWidgetPose(Vector3 position, Vector3 euler, float scale = 1, float opacity = 1, float background = 0, bool visible = true, float aspect = 1)
    {
        Position = position;
        Euler = euler;
        Scale = scale;
        Opacity = opacity;
        Background = background;
        Visible = visible;
        Aspect = aspect;
    }
}

internal sealed class HudWidgetProfiles
{
    internal const float MinScale = .35f, MaxScale = 3f, MinOpacity = .25f;
    internal static readonly string[] Ids = HudWidgetCatalog.CreateIds();
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private readonly ConfigFile _config;
    private readonly Dictionary<(string key, int slot), AircraftProfile> _aircraft = new();
    private sealed class AircraftProfile
    {
        internal ConfigEntry<bool> Override = null!;
        internal Dictionary<string, ConfigEntry<string>> Poses = null!;
    }

    internal HudWidgetProfiles(ConfigFile config)
    {
        _config = config;
    }
    private static readonly Dictionary<string, HudWidgetPose> Factory = HudWidgetCatalog.CreateDefaults();
    private static readonly Dictionary<string, HudWidgetPose> LegacyFactory = new(StringComparer.Ordinal)
    {
        ["weapons"] = new(new Vector3(.34f, -.34f, .75f), new Vector3(0, 12, 0)),
        ["countermeasures"] = new(new Vector3(-.34f, -.32f, .75f), new Vector3(0, -12, 0)),
        ["power"] = new(new Vector3(-.34f, -.43f, .75f), new Vector3(0, -12, 0)),
        ["threats"] = new(new Vector3(-.27f, -.12f, .85f), new Vector3(0, -12, 0)),
        ["ew"] = new(new Vector3(-.54f, -.31f, .85f), new Vector3(0, -20, 0)),
        ["feed"] = new(new Vector3(.27f, -.12f, .85f), new Vector3(0, 12, 0))
    };
    internal static Dictionary<string, HudWidgetPose> FactoryDefaults => new(Factory, StringComparer.Ordinal);
    internal static string Label(string id) => HudWidgetCatalog.Label(id);
    internal Dictionary<string, HudWidgetPose> Load(string? aircraftKey, int slot = 1)
    {
        ValidateSlot(slot);
        if (string.IsNullOrEmpty(aircraftKey))
            return FactoryDefaults;
        var aircraft = Aircraft(aircraftKey!, slot);
        return aircraft.Override.Value ? Read(aircraft.Poses, FactoryDefaults) : FactoryDefaults;
    }
    internal bool HasOverride(string? aircraftKey, int slot = 1)
    {
        ValidateSlot(slot);
        return !string.IsNullOrEmpty(aircraftKey) && Aircraft(aircraftKey!, slot).Override.Value;
    }
    internal bool HasSavedLayout(string? aircraftKey, int slot = 1) => HasOverride(aircraftKey, slot);
    internal void Save(string aircraftKey, Dictionary<string, HudWidgetPose> poses, int slot = 1)
    {
        ValidateSlot(slot);
        if (string.IsNullOrEmpty(aircraftKey))
            return;
        var snapshot = Merge(Load(aircraftKey, slot), poses);
        var aircraft = Aircraft(aircraftKey, slot);
        Store(aircraft.Poses, snapshot);
        aircraft.Override.Value = true;
        _config.Save();
    }
    internal void ResetAircraft(string aircraftKey, int slot = 1)
    {
        ValidateSlot(slot);
        if (string.IsNullOrEmpty(aircraftKey))
            return;
        var profile = Aircraft(aircraftKey, slot);
        Store(profile.Poses, FactoryDefaults);
        profile.Override.Value = false;
        _config.Save();
    }
    private AircraftProfile Aircraft(string key, int slot)
    {
        if (_aircraft.TryGetValue((key, slot), out var profile))
            return profile;
        // Slot1 deliberately keeps the pre-slot per-aircraft section. Shared
        // defaults are not read or imported into any aircraft/slot.
        var section = Section(key) + (slot == 1 ? "" : " Slot " + slot);
        profile = new AircraftProfile
        {
            Override = _config.Bind(section, "Use aircraft layout", false,
                "Saved HUD layout for " + key + ", slot " + slot + ". False initializes reachable defaults around the tracked seated user for this slot only."),
            Poses = BindPoses(section)
        };
        _aircraft.Add((key, slot), profile);
        return profile;
    }
    private static void ValidateSlot(int slot)
    {
        if (slot < 1 || slot > 3)
            throw new ArgumentOutOfRangeException(nameof(slot), "Layout slot must be 1, 2 or 3.");
    }
    private Dictionary<string, ConfigEntry<string>> BindPoses(string section)
    {
        var entries = new Dictionary<string, ConfigEntry<string>>(StringComparer.Ordinal);
        var factory = FactoryDefaults;
        foreach (var id in Ids)
            entries.Add(id, _config.Bind(section, id, Encode(factory[id]),
                Label(id) + ": original cockpit x/y/z, pitch/yaw/roll, scale, opacity, background, visible; optional map aspect ratio. Managed by View > Edit layout."));
        MigrateUntouchedDefaults(section, entries);
        return entries;
    }
    private void MigrateUntouchedDefaults(string section, Dictionary<string, ConfigEntry<string>> entries)
    {
        var applied = _config.Bind(section, "Compact reachable defaults applied", false,
            "One-time update of untouched 0.4.7.11 widgets to smaller, closer, forward-facing positions. Custom values are preserved.");
        if (applied.Value)
            return;
        var changed = false;
        foreach (var legacy in LegacyFactory)
        {
            // Exact stored stock values only. Even appearance-only customizations
            // retain their complete pose; later intentional stock restores are
            // protected by the per-section migration flag.
            if (entries[legacy.Key].Value != Encode(legacy.Value))
                continue;
            entries[legacy.Key].Value = Encode(Factory[legacy.Key]);
            changed = true;
        }
        applied.Value = true;
        if (changed)
            _config.Save();
    }
    private static Dictionary<string, HudWidgetPose> Read(Dictionary<string, ConfigEntry<string>> entries, Dictionary<string, HudWidgetPose> fallback)
    {
        var result = new Dictionary<string, HudWidgetPose>(StringComparer.Ordinal);
        foreach (var id in Ids)
            result.Add(id, Decode(id, entries[id].Value, fallback[id]));
        return result;
    }
    private static Dictionary<string, HudWidgetPose> Merge(Dictionary<string, HudWidgetPose> current, Dictionary<string, HudWidgetPose> changed)
    {
        foreach (var id in Ids)
            if (changed.TryGetValue(id, out var pose))
                current[id] = Sanitize(id, pose);
        return current;
    }
    private static void Store(Dictionary<string, ConfigEntry<string>> entries, Dictionary<string, HudWidgetPose> poses)
    {
        foreach (var id in Ids)
            entries[id].Value = Encode(Sanitize(id, poses[id]));
    }
    internal static HudWidgetPose Sanitize(string id, HudWidgetPose pose)
    {
        var fallback = Factory.TryGetValue(id, out var factory) ? factory : new HudWidgetPose(new Vector3(0, -.2f, .8f), Vector3.zero);
        pose.Position = new Vector3(Bound(pose.Position.x, -3, 3, fallback.Position.x),
            Bound(pose.Position.y, -3, 3, fallback.Position.y), Bound(pose.Position.z, -.5f, 5, fallback.Position.z));
        pose.Euler = new Vector3(HudWidgetAngleMath.Pitch(Angle(pose.Euler.x, fallback.Euler.x)),
            HudWidgetAngleMath.Yaw(pose.Euler.y, fallback.Euler.y), 0);
        pose.Scale = Bound(pose.Scale, MinScale, MaxScale, fallback.Scale);
        pose.Aspect = id == "map" ? HudWidgetShape.Aspect(pose.Aspect) : 1;
        HudWidgetShape.Normalize(ref pose.Scale, ref pose.Aspect, fallback.Scale);
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
        p.Scale.ToString("R", Invariant), p.Opacity.ToString("R", Invariant), p.Background.ToString("R", Invariant), p.Visible ? "1" : "0" }) +
        (p.Aspect == 1 ? "" : "|" + p.Aspect.ToString("R", Invariant));
    private static HudWidgetPose Decode(string id, string text, HudWidgetPose fallback)
    {
        var parts = (text ?? "").Split('|');
        if (parts.Length != 10 && parts.Length != 11)
            return fallback;
        var values = new float[9];
        for (var i = 0; i < values.Length; i++)
            if (!float.TryParse(parts[i], NumberStyles.Float, Invariant, out values[i]))
                return fallback;
        if (parts[9] != "0" && parts[9] != "1")
            return fallback;
        var aspect = 1f;
        if (parts.Length == 11 && !float.TryParse(parts[10], NumberStyles.Float, Invariant, out aspect))
            aspect = 1;
        return Sanitize(id, new HudWidgetPose(new Vector3(values[0], values[1], values[2]), new Vector3(values[3], values[4], values[5]),
            values[6], values[7], values[8], parts[9] == "1", aspect));
    }
    private static string Section(string key)
    {
        using var hash = SHA256.Create();
        return "VR HUD Widgets " + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").Substring(0, 32);
    }
}
