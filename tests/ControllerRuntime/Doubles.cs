#pragma warning disable CS0649, CS8618
namespace UnityEngine
{
    public readonly record struct Vector2(float x, float y)
    {
        public static Vector2 zero => default;
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    {
        public static Quaternion identity => new(0, 0, 0, 1);
        public static float Dot(Quaternion a, Quaternion b) => a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
        public static Quaternion operator *(Quaternion a, Quaternion b) => new(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y, a.w * b.y - a.x * b.z + a.y * b.w + a.z * b.x,
            a.w * b.z + a.x * b.y - a.y * b.x + a.z * b.w, a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
    }
    public class MonoBehaviour
    {
    }
    public static class Time
    {
        public static int frameCount; public static float unscaledTime;
    }
    public static class Debug
    {
        public static readonly List<string> Logs = new(), Warnings = new();
        public static void Log(string message) => Logs.Add(message);
        public static void LogWarning(string message) => Warnings.Add(message);
    }
    public static class Screen
    {
        public static int width = 1920, height = 1080;
    }
    public static class SystemInfo
    {
        public static string graphicsDeviceType = "Direct3D11";
    }
}
namespace UnityEngine.InputSystem.Controls
{
    public class AxisControl
    {
        public float Value; public virtual float ReadValue() => Value;
    }
    public class ButtonControl : AxisControl
    {
        public bool isPressed => Value >= .5f;
    }
    public class Vector2Control
    {
        public UnityEngine.Vector2 Value; public UnityEngine.Vector2 ReadValue() => Value;
    }
    public class Vector3Control
    {
        public UnityEngine.Vector3 Value; public UnityEngine.Vector3 ReadValue() => Value;
    }
    public class QuaternionControl
    {
        public UnityEngine.Quaternion Value = UnityEngine.Quaternion.identity; public UnityEngine.Quaternion ReadValue() => Value;
    }
    public class IntegerControl
    {
        public int Value; public int ReadValue() => Value;
    }
}
namespace UnityEngine.InputSystem.XR
{
    public class XRController
    {
        public static XRController? leftHand, rightHand;
        public string layout = "Test runtime derived layout";
        public readonly Dictionary<string, object> Controls = new();
        public T? TryGetChildControl<T>(string name) where T : class => Controls.GetValueOrDefault(name) as T;
    }
}
namespace UnityEngine.XR
{
    public enum XRNode
    {
        Head, LeftHand, RightHand
    }
    [Flags]
    public enum InputTrackingState
    {
        None = 0, Position = 1, Rotation = 2
    }
    public readonly record struct InputFeatureUsage<T>(string name);
    public class DeviceState
    {
        public string Name = "Test controller";
        public readonly Dictionary<string, object> Features = new();
        public DeviceState Set<T>(InputFeatureUsage<T> usage, T value) where T : notnull
        {
            Features[usage.name] = value;
            return this;
        }
    }
    public readonly struct InputDevice
    {
        private readonly DeviceState? _state;
        public InputDevice(DeviceState state) => _state = state;
        public bool isValid => _state != null;
        public string name => _state?.Name ?? "";
        public bool TryGetFeatureValue<T>(InputFeatureUsage<T> usage, out T value)
        {
            if (_state?.Features.GetValueOrDefault(usage.name) is T found)
            {
                value = found;
                return true;
            }
            value = default!;
            return false;
        }
    }
    public static class InputDevices
    {
        public static readonly Dictionary<XRNode, InputDevice> Devices = new();
        public static InputDevice GetDeviceAtXRNode(XRNode node) => Devices.GetValueOrDefault(node);
    }
    public static class XRSettings
    {
        public static bool enabled = true; public static int eyeTextureWidth = 3028, eyeTextureHeight = 3028;
    }
    public static class CommonUsages
    {
        public static readonly InputFeatureUsage<bool> isTracked = new("isTracked"), userPresence = new("userPresence"),
            gripButton = new("gripButton"), triggerButton = new("triggerButton"), primary2DAxisClick = new("primary2DAxisClick"),
            primaryButton = new("primaryButton"), secondaryButton = new("secondaryButton"), menuButton = new("menuButton");
        public static readonly InputFeatureUsage<float> grip = new("grip"), trigger = new("trigger");
        public static readonly InputFeatureUsage<InputTrackingState> trackingState = new("trackingState");
        public static readonly InputFeatureUsage<UnityEngine.Vector3> devicePosition = new("devicePosition");
        public static readonly InputFeatureUsage<UnityEngine.Quaternion> deviceRotation = new("deviceRotation");
        public static readonly InputFeatureUsage<UnityEngine.Vector2> primary2DAxis = new("primary2DAxis");
    }
}
namespace UnityEngine.XR.OpenXR.Input
{
    public class PoseControl
    {
        public readonly UnityEngine.InputSystem.Controls.ButtonControl isTracked = new();
        public readonly UnityEngine.InputSystem.Controls.IntegerControl trackingState = new();
        public readonly UnityEngine.InputSystem.Controls.Vector3Control position = new();
        public readonly UnityEngine.InputSystem.Controls.QuaternionControl rotation = new();
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
        public static int NameReads;
        public static bool Fail;
        public static string name
        {
            get
            {
                NameReads++;
                if (Fail)
                    throw new InvalidOperationException();
                return "SteamVR";
            }
        }
        public static string version => "1.0";
    }
    public partial class OpenXRSettings
    {
        internal Features.OpenXRFeature[] features = Array.Empty<Features.OpenXRFeature>();
        internal static T CreateInstance<T>() => Activator.CreateInstance<T>();
    }
}
namespace UnityEngine.XR.OpenXR.Features
{
    public class OpenXRFeature
    {
        public string nameUi, featureIdInternal, company, openxrExtensionStrings; public bool enabled;
    }
    public class OpenXRInteractionFeature : OpenXRFeature
    {
    }
}
namespace UnityEngine.XR.OpenXR.Features.Interactions
{
    public class OculusTouchControllerProfile : Features.OpenXRInteractionFeature
    {
        public const string featureId = "touch";
        public class OculusTouchController : UnityEngine.InputSystem.XR.XRController
        {
        }
    }
    public class MetaQuestTouchPlusControllerProfile : Features.OpenXRInteractionFeature
    {
        public const string featureId = "plus";
        public class QuestTouchPlusController : UnityEngine.InputSystem.XR.XRController
        {
        }
    }
    public class MetaQuestTouchProControllerProfile : Features.OpenXRInteractionFeature
    {
        public const string featureId = "pro";
        public class QuestProTouchController : UnityEngine.InputSystem.XR.XRController
        {
        }
    }
    public class ValveIndexControllerProfile : Features.OpenXRInteractionFeature
    {
        public const string featureId = "index";
    }
    public class HTCViveControllerProfile : Features.OpenXRInteractionFeature
    {
        public const string featureId = "vive";
    }
    public class MicrosoftMotionControllerProfile : Features.OpenXRInteractionFeature
    {
        public const string featureId = "wmr";
    }
    public class EyeGazeInteraction : Features.OpenXRInteractionFeature
    {
        public const string featureId = "eye", extensionString = "XR_EXT_eye_gaze_interaction";
    }
}
namespace Valve.OpenXR.Utils
{
    public class SteamFrameControllerProfile : UnityEngine.XR.OpenXR.Features.OpenXRInteractionFeature
    {
        public const string featureId = "frame";
        public class SteamFrameController : UnityEngine.InputSystem.XR.XRController
        {
        }
    }
}
namespace NOVR
{
    internal static class NOVRHeadsetData
    {
        public static UnityEngine.Vector3 TranslationAnchor, TranslationCalibrationOffset;
        public static UnityEngine.Quaternion RotationCalibrationOffset = UnityEngine.Quaternion.identity;
        public static UnityEngine.Vector3 MapTrackingPosition(UnityEngine.Vector3 p) => p + TranslationCalibrationOffset;
    }
}
namespace NOVR.MotionControls
{
    internal static class CockpitProfiles
    {
        public static UnityEngine.Vector3 SeatOffset;
    }
}
namespace NOVR.VrUi
{
    internal static class WristMenu
    {
        public static bool IsPerformancePage;
    }
}
