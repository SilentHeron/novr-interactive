// Unity's CLR reference and destroyed-object comparisons differ. Preserve that
// distinction here: two different destroyed objects are not equal to each other.
namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;
        public string name = "";
        public static bool operator ==(Object? a, Object? b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a is null)
                return b!.Destroyed;
            if (b is null)
                return a.Destroyed;
            return false;
        }
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => this == other as Object;
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
    }
    public class MonoBehaviour : Component
    {
        public bool enabled = true, activeInHierarchy = true;
        public bool isActiveAndEnabled => !Destroyed && enabled && activeInHierarchy;
    }
    public sealed class Camera : MonoBehaviour
    {
        public static Camera? main;
        public StereoTargetEyeMask stereoTargetEye;
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public float depth, nearClipPlane, farClipPlane;
        public int cullingMask;
        public bool allowHDR, allowMSAA;
        public object? targetTexture;
        public Rect rect;
    }
    public enum RenderMode
    {
        ScreenSpaceOverlay, WorldSpace
    }
    public sealed class Canvas : Component
    {
        private Camera? _worldCamera;
        public int CameraAssignments;
        public Camera? worldCamera
        {
            get => _worldCamera; set
            {
                _worldCamera = value;
                CameraAssignments++;
            }
        }
        public RenderMode renderMode;
        public float planeDistance;
    }
    public sealed class GameObject : Object
    {
        private readonly List<Component> _components = new();
        public readonly Transform transform;
        public bool activeSelf = true;
        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform { gameObject = this };
            _components.Add(transform);
        }
        public T AddComponent<T>() where T : Component
        {
            var value = Activator.CreateInstance<T>();
            value.gameObject = this;
            _components.Add(value);
            return value;
        }
        public T? GetComponent<T>() where T : Component => _components.OfType<T>().FirstOrDefault(x => !x.Destroyed);
        public void SetActive(bool value) => activeSelf = value;
    }
    public sealed class Transform : Component
    {
        public Transform? parent;
        public Vector3 position, localPosition, localScale;
        public Quaternion rotation, localRotation;
        private readonly List<Transform> _children = new();
        public int childCount => _children.Count;
        public Transform GetChild(int index) => _children[index];
        public void SetParent(Transform value) => parent = value;
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        internal System.Numerics.Vector3 Value => new(x, y, z);
        internal static Vector3 From(System.Numerics.Vector3 value) => new(value.X, value.Y, value.Z);
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.Value + b.Value);
        public static Vector3 operator -(Vector3 a, Vector3 b) => From(a.Value - b.Value);
        public static Vector3 operator -(Vector3 value) => From(-value.Value);
        public float magnitude => Value.Length();
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    {
        private System.Numerics.Quaternion Value => new(x, y, z, w);
        private static Quaternion From(System.Numerics.Quaternion value) => new(value.X, value.Y, value.Z, value.W);
        public static Quaternion identity => new(0, 0, 0, 1);
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => From(System.Numerics.Quaternion.Slerp(a.Value, b.Value, t));
        public static Quaternion Inverse(Quaternion value) => From(System.Numerics.Quaternion.Inverse(value.Value));
        public static Quaternion operator *(Quaternion a, Quaternion b) => From(a.Value * b.Value);
        public static Vector3 operator *(Quaternion q, Vector3 v) => Vector3.From(System.Numerics.Vector3.Transform(v.Value, q.Value));
        public static Quaternion Euler(float pitch, float yaw, float roll) => From(System.Numerics.Quaternion.CreateFromYawPitchRoll(yaw * MathF.PI / 180, pitch * MathF.PI / 180, roll * MathF.PI / 180));
        public Vector3 eulerAngles => new(Wrap(MathF.Asin(Math.Clamp(2 * (w * x - y * z), -1, 1))), Wrap(MathF.Atan2(2 * (w * y + x * z), 1 - 2 * (x * x + y * y))), Wrap(MathF.Atan2(2 * (w * z + x * y), 1 - 2 * (x * x + z * z))));
        private static float Wrap(float radians)
        {
            var degrees = radians * 180 / MathF.PI;
            return degrees < 0 ? degrees + 360 : degrees;
        }
    }
    public static class Mathf
    {
        public static float DeltaAngle(float from, float to)
        {
            var value = (to - from) % 360;
            if (value < 0)
                value += 360;
            return value > 180 ? value - 360 : value;
        }
    }
    public static class Application
    {
        public static event Action? onBeforeRender;
        internal static int Subscribers => onBeforeRender?.GetInvocationList().Length ?? 0;
        internal static void Render() => onBeforeRender?.Invoke();
    }
    public readonly record struct Color(float r, float g, float b, float a)
    {
        public static Color clear => new(0, 0, 0, 0);
    }
    public readonly record struct Rect(float x, float y, float width, float height);
    public enum CameraClearFlags
    {
        SolidColor, Depth
    }
    public enum StereoTargetEyeMask
    {
        Both
    }
    public static class Time
    {
        public static float unscaledDeltaTime = .01f;
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder(int order) : Attribute
    {
        public int Order => order;
    }
    public static class StackTraceUtility
    {
        public static string ExtractStackTrace() => "test";
    }
    public static class Debug
    {
        public static int Logs, Warnings, Errors, Exceptions;
        public static void Log(object message) => Logs++;
        public static void LogWarning(object message) => Warnings++;
        public static void LogError(object message) => Errors++;
        public static void LogException(Exception exception) => Exceptions++;
    }
}
namespace UnityEngine.Rendering.Universal
{
    public enum CameraRenderType
    {
        Base, Overlay
    }
    public sealed class UniversalAdditionalCameraData : UnityEngine.Component
    {
        public CameraRenderType renderType;
        public bool renderPostProcessing;
        public readonly List<UnityEngine.Camera> cameraStack = new();
    }
}
namespace NOVR
{
    public static class LayerHelper
    {
        public static int LayerAssignments;
        public static int GetVrUiLayer() => 30;
        public static void SetLayerRecursive(UnityEngine.Transform root, int layer) => LayerAssignments++;
    }
    public sealed class ModConfiguration
    {
        public static readonly ModConfiguration Instance = new();
        public readonly BepInEx.Configuration.ConfigFile Config = new();
        public readonly Setting HmdFollowResponse = new();
        public sealed class Setting
        {
            public float Value;
        }
    }
}
namespace NOVR.MotionControls
{
    public static class FlightControlMath
    {
        public static float SmoothingWeight(float dt, float response) => 1;
    }
    public static class CockpitProfiles
    {
        public static UnityEngine.Vector3 SeatOffset;
    }
}
namespace NOVR.VrCamera
{
    public static class VrCameraManager
    {
        public static readonly HashSet<UnityEngine.Camera> IgnoredCameras = new();
    }
}
namespace NOVR.VrUi
{
    public sealed class UIBehaviorPatcher : NOVR.NOVRBehaviour
    {
        public static void DoPatching()
        {
        }
    }
    public sealed class VrUiCursor : NOVR.NOVRBehaviour
    {
    }
}
namespace NOVR.VrUi.Native
{
    public sealed class NativeVrUiRoot : NOVR.NOVRBehaviour
    {
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    public sealed class CockpitAlerts : UnityEngine.MonoBehaviour
    {
    }
}
namespace BepInEx.Configuration
{
    public sealed class SettingChangedEventArgs : EventArgs
    {
    }
    public sealed class ConfigFile
    {
        public event EventHandler<SettingChangedEventArgs>? SettingChanged;
        internal int Subscribers => SettingChanged?.GetInvocationList().Length ?? 0;
        internal void Raise() => SettingChanged?.Invoke(this, new SettingChangedEventArgs());
    }
}
namespace UnityEngine.XR
{
    public enum XRNode
    {
        CenterEye = 2
    }
    public static class InputTracking
    {
        internal static Vector3 Position;
        internal static Quaternion Rotation = Quaternion.identity;
        internal static int PositionReads, RotationReads;
        internal static XRNode PositionNode, RotationNode;
        [Obsolete("Keep the installed center-eye API contract in the source-linked check.")]
        public static Vector3 GetLocalPosition(XRNode node)
        {
            PositionReads++;
            PositionNode = node;
            return Position;
        }
        [Obsolete("Keep the installed center-eye API contract in the source-linked check.")]
        public static Quaternion GetLocalRotation(XRNode node)
        {
            RotationReads++;
            RotationNode = node;
            return Rotation;
        }
    }
}
