// Bounded UGUI/XR boundary doubles. The production pointer and trigger gates
// run unchanged; camera projection is perspective so depth/parallax bugs remain
// visible. These checks do not stand in for Unity rendering or headset testing.
using N = System.Numerics;
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed; public string name = "";
        public static bool operator ==(Object? a, Object? b) => ReferenceEquals(a, b) || a is null && b!.Destroyed || b is null && a!.Destroyed;
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? o) => this == o as Object;
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class Component : Object
    {
        public GameObject gameObject = null!; public Transform transform => gameObject.transform;
        public T? GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    }
    public class Behaviour : Component
    {
        public bool enabled = true; public bool isActiveAndEnabled => !Destroyed && enabled && gameObject.activeInHierarchy;
    }
    public sealed class GameObject : Object
    {
        public readonly List<Component> Components = new(); public readonly Transform transform;
        public int layer = 30; public bool activeSelf = true; public bool activeInHierarchy => !Destroyed && activeSelf && (transform.parent?.gameObject.activeInHierarchy ?? true);
        public GameObject(string name)
        {
            this.name = name;
            transform = new RectTransform { gameObject = this };
            Components.Add(transform);
        }
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { gameObject = this };
            Components.Add(c);
            return c;
        }
        public T? GetComponent<T>() where T : class => Components.OfType<T>().FirstOrDefault(c => c is not Object o || !o.Destroyed);
        public T? GetComponentInParent<T>() where T : class
        {
            for (Transform? p = transform; p != null; p = p.parent)
            {
                var c = p.gameObject.GetComponent<T>();
                if (c != null)
                    return c;
            }
            return null;
        }
    }
    public class Transform : Component
    {
        public Transform? parent; public Vector3 position; public Quaternion rotation = Quaternion.identity; public Vector3 localScale = new(1, 1, 1);
        public Vector3 forward => rotation * Vector3.forward; public Vector3 up => rotation * new Vector3(0, 1, 0);
        public Vector3 TransformPoint(Vector3 v) => position + rotation * new Vector3(v.x * localScale.x, v.y * localScale.y, v.z * localScale.z);
        public Vector3 InverseTransformPoint(Vector3 v) => InverseTransformVector(v - position);
        public Vector3 InverseTransformVector(Vector3 v)
        {
            v = Quaternion.Inverse(rotation) * v;
            return new(v.x / localScale.x, v.y / localScale.y, v.z / localScale.z);
        }
        public bool IsChildOf(Transform other)
        {
            for (Transform? p = this; p != null; p = p.parent)
                if (p == other)
                    return true;
            return false;
        }
    }
    public sealed class RectTransform : Transform
    {
        public Rect rect = new(-.5f, -.5f, 1, 1);
    }
    public readonly record struct Rect(float x, float y, float width, float height)
    {
        public float xMin => x; public float xMax => x + width; public float yMin => y; public float yMax => y + height;
    }
    public readonly record struct Vector2(float x, float y)
    {
        public static Vector2 zero => default; public float sqrMagnitude => x * x + y * y;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
        public static explicit operator Vector2(Vector3 a) => new(a.x, a.y);
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        internal N.Vector3 Numerics => new(x, y, z); internal static Vector3 From(N.Vector3 n) => new(n.X, n.Y, n.Z);
        public static Vector3 forward => new(0, 0, 1); public static Vector3 zero => default;
        public static Vector3 operator +(Vector3 a, Vector3 b) => From(a.Numerics + b.Numerics); public static Vector3 operator -(Vector3 a, Vector3 b) => From(a.Numerics - b.Numerics);
        public static Vector3 operator -(Vector3 a) => new(-a.x, -a.y, -a.z); public static Vector3 operator *(Vector3 a, float b) => From(a.Numerics * b); public static float Dot(Vector3 a, Vector3 b) => N.Vector3.Dot(a.Numerics, b.Numerics);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => From(N.Vector3.Lerp(a.Numerics, b.Numerics, t));
    }
    public readonly struct Quaternion(N.Quaternion value)
    {
        internal N.Quaternion Value => value; public static Quaternion identity => new(N.Quaternion.Identity);
        public static Quaternion Inverse(Quaternion a) => new(N.Quaternion.Inverse(a.Value));
        public static Quaternion operator *(Quaternion a, Quaternion b) => new(a.Value * b.Value);
        public static Vector3 operator *(Quaternion a, Vector3 b) => Vector3.From(N.Vector3.Transform(b.Numerics, a.Value));
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => new(N.Quaternion.Slerp(a.Value, b.Value, t));
        public static Quaternion Forward(Vector3 direction)
        {
            var d = N.Vector3.Normalize(direction.Numerics);
            var dot = Math.Clamp(N.Vector3.Dot(N.Vector3.UnitZ, d), -1, 1);
            if (dot > .999999f)
                return identity;
            if (dot < -.999999f)
                return new(N.Quaternion.CreateFromAxisAngle(N.Vector3.UnitY, MathF.PI));
            return new(N.Quaternion.CreateFromAxisAngle(N.Vector3.Normalize(N.Vector3.Cross(N.Vector3.UnitZ, d)), MathF.Acos(dot)));
        }
    }
    public readonly struct Ray
    {
        public readonly Vector3 origin, direction; public Ray(Vector3 origin, Vector3 direction)
        {
            this.origin = origin;
            this.direction = direction;
        }
        public Vector3 GetPoint(float distance) => origin + direction * distance;
    }
    public readonly struct Plane(Vector3 normal, Vector3 point)
    {
        public bool Raycast(Ray ray, out float distance)
        {
            var denominator = Vector3.Dot(normal, ray.direction);
            distance = Math.Abs(denominator) < 1e-6f ? 0 : Vector3.Dot(normal, point - ray.origin) / denominator;
            return Math.Abs(denominator) >= 1e-6f && distance > 0;
        }
    }
    public sealed class Camera : Behaviour
    {
        public enum MonoOrStereoscopicEye
        {
            Mono
        }
        public Vector3 WorldToScreenPoint(Vector3 point, MonoOrStereoscopicEye eye)
        {
            point = transform.InverseTransformPoint(point);
            return new(point.x / point.z * 1000, point.y / point.z * 1000, point.z);
        }
        public Ray ScreenRay(Vector2 p) => new(transform.position, transform.rotation * Vector3.From(N.Vector3.Normalize(new(p.x / 1000, p.y / 1000, 1))));
    }
    public enum RenderMode
    {
        WorldSpace, ScreenSpaceOverlay
    }
    public sealed class Canvas : Behaviour
    {
        public RenderMode renderMode = RenderMode.WorldSpace; public int sortingLayerID, sortingOrder;
    }
    public static class SortingLayer
    {
        public static int GetLayerValueFromID(int id) => id;
    }
    public static class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a, b);
    }
    public static class Time
    {
        public static float unscaledDeltaTime = 1f / 90, unscaledTime;
    }
    public static class Debug
    {
        public static void Log(object message)
        {
        }
    }
}
namespace UnityEngine.EventSystems
{
    public sealed class EventSystem : Behaviour
    {
        public static EventSystem? current; public int pixelDragThreshold = 5; public GameObject? currentSelectedGameObject;
        public void SetSelectedGameObject(GameObject? target, PointerEventData data) => currentSelectedGameObject = target;
    }
    public class BaseRaycaster : Behaviour
    {
        public Camera? eventCamera;
    }
    public struct RaycastResult
    {
        public GameObject? gameObject; public BaseRaycaster? module; public float distance; public Vector3 worldPosition, worldNormal; public Vector2 screenPosition; public int depth, sortingLayer, sortingOrder;
    }
    public sealed class PointerEventData(EventSystem system)
    {
        public enum InputButton
        {
            Left
        }
        public int pointerId, clickCount; public InputButton button; public float clickTime;
        public Vector2 position, delta, pressPosition, scrollDelta;
        public RaycastResult pointerCurrentRaycast, pointerPressRaycast;
        public GameObject? pointerEnter, pointerPress, rawPointerPress, pointerDrag, lastPress;
        public readonly List<GameObject> hovered = new(); public bool eligibleForClick, dragging, useDragThreshold;
        public Camera? pressEventCamera => pointerPressRaycast.module?.eventCamera;
        public EventSystem eventSystem => system;
    }
    public interface IPointerClickHandler
    {
        void OnPointerClick(PointerEventData d);
    }
    public interface IPointerDownHandler
    {
        void OnPointerDown(PointerEventData d);
    }
    public interface IPointerUpHandler
    {
        void OnPointerUp(PointerEventData d);
    }
    public interface IPointerEnterHandler
    {
        void OnPointerEnter(PointerEventData d);
    }
    public interface IPointerExitHandler
    {
        void OnPointerExit(PointerEventData d);
    }
    public interface IDragHandler
    {
        void OnDrag(PointerEventData d);
    }
    public interface IBeginDragHandler
    {
        void OnBeginDrag(PointerEventData d);
    }
    public interface IEndDragHandler
    {
        void OnEndDrag(PointerEventData d);
    }
    public interface IInitializePotentialDragHandler
    {
        void OnInitializePotentialDrag(PointerEventData d);
    }
    public interface IScrollHandler
    {
        void OnScroll(PointerEventData d);
    }
    public interface IDropHandler
    {
        void OnDrop(PointerEventData d);
    }
    public interface ISelectHandler
    {
    }
    public static class ExecuteEvents
    {
        public static readonly Action<IPointerClickHandler, PointerEventData> pointerClickHandler = (x, d) => x.OnPointerClick(d);
        public static readonly Action<IPointerDownHandler, PointerEventData> pointerDownHandler = (x, d) => x.OnPointerDown(d);
        public static readonly Action<IPointerUpHandler, PointerEventData> pointerUpHandler = (x, d) => x.OnPointerUp(d);
        public static readonly Action<IPointerEnterHandler, PointerEventData> pointerEnterHandler = (x, d) => x.OnPointerEnter(d);
        public static readonly Action<IPointerExitHandler, PointerEventData> pointerExitHandler = (x, d) => x.OnPointerExit(d);
        public static readonly Action<IDragHandler, PointerEventData> dragHandler = (x, d) => x.OnDrag(d);
        public static readonly Action<IBeginDragHandler, PointerEventData> beginDragHandler = (x, d) => x.OnBeginDrag(d);
        public static readonly Action<IEndDragHandler, PointerEventData> endDragHandler = (x, d) => x.OnEndDrag(d);
        public static readonly Action<IInitializePotentialDragHandler, PointerEventData> initializePotentialDrag = (x, d) => x.OnInitializePotentialDrag(d);
        public static readonly Action<IScrollHandler, PointerEventData> scrollHandler = (x, d) => x.OnScroll(d);
        public static readonly Action<IDropHandler, PointerEventData> dropHandler = (x, d) => x.OnDrop(d);
        private static IEnumerable<T> Handlers<T>(GameObject? go) => go != null && go.activeInHierarchy ? go.Components.OfType<T>().Where(c => c is not Behaviour b || b.isActiveAndEnabled) : Array.Empty<T>();
        public static bool Execute<T>(GameObject? target, PointerEventData data, Action<T, PointerEventData> action)
        {
            var found = false;
            foreach (var c in Handlers<T>(target))
            {
                action(c, data);
                found = true;
            }
            return found;
        }
        public static GameObject? ExecuteHierarchy<T>(GameObject? target, PointerEventData data, Action<T, PointerEventData> action)
        {
            for (var t = target?.transform; t != null; t = t.parent)
                if (Execute(t.gameObject, data, action))
                    return t.gameObject;
            return null;
        }
        public static GameObject? GetEventHandler<T>(GameObject? target)
        {
            for (var t = target?.transform; t != null; t = t.parent)
                if (Handlers<T>(t.gameObject).Any())
                    return t.gameObject;
            return null;
        }
    }
    public static class RaycasterManager
    {
        public static readonly List<BaseRaycaster> All = new(); public static List<BaseRaycaster> GetRaycasters() => All;
    }
}
namespace UnityEngine.UI
{
    public class Selectable : Behaviour
    {
        public bool interactable = true; public bool IsInteractable() => interactable;
    }
    public class Graphic : Behaviour
    {
        public bool raycastTarget = true; public int depth = 1; public readonly CanvasRenderer canvasRenderer = new(); public RectTransform rectTransform => (RectTransform)transform;
        public bool Raycast(Vector2 screen, Camera camera) => true;
    }
    public sealed class Image : Graphic
    {
    }
    public sealed class CanvasRenderer
    {
        public bool cull;
    }
    public sealed class GraphicRaycaster : EventSystems.BaseRaycaster
    {
        public bool ignoreReversedGraphics = true;
    }
    public static class GraphicRegistry
    {
        public static readonly Dictionary<Canvas, List<Graphic>> All = new(); public static IList<Graphic> GetGraphicsForCanvas(Canvas c) => All[c];
    }
}
namespace NOVR
{
    public static class LayerHelper
    {
        public static int GetVrUiLayer() => 30;
    }
    public sealed class ModConfiguration
    {
        public static readonly ModConfiguration Instance = new();
        public readonly Bindings ControllerBindings = new(); public readonly Motion MotionControls = new();
        public sealed class Setting<T>(T value)
        {
            public T Value = value;
        }
        public sealed class Bindings
        {
            public readonly Setting<bool> PointerEnabled = new(true); public readonly Setting<float> PointerResponse = new(0);
        }
        public sealed class Motion
        {
            public readonly Setting<bool> LeftHanded = new(false);
        }
    }
}
namespace NOVR.MotionControls
{
    public struct ControllerSample
    {
        public bool Valid, Primary, Secondary, Menu, Shoulder, Top, Outside, StickClick; public float Trigger, Grip; public UnityEngine.Vector2 Axis; public UnityEngine.Vector3 AimPosition; public UnityEngine.Quaternion AimRotation; public int Device;
    }
    public static class TrackedControllers
    {
        public static ControllerSample Left, Right; public static ControllerSample Read(bool left) => left ? Left : Right;
    }
    public static class HelmetNightVision
    {
        public static bool Left, Right; public static bool OwnsHand(bool left) => left ? Left : Right;
    }
    public static class NativeHandOptionWheel
    {
        public static bool Left, Right; public static bool OwnsHand(bool left) => left ? Left : Right;
    }
    public static class InteractionHaptics
    {
        public static int Pulses; public static void Pulse(int device, float strength, float duration) => Pulses++;
    }
}
namespace NOVR.VrUi.Native
{
    public sealed class FeedbackPreviewButton : UnityEngine.Behaviour
    {
        public void Arm(bool left, int device)
        {
        }
        public void Cancel()
        {
        }
    }
}
public static class SceneSingleton<T>
{
    public static T? i;
}
public sealed class DynamicMap : UnityEngine.Behaviour
{
    public static bool mapMaximized; public UnityEngine.UI.Image? mapBackground;
}
