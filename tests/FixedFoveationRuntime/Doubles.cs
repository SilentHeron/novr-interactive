using System.Reflection;
using System.Text.Json;

namespace HarmonyLib
{
    public sealed class Harmony(string id)
    {
        internal static MethodInfo? Postfix;
        internal static int Patches, Unpatches;
        internal readonly string Id = id;
        public void Patch(MethodInfo original, HarmonyMethod postfix)
        {
            Postfix = postfix.Method;
            Patches++;
        }
        public void UnpatchSelf()
        {
            Postfix = null;
            Unpatches++;
        }
    }
    public sealed class HarmonyMethod(Type type, string name)
    {
        internal readonly MethodInfo Method = AccessTools.Method(type, name)!;
    }
    public static class AccessTools
    {
        public static MethodInfo? Method(Type type, string name) => type.GetMethod(name,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }
}

namespace UnityEngine
{
    public class Object
    {
        public static void Destroy(Object item)
        {
            if (item is Texture2D)
                Texture2D.Destroyed++;
        }
    }
    public class MonoBehaviour : Object
    {
        public bool isActiveAndEnabled = true;
    }
    public enum RuntimePlatform
    {
        WindowsPlayer, LinuxPlayer, OSXPlayer
    }
    public static class Application
    {
        public static RuntimePlatform platform = RuntimePlatform.WindowsPlayer;
    }
    public static class SystemInfo
    {
        public static Rendering.GraphicsDeviceType graphicsDeviceType = Rendering.GraphicsDeviceType.Direct3D11;
        public static int graphicsDeviceVendorID = 0x10DE;
    }
    public static class Time
    {
        public static int frameCount; public static float unscaledTime;
    }
    public static class Debug
    {
        internal static readonly List<string> Logs = new(), Warnings = new();
        public static void Log(string message) => Logs.Add(message);
        public static void LogWarning(string message) => Warnings.Add(message);
    }
    public enum TextureFormat
    {
        RGBA32
    }
    public class Texture2D : Object
    {
        internal static int Created, Destroyed, NativePointerReads;
        public Texture2D(int width, int height, TextureFormat format, bool mipmaps) => Created++;
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable)
        {
        }
        public IntPtr GetNativeTexturePtr()
        {
            NativePointerReads++;
            return (IntPtr)42;
        }
    }
    public enum CameraType
    {
        Game, SceneView, Preview, Reflection
    }
    public class Camera : Object
    {
        public object? targetTexture;
        public CameraType cameraType = CameraType.Game;
        public readonly Transform transform = new();
    }
    public struct RenderTextureDescriptor
    {
        public Rendering.TextureDimension dimension;
        public int volumeDepth, width, height, msaaSamples;
    }
    public struct Rect
    {
        public float x, y, width, height;
    }
    public struct Matrix4x4
    {
        public float m00, m01, m02, m03, m10, m11, m12, m13, m20, m21, m22, m23;
        public Vector3 MultiplyVector(Vector3 value) => new(
            m00 * value.x + m01 * value.y + m02 * value.z,
            m10 * value.x + m11 * value.y + m12 * value.z,
            m20 * value.x + m21 * value.y + m22 * value.z);

        internal static Matrix4x4 EyeView(Quaternion worldRotation, Vector3 worldPosition = default)
        {
            var inverse = Quaternion.Inverse(worldRotation);
            var x = inverse * new Vector3(1, 0, 0);
            var y = inverse * new Vector3(0, 1, 0);
            var z = inverse * new Vector3(0, 0, 1);
            var translation = inverse * worldPosition;
            return new Matrix4x4
            {
                m00 = x.x,
                m01 = y.x,
                m02 = z.x,
                m03 = -translation.x,
                m10 = x.y,
                m11 = y.y,
                m12 = z.y,
                m13 = -translation.y,
                m20 = -x.z,
                m21 = -y.z,
                m22 = -z.z,
                m23 = translation.z
            };
        }
    }
    public static class JsonUtility
    {
        public static T? FromJson<T>(string json)
        {
            if (json == "null")
                return default;
            using var document = JsonDocument.Parse(json);
            var instance = (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;
            foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
                if (document.RootElement.TryGetProperty(field.Name, out var value))
                    field.SetValue(instance, JsonSerializer.Deserialize(value.GetRawText(), field.FieldType));
            return instance;
        }
    }
    public static class Graphics
    {
        public static void ExecuteCommandBuffer(Rendering.CommandBuffer command) => command.Execute();
    }
}

namespace UnityEngine.XR
{
    public static class XRSettings
    {
        public static bool enabled = true;
        public static float eyeTextureResolutionScale = 1.25f;
    }
}

namespace UnityEngine.Rendering
{
    public enum GraphicsDeviceType
    {
        Direct3D11, Direct3D12, Vulkan, OpenGLCore
    }
    public enum TextureDimension
    {
        Tex2D, Tex2DArray, Cube
    }
    public sealed class CommandBuffer
    {
        private readonly List<(int id, IntPtr packet)> _events = new();
        public void IssuePluginEventAndData(IntPtr callback, int id, IntPtr packet) => _events.Add((id, packet));
        internal void Execute()
        {
            foreach (var (id, packet) in _events)
                NOVR.Rendering.FixedFoveationNative.Dispatch(id, packet);
        }
    }
    public static class CommandBufferPool
    {
        internal static int Acquired, Released;
        public static CommandBuffer Get(string name)
        {
            Acquired++;
            return new();
        }
        public static void Release(CommandBuffer buffer) => Released++;
    }
    public readonly struct ScriptableRenderContext
    {
        public void ExecuteCommandBuffer(CommandBuffer command) => command.Execute();
    }
    public static class RenderPipelineManager
    {
        public static event Action<ScriptableRenderContext, UnityEngine.Camera[]>? endFrameRendering;
        internal static int Subscribers => endFrameRendering?.GetInvocationList().Length ?? 0;
        internal static void EndFrame() => endFrameRendering?.Invoke(default, Array.Empty<UnityEngine.Camera>());
    }
}

namespace UnityEngine.Rendering.Universal
{
    public enum RenderPassEvent
    {
        BeforeRendering = 0, BeforeRenderingOpaques = 250, BeforeRenderingSkybox = 350
    }
    public enum CameraRenderType
    {
        Base, Overlay
    }
    public enum RenderingMode
    {
        Forward, ForwardPlus, Deferred
    }
    public class ScriptableRenderPass
    {
        internal bool useNativeRenderPass { get; set; } = true;
        internal int ConfiguredTargets;
        public RenderPassEvent renderPassEvent;
        protected void ConfigureTarget(object colour, object depth) => ConfiguredTargets++;
        public virtual void OnCameraSetup(CommandBuffer command, ref RenderingData data)
        {
        }
        public virtual void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
        }
    }
    public class ScriptableRenderer
    {
        internal readonly List<ScriptableRenderPass> Passes = new();
        public readonly object cameraColorTargetHandle = new(), cameraDepthTargetHandle = new();
        public void EnqueuePass(ScriptableRenderPass pass) => Passes.Add(pass);
        internal void AddRenderPasses(ref RenderingData data)
        {
            Passes.Clear();
            object[] arguments = { this, data };
            HarmonyLib.Harmony.Postfix?.Invoke(null, arguments);
            data = (RenderingData)arguments[1];
        }
    }
    public sealed class UniversalRenderer : ScriptableRenderer
    {
        internal RenderingMode renderingModeRequested { get; set; } = RenderingMode.Forward;
    }
    public sealed class XrPass
    {
        public bool enabled = true, singlePassEnabled, supportsFoveatedRendering;
        public int viewCount = 1, multipassId;
        public IntPtr foveatedRenderingInfo;
        internal Rect Viewport = new() { width = 2000, height = 2000 };
        internal Matrix4x4 Projection = new() { m00 = 1, m11 = 1 };
        internal Matrix4x4 View = Matrix4x4.EyeView(Quaternion.identity);
        internal int ViewReads;
        internal bool ThrowViewRead;
        public Rect GetViewport(int index) => Viewport;
        public Matrix4x4 GetProjMatrix(int index) => Projection;
        public Matrix4x4 GetViewMatrix(int index)
        {
            ViewReads++;
            if (ThrowViewRead)
                throw new InvalidOperationException("fixture optional eye view unavailable");
            return View;
        }
    }
    public struct CameraData
    {
        public Camera camera;
        public ScriptableRenderer renderer;
        public XrPass xr;
        public RenderTextureDescriptor cameraTargetDescriptor;
        public CameraRenderType renderType;
    }
    public struct RenderingData
    {
        public CameraData cameraData;
    }
}

namespace NOVR
{
    internal sealed class ModConfiguration
    {
        internal static ModConfiguration Instance = new();
        internal readonly Rendering.FixedFoveationSettings FixedFoveation = new(new BepInEx.Configuration.ConfigFile());
    }
    internal static class APIBus
    {
        internal static UnityEngine.Camera MainCamera = new();
    }
}
internal enum CameraMode
{
    cockpit, orbit, menu
}
internal static class CameraStateManager
{
    internal static CameraMode cameraMode = CameraMode.cockpit;
}
internal sealed class Aircraft
{
}
internal static class GameManager
{
    internal static bool HasAircraft = true;
    internal static bool GetLocalAircraft(out Aircraft? aircraft)
    {
        aircraft = HasAircraft ? new Aircraft() : null;
        return HasAircraft;
    }
}

namespace NOVR.Rendering
{
    // Driver/event boundary only: these tests observe managed commands and
    // ownership, never claim the GPU actually applies VRS or saves work.
    internal sealed class FixedFoveationNative
    {
        internal static int Constructed, Initialized, WineChecks, Reports;
        internal static bool Wine, Supported = true, Failed, RejectEnable, RejectInitialize, RejectAcquire, ThrowAcquire;
        internal static bool Enabled, DiagnosticsEnabled, RejectDiagnostics;
        internal static string Reason = "fixture capability";
        internal static string DiagnosticsReason = "";
        internal static ulong LeftApplied, RightApplied;
        internal static ulong LeftSequence, RightSequence;
        internal static int LeftSampleMode = -1, RightSampleMode = -1;
        internal static int LeftProfile, RightProfile, LeftCoarsestRate = 2, RightCoarsestRate = 2;
        internal static int LeftSampleCoarsestRate = 2, RightSampleCoarsestRate = 2;
        internal static bool LeftRateClamped, RightRateClamped, LeftSampleRateClamped, RightSampleRateClamped;
        internal static string LeftClampReason = "none", RightClampReason = "none";
        internal static double LeftGpuMs = -1, RightGpuMs = -1;
        internal static readonly List<bool> Enables = new();
        internal static readonly List<bool> DiagnosticRequests = new();
        internal static readonly List<(int id, IntPtr packet)> Events = new();
        internal static readonly List<Scope> AcquiredScopes = new();
        internal static readonly Dictionary<IntPtr, Scope> LiveScopes = new();
        internal readonly IntPtr Callback = (IntPtr)100;
        internal readonly int ProbeEvent = 0, BeginEvent = 1, EndEvent = 2, ResetEvent = 3, ShutdownEvent = 4;
        internal FixedFoveationNative() => Constructed++;
        internal static bool IsWine()
        {
            WineChecks++;
            return Wine;
        }
        internal bool InitializeDevice(IntPtr resource)
        {
            Initialized++;
            return !RejectInitialize;
        }
        internal bool Enable(bool enabled)
        {
            Enables.Add(enabled);
            if (RejectEnable && enabled)
                return false;
            Enabled = enabled;
            return true;
        }
        internal bool SetDiagnostics(bool enabled)
        {
            DiagnosticRequests.Add(enabled);
            if (RejectDiagnostics && enabled)
                return false;
            DiagnosticsEnabled = enabled;
            if (enabled)
                DiagnosticsReason = "";
            return true;
        }
        internal IntPtr AcquireScope(int width, int height, int samples, int eye, float x, float y, FixedFoveationLevel level)
        {
            if (ThrowAcquire)
                throw new InvalidOperationException("fixture acquisition failure");
            if (RejectAcquire)
                return IntPtr.Zero;
            var packet = (IntPtr)(AcquiredScopes.Count + 1);
            var scope = new Scope(packet, width, height, samples, eye, x, y, level);
            AcquiredScopes.Add(scope);
            LiveScopes.Add(packet, scope);
            return packet;
        }
        internal void CancelScope(IntPtr packet) => LiveScopes.Remove(packet);
        internal string ReadReport()
        {
            Reports++;
            return JsonSerializer.Serialize(new
            {
                supported = Supported,
                failed = Failed,
                reason = Reason,
                diagnosticsEnabled = DiagnosticsEnabled,
                diagnosticsReason = DiagnosticsReason,
                leftApplied = LeftApplied,
                rightApplied = RightApplied,
                leftSequence = LeftSequence,
                rightSequence = RightSequence,
                leftSampleMode = LeftSampleMode,
                rightSampleMode = RightSampleMode,
                leftProfile = LeftProfile,
                rightProfile = RightProfile,
                leftCoarsestRate = LeftCoarsestRate,
                rightCoarsestRate = RightCoarsestRate,
                leftRateClamped = LeftRateClamped,
                rightRateClamped = RightRateClamped,
                leftClampReason = LeftClampReason,
                rightClampReason = RightClampReason,
                leftSampleCoarsestRate = LeftSampleCoarsestRate,
                rightSampleCoarsestRate = RightSampleCoarsestRate,
                leftSampleRateClamped = LeftSampleRateClamped,
                rightSampleRateClamped = RightSampleRateClamped,
                leftGpuMs = LeftGpuMs,
                rightGpuMs = RightGpuMs,
                leftPixelInvocations = 3000000UL,
                rightPixelInvocations = 4000000UL
            });
        }
        internal static void Dispatch(int id, IntPtr packet)
        {
            Events.Add((id, packet));
            if (id == 1 && Enabled && Supported && !Failed && LiveScopes.TryGetValue(packet, out var scope))
            {
                if (scope.Eye == 0)
                {
                    LeftApplied++;
                    LeftProfile = (int)scope.Level - 1;
                    LeftCoarsestRate = scope.Level > FixedFoveationLevel.Strong ? 4 : 2;
                    LeftRateClamped = false;
                    LeftClampReason = "none";
                }
                else
                {
                    RightApplied++;
                    RightProfile = (int)scope.Level - 1;
                    RightCoarsestRate = scope.Level > FixedFoveationLevel.Strong ? 4 : 2;
                    RightRateClamped = false;
                    RightClampReason = "none";
                }
            }
            if (id == 2)
                LiveScopes.Remove(packet);
            if (id == 4)
            {
                Enabled = false;
                LiveScopes.Clear();
            }
        }
        internal static void ResetFixture()
        {
            Constructed = Initialized = WineChecks = Reports = 0;
            Wine = Failed = RejectEnable = RejectInitialize = RejectAcquire = ThrowAcquire = Enabled = false;
            DiagnosticsEnabled = RejectDiagnostics = false;
            Supported = true;
            Reason = "fixture capability";
            DiagnosticsReason = "";
            LeftApplied = RightApplied = 0;
            LeftSequence = RightSequence = 0;
            LeftSampleMode = RightSampleMode = -1;
            LeftProfile = RightProfile = 0;
            LeftCoarsestRate = RightCoarsestRate = LeftSampleCoarsestRate = RightSampleCoarsestRate = 2;
            LeftRateClamped = RightRateClamped = LeftSampleRateClamped = RightSampleRateClamped = false;
            LeftClampReason = RightClampReason = "none";
            LeftGpuMs = RightGpuMs = -1;
            Enables.Clear();
            DiagnosticRequests.Clear();
            Events.Clear();
            AcquiredScopes.Clear();
            LiveScopes.Clear();
        }
        internal sealed record Scope(IntPtr Packet, int Width, int Height, int Samples, int Eye, float X, float Y, FixedFoveationLevel Level);
    }
}
