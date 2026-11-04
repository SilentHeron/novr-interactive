namespace UnityEngine
{
    public class Object { public bool destroyed; public static void Destroy(Object obj) => obj.destroyed = true; }
    public class Component : Object { public GameObject gameObject = null!; public Transform transform => gameObject.transform; }
    public class GameObject : Object
    {
        public readonly string Name; public bool activeSelf; public int layer; public readonly Transform transform;
        private readonly Dictionary<Type, Component> _components = new();
        public GameObject(string name, params Type[] types)
        {
            Name = name; transform = new RectTransform { gameObject = this };
            _components[typeof(RectTransform)] = transform;
            foreach (var type in types.Where(t => t != typeof(RectTransform)))
            { var component = (Component)Activator.CreateInstance(type)!; component.gameObject = this; _components[type] = component; }
        }
        public T GetComponent<T>() => (T)(object)_components[typeof(T)];
        public T AddComponent<T>() where T : Component, new() { var item = new T { gameObject = this }; _components[typeof(T)] = item; return item; }
        public void SetActive(bool value) => activeSelf = value;
    }
    public class Transform : Component
    {
        public Transform parent = null!;
        public readonly List<Transform> children = new();
        public Quaternion localRotation;
        public void SetParent(Transform value, bool _) { parent?.children.Remove(this); parent = value; value.children.Add(this); }
        public T GetComponent<T>() => gameObject.GetComponent<T>();
    }
    public sealed class RectTransform : Transform { public Vector2 anchoredPosition, sizeDelta; }
    public sealed class CanvasRenderer : Component { }
    public sealed class CanvasGroup : Component { public float alpha = 1; public bool interactable, blocksRaycasts; }
    public readonly record struct Quaternion(float x, float y, float z) { public static Quaternion Euler(float x, float y, float z) => new(x, y, z); }
    public readonly record struct Vector2(float x, float y)
    { public static Vector2 zero => default; public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x+b.x,a.y+b.y); public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x-b.x,a.y-b.y); }
    public readonly record struct Vector3(float x, float y, float z) { public static Vector3 zero => default; }
    public readonly record struct Color(float r, float g, float b, float a = 1) { public static Color clear => new(0, 0, 0, 0); public static Color white => new(1,1,1,1); }
    public static class Mathf
    { public static bool Approximately(float a, float b) => Math.Abs(a - b) < .000001f; public static float Abs(float value) => Math.Abs(value); public static float Clamp(float value,float min,float max) => Math.Clamp(value,min,max); public static float Clamp01(float value) => Clamp(value,0,1); public static int RoundToInt(float value) => (int)MathF.Round(value); }
    public static class Time { public static float unscaledTime; }
    public sealed class Font : Object { }
    public static class Resources { public static T GetBuiltinResource<T>(string _) where T : new() => new(); }
    public enum TextAnchor { MiddleCenter }
}
namespace UnityEngine.XR { public readonly record struct InputDevice(int Id); }
namespace UnityEngine.UI
{
    public sealed class Button : UnityEngine.Component { public bool interactable = true; }
    public sealed class Text : UnityEngine.Component { public string text = ""; public UnityEngine.Font font = null!; public int fontSize; public UnityEngine.TextAnchor alignment; public UnityEngine.Color color; public bool raycastTarget; }
    public sealed class Image : UnityEngine.Component { public UnityEngine.Color color; public bool raycastTarget; public UnityEngine.RectTransform rectTransform => (UnityEngine.RectTransform)transform; }
    public struct Navigation { public Mode mode; public enum Mode { None } }
    public sealed class Slider : UnityEngine.Component
    {
        public enum Direction { LeftToRight }
        public Direction direction; public bool wholeNumbers, interactable = true; public float minValue, maxValue;
        public UnityEngine.RectTransform handleRect = null!; public Image targetGraphic = null!; public Navigation navigation;
        public readonly ChangedEvent onValueChanged = new(); private float _value;
        public float value { get => _value; set { var previous = _value; SetValueWithoutNotify(value); if (_value != previous) onValueChanged.Invoke(_value); } }
        public void SetValueWithoutNotify(float input) => _value = Math.Clamp(wholeNumbers ? MathF.Round(input) : input, minValue, maxValue);
        public sealed class ChangedEvent { private Action<float>? _handlers; public void AddListener(Action<float> action) => _handlers += action; public void Invoke(float value) => _handlers?.Invoke(value); }
    }
}
namespace NOVR
{
    internal static class LayerHelper { internal static int GetVrUiLayer() => 0; internal static void SetLayerRecursive(UnityEngine.Transform _, int __) { } }
}
namespace NOVR.MotionControls
{
    internal static class FlightControlMath { internal static bool IsFinite(float value) => float.IsFinite(value); internal static float Clamp(float value,float min,float max) => Math.Clamp(value,min,max); }
    internal enum InteractionCue { Touch, Click, Grab }
    internal static class InteractionHaptics
    {
        internal static readonly List<InteractionCue> Cues = new(); internal static int SilentPulses;
        internal static void Pulse(UnityEngine.XR.InputDevice device, InteractionCue cue) => Cues.Add(cue);
        internal static void PulseOnly(UnityEngine.XR.InputDevice device, float strength, float duration) => SilentPulses++;
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    internal struct HudWidgetPose { internal float Scale, Opacity, Background; internal bool Visible; internal UnityEngine.Vector3 Position, Euler; }
    internal static class HudWidgetProfiles
    {
        internal static readonly string[] Ids = { "weapons", "countermeasures", "power", "threats", "ew", "feed", "map", "damage", "objectives" };
        internal static string Label(string id) => id;
        internal static HudWidgetPose Sanitize(string id, HudWidgetPose pose) => pose;
    }
    internal sealed class CockpitHudWidgets
    {
        internal static CockpitHudWidgets? Instance;
        internal static bool Editing => Instance?.Preview == true;
        internal bool Preview, Available = true, SnapEnabled = true;
        internal bool CanSaveProfile => Available;
        internal int Begins, AppearanceSaves, AngleWrites;
        internal int ProfileSlot = 1;
        internal string ProfileAircraftName = "FS-20 Revoker";
        internal readonly List<int> SaveRequests = new(), LoadRequests = new(), SuccessfulSaves = new(), SuccessfulLoads = new();
        internal string SelectedId = "weapons";
        internal string? AppearanceFocus;
        internal void FocusAppearance(string? id) => AppearanceFocus = id;
        private Dictionary<string, HudWidgetPose> _poses = HudWidgetProfiles.Ids.ToDictionary(x => x, _ => new HudWidgetPose { Position = new(.2f, -.3f, .4f), Scale = 1, Opacity = 1, Visible = true });
        private Dictionary<string, HudWidgetPose>? _before;
        internal void Begin() { if (Preview || !Available) return; Begins++; Preview = true; _before = new(_poses); }
        internal void Finish(bool save) { if (!save && _before != null) _poses = _before; _before = null; Preview = false; }
        internal void BringWithinReach() { }
        internal void ResetPreview() { }
        // The fixture records routing; actual persistence and unified layout
        // transactions are exercised by the profile/manager checks separately.
        internal bool SaveToSlot(int slot)
        {
            SaveRequests.Add(slot);
            if (slot < 1 || slot > 3 || !Preview || !Available) return false;
            SuccessfulSaves.Add(slot); ProfileSlot = slot; Finish(true); return true;
        }
        internal bool LoadSlot(int slot)
        {
            LoadRequests.Add(slot);
            if (slot < 1 || slot > 3 || Preview || !Available) return false;
            SuccessfulLoads.Add(slot); ProfileSlot = slot; return true;
        }
        internal void Select(string id) => SelectedId = id;
        internal HudWidgetPose GetPose(string id) => _poses[id];
        internal void SetAppearance(string id, HudWidgetPose pose) { AppearanceSaves++; _poses[id] = pose; }
        internal void SetAngles(string id, UnityEngine.Vector2 angles)
        { AngleWrites++; var pose = _poses[id]; pose.Euler = new(angles.x, angles.y, 0); _poses[id] = pose; }
    }
}
namespace NOVR.VrUi
{
    using UnityEngine;
    using UnityEngine.UI;
    internal sealed partial class WristMenu
    {
        private readonly List<(Text label, Func<string> text)> _liveLabels = new();
        private readonly List<(string page, int column, int row, Text label, Action? action)> _buttons = new();
        internal Dictionary<(string page, string text), (Text label, Action? action)> Buttons =>
            _buttons.ToDictionary(b => (b.page, b.label.text), b => (b.label, b.action));
        private readonly List<GameObject> _pages = new();
        private string _building = "";
        internal Slider Backplate => _hudBackplateSlider!;
        internal Text BackplateLabel => _hudBackplateLabel!;
        internal Text AngleInfo => _hudAngleInfo!;
        internal Text ProfilesInfo => _aircraftProfilesInfo!;
        internal string ActivePage => _pages.Single(page => page.activeSelf).Name;
        internal static int Closes;
        private GameObject Page(string title) { _building = title; var page = new GameObject(title); _pages.Add(page); return page; }
        private Text AddButton(string text, int column, int row, Action? action)
        {
            var button = new GameObject(text, typeof(Button)); var label = Label(button.transform, text, default, default, 20);
            _buttons.Add((_building, column, row, label, action)); return label;
        }
        private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        { var go = new GameObject("Label", typeof(Text)); go.transform.SetParent(parent, false); var label = go.GetComponent<Text>(); label.text = text; return label; }
        private void ShowPage(GameObject page) { foreach (var other in _pages) other.activeSelf = other == page; }
        private void Back() { }
        internal static void Close() => Closes++;
        internal void Build() { CreateHudLayoutPages(); Refresh(); }
        internal void Appearance() => OpenWidgetAppearance();
        internal void Profiles() => OpenAircraftProfiles();
        internal void Refresh()
        {
            // Match the real menu Update: dynamic labels are refreshed every
            // frame. Distinct initially-empty labels must retain their callbacks.
            foreach (var item in _liveLabels) item.label.text = item.text();
            RefreshHudLayoutEditor();
        }
        internal Text Cell(string page, int column, int row) =>
            _buttons.Single(b => b.page == page && b.column == column && b.row == row).label;
        internal void Click(string page, string text)
        { var button = Buttons[(page, text)]; if (button.label.transform.parent.GetComponent<Button>().interactable) button.action?.Invoke(); }
        internal bool Enabled(string page, string text) => Buttons[(page, text)].label.transform.parent.GetComponent<Button>().interactable;
        internal void ForceCallback(string page, string text) => Buttons[(page, text)].action?.Invoke();
    }
}
