using System;
using System.Runtime.InteropServices;
using HarmonyLib;
using Rewired;
using Rewired.Platforms;
using UnityEngine;

namespace NOVR.Compatibility;

// A captured Proton hang stopped the main thread in XInputGetState, waiting
// for Wine's xinput_cs. Configure Rewired before its first input update;
// neither OpenXR controller polling nor XR haptics use this legacy backend.
internal static class ProtonControllerCompatibility
{
    private static bool _registered;
    private static bool _enabled;

    [DllImport("ntdll.dll", EntryPoint = "wine_get_version", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr WineGetVersion();

    internal static void Initialize()
    {
        if (_registered)
            return;
        _registered = true;
        _enabled = ModConfiguration.Instance.DisableProtonXInput.Value && IsWine();
        if (_enabled && ReInput.isReady)
            Debug.LogWarning("[NOVR] Proton compatibility loaded after Rewired initialization. Restart the game to disable legacy XInput safely.");
    }

    private static bool IsWine()
    {
        try
        {
            return WineGetVersion() != IntPtr.Zero;
        }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
        catch (BadImageFormatException) { return false; }
    }

    internal static void BeforeInitialization(InputManager_Base manager)
    {
        if (!_enabled || !ModConfiguration.Instance.DisableProtonXInput.Value || ReInput.isReady || manager == null)
            return;
        try
        {
            // Awake/OnEnable have not created the platform backend yet. Change
            // its public startup data in place, without resetting live players
            // or touching saved bindings. The runtime configuration setter
            // resets Rewired and is unsafe inside its InitializedEvent callback.
            var config = manager.userData?.ConfigVars;
            if (config == null)
                return;
            var xinputPrimary = config.windowsStandalonePrimaryInputSource == WindowsStandalonePrimaryInputSource.XInput;
            if (!config.useXInput && !xinputPrimary)
                return;
            config.useXInput = false;
            if (xinputPrimary)
                config.windowsStandalonePrimaryInputSource = WindowsStandalonePrimaryInputSource.RawInput;
            Debug.Log("[NOVR] Proton compatibility: legacy Rewired XInput polling disabled; OpenXR motion-controller input retained.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[NOVR] Could not disable legacy Proton XInput polling: " + exception);
        }
    }
}

[HarmonyPatch(typeof(InputManager_Base), "Awake")]
internal static class ProtonInputManagerAwakePatch
{
    [HarmonyPrefix]
    private static void Prefix(InputManager_Base __instance) => ProtonControllerCompatibility.BeforeInitialization(__instance);
}

[HarmonyPatch(typeof(InputManager_Base), "OnEnable")]
internal static class ProtonInputManagerEnablePatch
{
    [HarmonyPrefix]
    private static void Prefix(InputManager_Base __instance) => ProtonControllerCompatibility.BeforeInitialization(__instance);
}
