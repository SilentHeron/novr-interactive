// Only rendering/device boundaries are doubled. VrUiCursor.Update, pointer
// arbitration, gates and controller UGUI dispatch are the production methods.
// Binding restoration itself is exercised by the UiBindingOwnership suite.
namespace UnityEngine
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder(int order) : System.Attribute { public int Order => order; }
    public static class Application { public static bool isFocused = true; }
    public static class Screen { public static int width = 1920, height = 1080; }
    public enum CursorLockMode { None, Locked }
    public static class Cursor { public static bool visible = true; public static CursorLockMode lockState; }
    public readonly record struct Color(float r, float g, float b, float a)
    {
        public static Color white => new(1, 1, 1, 1);
        public static Color Lerp(Color a, Color b, float value) => value >= .5f ? b : a;
        public static implicit operator Color(Color32 value) => new(value.r / 255f, value.g / 255f, value.b / 255f, value.a / 255f);
    }
    public readonly record struct Color32(byte r, byte g, byte b, byte a)
    {
        public static implicit operator Color32(Color value) => new((byte)(value.r * 255), (byte)(value.g * 255), (byte)(value.b * 255), (byte)(value.a * 255));
    }
    public enum TextureFormat { RGBA32 }
    public enum FilterMode { Bilinear }
    public enum TextureWrapMode { Clamp }
    public sealed class Texture2D(int width, int height, TextureFormat format, bool mipmap) : Object
    {
        public FilterMode filterMode; public TextureWrapMode wrapMode;
        public void SetPixels32(Color32[] values) { _ = width + height; _ = format; _ = mipmap; }
        public void Apply() { }
    }
    public sealed class Material : Object
    {
        public bool HasProperty(string name) => true;
        public void SetFloat(string name, float value) { }
    }
    public sealed class LineRenderer : Behaviour
    {
        public bool useWorldSpace, receiveShadows; public int positionCount;
        public float startWidth, endWidth; public Rendering.ShadowCastingMode shadowCastingMode;
        public Material? sharedMaterial;
        public void SetPosition(int index, Vector3 value) { }
    }
    public static class RectTransformUtility
    {
        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 point, Camera camera) => false;
    }
}
namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off }
    public enum CullMode { Off }
}
namespace UnityEngine.UI
{
    public sealed class RawImage : Graphic { public Texture2D? texture; public Color color; }
}
namespace UnityEngine.InputSystem.Controls
{
    public sealed class ButtonControl { public bool isPressed, wasPressedThisFrame; }
    public sealed class Vector2Control { public Vector2 Value; public Vector2 ReadValue() => Value; }
}
namespace UnityEngine.InputSystem
{
    public class Mouse
    {
        public static Mouse? current;
        public bool added = true; public string path = "/Mouse";
        public readonly Controls.ButtonControl leftButton = new(), rightButton = new(), middleButton = new();
        public readonly Controls.Vector2Control position = new(), delta = new(), scroll = new();
        public LowLevel.MouseState State;
        public void MakeCurrent() => current = this;
    }
    public static class InputSystem
    {
        public static readonly List<object> devices = new();
        public static T AddDevice<T>(string name) where T : Mouse, new()
        {
            var mouse = new T { path = "/" + name };
            devices.Add(mouse);
            mouse.MakeCurrent();
            return mouse;
        }
        public static void RemoveDevice(Mouse mouse) { mouse.added = false; devices.Remove(mouse); if (Mouse.current == mouse) Mouse.current = null; }
    }
}
namespace UnityEngine.InputSystem.LowLevel
{
    public struct MouseState { public Vector2 position, delta, scroll; public ushort buttons; }
    public static class InputState { public static void Change(Mouse mouse, MouseState state) => mouse.State = state; }
}
namespace UnityEngine.InputSystem.UI
{
    public sealed class InputSystemUIInputModule : Behaviour { }
}
namespace UnityEngine.XR
{
    public static class XRSettings { public static bool enabled = true; }
    public enum XRNode { LeftHand, RightHand, Head }
    public struct InputFeatureUsage<T> { }
    public static class CommonUsages
    {
        public static readonly InputFeatureUsage<bool> isTracked = new(), userPresence = new();
        public static readonly InputFeatureUsage<int> trackingState = new();
    }
    public readonly struct InputDevice
    {
        public bool isValid => true;
        public bool TryGetFeatureValue<T>(InputFeatureUsage<T> usage, out T value) { value = default!; return false; }
    }
    public static class InputDevices { public static InputDevice GetDeviceAtXRNode(XRNode node) => default; }
}
namespace UnityEngine.XR.OpenXR
{
    public static class OpenXRUtility { public static bool IsSessionFocused = true; }
}
namespace NOVR
{
    public class NOVRBehaviour : UnityEngine.Behaviour
    {
        protected virtual void Awake() { }
        protected virtual void OnDisable() { }
    }
    public static class APIBus { public static UnityEngine.Camera? CockpitHudCamera; }
}
namespace NOVR.MotionControls
{
    public static class CockpitMaterials
    {
        public static UnityEngine.Material Opaque(string name, UnityEngine.Color color) => new();
    }
}
namespace NOVR.VrUi
{
    public static class WristMenu { public static bool IsOpen; }
    internal sealed class VirtualMouseBindingOwnership
    {
        internal static readonly List<VirtualMouseBindingOwnership> Instances = new();
        internal UnityEngine.InputSystem.UI.InputSystemUIInputModule? Module;
        internal int Acquires, Releases;
        internal bool IsAcquired => Module != null;
        internal VirtualMouseBindingOwnership() => Instances.Add(this);
        internal bool Matches(UnityEngine.InputSystem.UI.InputSystemUIInputModule? module) => module != null && Module == module;
        internal bool Acquire(UnityEngine.InputSystem.UI.InputSystemUIInputModule module, string path) { Module = module; Acquires++; return true; }
        internal void Release() { if (Module != null) Releases++; Module = null; }
    }
}
namespace NOVR.VrUi.Native
{
    public sealed class NativeVrUiRoot : UnityEngine.Behaviour { public bool IsVisible; }
}
