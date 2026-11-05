namespace UnityEngine
{
    public readonly record struct Vector3(float x, float y, float z);
    public readonly record struct Quaternion(float x, float y, float z, float w);
    public static class Time
    {
        public static int frameCount;
    }
}
namespace UnityEngine.XR
{
    [Flags]
    public enum InputTrackingState
    {
        None = 0, Position = 1, Rotation = 2
    }
    public enum XRNode
    {
        Head
    }
    public readonly record struct InputFeatureUsage<T>(string Name);
    public static class CommonUsages
    {
        public static readonly InputFeatureUsage<bool> isTracked = new("isTracked");
        public static readonly InputFeatureUsage<InputTrackingState> trackingState = new("trackingState");
        public static readonly InputFeatureUsage<Vector3> centerEyePosition = new("centerEyePosition"), devicePosition = new("devicePosition");
        public static readonly InputFeatureUsage<Quaternion> centerEyeRotation = new("centerEyeRotation"), deviceRotation = new("deviceRotation");
    }
    public sealed class InputDevice
    {
        public bool isValid = true;
        public readonly Dictionary<string, object> Values = new();
        public bool TryGetFeatureValue<T>(InputFeatureUsage<T> usage, out T value)
        {
            value = default!;
            if (!Values.TryGetValue(usage.Name, out var boxed))
                return false;
            value = (T)boxed;
            return true;
        }
        public void Set<T>(InputFeatureUsage<T> usage, T value) => Values[usage.Name] = value!;
    }
    public static class InputDevices
    {
        public static InputDevice Head = new();
        public static InputDevice GetDeviceAtXRNode(XRNode _) => Head;
    }
    public static class XRSettings
    {
        public static bool enabled = true;
    }
}
namespace UnityEngine.InputSystem
{
    public class InputDevice
    {
        public bool added = true, enabled = true;
        public double lastUpdateTime;
    }
    public static class InputSystem
    {
        public static readonly List<InputDevice> devices = new();
    }
}
namespace UnityEngine.InputSystem.LowLevel
{
    public static class InputState
    {
        public static double currentTime;
    }
}
namespace UnityEngine.XR.OpenXR
{
    public static class OpenXRUtility
    {
        public static bool IsSessionFocused = true;
    }
    public static class OpenXRRuntime
    {
        public static bool ExtensionEnabled = true;
        public static bool IsExtensionEnabled(string name) => ExtensionEnabled && name == Features.Interactions.EyeGazeInteraction.extensionString;
    }
}
namespace UnityEngine.XR.OpenXR.Features.Interactions
{
    public class EyeGazeInteraction
    {
        public const string extensionString = "XR_EXT_eye_gaze_interaction";
        public class EyeGazeDevice : InputSystem.InputDevice
        {
            public GazeControl? pose = new();
        }
    }
    public struct GazeValue
    {
        public bool isTracked;
        public InputTrackingState trackingState;
        public Vector3 position;
        public Quaternion rotation;
    }
    public sealed class GazeControl
    {
        public GazeValue Value;
        public int Reads;
        public GazeValue ReadValue()
        {
            Reads++;
            return Value;
        }
    }
}
