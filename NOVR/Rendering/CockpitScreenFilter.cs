using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace NOVR.Rendering;

// The cockpit's tactical screen and target window are cameras drawing into
// render textures that the game ships with no mipmaps and with anisotropic
// filtering switched off (level 0, which the forced 16x game setting skips).
// On a monitor the screens are small and seen square-on. In a headset they are
// seen at every size and angle, and their text and lines crawl as the head
// moves. What the game shipped is put back when VR stops or the option is off.
//
// Every screen gets anisotropic filtering. The tactical screen, which only an
// interface camera draws, also gets mipmaps, and that takes two changes the
// engine forces (both checked in the running game):
// - A multisampled render texture gets no mipmap levels at all, so the screen
//   gives up its 2x multisampling. Its lower levels are averages of four or
//   more pixels, which smooth edges better whenever the screen is shrunk.
// - The device's mipmap generation averages 8-bit sRGB values as plain
//   numbers, which would dim thin bright symbols on the dark screen to less
//   than half. Half-float colour is linear, so the same generation is correct.
// The target window shows the outside world and keeps its format: a float
// target would let highlights through that the 8-bit one clamps.
internal static class CockpitScreenFilter
{
    internal const int AnisoLevel = 8;
    internal const GraphicsFormat MipmappedFormat = GraphicsFormat.R16G16B16A16_SFloat;

    private sealed class Screen
    {
        internal readonly bool InterfaceOnly;
        internal readonly int AntiAliasing, AnisoLevel;
        internal readonly GraphicsFormat Format;
        internal readonly bool UseMipMap, AutoGenerateMips;
        internal readonly FilterMode FilterMode;
        internal bool Filtered;
        internal Screen(RenderTexture texture, bool interfaceOnly)
        {
            InterfaceOnly = interfaceOnly;
            AntiAliasing = texture.antiAliasing;
            Format = texture.graphicsFormat;
            UseMipMap = texture.useMipMap;
            AutoGenerateMips = texture.autoGenerateMips;
            FilterMode = texture.filterMode;
            AnisoLevel = texture.anisoLevel;
        }
    }

    private static readonly Dictionary<RenderTexture, Screen> Screens = new();
    private static readonly List<RenderTexture> Gone = new();
    private static bool _wanted, _warned;

    // A screen texture the game has just bound to one of its cockpit cameras.
    // interfaceOnly: nothing but interface graphics is ever drawn into it.
    internal static void Track(RenderTexture? texture, bool interfaceOnly)
    {
        if (texture == null || Screens.ContainsKey(texture))
            return;
        var screen = new Screen(texture, interfaceOnly);
        Screens.Add(texture, screen);
        if (_wanted)
            Apply(texture, screen);
    }

    internal static void Sync(bool wanted)
    {
        _wanted = wanted;
        Gone.Clear();
        foreach (var pair in Screens)
        {
            if (pair.Key == null)
                Gone.Add(pair.Key!);
            else if (wanted)
                Apply(pair.Key, pair.Value);
            else
                Restore(pair.Key, pair.Value);
        }
        foreach (var texture in Gone)
            Screens.Remove(texture);
        Gone.Clear();
    }

    private static void Apply(RenderTexture texture, Screen screen)
    {
        if (screen.Filtered)
            return;
        screen.Filtered = true;
        try
        {
            if (screen.InterfaceOnly && SystemInfo.IsFormatSupported(MipmappedFormat, FormatUsage.Render))
            {
                var created = texture.IsCreated();
                if (Rebuild(texture, 1, MipmappedFormat, true, true))
                    texture.filterMode = FilterMode.Trilinear;
                else
                {
                    // This device refused the mipmapped form: back to the
                    // shipped one, which keeps anisotropic filtering below.
                    Rebuild(texture, screen.AntiAliasing, screen.Format, screen.UseMipMap, screen.AutoGenerateMips);
                    if (created && !texture.IsCreated())
                        texture.Create();
                    Warn("no mipmaps for " + texture.name);
                }
            }
            texture.anisoLevel = AnisoLevel;
        }
        catch (Exception error)
        {
            Warn(error.Message);
        }
    }

    private static void Restore(RenderTexture texture, Screen screen)
    {
        if (!screen.Filtered)
            return;
        screen.Filtered = false;
        try
        {
            Rebuild(texture, screen.AntiAliasing, screen.Format, screen.UseMipMap, screen.AutoGenerateMips);
            texture.filterMode = screen.FilterMode;
            texture.anisoLevel = screen.AnisoLevel;
        }
        catch (Exception error)
        {
            Warn(error.Message);
        }
    }

    // Storage settings can only change while the texture has no GPU storage.
    // Its camera draws every frame, so the contents return at once. A texture
    // that had no storage yet is left for the engine to create when first used.
    private static bool Rebuild(RenderTexture texture, int antiAliasing, GraphicsFormat format, bool useMipMap, bool autoGenerateMips)
    {
        if (texture.antiAliasing == antiAliasing && texture.graphicsFormat == format &&
            texture.useMipMap == useMipMap && texture.autoGenerateMips == autoGenerateMips)
            return true;
        var created = texture.IsCreated();
        if (created)
            texture.Release();
        texture.antiAliasing = antiAliasing;
        texture.graphicsFormat = format;
        texture.useMipMap = useMipMap;
        texture.autoGenerateMips = autoGenerateMips;
        return !created || texture.Create();
    }

    private static void Warn(string detail)
    {
        if (_warned)
            return;
        _warned = true;
        Debug.LogWarning("[NOVR] Cockpit screen filtering is incomplete: " + detail);
    }
}

[HarmonyPatch(typeof(TacScreen), nameof(TacScreen.Initialize))]
internal static class TacScreenFilterPatch
{
    // The screen's own camera draws its interface canvas and nothing else.
    [HarmonyPostfix]
    private static void Postfix(Camera ___cam) =>
        CockpitScreenFilter.Track(___cam != null ? ___cam.targetTexture : null, interfaceOnly: true);
}
