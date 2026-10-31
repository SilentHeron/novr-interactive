namespace UnityEngine
{
    public class Component { public GameObject gameObject = null!; public Transform transform => gameObject.transform; }
    public class GameObject
    {
        public readonly string Name; public bool activeSelf; public readonly Transform transform;
        private readonly Dictionary<Type, Component> _components = new();
        public GameObject(string name, params Type[] types)
        {
            Name = name; transform = new RectTransform { gameObject = this };
            _components[typeof(RectTransform)] = transform;
            foreach (var type in types.Where(t => t != typeof(RectTransform)))
            { var component = (Component)Activator.CreateInstance(type)!; component.gameObject = this; _components[type] = component; }
        }
        public T GetComponent<T>() => (T)(object)_components[typeof(T)];
    }
    public class Transform : Component
    {
        public Transform parent = null!;
        public void SetParent(Transform value, bool _) => parent = value;
        public T GetComponent<T>() => gameObject.GetComponent<T>();
    }
    public sealed class RectTransform : Transform { public Vector2 anchoredPosition, sizeDelta; }
    public sealed class CanvasRenderer : Component { }
    public readonly record struct Vector2(float x, float y);
    public readonly record struct Color(float r, float g, float b, float a = 1) { public static Color clear => new(0, 0, 0, 0); }
    public static class Mathf { public static bool Approximately(float a, float b) => Math.Abs(a - b) < .000001f; }
}
namespace UnityEngine.UI
{
    public sealed class Button : UnityEngine.Component { public bool interactable = true; }
    public sealed class Text : UnityEngine.Component { public string text = ""; }
    public sealed class Image : UnityEngine.Component { public UnityEngine.Color color; public bool raycastTarget; }
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
namespace NOVR.VrUi.SpecialBehavior
{
    internal struct HudWidgetPose { internal float Scale, Opacity, Background; internal bool Visible; }
    internal static class HudWidgetProfiles
    {
        internal static readonly string[] Ids = { "weapons", "countermeasures", "power", "threats", "ew", "feed", "map" };
        internal static string Label(string id) => id;
        internal static HudWidgetPose Sanitize(string id, HudWidgetPose pose) => pose;
    }
    internal sealed class CockpitHudWidgets
    {
        internal static CockpitHudWidgets? Instance;
        internal static bool Editing => Instance?.Preview == true;
        internal bool Preview, Available = true, SnapEnabled = true;
        internal int Begins, AppearanceSaves;
        internal string SelectedId = "weapons";
        private readonly Dictionary<string, HudWidgetPose> _poses = HudWidgetProfiles.Ids.ToDictionary(x => x, _ => new HudWidgetPose { Scale = 1, Opacity = 1, Visible = true });
        internal void Begin() { Begins++; Preview = true; }
        internal void Finish(bool _) => Preview = false;
        internal void BringWithinReach() { }
        internal void ResetPreview() { }
        internal void UseDefaultForAll() => Finish(true);
        internal void Select(string id) => SelectedId = id;
        internal HudWidgetPose GetPose(string id) => _poses[id];
        internal void SetAppearance(string id, HudWidgetPose pose) { AppearanceSaves++; _poses[id] = pose; }
    }
}
namespace NOVR.VrUi
{
    using UnityEngine;
    using UnityEngine.UI;
    internal sealed partial class WristMenu
    {
        private readonly List<(Text label, Func<string> text)> _liveLabels = new();
        internal readonly Dictionary<(string page, string text), (Text label, Action? action)> Buttons = new();
        private readonly List<GameObject> _pages = new();
        private string _building = "";
        internal Slider Backplate => _hudBackplateSlider!;
        internal Text BackplateLabel => _hudBackplateLabel!;
        private GameObject Page(string title) { _building = title; var page = new GameObject(title); _pages.Add(page); return page; }
        private Text AddButton(string text, int column, int row, Action? action)
        {
            var button = new GameObject(text, typeof(Button)); var label = Label(button.transform, text, default, default, 20);
            Buttons[(_building, text)] = (label, action); return label;
        }
        private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
        { var go = new GameObject("Label", typeof(Text)); go.transform.SetParent(parent, false); var label = go.GetComponent<Text>(); label.text = text; return label; }
        private void ShowPage(GameObject page) { foreach (var other in _pages) other.activeSelf = other == page; }
        private void Back() { }
        internal static void Close() { }
        internal void Build() => CreateHudLayoutPages();
        internal void Appearance() => OpenWidgetAppearance();
        internal void Refresh() => RefreshHudLayoutEditor();
        internal void Click(string page, string text)
        { var button = Buttons[(page, text)]; if (button.label.transform.parent.GetComponent<Button>().interactable) button.action?.Invoke(); }
    }
}
