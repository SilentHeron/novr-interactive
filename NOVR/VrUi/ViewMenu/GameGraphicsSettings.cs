using System;
using NuclearOption.Effects;
using UnityEngine;

namespace NOVR.VrUi;

// Use the game's existing setters: they apply graphics changes and own their
// persisted keys. Reading or opening the menu never applies a preset/default.
internal static class GameGraphicsSettings
{
    internal static bool IsAvailable => PlayerSettings.graphics != null && PlayerSettings.DetailSettings != null;
    internal static float TreeMinimum => DetailSettings.TreeRangeMultiplierMin;
    internal static float TreeMaximum => DetailSettings.TreeRangeMultiplierMax;
    internal static string[] ShadowOptions => GraphicsHelper.ShadowQualityOptions.ToArray();
    internal static float TreeDistance => PlayerSettings.DetailSettings?.TreeRangeMultiplier ?? 1f;
    internal static float CloudDetail => PlayerSettings.graphics?.CloudDetail ?? 1f;
    internal static int ShadowQuality => PlayerSettings.graphics?.ShadowQuality ?? -1;

    internal static void SetTreeDistance(float value)
    {
        if (!IsAvailable || !Finite(value))
            return;
        value = Math.Max(TreeMinimum, Math.Min(TreeMaximum, value));
        if (PlayerSettings.DetailSettings.TreeRangeMultiplier == value)
            return;
        PlayerSettings.DetailSettings.TreeRangeMultiplier = value;
        PlayerPrefs.Save();
    }

    internal static void SetCloudDetail(float value)
    {
        if (!IsAvailable || !Finite(value))
            return;
        value = Math.Max(0f, Math.Min(1f, value));
        if (PlayerSettings.graphics.CloudDetail == value)
            return;
        PlayerSettings.graphics.CloudDetail = value;
        PlayerPrefs.Save();
    }

    internal static void SetShadowQuality(int value)
    {
        if (!IsAvailable || value < 0 || value >= GraphicsHelper.ShadowQualityOptions.Count ||
            PlayerSettings.graphics.ShadowQuality == value)
            return;
        PlayerSettings.graphics.ShadowQuality = value;
        PlayerPrefs.Save();
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
