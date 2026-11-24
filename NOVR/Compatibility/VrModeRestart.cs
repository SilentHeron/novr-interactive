using System;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace NOVR.Compatibility;

// Switching between VR and the flat game takes a new process: the renderer,
// the headset session and every patch are decided at startup. This restarts
// the game the way the one-time Direct3D 11 handoff does, with -novr-flat
// added or removed, so the Steam and Proton environment and the player's
// other launch arguments carry over.
internal static class VrModeRestart
{
    internal const string FlatFallback = "To play without VR, add -novr-flat to the game's launch options.";
    internal const string VrFallback = "To play in VR, remove -novr-flat from the game's launch options.";
    private static bool _started;

    // Null once the replacement has started and this game is closing;
    // otherwise what to tell the player. The current game then stays open.
    internal static string? Restart(bool flat)
    {
        // A second click while this game closes must not start a second copy.
        if (_started)
            return null;
        string? failure;
        try
        {
            failure = Restart(D3D11Startup.CurrentContext(false), flat, D3D11Startup.StartReplacement, Application.Quit, Debug.LogWarning);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[NOVR] Could not prepare the restart (" + exception.GetType().Name + ").");
            failure = "NOVR could not restart the game. " + (flat ? FlatFallback : VrFallback);
        }
        _started = failure == null;
        return failure;
    }

    internal static string? Restart(GraphicsStartupContext context, bool flat,
        Func<GraphicsStartupContext, string, bool> start, Action quit, Action<string> report)
    {
        var fallback = flat ? FlatFallback : VrFallback;
        try
        {
            if (!context.WindowsPlayer || context.Headless || !D3D11Startup.CanRelaunch(context))
                return "NOVR cannot restart the game from here. " + fallback;
            var arguments = flat
                ? GraphicsStartupProtocol.BuildFlatArguments(context.Arguments)
                : GraphicsStartupProtocol.BuildVrArguments(context.Arguments);
            if (!start(context, arguments))
                return "The restart did not start. " + fallback;
        }
        catch (Exception exception)
        {
            report("[NOVR] Restart " + (flat ? "without VR" : "in VR") + " failed; the current game remains open (" + exception.GetType().Name + ").");
            return "The restart failed. " + fallback;
        }
        report("[NOVR] Restarting " + (flat ? "without VR." : "in VR.") + " Other launch arguments and the Steam/Proton environment are preserved.");
        try
        {
            quit();
        }
        catch (Exception exception)
        {
            // The replacement's preloader waits for this process and stops
            // itself if it stays open, so two games never run together.
            report("[NOVR] Could not close the game for the restart (" + exception.GetType().Name + "). The replacement will stop if this game stays open.");
        }
        return null;
    }
}
