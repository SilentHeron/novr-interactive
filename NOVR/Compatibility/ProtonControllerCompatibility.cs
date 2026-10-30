using System;
using System.Runtime.InteropServices;
using Rewired;
using UnityEngine;

namespace NOVR.Compatibility;

// A captured Proton hang stopped the main thread in XInputGetState, waiting
// for Wine's xinput_cs. Configure Rewired before its first input update;
// neither OpenXR controller polling nor XR haptics use this legacy backend.
internal static class ProtonControllerCompatibility
{
    private static bool _registered;
    private static bool _applying;

    [DllImport("ntdll.dll", EntryPoint = "wine_get_version", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr WineGetVersion();

    internal static void Initialize()
    {
        if (_registered || !ModConfiguration.Instance.DisableProtonXInput.Value || !IsWine()) return;
        _registered = true;
        ReInput.InitializedEvent += Apply;
        if (ReInput.isReady) Apply();
    }

    private static bool IsWine()
    {
        try { return WineGetVersion() != IntPtr.Zero; }
        catch (DllNotFoundException) { return false; }
        catch (EntryPointNotFoundException) { return false; }
        catch (BadImageFormatException) { return false; }
    }

    private static void Apply()
    {
        if (_applying || !ReInput.isReady || !ReInput.configuration.useXInput) return;
        _applying = true;
        try
        {
            // This is Rewired's supported runtime setting. It retains its other
            // input backends and does not rewrite the game's saved bindings.
            // ResetAll may notify InitializedEvent again, hence the reentry guard.
            ReInput.configuration.useXInput = false;
            Debug.Log("[NOVR] Proton compatibility: legacy Rewired XInput polling disabled; OpenXR motion-controller input retained.");
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[NOVR] Could not disable legacy Proton XInput polling: " + exception);
        }
        finally { _applying = false; }
    }
}
