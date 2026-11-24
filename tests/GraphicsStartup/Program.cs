using System.ComponentModel;
using NOVR.Compatibility;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition)
        throw new Exception(name);
    checks++;
}

foreach (var (value, expected) in new[]
{
    ("", "\"\""), ("plain", "\"plain\""), ("with space", "\"with space\""),
    ("a\"b", "\"a\\\"b\""), ("C:\\directory\\", "\"C:\\directory\\\\\""),
    ("a\\\"b", "\"a\\\\\\\"b\""), ("x&echo %PATH% | $HOME", "\"x&echo %PATH% | $HOME\"")
})
    Check(GraphicsStartupProtocol.QuoteWindowsArgument(value) == expected, "Windows quoting: " + value);

var childArguments = GraphicsStartupProtocol.BuildD3D11Arguments(new[]
{
    "-force-vulkan", "-screen-width", "1920", "-force-d3d12", "-force-D3D11", "-force-device-index", "1",
    "-logFile", "C:\\logs with spaces\\run.log", "-force-vulkan-layers", "layer name", "--custom", "", "a\"b"
});
Check(childArguments == "\"-screen-width\" \"1920\" \"-force-device-index\" \"1\" \"-logFile\" \"C:\\logs with spaces\\run.log\" \"-force-vulkan-layers\" \"layer name\" \"--custom\" \"\" \"a\\\"b\" -force-d3d11",
    "exact graphics selectors replaced; unknown/display/GPU/log arguments preserved");
Check(GraphicsStartupProtocol.BuildD3D11Arguments(Array.Empty<string>()) == "-force-d3d11", "empty launch acquires D3D11");
foreach (var api in new[] { "-force-d3d9", "-force-glcore", "-force-glcore45", "-force-opengl", "-force-gles31aep", "-force-metal" })
    Check(GraphicsStartupProtocol.BuildD3D11Arguments(new[] { api }) == "-force-d3d11", "alternate selector replaced: " + api);
foreach (var flat in new[] { "-novr-flat", "-batchmode", "-nographics", "-NOVR-FLAT" })
    Check(GraphicsStartupProtocol.IsFlatLaunch(new[] { "unknown", flat }), "explicit flat/headless recognized: " + flat);
Check(!GraphicsStartupProtocol.IsFlatLaunch(new[] { "-novr-flat-extra", "-screen-fullscreen", "0" }), "windowed and unknown arguments do not imply flat launch");

// 0.4.8.35: restart with or without VR. Only -novr-flat with a window is a
// session that can offer the way back; headless flags never are.
foreach (var flat in new[] { "-novr-flat", "-NOVR-FLAT" })
    Check(GraphicsStartupProtocol.IsFlatSession(new[] { "-screen-width", "1920", flat }), "a windowed flat session is recognized: " + flat);
foreach (var headless in new[] { "-batchmode", "-nographics", "-BatchMode" })
{
    Check(!GraphicsStartupProtocol.IsFlatSession(new[] { headless }), "a headless process is not a flat session: " + headless);
    Check(!GraphicsStartupProtocol.IsFlatSession(new[] { "-novr-flat", headless }) && !GraphicsStartupProtocol.IsFlatSession(new[] { headless, "-novr-flat" }),
        "nor with the flat flag beside it, in either order: " + headless);
}
Check(!GraphicsStartupProtocol.IsFlatSession(Array.Empty<string>()) && !GraphicsStartupProtocol.IsFlatSession(new[] { "-novr-flat-extra", "novr-flat" }),
    "an ordinary launch is not a flat session");
Check(GraphicsStartupProtocol.BuildFlatArguments(Array.Empty<string>()) == "-novr-flat", "an empty launch restarts flat with just the flag");
var flatArguments = GraphicsStartupProtocol.BuildFlatArguments(new[]
{
    "-force-d3d11", "-screen-width", "1920", "-NOVR-FLAT", "-logFile", "C:\\logs with spaces\\run.log", "-novr-flat", "", "a\"b"
});
Check(flatArguments == "\"-force-d3d11\" \"-screen-width\" \"1920\" \"-logFile\" \"C:\\logs with spaces\\run.log\" \"\" \"a\\\"b\" -novr-flat",
    "a flat restart keeps every argument and the renderer, quoted, with the flag exactly once");
foreach (var renderer in new[] { "-force-vulkan", "-force-d3d12", "-force-glcore" })
    Check(GraphicsStartupProtocol.BuildFlatArguments(new[] { renderer }) == "\"" + renderer + "\" -novr-flat", "a flat restart leaves the renderer choice alone: " + renderer);
Check(GraphicsStartupProtocol.BuildVrArguments(new[] { "-novr-flat" }) == "-force-d3d11" && GraphicsStartupProtocol.BuildVrArguments(Array.Empty<string>()) == "-force-d3d11",
    "a VR restart drops the flat flag and selects Direct3D 11");
Check(GraphicsStartupProtocol.BuildVrArguments(new[] { "-force-vulkan", "-screen-width", "1920", "-Novr-Flat", "-novr-flat", "-novr-flat-extra" }) ==
    "\"-screen-width\" \"1920\" \"-novr-flat-extra\" -force-d3d11", "a VR restart keeps the other arguments and replaces the renderer selector");
Check(GraphicsStartupProtocol.IsNuclearOptionSteamContext("2168680", null) && GraphicsStartupProtocol.IsNuclearOptionSteamContext(null, "2168680"), "Steam app or game identity recognizes Nuclear Option");
Check(!GraphicsStartupProtocol.IsNuclearOptionSteamContext(null, null) && !GraphicsStartupProtocol.IsNuclearOptionSteamContext("999", "999"), "untracked or other-game launch cannot trigger an automatic restart");

var root = Path.Combine(Path.GetTempPath(), "novr-startup-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var exe = Path.Combine(root, "NuclearOption.exe");
    File.WriteAllText(exe, "test placeholder; never executed");
    var messages = new List<string>();
    var calls = new List<string>();
    var ticks = DateTime.UtcNow.Ticks;
    GraphicsStartupContext Context(bool windows = true, bool d3d11 = false, bool headless = false, bool retry = false, bool steam = true,
        string[]? arguments = null, string? executable = null) =>
        new(windows, d3d11, headless, retry, steam, arguments ?? new[] { "-force-vulkan", "-screen-width", "1920" }, executable ?? exe, 42, ticks);
    bool Start(GraphicsStartupContext context, string arguments)
    {
        Check(context.ProcessId == 42 && context.StartTicks == ticks, "parent identity reaches child-start boundary");
        Check(arguments == "\"-screen-width\" \"1920\" -force-d3d11", "child gets clean D3D11 argument vector");
        calls.Add("start");
        return true;
    }
    void Quit() => calls.Add("quit");
    bool Prepare(GraphicsStartupContext context) => D3D11Startup.Prepare(context, Start, Quit, messages.Add);
    Check(!Prepare(Context()) && calls.SequenceEqual(new[] { "start", "quit" }), "successful restart suppresses original initialization, starts before quit");
    calls.Clear();
    Check(Prepare(Context(d3d11: true)) && calls.Count == 0, "existing D3D11 process never restarts");
    Check(Prepare(Context(windows: false)) && calls.Count == 0, "non-Windows player left unchanged");
    Check(!Prepare(Context(headless: true)) && calls.Count == 0, "null renderer skips VR without restart");
    foreach (var flat in new[] { "-novr-flat", "-batchmode", "-nographics" })
        Check(!Prepare(Context(arguments: new[] { flat })) && calls.Count == 0, "intentional flat/headless process skips VR: " + flat);
    Check(Prepare(Context(retry: true)) && calls.Count == 0 && messages.Last().Contains("automatic relaunch stopped"), "ignored D3D11 argument cannot create retry loop");
    Check(Prepare(Context(steam: false)) && calls.Count == 0, "direct launch without inherited Steam app context is not restarted");
    Check(Prepare(Context(executable: Path.Combine(root, "OtherGame.exe"))) && calls.Count == 0, "unrecognized executable not relaunched");
    Check(D3D11Startup.Prepare(Context(), (_, _) => false, Quit, messages.Add) && calls.Count == 0, "failed process creation leaves original initialization available");
    Check(D3D11Startup.Prepare(Context(), (_, _) => throw new Win32Exception(), Quit, messages.Add) && calls.Count == 0, "process-start exception never quits original");
    Check(!D3D11Startup.Prepare(Context(), Start, () => throw new InvalidOperationException(), messages.Add) && calls.SequenceEqual(new[] { "start" }), "quit failure does not create a second VR owner");

    // Restart with or without VR, through the same child-start boundary.
    Check(D3D11Startup.CanRelaunch(Context()) && !D3D11Startup.CanRelaunch(Context(steam: false)) &&
        !D3D11Startup.CanRelaunch(Context(executable: Path.Combine(root, "OtherGame.exe"))) &&
        !D3D11Startup.CanRelaunch(Context(executable: Path.Combine(root, "missing", "NuclearOption.exe"))),
        "only a Steam-launched Nuclear Option that exists on disk is restarted");
    string? started = null;
    bool Begin(GraphicsStartupContext context, string arguments)
    {
        Check(context.ProcessId == 42 && context.StartTicks == ticks, "the restart hands the same parent identity to the child");
        started = arguments;
        calls.Add("start");
        return true;
    }
    string? Restart(GraphicsStartupContext context, bool flat) => VrModeRestart.Restart(context, flat, Begin, Quit, messages.Add);
    calls.Clear();
    Check(Restart(Context(d3d11: true, arguments: new[] { "-force-d3d11", "-screen-width", "1920" }), true) == null &&
        calls.SequenceEqual(new[] { "start", "quit" }) && started == "\"-force-d3d11\" \"-screen-width\" \"1920\" -novr-flat",
        "restart without VR starts the flat child first, then closes this game");
    Check(messages.Last().Contains("without VR"), "the restart is reported");
    Check(GraphicsStartupProtocol.IsFlatSession(new[] { "-force-d3d11", "-screen-width", "1920", "-novr-flat" }), "that child is a flat session");
    calls.Clear();
    Check(Restart(Context(arguments: new[] { "-novr-flat", "-force-vulkan", "-screen-width", "1920" }), false) == null &&
        calls.SequenceEqual(new[] { "start", "quit" }) && started == "\"-screen-width\" \"1920\" -force-d3d11",
        "restart in VR drops the flat flag and selects Direct3D 11, whatever renderer the flat game used");
    Check(messages.Last().Contains("in VR"), "and is reported");
    foreach (var flat in new[] { false, true })
    {
        var fallback = flat ? VrModeRestart.FlatFallback : VrModeRestart.VrFallback;
        foreach (var (context, why) in new[]
        {
            (Context(windows: false), "not a Windows player"), (Context(headless: true), "headless"), (Context(steam: false), "not launched by Steam"),
            (Context(executable: Path.Combine(root, "OtherGame.exe")), "another executable")
        })
        {
            calls.Clear();
            var failure = Restart(context, flat);
            Check(failure != null && failure.EndsWith(fallback) && calls.Count == 0, $"flat={flat}: no restart and the launch option is named when {why}");
        }
        calls.Clear();
        var refused = VrModeRestart.Restart(Context(), flat, (_, _) => false, Quit, messages.Add);
        Check(refused != null && refused.EndsWith(fallback) && calls.Count == 0, $"flat={flat}: a child that did not start leaves this game open");
        var threw = VrModeRestart.Restart(Context(), flat, (_, _) => throw new Win32Exception(), Quit, messages.Add);
        Check(threw != null && threw.EndsWith(fallback) && calls.Count == 0 && messages.Last().Contains("remains open"), $"flat={flat}: a process-start exception never closes this game");
        Check(VrModeRestart.Restart(Context(), flat, Begin, () => throw new InvalidOperationException(), messages.Add) == null &&
            calls.SequenceEqual(new[] { "start" }) && messages.Last().Contains("replacement will stop"),
            $"flat={flat}: a failed quit after the child started is reported, not retried");
    }
    Check(VrModeRestart.FlatFallback.Contains("-novr-flat") && VrModeRestart.VrFallback.Contains("-novr-flat"), "both fallbacks name the launch option");

    var environment = new Dictionary<string, string?>
    {
        ["DOORSTOP_INITIALIZED"] = "TRUE",
        ["DOORSTOP_DISABLE"] = "TRUE",
        ["SteamAppId"] = "2168680",
        ["WINEDLLOVERRIDES"] = "winhttp=n,b",
        ["WINEPREFIX"] = "test prefix with spaces",
        ["PROTON_LOG"] = "0"
    };
    var previous = environment.Keys.ToDictionary(k => k, Environment.GetEnvironmentVariable);
    try
    {
        foreach (var entry in environment)
            Environment.SetEnvironmentVariable(entry.Key, entry.Value);
        var info = D3D11Startup.CreateStartInfo(Context(), childArguments);
        Check(info.FileName == exe && info.Arguments == childArguments && !info.UseShellExecute && info.WorkingDirectory == root, "direct same-executable launch avoids shell or Steam-option rewrite");
        Check(!info.EnvironmentVariables.ContainsKey("DOORSTOP_INITIALIZED"), "child clears inherited Doorstop bootstrap sentinel");
        Check(!info.EnvironmentVariables.ContainsKey("DOORSTOP_DISABLE"), "child clears Doorstop native-hook sentinel so its loader actually runs");
        foreach (var entry in environment.Where(e => e.Key != "DOORSTOP_INITIALIZED" && e.Key != "DOORSTOP_DISABLE"))
            Check(info.EnvironmentVariables[entry.Key] == entry.Value, "child preserves environment: " + entry.Key);
        Check(info.EnvironmentVariables[GraphicsStartupProtocol.RetryVariable] == "1" &&
            info.EnvironmentVariables[GraphicsStartupProtocol.ParentIdVariable] == "42" &&
            info.EnvironmentVariables[GraphicsStartupProtocol.ParentStartVariable] == ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), "retry and parent identity scoped to child environment");
        Check(Environment.GetEnvironmentVariable("DOORSTOP_INITIALIZED") == "TRUE" && Environment.GetEnvironmentVariable("DOORSTOP_DISABLE") == "TRUE" &&
            Environment.GetEnvironmentVariable(GraphicsStartupProtocol.ParentIdVariable) == null, "parent environment unchanged by child launch preparation");
    }
    finally { foreach (var entry in previous) Environment.SetEnvironmentVariable(entry.Key, entry.Value); }

    var opened = 0;
    var parent = new Parent { Start = ticks, Exits = true };
    IStartupParent? Open(int id)
    {
        opened++;
        Check(id == 42, "handoff opens only named parent");
        return parent;
    }
    bool Wait(string? id = "42", string? start = null, bool retry = true, int current = 43) =>
        StartupHandoff.WaitForOriginalProcess(id, start ?? ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), retry, current, Open, messages.Add);
    Check(StartupHandoff.WaitForOriginalProcess(null, null, false, 43, Open, messages.Add) && opened == 0, "ordinary startup has no wait or parent lookup");
    foreach (var invalid in new[] { "", "0", "-1", "+42", "42x", "2147483648" })
        Check(!Wait(id: invalid) && opened == 0, "invalid parent PID refused before lookup: " + invalid);
    foreach (var invalid in new[] { "", "0", "-1", "x", long.MaxValue.ToString() })
        Check(!Wait(start: invalid) && opened == 0, "invalid start identity refused: " + invalid);
    Check(!Wait(retry: false) && opened == 0, "parent metadata without retry owner refused");
    Check(!Wait(current: 42) && opened == 0, "self-wait refused");
    Check(Wait() && parent.WaitMilliseconds == 15000 && parent.Disposed, "matching parent is boundedly awaited and disposed before payload setup");
    parent = new Parent { Start = ticks + 1, Exits = false };
    Check(Wait() && parent.WaitMilliseconds == 0 && parent.Disposed, "reused PID never waited on or terminated");
    parent = new Parent { Start = ticks, Exits = false };
    Check(!Wait() && parent.WaitMilliseconds == 15000 && parent.Disposed, "parent timeout stops replacement instead of concurrent initialization");
    parent = new Parent { Start = ticks, Gone = true };
    Check(Wait() && parent.WaitMilliseconds == 0 && parent.Disposed, "already exited parent permits child startup");
    parent = new Parent { Start = ticks, ExitDuringIdentityRead = true };
    Check(Wait() && parent.Disposed, "parent exiting during identity read is normal completion");
    Check(StartupHandoff.WaitForOriginalProcess("42", ticks.ToString(), true, 43, _ => null, messages.Add), "missing parent process permits child startup");
    Check(!StartupHandoff.WaitForOriginalProcess("42", ticks.ToString(), true, 43, _ => throw new Win32Exception(), messages.Add), "unverifiable parent fails closed");

    // Invoke the real one-shot wrapper on the already-supported backend. No OS
    // process is created; a later API change must not reopen startup ownership.
    Environment.SetEnvironmentVariable(GraphicsStartupProtocol.RetryVariable, "1");
    Check(D3D11Startup.Prepare(), "actual startup wrapper accepts supported renderer");
    Check(!D3D11Startup.FlatSession, "an ordinary launch is not treated as a flat session");
    Check(Environment.GetEnvironmentVariable(GraphicsStartupProtocol.RetryVariable) == null, "actual startup wrapper consumes retry marker before other mods inherit it");
    UnityEngine.SystemInfo.graphicsDeviceType = UnityEngine.Rendering.GraphicsDeviceType.Vulkan;
    Check(D3D11Startup.Prepare() && UnityEngine.Application.QuitCalls == 0 && UnityEngine.Debug.Warnings.Count == 0,
        "startup wrapper cannot rerun handoff after initial preparation");

    // Invoke the real preloader wrapper with an invalid, self-owned handoff. It
    // must consume both metadata fields without opening or terminating a process.
    Environment.SetEnvironmentVariable(GraphicsStartupProtocol.ParentIdVariable, "not-a-pid");
    Environment.SetEnvironmentVariable(GraphicsStartupProtocol.ParentStartVariable, ticks.ToString());
    Check(!StartupHandoff.WaitForOriginalProcess(), "actual preloader wrapper refuses invalid inherited handoff");
    Check(Environment.GetEnvironmentVariable(GraphicsStartupProtocol.ParentIdVariable) == null &&
        Environment.GetEnvironmentVariable(GraphicsStartupProtocol.ParentStartVariable) == null, "actual preloader wrapper clears consumed identity metadata");
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"Graphics startup: {checks} checks passed.");

sealed class Parent : IStartupParent
{
    internal long Start;
    internal bool Exits, Gone, ExitDuringIdentityRead, Disposed;
    internal int WaitMilliseconds;
    public bool HasExited => Gone;
    public long StartTimeUtcTicks
    {
        get
        {
            if (ExitDuringIdentityRead)
            {
                Gone = true;
                throw new InvalidOperationException();
            }
            return Start;
        }
    }
    public bool WaitForExit(int milliseconds)
    {
        WaitMilliseconds = milliseconds;
        return Exits;
    }
    public void Dispose() => Disposed = true;
}
