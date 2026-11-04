using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private readonly List<(GameObject page, Action refresh)> _settingRefresh = new();
    private void Section(Transform parent, string title, float y, float width = ViewMenuControls.Width, float x = 0) =>
        ViewMenuControls.Section(parent, _font!, title, y, width, x);
    private void Note(Transform parent, string text, float y, float height = 52)
    {
        var label = ViewMenuControls.Text(parent, _font!, text, new Vector2(0, y), new Vector2(ViewMenuControls.Width - 12, height), 18);
        label.color = ViewMenuControls.Muted;
    }
    private void Setting(Transform parent, string title, ConfigEntry<bool> entry, int column, float y, Action? after = null) =>
        Setting(parent, title, () => entry.Value, value => { entry.Value = value; after?.Invoke(); }, column, y);
    private void Setting(Transform parent, string title, Func<bool> read, Action<bool> write, int column, float y)
    {
        var control = ViewMenuControls.Checkbox(parent, _font!, title, new Vector2(column == 0 ? -160 : 160, y), 300, read, write);
        _settingRefresh.Add((parent.gameObject, control.Refresh));
    }
    private ViewMenuControls.Scalar Setting(Transform parent, string title, ConfigEntry<float> entry, float y, Func<float, string> format, float step = 0, Action<float>? write = null)
    {
        var control = ViewMenuControls.Slider(parent, _font!, title, y, entry, format, step, write);
        _settingRefresh.Add((parent.gameObject, control.Refresh));
        return control;
    }
    private ViewMenuControls.Scalar Setting(Transform parent, string title, float y, float min, float max, Func<float> read, Action<float> write, Func<float, string> format, float step = 0)
    {
        var control = ViewMenuControls.Slider(parent, _font!, title, y, min, max, read, write, format, step);
        _settingRefresh.Add((parent.gameObject, control.Refresh));
        return control;
    }
    private Text ActionButton(Transform parent, string title, int column, float y, Action action, bool danger = false)
    {
        var label = AddButton(parent, title, column, 0, action);
        var rect = (RectTransform)label.transform.parent;
        rect.anchoredPosition = new Vector2(column == 0 ? -160 : 160, y);
        rect.sizeDelta = new Vector2(300, 42);
        ((RectTransform)label.transform).sizeDelta = new Vector2(282, 40);
        var button = rect.GetComponent<Button>();
        NativeButtonFeedback.Configure(button, danger ? new Color(.36f, .13f, .10f) : new Color(.12f, .18f, .21f));
        return label;
    }
    private void Choice(Transform parent, string title, string[] options, Func<int> read, Action<int> write, float y)
    {
        var label = ViewMenuControls.Text(parent, _font!, title, new Vector2(0, y + 22), new Vector2(620, 24), 19);
        var width = (620 - (options.Length - 1) * 8f) / options.Length;
        var buttons = new Button[options.Length];
        for (var i = 0; i < options.Length; i++)
        {
            var index = i;
            var text = AddButton(parent, options[i], 0, 0, () => write(index));
            var rect = (RectTransform)text.transform.parent;
            rect.anchoredPosition = new Vector2((i - (options.Length - 1) * .5f) * (width + 8), y - 13);
            rect.sizeDelta = new Vector2(width, 38);
            ((RectTransform)text.transform).sizeDelta = new Vector2(width - 8, 36);
            text.fontSize = 18;
            buttons[i] = rect.GetComponent<Button>();
        }
        var shown = int.MinValue;
        _settingRefresh.Add((parent.gameObject, () =>
        {
            var selected = read();
            if (selected == shown)
                return;
            shown = selected;
            for (var i = 0; i < buttons.Length; i++)
                NativeButtonFeedback.Configure(buttons[i], i == selected ? ViewMenuControls.Accent : new Color(.11f, .16f, .19f));
        }
        ));
    }
    private void CompactAction(Text label, int column, int count)
    {
        var width = (620 - (count - 1) * 12f) / count;
        var rect = (RectTransform)label.transform.parent;
        rect.anchoredPosition = new Vector2((column - (count - 1) * .5f) * (width + 12), rect.anchoredPosition.y);
        rect.sizeDelta = new Vector2(width, 42);
        ((RectTransform)label.transform).sizeDelta = new Vector2(width - 8, 40);
        label.fontSize = 18;
    }
    private void HandPositionSetting(Transform parent, string label, int axis, float y)
    {
        var m = ModConfiguration.Instance.MotionControls;
        Setting(parent, label, y, -.15f, .15f, () => (_fittingLeftHand ? m.LeftGloveOffset : m.RightGloveOffset).Value[axis], value =>
        {
            var entry = _fittingLeftHand ? m.LeftGloveOffset : m.RightGloveOffset;
            var delta = Vector3.zero;
            delta[axis] = value - entry.Value[axis];
            m.MoveFreeHand(_fittingLeftHand, delta);
        }, value => $"{value * 100:+0.#;-0.#;0} cm", .005f);
    }
    private void HandAngleSetting(Transform parent, string label, int axis, float y)
    {
        var m = ModConfiguration.Instance.MotionControls;
        Setting(parent, label, y, -180, 180, () => (_fittingLeftHand ? m.LeftGloveRotation : m.RightGloveRotation).Value[axis], value =>
        {
            var entry = _fittingLeftHand ? m.LeftGloveRotation : m.RightGloveRotation;
            var delta = Vector3.zero;
            delta[axis] = value - entry.Value[axis];
            m.TurnFreeHand(_fittingLeftHand, delta);
        }, value => $"{value:+0;-0;0}°", 1);
    }
    private static string Percent(float value) => $"{value * 100:0}%";
    private static string Centimeters(float value) => $"{value * 100:0.#} cm";
    private static string Degrees(float value) => $"{value:0.#}°";
}
