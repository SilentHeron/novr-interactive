using System;
using System.Collections.Generic;

namespace NOVR.VrUi.Controllers;

// What a SteamVR render model's description says about its parts: where each
// button sits, and where the OpenXR grip pose is. The model's own coordinates
// are OpenVR's (right-handed, -Z forward); everything handed out here is in
// OpenXR grip space with Unity's axes, which is the space the tracked hand
// pose is in. The quick wheel's thumbstick offsets were derived from the same
// files in the same way.
internal sealed class RenderModelInfo
{
    internal readonly struct Part
    {
        internal readonly string Name;
        internal readonly float X, Y, Z;
        internal Part(string name, float x, float y, float z)
        {
            Name = name;
            X = x;
            Y = y;
            Z = z;
        }
    }

    private readonly float _gripX, _gripY, _gripZ, _cos, _sin;
    // Parts that have geometry of their own (buttons, sticks, triggers), with
    // their origin in grip space.
    internal readonly IReadOnlyList<Part> Parts;

    private RenderModelInfo(float gripX, float gripY, float gripZ, float pitchDegrees, List<(string name, float x, float y, float z)> parts)
    {
        _gripX = gripX;
        _gripY = gripY;
        _gripZ = gripZ;
        var radians = pitchDegrees * Math.PI / 180;
        _cos = (float)Math.Cos(radians);
        _sin = (float)Math.Sin(radians);
        var list = new List<Part>();
        foreach (var part in parts)
        {
            ToGrip(part.x, part.y, part.z, out var x, out var y, out var z);
            list.Add(new Part(part.name, x, y, z));
        }
        Parts = list;
    }

    // A point in the model's coordinates, expressed in grip space.
    internal void ToGrip(float x, float y, float z, out float gripX, out float gripY, out float gripZ)
    {
        x -= _gripX;
        y -= _gripY;
        z -= _gripZ;
        gripX = x;
        gripY = y * _cos + z * _sin;
        gripZ = -(-y * _sin + z * _cos);
    }

    internal static RenderModelInfo Parse(string json)
    {
        if (MiniJson.Parse(json) is not Dictionary<string, object?> root ||
            !root.TryGetValue("components", out var value) || value is not Dictionary<string, object?> components)
            throw new FormatException("Render model description has no components.");
        if (!components.TryGetValue("openxr_grip", out var gripValue) || gripValue is not Dictionary<string, object?> grip ||
            !Local(grip, out var origin, out var rotation))
            throw new FormatException("Render model description has no OpenXR grip pose.");
        // Every controller model seen so far only pitches its grip. Anything
        // else would need a full rotation here, so refuse rather than misplace.
        if (Math.Abs(rotation[1]) > .01f || Math.Abs(rotation[2]) > .01f)
            throw new FormatException("Render model grip pose is rotated on more than one axis.");
        var parts = new List<(string, float, float, float)>();
        foreach (var pair in components)
        {
            if (pair.Value is not Dictionary<string, object?> component || !component.TryGetValue("filename", out var file) ||
                file is not string || !Local(component, out var at, out _))
                continue;
            parts.Add((pair.Key, at[0], at[1], at[2]));
        }
        return new RenderModelInfo(origin[0], origin[1], origin[2], rotation[0], parts);
    }

    private static bool Local(Dictionary<string, object?> component, out float[] origin, out float[] rotation)
    {
        origin = rotation = new float[3];
        return component.TryGetValue("component_local", out var value) && value is Dictionary<string, object?> local &&
            Triple(local, "origin", out origin) && Triple(local, "rotate_xyz", out rotation);
    }

    private static bool Triple(Dictionary<string, object?> map, string key, out float[] result)
    {
        result = new float[3];
        if (!map.TryGetValue(key, out var value) || value is not List<object?> list || list.Count != 3)
            return false;
        for (var i = 0; i < 3; i++)
        {
            if (list[i] is not double number || Math.Abs(number) > 1000)
                return false;
            result[i] = (float)number;
        }
        return true;
    }
}
