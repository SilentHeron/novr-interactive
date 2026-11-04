using System;
using UnityEngine;

namespace NOVR.VrUi.Native;

// The native preference boundary: retain assignment, persistence and notification
// order here while the panel owns row rendering and refresh. Graphics properties
// already apply their own values; these wrappers serve the plain native fields.
internal static class NativePlayerSettingsAccess
{
    internal static void Save() => PlayerPrefs.Save();

    internal static void SaveAndApply()
    {
        Save();
        // HUD application includes the player notification; do not send it twice.
        ApplyHudSettings();
    }

    internal static void SetUnitSystem(int value)
    {
        PlayerSettings.unitSystem = (PlayerSettings.UnitSystem)value;
        PlayerPrefs.SetInt("UnitSystem", value);
        ApplyPlayerSettings();
    }

    internal static void SetCinematicMode(bool value)
    {
        PlayerSettings.cinematicMode = value;
        PlayerPrefs.SetInt("CinematicMode", value ? 1 : 0);
    }

    internal static void SetDebugVisuals(bool value)
    {
        PlayerSettings.debugVis = value;
        PlayerPrefs.SetInt("DebugVis", value ? 1 : 0);
    }

    internal static void SetPlayerBool(string key, bool value, Action<bool> assign, bool apply = false)
    {
        assign(value);
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        if (apply)
        {
            ApplyPlayerSettings();
        }
    }

    internal static void SetHudBool(string key, bool value, Action<bool> assign)
    {
        assign(value);
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        ApplyHudSettings();
    }

    internal static void SetPlayerFloat(string key, float value, Action<float> assign, bool apply = false)
    {
        assign(value);
        PlayerPrefs.SetFloat(key, value);
        if (apply)
        {
            ApplyPlayerSettings();
        }
    }

    internal static void SetHudFloat(string key, float value, Action<float> assign)
    {
        assign(value);
        PlayerPrefs.SetFloat(key, value);
        ApplyHudSettings();
    }

    internal static void SetPlayerInt(string key, int value, Action<int> assign, bool apply = false)
    {
        assign(value);
        PlayerPrefs.SetInt(key, value);
        if (apply)
        {
            ApplyPlayerSettings();
        }
    }

    internal static void ReloadPlayerSettings()
    {
        try
        {
            PlayerSettings.LoadPrefs();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to reload PlayerSettings: {exception}");
        }
    }

    internal static void ApplyPlayerSettings()
    {
        try
        {
            PlayerSettings.ApplyPrefs();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to apply PlayerSettings: {exception}");
        }
    }

    internal static void ApplyHudSettings()
    {
        try
        {
            PlayerSettings.ApplyPrefs();
            if (SceneSingleton<HUDOptions>.i != null)
            {
                SceneSingleton<HUDOptions>.i.ApplyHUDSettings();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to apply HUD settings: {exception}");
        }
    }
}
