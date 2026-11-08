using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NOVR.Rendering;

// Explicit, absolute-path loading keeps the optional Windows driver component
// out of Unity's automatic plugin discovery and the Proton graphics stack.
internal sealed class FixedFoveationNative
{
    private readonly IntPtr _library;
    private readonly Initialize _initialize;
    private readonly IntCall _enable;
    private readonly IntCall _diagnostics;
    private readonly Acquire _acquire;
    private readonly Cancel _cancel;
    private readonly Report _report;
    private readonly byte[] _reportBuffer = new byte[4096];
    internal readonly IntPtr Callback;
    internal readonly int ProbeEvent, BeginEvent, EndEvent, ResetEvent, ShutdownEvent;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Version();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Initialize(IntPtr resource);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int IntCall(int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr Pointer();
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate IntPtr Acquire(IntPtr texture,
        uint width, uint height, uint samples, uint eye, float x, float y, float inner, float outer, uint profile, uint frameIndex);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Cancel(IntPtr scope);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Report([Out] byte[] text, int capacity);

    internal FixedFoveationNative()
    {
        var directory = Path.GetDirectoryName(typeof(FixedFoveationNative).Assembly.Location)!;
        var path = Path.Combine(directory, "Native", "NovrFoveation.dll");
        if (!File.Exists(path))
            throw new FileNotFoundException("This build does not include the optional foveation component.");
        _library = LoadLibraryEx(path, IntPtr.Zero, 0x00000100 | 0x00000800); // DLL directory + System32 only
        if (_library == IntPtr.Zero)
            throw new InvalidOperationException("The optional foveation component could not be loaded.");
        if (Export<Version>("GetApiVersion")() != 2)
            throw new InvalidOperationException("Foveation component version mismatch.");
        _initialize = Export<Initialize>("InitializeFromTexture");
        _enable = Export<IntCall>("SetEnabled");
        _diagnostics = Export<IntCall>("SetDiagnostics");
        _acquire = Export<Acquire>("AcquireScope");
        _cancel = Export<Cancel>("CancelScope");
        _report = Export<Report>("CopyReport");
        Callback = Export<Pointer>("GetRenderEventAndDataFunc")();
        var eventId = Export<IntCall>("GetEventId");
        ProbeEvent = eventId(0); BeginEvent = eventId(1); EndEvent = eventId(2); ResetEvent = eventId(3);
        ShutdownEvent = eventId(4);
        if (Callback == IntPtr.Zero || BeginEvent == EndEvent || ResetEvent < 0)
            throw new InvalidOperationException("Foveation render callbacks unavailable.");
        // The module remains loaded for the process lifetime: queued render
        // callbacks must never point at an unloaded DLL after component teardown.
    }

    private T Export<T>(string suffix) where T : Delegate
    {
        var address = GetProcAddress(_library, "NovrFoveation" + suffix);
        if (address == IntPtr.Zero) throw new MissingMethodException("Foveation export: " + suffix);
        return (T)Marshal.GetDelegateForFunctionPointer(address, typeof(T));
    }

    internal bool InitializeDevice(IntPtr resource) => _initialize(resource) == 1;
    internal bool Enable(bool enabled) => _enable(enabled ? 1 : 0) == (enabled ? 1 : 0);
    internal bool SetDiagnostics(bool enabled) => _diagnostics(enabled ? 1 : 0) == (enabled ? 1 : 0);
    internal IntPtr AcquireScope(int width, int height, int samples, int eye, float x, float y, FixedFoveationLevel level,
        float inner, float outer)
    {
        // Mild/Balanced/Strong shade 1x1/1x1/2x2. Test levels request 1x1/2x2/4x4;
        // native code caps the latter for MSAA/driver limits and reports it.
        // Radii arrive already scaled for this eye's FOV and gaze margins.
        return _acquire(IntPtr.Zero, (uint)width, (uint)height, (uint)samples, (uint)eye,
            x, y, inner, outer, (uint)level - 1, unchecked((uint)UnityEngine.Time.frameCount));
    }
    internal void CancelScope(IntPtr packet) => _cancel(packet);
    internal string ReadReport()
    {
        var length = _report(_reportBuffer, _reportBuffer.Length);
        if (length <= 0 || length >= _reportBuffer.Length) throw new InvalidOperationException("Invalid foveation status.");
        return Encoding.UTF8.GetString(_reportBuffer, 0, length);
    }

    internal static bool IsWine()
    {
        var ntdll = GetModuleHandle("ntdll.dll");
        return ntdll != IntPtr.Zero && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadLibraryExW")]
    private static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetModuleHandleW")]
    private static extern IntPtr GetModuleHandle(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, string name);
}
