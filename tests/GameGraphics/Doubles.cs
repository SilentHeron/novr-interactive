internal static class PlayerSettings
{
    internal static GraphicsHelper graphics = null!;
    internal static NuclearOption.Effects.DetailSettings DetailSettings = null!;
}

internal sealed class GraphicsHelper
{
    internal static readonly List<string> ShadowQualityOptions = new() { "Off", "Low", "Medium", "High", "Ultra" };
    private float _cloudDetail = .73f;
    private int _shadowQuality = 3;
    internal float CloudDetail
    {
        get => _cloudDetail;
        set
        {
            _cloudDetail = value;
            NativeCalls.Events.Add("cloud:" + value);
        }
    }
    internal int ShadowQuality
    {
        get => _shadowQuality;
        set
        {
            _shadowQuality = value;
            NativeCalls.Events.Add("shadow:" + value);
        }
    }
    internal readonly int AntiAliasing = 2, ShadowDistance = 4500;
    internal readonly bool Vsync = true;
}

namespace NuclearOption.Effects
{
    internal sealed class DetailSettings
    {
        internal static readonly float TreeRangeMultiplierMin = .5f, TreeRangeMultiplierMax = 2f;
        private float _trees = 1.37f;
        internal float TreeRangeMultiplier
        {
            get => _trees;
            set
            {
                _trees = value;
                NativeCalls.Events.Add("trees:" + value);
            }
        }
        internal readonly bool GrassEnabled = true;
    }
}

internal static class NativeCalls
{
    internal static readonly List<string> Events = new();
    internal static void Reset()
    {
        Events.Clear();
        PlayerSettings.graphics = new();
        PlayerSettings.DetailSettings = new();
    }
}

namespace UnityEngine
{
    internal static class PlayerPrefs
    {
        internal static void Save() => NativeCalls.Events.Add("save");
    }
    internal sealed class GameObject
    {
        internal readonly string Name;
        internal readonly Transform transform = new();
        internal GameObject(string name)
        {
            Name = name;
            transform.gameObject = this;
        }
    }
    internal sealed class Transform
    {
        internal GameObject gameObject = null!;
        internal readonly List<UiBounds> Widgets = new();
        internal readonly Dictionary<string, NOVR.VrUi.ViewMenuControls.Check> Checks = new();
    }
    internal readonly record struct UiBounds(string Label, float X, float Y, float Width, float Height)
    {
        internal float Left => X - Width / 2;
        internal float Right => X + Width / 2;
        internal float Bottom => Y - Height / 2;
        internal float Top => Y + Height / 2;
    }
    internal sealed class Font
    {
    }
    internal readonly record struct Vector2(float X, float Y);
    internal readonly record struct Color;
}

namespace NOVR.VrUi
{
    // uGUI is the boundary here. Capture the actual page's callbacks and reads;
    // Primitive dimensions are the shared-control boundary contract. Capture
    // page positions here; real drag/snap/no-notify behavior has its own suite.
    internal static class ViewMenuControls
    {
        internal static readonly UnityEngine.Color Muted = new();
        internal sealed class TextValue
        {
            internal string text = "";
            internal UnityEngine.Color color;
        }
        internal sealed class SliderValue
        {
            internal bool interactable = true;
        }
        internal sealed class Scalar(float min, float max, Func<float> read, Action<float> write, Func<float, string> format, float step)
        {
            internal readonly float Min = min, Max = max;
            internal readonly float Step = step;
            internal readonly Func<float> Read = read;
            internal readonly Action<float> Write = write;
            internal readonly Func<float, string> Format = format;
            internal readonly SliderValue Slider = new();
        }
        internal sealed class ToggleValue
        {
            internal bool interactable = true;
            internal bool isOn;
        }
        internal sealed class Check(Func<bool> read, Action<bool> write)
        {
            internal readonly ToggleValue Toggle = new();
            internal readonly Action<bool> Write = write;
            internal void Refresh() => Toggle.isOn = read();
        }
        internal static Check Checkbox(UnityEngine.Transform parent, UnityEngine.Font font, string label,
            UnityEngine.Vector2 point, float width, Func<bool> read, Action<bool> write)
        {
            var check = new Check(read, write);
            check.Refresh();
            parent.Checks.Add(label, check);
            parent.Widgets.Add(new(label, point.X, point.Y, width, 42));
            return check;
        }
        internal static TextValue Text(UnityEngine.Transform parent, UnityEngine.Font font, string text,
            UnityEngine.Vector2 point, UnityEngine.Vector2 size, int sizePx)
        {
            parent.Widgets.Add(new("Live text", point.X, point.Y, size.X, size.Y));
            return new() { text = text };
        }
    }

    internal sealed partial class WristMenu
    {
        private readonly UnityEngine.Font _font = new();
        private readonly List<(UnityEngine.GameObject page, Action refresh)> _settingRefresh = new();
        private readonly List<(ViewMenuControls.TextValue text, Func<string> read)> _liveLabels = new();
        internal readonly Dictionary<string, ViewMenuControls.Scalar> Scalars = new();
        internal readonly Dictionary<string, ChoiceValue> Choices = new();
        internal readonly List<string> Notes = new();
        internal UnityEngine.GameObject Build() => BuildGameGraphicsPage();
        internal UnityEngine.GameObject BuildFoveation() => BuildFixedFoveationPage();
        internal UnityEngine.GameObject BuildImageQuality() => BuildImageQualityPage();
        internal void Refresh()
        {
            foreach (var (_, refresh) in _settingRefresh)
                refresh();
            foreach (var (text, read) in _liveLabels)
                text.text = read();
        }
        internal string Status => _liveLabels.Single().text.text;
        internal IEnumerable<string> LiveText => _liveLabels.Select(label => label.text.text);
        private static UnityEngine.GameObject Page(string title) => new(title);
        private static void Section(UnityEngine.Transform parent, string title, float y)
        {
            parent.Widgets.Add(new(title, 0, y, 620, 30));
        }
        private void Note(UnityEngine.Transform parent, string text, float y, float height)
        {
            Notes.Add(text);
            parent.Widgets.Add(new(text, 0, y, 608, height));
        }
        private ViewMenuControls.Scalar Setting(UnityEngine.Transform parent, string title, float y, float min, float max,
            Func<float> read, Action<float> write, Func<float, string> format, float step = 0)
        {
            var scalar = new ViewMenuControls.Scalar(min, max, read, write, format, step);
            Scalars.Add(title, scalar);
            parent.Widgets.Add(new(title, 0, y, 620, 62));
            return scalar;
        }
        private void Setting(UnityEngine.Transform parent, string title, NOVR.Rendering.SettingValue<bool> entry, int column, float y) =>
            Setting(parent, title, () => entry.Value, value => entry.Value = value, column, y);
        private void Setting(UnityEngine.Transform parent, string title, Func<bool> read, Action<bool> write, int column, float y)
        {
            var control = ViewMenuControls.Checkbox(parent, _font, title, new UnityEngine.Vector2(column == 0 ? -160 : 160, y), 300, read, write);
            _settingRefresh.Add((parent.gameObject, control.Refresh));
        }
        private void Choice(UnityEngine.Transform parent, string title, string[] options, Func<int> read,
            Action<int> write, float y, Func<int, bool>? canSelect = null) =>
            Choices.Add(title, new(options, read, write, canSelect!));
        private static string Percent(float value) => $"{value * 100:0}%";
        internal sealed record ChoiceValue(string[] Options, Func<int> Read, Action<int> Write, Func<int, bool> CanSelect);
    }
}
