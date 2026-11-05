namespace UnityEngine
{
    public class Object
    {
        public bool destroyed; public static void Destroy(Object obj) => obj.destroyed = true;
    }
    public class Component : Object
    {
        public GameObject gameObject = null!; public Transform transform => gameObject.transform;
    }
    public class GameObject : Object
    {
        public readonly string Name; public bool activeSelf; public int layer; public readonly Transform transform;
        private readonly Dictionary<Type, Component> _components = new();
        public GameObject(string name, params Type[] types)
        {
            Name = name;
            transform = new RectTransform { gameObject = this };
            _components[typeof(RectTransform)] = transform;
            foreach (var type in types.Where(t => t != typeof(RectTransform)))
            {
                var component = (Component)Activator.CreateInstance(type)!;
                component.gameObject = this;
                _components[type] = component;
            }
        }
        public T GetComponent<T>() => (T)(object)_components[typeof(T)];
        public Component AddComponent(Type type)
        {
            var value = (Component)Activator.CreateInstance(type)!;
            value.gameObject = this;
            _components[type] = value;
            return value;
        }
        public T AddComponent<T>() where T : Component, new()
        {
            var item = new T { gameObject = this };
            _components[typeof(T)] = item;
            return item;
        }
        public void SetActive(bool value) => activeSelf = value;
    }
    public class Transform : Component
    {
        public Transform parent = null!;
        public readonly List<Transform> children = new();
        public Quaternion localRotation;
        public void SetParent(Transform value, bool _)
        {
            parent?.children.Remove(this);
            parent = value;
            value.children.Add(this);
        }
        public T GetComponent<T>() => gameObject.GetComponent<T>();
    }
    public sealed class RectTransform : Transform
    {
        public Vector2 anchoredPosition, sizeDelta, anchorMin, anchorMax;
    }
    public sealed class CanvasRenderer : Component
    {
    }
    public sealed class CanvasGroup : Component
    {
        public float alpha = 1; public bool interactable, blocksRaycasts;
    }
    public readonly record struct Quaternion(float x, float y, float z)
    {
        public static Quaternion Euler(float x, float y, float z) => new(x, y, z);
    }
    public readonly record struct Vector2(float x, float y)
    {
        public static Vector2 zero => default; public static Vector2 one => new(1, 1); public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y); public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x - b.x, a.y - b.y);
    }
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 zero => default;
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 down => new(0, -1, 0);
        public static Vector3 left => new(-1, 0, 0);
        public static Vector3 right => new(1, 0, 0);
        public static Vector3 forward => new(0, 0, 1);
        public static Vector3 back => new(0, 0, -1);
        public static Vector3 operator *(Vector3 value, float scale) => new(value.x * scale, value.y * scale, value.z * scale);
    }
    public readonly record struct Color(float r, float g, float b, float a = 1)
    {
        public static Color clear => new(0, 0, 0, 0); public static Color white => new(1, 1, 1, 1);
    }
    public static class Mathf
    {
        public static bool Approximately(float a, float b) => Math.Abs(a - b) < .000001f; public static float Abs(float value) => Math.Abs(value); public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max); public static float Clamp01(float value) => Clamp(value, 0, 1); public static int RoundToInt(float value) => (int)MathF.Round(value);
    }
    public static class Time
    {
        public static float unscaledTime;
    }
    public sealed class Font : Object
    {
    }
    public static class Resources
    {
        public static T GetBuiltinResource<T>(string _) where T : new() => new();
    }
    public enum TextAnchor
    {
        MiddleCenter, MiddleLeft, MiddleRight
    }
}
namespace UnityEngine.XR
{
    public readonly record struct InputDevice(int Id);
}
namespace UnityEngine.UI
{
    public sealed class Button : UnityEngine.Component
    {
        public bool interactable = true;
    }
    public sealed class Text : UnityEngine.Component
    {
        public string text = ""; public UnityEngine.Font font = null!; public int fontSize; public UnityEngine.TextAnchor alignment; public UnityEngine.Color color; public bool raycastTarget;
    }
    public sealed class Image : UnityEngine.Component
    {
        public UnityEngine.Color color; public bool raycastTarget; public UnityEngine.RectTransform rectTransform => (UnityEngine.RectTransform)transform;
    }
    public struct Navigation
    {
        public Mode mode; public enum Mode
        {
            None
        }
    }
    public sealed class Toggle : UnityEngine.Component
    {
        public enum ToggleTransition
        {
            None
        }
        public ToggleTransition toggleTransition; public Navigation navigation; public Image targetGraphic = null!, graphic = null!; public bool interactable = true;
        public readonly ChangedEvent onValueChanged = new(); private bool _on;
        public bool isOn
        {
            get => _on; set
            {
                var changed = _on != value;
                _on = value;
                if (changed)
                    onValueChanged.Invoke(value);
            }
        }
        public void SetIsOnWithoutNotify(bool value) => _on = value;
        public sealed class ChangedEvent
        {
            private Action<bool>? _handlers; public void AddListener(Action<bool> action) => _handlers += action; public void Invoke(bool value) => _handlers?.Invoke(value);
        }
    }
    public sealed class Slider : UnityEngine.Component
    {
        public enum Direction
        {
            LeftToRight
        }
        public Direction direction; public bool interactable = true; private bool _wholeNumbers; public bool wholeNumbers
        {
            get => _wholeNumbers; set
            {
                if (_wholeNumbers == value)
                    return;
                _wholeNumbers = value;
                this.value = _value;
            }
        }
        public float minValue, maxValue;
        public UnityEngine.RectTransform handleRect = null!, fillRect = null!; public Image targetGraphic = null!; public Navigation navigation;
        public readonly ChangedEvent onValueChanged = new(); private float _value;
        public float value
        {
            get => _value; set
            {
                var previous = _value;
                SetValueWithoutNotify(value);
                if (_value != previous)
                    onValueChanged.Invoke(_value);
            }
        }
        public void SetValueWithoutNotify(float input) => _value = Math.Clamp(wholeNumbers ? MathF.Round(input) : input, minValue, maxValue);
        public sealed class ChangedEvent
        {
            private Action<float>? _handlers; public void AddListener(Action<float> action) => _handlers += action; public void Invoke(float value) => _handlers?.Invoke(value);
        }
    }
}
namespace NOVR
{
    internal sealed partial class ModConfiguration
    {
        internal readonly BepInEx.Configuration.ConfigEntry<bool> FocusedFlightHud = new(false, new(null));
        internal readonly BepInEx.Configuration.ConfigEntry<float> FlightHudScale = new(.38f, new(new BepInEx.Configuration.AcceptableValueRange<float>(.22f, .75f))),
            MarkerScale = new(.45f, new(null)), MarkerLabelScale = new(.55f, new(null)), ReticleScale = new(.45f, new(null)), WeaponHudScale = new(.5f, new(null));
    }
    internal static class LayerHelper
    {
        internal static int GetVrUiLayer() => 0; internal static void SetLayerRecursive(UnityEngine.Transform _, int __)
        {
        }
    }
}
namespace NOVR.MotionControls
{
    internal static class FlightControlMath
    {
        internal static bool IsFinite(float value) => float.IsFinite(value); internal static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
    }
    internal enum InteractionCue
    {
        Touch, Click, Grab
    }
    internal static class InteractionHaptics
    {
        internal static readonly List<InteractionCue> Cues = new(); internal static int SilentPulses;
        internal static void Pulse(UnityEngine.XR.InputDevice device, InteractionCue cue) => Cues.Add(cue);
        internal static void PulseOnly(UnityEngine.XR.InputDevice device, float strength, float duration) => SilentPulses++;
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    internal struct HudWidgetPose
    {
        internal float Scale, Opacity, Background; internal bool Visible; internal UnityEngine.Vector3 Position, Euler;
    }
    internal static class HudWidgetProfiles
    {
        internal const float MinScale = .35f, MaxScale = 3f, MinOpacity = .25f;
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
        internal void Begin()
        {
            if (Preview || !Available)
                return;
            Begins++;
            Preview = true;
            _before = new(_poses);
        }
        internal void Finish(bool save)
        {
            if (!save && _before != null)
                _poses = _before;
            _before = null;
            Preview = false;
        }
        internal void BringWithinReach()
        {
        }
        internal void ResetPreview()
        {
        }
        // The fixture records routing; actual persistence and unified layout
        // transactions are exercised by the profile/manager checks separately.
        internal bool SaveToSlot(int slot)
        {
            SaveRequests.Add(slot);
            if (slot < 1 || slot > 3 || !Preview || !Available)
                return false;
            SuccessfulSaves.Add(slot);
            ProfileSlot = slot;
            Finish(true);
            return true;
        }
        internal bool LoadSlot(int slot)
        {
            LoadRequests.Add(slot);
            if (slot < 1 || slot > 3 || Preview || !Available)
                return false;
            SuccessfulLoads.Add(slot);
            ProfileSlot = slot;
            return true;
        }
        internal void Select(string id) => SelectedId = id;
        internal HudWidgetPose GetPose(string id) => _poses[id];
        internal void SetAppearance(string id, HudWidgetPose pose)
        {
            AppearanceSaves++;
            _poses[id] = pose;
        }
        internal void SetAngles(string id, UnityEngine.Vector2 angles)
        {
            AngleWrites++;
            var pose = _poses[id];
            pose.Euler = new(angles.x, angles.y, 0);
            _poses[id] = pose;
        }
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
        private readonly Font _font = new();
        private void Section(Transform parent, string title, float y, float width = 620, float x = 0) => ViewMenuControls.Section(parent, _font, title, y, width, x);
        private static string Percent(float value) => $"{value * 100:0}%";
        private static string Degrees(float value) => $"{value:0.#}°";
        private Text ActionButton(Transform parent, string title, int column, float y, Action action, bool danger = false) => AddButton(parent, title, column, (int)y, action);
        internal Slider Scalar(string title) => _hudSliders.Single(item => item.control.Slider.gameObject.Name == title).control.Slider;
        internal Toggle Visibility => _hudVisibility!.Toggle;
        internal void Nudge(string title, float delta)
        {
            var slider = Scalar(title);
            if (slider.interactable)
                slider.value += delta;
        }
        internal void ForceNudge(string title, float delta)
        {
            var slider = Scalar(title);
            slider.onValueChanged.Invoke(slider.value + delta);
        }
        internal Slider Backplate => _hudBackplateSlider!;
        internal Text BackplateLabel => _hudBackplateLabel!;
        internal Text AngleInfo => _hudAngleInfo!;
        internal Text ProfilesInfo => _aircraftProfilesInfo!;
        internal string ActivePage => _pages.Single(page => page.activeSelf).Name;
        internal bool ButtonsHavePageParents => _buttons.All(button =>
            button.label.transform.parent.parent.gameObject.Name == button.page);
        internal string ScalarPage(string title) => Scalar(title).transform.parent.gameObject.Name;
        internal static int Closes;
        private GameObject Page(string title)
        {
            var page = new GameObject(title);
            _pages.Add(page);
            return page;
        }
        private Text AddButton(Transform parent, string text, int column, int row, Action? action)
        {
            var button = new GameObject(text, typeof(Button));
            var label = Label(button.transform, text, default, default, 20);
            button.transform.SetParent(parent, false);
            _buttons.Add((parent.gameObject.Name, column, row, label, action));
            return label;
        }
        private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        {
            var go = new GameObject("Label", typeof(Text));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.text = text;
            return label;
        }
        private void ShowPage(GameObject page)
        {
            foreach (var other in _pages)
                other.activeSelf = other == page;
        }
        private void Back()
        {
        }
        internal static void Close() => Closes++;
        internal void Build()
        {
            CreateHudLayoutPages();
            Refresh();
        }
        internal void Appearance() => OpenWidgetAppearance();
        internal void Profiles() => OpenAircraftProfiles();
        internal void Refresh()
        {
            // Match the real menu Update: dynamic labels are refreshed every
            // frame. Distinct initially-empty labels must retain their callbacks.
            foreach (var item in _liveLabels)
                item.label.text = item.text();
            RefreshHudLayoutEditor();
        }
        internal Text Cell(string page, int column, int row) =>
            _buttons.Single(b => b.page == page && b.column == column && b.row == row).label;
        internal void Click(string page, string text)
        {
            var button = Buttons[(page, text)];
            if (button.label.transform.parent.GetComponent<Button>().interactable)
                button.action?.Invoke();
        }
        internal bool Enabled(string page, string text) => Buttons[(page, text)].label.transform.parent.GetComponent<Button>().interactable;
        internal void ForceCallback(string page, string text) => Buttons[(page, text)].action?.Invoke();
    }
}

namespace BepInEx.Configuration
{
    public sealed class AcceptableValueRange<T>
    {
        public T MinValue, MaxValue; public AcceptableValueRange(T min, T max)
        {
            MinValue = min;
            MaxValue = max;
        }
    }
    public sealed class ConfigDescription
    {
        public object? AcceptableValues; public ConfigDescription(object? values)
        {
            AcceptableValues = values;
        }
    }
    public sealed class ConfigEntry<T>
    {
        public T Value; public ConfigDescription Description; public ConfigEntry(T value, ConfigDescription description)
        {
            Value = value;
            Description = description;
        }
    }
}
