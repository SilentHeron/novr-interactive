using System;
using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

// Ordinary uGUI Selectables keep the same point/trigger/drag path as native
// menus. Refresh never fires a change event or rounds a saved setting.
internal static class ViewMenuControls
{
    internal static readonly Color Surface = new(.055f, .085f, .10f, 1);
    internal static readonly Color Accent = new(.08f, .45f, .49f, 1);
    internal static readonly Color Muted = new(.68f, .78f, .80f, 1);
    internal const float Width = 620;
    internal sealed class Scalar
    {
        internal readonly Slider Slider;
        internal readonly Text Value;
        private readonly Func<float> _read;
        private readonly Func<float, string> _format;
        private float _shown = float.NaN;
        internal Scalar(Slider slider, Text value, Func<float> read, Func<float, string> format)
        {
            Slider = slider;
            Value = value;
            _read = read;
            _format = format;
        }
        internal void Refresh()
        {
            var value = _read();
            Slider.SetValueWithoutNotify(value);
            if (_shown == value)
                return;
            _shown = value;
            Value.text = _format(value);
        }
    }
    internal sealed class Check
    {
        internal readonly Toggle Toggle;
        private readonly Func<bool> _read;
        internal Check(Toggle toggle, Func<bool> read)
        {
            Toggle = toggle;
            _read = read;
        }
        internal void Refresh() => Toggle.SetIsOnWithoutNotify(_read());
    }
    internal static RectTransform Rect(Transform parent, string name, Vector2 point, Vector2 size, params Type[] components)
    {
        var go = new GameObject(name, typeof(RectTransform));
        foreach (var component in components)
            go.AddComponent(component);
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = point;
        rect.sizeDelta = size;
        return rect;
    }
    internal static Image Panel(Transform parent, string name, Vector2 point, Vector2 size, Color color, bool target = false)
    {
        var rect = Rect(parent, name, point, size, typeof(CanvasRenderer), typeof(Image));
        var image = rect.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = target;
        return image;
    }
    internal static Text Text(Transform parent, Font font, string label, Vector2 point, Vector2 size, int sizePx = 19, TextAnchor align = TextAnchor.MiddleLeft)
    {
        var rect = Rect(parent, label, point, size, typeof(CanvasRenderer), typeof(Text));
        var text = rect.GetComponent<Text>();
        text.font = font;
        text.fontSize = sizePx;
        text.color = Color.white;
        text.alignment = align;
        text.raycastTarget = false;
        text.text = label;
        return text;
    }
    internal static void Section(Transform parent, Font font, string label, float y, float width = Width, float x = 0)
    {
        var panel = Panel(parent, label + " section", new Vector2(x, y), new Vector2(width, 30), Accent);
        Text(panel.transform, font, label, new Vector2(10, 0), new Vector2(width - 20, 28), 19);
    }
    internal static Check Checkbox(Transform parent, Font font, string label, Vector2 point, float width, Func<bool> read, Action<bool> write)
    {
        var root = Rect(parent, label, point, new Vector2(width, 42), typeof(CanvasRenderer), typeof(Image), typeof(Toggle));
        root.GetComponent<Image>().color = Surface;
        root.GetComponent<Image>().raycastTarget = true;
        var box = Panel(root, "Checkbox", new Vector2(-width * .5f + 22, 0), new Vector2(25, 25), new Color(.28f, .37f, .40f), false);
        var mark = Panel(box.transform, "Checked", Vector2.zero, new Vector2(15, 15), new Color(.63f, .98f, .93f), false);
        Text(root, font, label, new Vector2(20, 0), new Vector2(width - 66, 36), 19);
        var toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = mark;
        toggle.navigation = new Navigation { mode = Navigation.Mode.None };
        toggle.toggleTransition = Toggle.ToggleTransition.None;
        var control = new Check(toggle, read);
        control.Refresh();
        toggle.onValueChanged.AddListener(value => { if (toggle.interactable) { write(value); control.Refresh(); } });
        return control;
    }
    internal static Scalar Slider(Transform parent, Font font, string label, float y, ConfigEntry<float> entry, Func<float, string> format, float step = 0, Action<float>? write = null)
    {
        var range = entry.Description.AcceptableValues as AcceptableValueRange<float>;
        if (range == null)
            throw new ArgumentException("View slider requires the setting's declared bounds", nameof(entry));
        return Slider(parent, font, label, y, range.MinValue, range.MaxValue, () => entry.Value, write ?? (value => entry.Value = value), format, step);
    }
    internal static Scalar Slider(Transform parent, Font font, string label, float y, float min, float max, Func<float> read, Action<float> write, Func<float, string> format, float step = 0)
    {
        var root = Rect(parent, label, new Vector2(0, y), new Vector2(Width, 62), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
        root.GetComponent<Image>().color = Surface;
        root.GetComponent<Image>().raycastTarget = true;
        Text(root, font, label, new Vector2(-85, 17), new Vector2(430, 25), 19);
        var valueText = Text(root, font, "", new Vector2(220, 17), new Vector2(160, 25), 19, TextAnchor.MiddleRight);
        var track = Panel(root, "Track", new Vector2(0, -14), new Vector2(Width - 42, 8), new Color(.19f, .27f, .30f));
        var fill = Panel(track.transform, "Fill", Vector2.zero, Vector2.zero, Accent);
        var fillRect = (RectTransform)fill.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        var area = Rect(root, "Handle area", new Vector2(0, -14), new Vector2(Width - 42, 28));
        var handle = Panel(area, "Handle", Vector2.zero, new Vector2(24, 28), new Color(.78f, .98f, .96f));
        var slider = root.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.handleRect = (RectTransform)handle.transform;
        slider.fillRect = fillRect;
        slider.targetGraphic = handle;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        var control = new Scalar(slider, valueText, read, format);
        control.Refresh();
        slider.onValueChanged.AddListener(value =>
        {
            if (!slider.interactable || float.IsNaN(value) || float.IsInfinity(value))
                return;
            var bounded = ViewMenuValue.Snap(value, min, max, step);
            if (Math.Abs(read() - bounded) > 0.000001f)
                write(bounded);
            control.Refresh();
        });
        return control;
    }
}

internal static class ViewMenuValue
{
    internal static float Snap(float value, float min, float max, float step)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return min;
        value = Math.Max(min, Math.Min(max, value));
        if (step > 0)
            value = min + (float)Math.Round((value - min) / step) * step;
        return Math.Max(min, Math.Min(max, value));
    }
}
