namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class DefaultExecutionOrder : Attribute
    {
        internal DefaultExecutionOrder(int order)
        {
        }
    }
    internal class MonoBehaviour;
    internal readonly record struct Vector3(float x, float y, float z)
    {
        internal static Vector3 right => new(1, 0, 0);
        internal static Vector3 up => new(0, 1, 0);
        internal static Vector3 forward => new(0, 0, 1);
        internal float magnitude => MathF.Sqrt(x * x + y * y + z * z);
    }
    internal sealed class Transform
    {
        internal Vector3 position;
        internal Vector3 TransformVector(Vector3 value) => value;
    }
    internal sealed class Mesh
    {
        internal string name = "Seat";
        internal int vertexCount = 24;
        internal int subMeshCount = 1;
        internal Bounds bounds = new(new(.5f, 1f, .4f));
    }
    internal readonly record struct Bounds(Vector3 size);
    internal sealed class MeshFilter
    {
        internal string name = "Seat";
        internal Mesh? sharedMesh = new();
        internal readonly Transform transform = new();
        internal readonly MeshRenderer Renderer = new();
        internal Pilot? ParentPilot;
        internal T? GetComponent<T>() where T : class => Renderer as T;
        internal T? GetComponentInParent<T>() where T : class => ParentPilot as T;
    }
    internal sealed class MeshRenderer
    {
        internal readonly List<Material> Materials = new() { new() };
        internal void GetSharedMaterials(List<Material> target)
        {
            target.Clear();
            target.AddRange(Materials);
        }
        internal bool isPartOfStaticBatch
        {
            get; set;
        }
    }
    internal sealed class Shader
    {
        internal string name = "Universal Render Pipeline/Lit";
    }
    internal sealed class Material
    {
        internal string name = "Seats";
        internal Shader? shader = new();
        internal int renderQueue = 2000;
        internal string RenderType = "Opaque";
        internal readonly Dictionary<string, float> Floats = new() { ["_Surface"] = 0 };
        internal readonly HashSet<string> Keywords = new();
        internal string GetTag(string tag, bool searchFallback, string defaultValue) => RenderType;
        internal bool HasProperty(string property) => Floats.ContainsKey(property);
        internal float GetFloat(string property) => Floats[property];
        internal bool IsKeywordEnabled(string keyword) => Keywords.Contains(keyword);
    }
    internal sealed class SkinnedMeshRenderer;
    internal enum CameraType
    {
        Game, SceneView
    }
    internal enum StereoTargetEyeMask
    {
        None, Both
    }
    internal sealed class Camera
    {
        internal readonly Transform transform = new();
        internal CameraType cameraType = CameraType.Game;
        internal StereoTargetEyeMask stereoTargetEye = StereoTargetEyeMask.Both;
    }
    internal static class Mathf
    {
        internal static float Clamp(float value, float minimum, float maximum) => Math.Clamp(value, minimum, maximum);
    }
    internal static class Time
    {
        internal static float unscaledTime;
    }
    internal static class Debug
    {
        internal static void Log(string message)
        {
        }
        internal static void LogWarning(string message)
        {
        }
    }
}

namespace UnityEngine.XR
{
    internal static class XRSettings
    {
        internal static bool enabled;
    }
}

namespace UnityEngine.Rendering
{
    internal readonly struct ScriptableRenderContext;
    internal static class RenderPipelineManager
    {
        internal static event Action<ScriptableRenderContext, UnityEngine.Camera>? beginCameraRendering, endCameraRendering;
        internal static event Action<ScriptableRenderContext, UnityEngine.Camera[]>? endFrameRendering;
        internal static int Subscriptions => (beginCameraRendering?.GetInvocationList().Length ?? 0)
            + (endCameraRendering?.GetInvocationList().Length ?? 0) + (endFrameRendering?.GetInvocationList().Length ?? 0);
        internal static void Begin(UnityEngine.Camera camera) => beginCameraRendering?.Invoke(default, camera);
        internal static void End(UnityEngine.Camera camera) => endCameraRendering?.Invoke(default, camera);
        internal static void EndFrame() => endFrameRendering?.Invoke(default, Array.Empty<UnityEngine.Camera>());
    }
}

internal sealed class Pilot
{
    internal object player = new();
    internal bool dead
    {
        get; set;
    }
    internal bool ejected
    {
        get; set;
    }
    internal UnityEngine.MeshFilter[] Filters = Array.Empty<UnityEngine.MeshFilter>();
    internal int DiscoveryReads;
    internal T[] GetComponentsInChildren<T>(bool includeInactive)
    {
        DiscoveryReads++;
        return Filters.OfType<T>().ToArray();
    }
}
internal sealed class Cockpit
{
    internal bool Detached
    {
        get; set;
    }
    internal bool IsDetached() => Detached;
}
internal sealed class Aircraft
{
    internal Cockpit? cockpit = new();
    internal Pilot[]? pilots;
    internal bool Ejected;
    internal bool HasEjected() => Ejected;
}
internal enum CameraMode
{
    cockpit, external
}
internal sealed class CameraStateManager
{
    internal static CameraMode cameraMode;
    internal Aircraft? followingUnit;
    internal UnityEngine.Camera? cockpitCamRender;
}
internal static class SceneSingleton<T> where T : class
{
    internal static T? i;
}
internal static class GameManager
{
    internal static Aircraft? LocalAircraft;
    internal static object LocalPlayer = new();
    internal static bool GetLocalAircraft(out Aircraft? value)
    {
        value = LocalAircraft;
        return value != null;
    }
    internal static bool IsLocalPlayer(object player) => ReferenceEquals(player, LocalPlayer);
}
internal static class APIBus
{
    internal static UnityEngine.Camera? MainCamera;
}
namespace NOVR
{
    internal sealed class Setting<T>(T value)
    {
        internal T Value = value;
    }
    internal sealed class ModConfiguration
    {
        internal static ModConfiguration Instance = new();
        internal readonly Setting<bool> SeatClearanceEnabled = new(true);
        internal readonly Setting<float> SeatClearanceRadius = new(Comfort.SeatClearanceRange.Default);
    }
}
namespace NOVR.MotionControls
{
    internal static class TrackedControllers
    {
        internal static bool Available;
    }
}
namespace NOVR.Comfort
{
    // Resource/geometry boundary only. The production coordinator owns when
    // this lease is prepared, applied, restored and disposed.
    internal sealed class SeatClearanceMesh : IDisposable
    {
        internal static readonly List<SeatClearanceMesh> Created = new();
        internal readonly UnityEngine.MeshFilter Filter;
        internal bool Applied, Disposed;
        internal int Prepares, Applies, Restores, Disposals;
        internal UnityEngine.Vector3 PreparedCenter;
        internal float PreparedRadius;
        internal SeatClearanceMesh(UnityEngine.MeshFilter filter, UnityEngine.MeshRenderer renderer)
        {
            Filter = filter;
            Created.Add(this);
        }
        internal void Prepare(UnityEngine.Vector3 center, float radius)
        {
            if (Disposed)
                throw new InvalidOperationException("Disposed seat was reused.");
            Prepares++;
            PreparedCenter = center;
            PreparedRadius = radius;
        }
        internal void Apply()
        {
            Applies++;
            Applied = true;
        }
        internal void Restore()
        {
            Restores++;
            Applied = false;
        }
        public void Dispose()
        {
            Disposals++;
            Disposed = true;
            Applied = false;
        }
    }
}
