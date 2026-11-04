using System;
using System.ComponentModel;
using System.Diagnostics;
using NOVR.Compatibility;

internal static class StartupHandoff
{
    internal static bool WaitForOriginalProcess()
    {
        var id = Environment.GetEnvironmentVariable(GraphicsStartupProtocol.ParentIdVariable);
        var start = Environment.GetEnvironmentVariable(GraphicsStartupProtocol.ParentStartVariable);
        // Consume handoff ownership before any child process or mod can inherit it.
        Environment.SetEnvironmentVariable(GraphicsStartupProtocol.ParentIdVariable, null);
        Environment.SetEnvironmentVariable(GraphicsStartupProtocol.ParentStartVariable, null);
        using var current = Process.GetCurrentProcess();
        return WaitForOriginalProcess(id, start,
            Environment.GetEnvironmentVariable(GraphicsStartupProtocol.RetryVariable) == "1",
            current.Id, OpenParent, Console.WriteLine);
    }

    internal static bool WaitForOriginalProcess(string? id, string? start, bool retry, int currentId,
        Func<int, IStartupParent?> openParent, Action<string> report)
    {
        if (id == null && start == null)
            return true;
        if (!retry || !GraphicsStartupProtocol.TryReadParent(id, start, out var parentId, out var startTicks) || parentId == currentId)
        {
            report("[NOVR.Patcher] Invalid graphics-startup handoff; stopping this replacement process.");
            return false;
        }
        try
        {
            using var parent = openParent(parentId);
            if (parent == null || parent.HasExited)
                return true;
            long actualStart;
            try
            {
                actualStart = parent.StartTimeUtcTicks;
            }
            catch (InvalidOperationException) when (parent.HasExited) { return true; }
            catch (Win32Exception) when (parent.HasExited) { return true; }
            if (actualStart != startTicks)
                return true; // Parent exited, or this PID has been reused by an unrelated process.
            if (parent.WaitForExit(GraphicsStartupProtocol.ParentWaitMilliseconds))
                return true;
            report("[NOVR.Patcher] The original game did not exit within 15 seconds. Stopping the replacement to avoid two active games.");
        }
        catch (Exception exception)
        {
            report("[NOVR.Patcher] Cannot verify the original game has exited; stopping the replacement (" + exception.GetType().Name + ").");
        }
        return false;
    }

    private static IStartupParent? OpenParent(int id)
    {
        try
        {
            return new StartupParent(Process.GetProcessById(id));
        }
        catch (ArgumentException) { return null; }
    }

    private sealed class StartupParent : IStartupParent
    {
        private readonly Process _process;
        internal StartupParent(Process process) => _process = process;
        public bool HasExited => _process.HasExited;
        public long StartTimeUtcTicks => _process.StartTime.ToUniversalTime().Ticks;
        public bool WaitForExit(int milliseconds) => _process.WaitForExit(milliseconds);
        public void Dispose() => _process.Dispose();
    }
}

internal interface IStartupParent : IDisposable
{
    bool HasExited
    {
        get;
    }
    long StartTimeUtcTicks
    {
        get;
    }
    bool WaitForExit(int milliseconds);
}
