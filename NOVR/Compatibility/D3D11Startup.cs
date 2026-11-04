using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace NOVR.Compatibility;

internal static class D3D11Startup
{
    // The plugin constructor can run again during a domain/lifecycle transition.
    // Only the original startup may start a replacement process.
    private static bool _prepared;
    private static bool _continueInitialization;

    internal static bool Prepare()
    {
        if (_prepared)
            return _continueInitialization;
        _prepared = true;
        _continueInitialization = true;
        var retried = Environment.GetEnvironmentVariable(GraphicsStartupProtocol.RetryVariable) == "1";
        Environment.SetEnvironmentVariable(GraphicsStartupProtocol.RetryVariable, null);
        try
        {
            using var process = Process.GetCurrentProcess();
            var context = new GraphicsStartupContext(
                Application.platform == RuntimePlatform.WindowsPlayer,
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11,
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null,
                retried, GraphicsStartupProtocol.IsNuclearOptionSteamContext(
                    Environment.GetEnvironmentVariable("SteamAppId"), Environment.GetEnvironmentVariable("SteamGameId")),
                Environment.GetCommandLineArgs().Skip(1).ToArray(),
                process.MainModule?.FileName ?? string.Empty, process.Id, process.StartTime.ToUniversalTime().Ticks);
            _continueInitialization = Prepare(context, StartReplacement, Application.Quit, Debug.LogWarning);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[NOVR] Could not prepare Direct3D 11 startup. " + Fallback + " (" + exception.GetType().Name + ")");
        }
        return _continueInitialization;
    }

    private const string Fallback = "For VR, restart Nuclear Option with -force-d3d11; replace conflicting renderer options and preserve your other Steam launch settings.";

    // Returns false only for an intentional flat/headless launch or a successful
    // handoff. A failed Process.Start leaves the original process usable.
    internal static bool Prepare(GraphicsStartupContext context, Func<GraphicsStartupContext, string, bool> start,
        Action quit, Action<string> report)
    {
        if (context.Headless || GraphicsStartupProtocol.IsFlatLaunch(context.Arguments))
            return false;
        if (!context.WindowsPlayer || context.D3D11)
            return true;
        if (context.Retried)
        {
            report("[NOVR] Direct3D 11 was not selected after the startup retry; automatic relaunch stopped. " + Fallback);
            return true;
        }
        try
        {
            if (!context.SteamGame || context.ProcessId <= 0 || context.StartTicks <= 0 ||
                !string.Equals(Path.GetFileName(context.Executable), "NuclearOption.exe", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(context.Executable))
                throw new InvalidOperationException("Cannot identify the current Nuclear Option executable.");
            if (!start(context, GraphicsStartupProtocol.BuildD3D11Arguments(context.Arguments)))
                throw new InvalidOperationException("The replacement process did not start.");
        }
        catch (Exception exception)
        {
            report("[NOVR] Direct3D 11 relaunch failed; the current game remains open. " + Fallback + " (" + exception.GetType().Name + ")");
            return true;
        }
        report("[NOVR] Restarting once with Direct3D 11 for VR. Other launch arguments and the Steam/Proton environment are preserved.");
        try
        {
            quit();
        }
        catch (Exception exception)
        {
            // Do not initialize another VR owner after a child has started. Its
            // preloader will time out rather than enter the game concurrently.
            report("[NOVR] Could not close the original startup process (" + exception.GetType().Name + "). The replacement will stop if this process stays open.");
        }
        return false;
    }

    private static bool StartReplacement(GraphicsStartupContext context, string arguments)
    {
        using var child = Process.Start(CreateStartInfo(context, arguments));
        return child != null;
    }

    internal static ProcessStartInfo CreateStartInfo(GraphicsStartupContext context, string arguments)
    {
        var start = new ProcessStartInfo(context.Executable, arguments)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(context.Executable) ?? Environment.CurrentDirectory
        };
        start.EnvironmentVariables[GraphicsStartupProtocol.RetryVariable] = "1";
        start.EnvironmentVariables[GraphicsStartupProtocol.ParentIdVariable] = context.ProcessId.ToString(CultureInfo.InvariantCulture);
        start.EnvironmentVariables[GraphicsStartupProtocol.ParentStartVariable] = context.StartTicks.ToString(CultureInfo.InvariantCulture);
        // Doorstop 4 sets DISABLE after installing its native hooks, then sets
        // INITIALIZED after entering Mono. Both are process-local sentinels;
        // inheriting either can skip BepInEx (including the parent-exit wait).
        start.EnvironmentVariables.Remove("DOORSTOP_INITIALIZED");
        start.EnvironmentVariables.Remove("DOORSTOP_DISABLE");
        return start;
    }
}

internal sealed class GraphicsStartupContext
{
    internal readonly bool WindowsPlayer, D3D11, Headless, Retried, SteamGame;
    internal readonly string[] Arguments;
    internal readonly string Executable;
    internal readonly int ProcessId;
    internal readonly long StartTicks;

    internal GraphicsStartupContext(bool windowsPlayer, bool d3d11, bool headless, bool retried, bool steamGame,
        string[] arguments, string executable, int processId, long startTicks)
    {
        WindowsPlayer = windowsPlayer;
        D3D11 = d3d11;
        Headless = headless;
        Retried = retried;
        SteamGame = steamGame;
        Arguments = arguments;
        Executable = executable;
        ProcessId = processId;
        StartTicks = startTicks;
    }
}
