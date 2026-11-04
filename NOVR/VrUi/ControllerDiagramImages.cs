using System;
using System.Collections.Generic;
using System.IO;
using NOVR.MotionControls;
using UnityEngine;

namespace NOVR.VrUi;

// Both original diagram sheets remain unchanged. UV crops select a physical
// hand; the supplied black Touch drawing is displayed on a separate light card.
internal static class ControllerDiagramImages
{
    private static readonly Dictionary<string, Texture2D?> Textures = new();
    internal static Vector2 Position(bool frame, bool side) => !frame ? Vector2.zero : side ? new Vector2(82, -89) : new Vector2(-40, 8);
    internal static Vector2 Size(bool frame, bool side) => !frame ? new Vector2(170, 170f * 371 / 215) :
        side ? new Vector2(76, 76f * 241 / 202) : new Vector2(160, 160f * 348 / 202);
    internal static string File(bool frame, bool left, bool side) => frame ? "frame-views.png" : "touch-diagram.png";
    internal static Rect Region(bool frame, bool left, bool side)
    {
        if (!frame) return new Rect(left ? 0 : 585f / 800, 0, 215f / 800, 1);
        var x = side ? (left ? 122 : 86) : (left ? 1262 : 1272);
        var y = side ? (left ? 1210 : 438) : (left ? 768 : 31);
        var height = side ? 241 : 348;
        return new Rect(x / 2195f, 1 - (y + height) / 1492f, 202 / 2195f, height / 1492f);
    }
    internal static Vector2 Endpoint(bool frame, bool left, ControllerButton? button)
    {
        var side = frame && (button == ControllerButton.Trigger || button == ControllerButton.Shoulder);
        var point = frame ? FramePoint(left, button) : TouchPoint(left, button);
        var size = Size(frame, side);
        return Position(frame, side) + new Vector2((point.x - .5f) * size.x, (.5f - point.y) * size.y);
    }
    private static Vector2 FramePoint(bool left, ControllerButton? button)
    {
        // Measured against Valve's original front/top and underside drawings.
        if (button == ControllerButton.Trigger) return new Vector2(left ? 106.25f / 202 : 100f / 202, 133f / 241);
        if (button == ControllerButton.Shoulder) return new Vector2(left ? 103f / 202 : 100f / 202, 86f / 241);
        var point = button switch
        {
            ControllerButton.Menu => new Vector2(left ? 101.25f : 100.625f, 67.5f),
            ControllerButton.Top => new Vector2(left ? 58.75f : 144.375f, left ? 79.375f : 78.125f),
            ControllerButton.Outside => new Vector2(left ? 35.625f : 168.75f, 103.125f),
            ControllerButton.Secondary => new Vector2(left ? 78.75f : 120f, 103.125f),
            ControllerButton.Primary => new Vector2(left ? 58.75f : 144.375f, left ? 123.75f : 127.5f),
            ControllerButton.StickClick => new Vector2(left ? 135.625f : 66.25f, 121.875f),
            _ => new Vector2(left ? 141.25f : 60f, 204f)
        };
        return new Vector2(point.x / 202, point.y / 348);
    }
    private static Vector2 TouchPoint(bool left, ControllerButton? button)
    {
        // Pixel centers measured on the supplied 800x371 outline diagram.
        // Right-hand x values are relative to its crop beginning at x=585.
        var point = button switch
        {
            ControllerButton.Primary => new Vector2(left ? 116 : 99, 153),
            ControllerButton.Secondary => new Vector2(left ? 133 : 82, 106),
            ControllerButton.StickClick => new Vector2(left ? 67 : 149, 118),
            ControllerButton.Menu => new Vector2(left ? 74 : 141, 183),
            ControllerButton.Trigger => new Vector2(108, 12),
            _ => new Vector2(left ? 145 : 70, 239)
        };
        return new Vector2(point.x / 215, point.y / 371);
    }
    internal static Texture2D? Get(bool frame, bool left, bool side)
    {
        var file = File(frame, left, side);
        if (Textures.TryGetValue(file, out var cached)) return cached;
        Texture2D? texture = null;
        try
        {
            using var stream = typeof(ControllerDiagramImages).Assembly.GetManifestResourceStream("NOVR.Controllers." + file);
            if (stream == null) throw new InvalidDataException("Missing controller artwork: " + file);
            using var bytes = new MemoryStream(); stream.CopyTo(bytes);
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = file, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            if (!ImageConversion.LoadImage(texture, bytes.ToArray(), true)) throw new InvalidDataException("Invalid controller artwork: " + file);
        }
        catch (Exception e)
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null; Debug.LogWarning("[NOVR] " + e.Message);
        }
        Textures[file] = texture; return texture;
    }
}
