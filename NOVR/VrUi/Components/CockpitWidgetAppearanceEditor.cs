using System;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// Edit-only direct contacts. All coordinates supplied by the owner are pixels
// in its widget plane; only depth/tolerance are converted to physical metres.
internal sealed class CockpitWidgetAppearanceEditor : IDisposable
{
    private static CockpitWidgetAppearanceEditor? _open;
    private readonly GameObject _pencil, _popup;
    private readonly CanvasGroup _pencilGroup, _popupGroup;
    private readonly Image _pencilBack, _sliderHandle;
    private readonly Text _value;
    private readonly Action<float> _setBackground;
    private readonly CockpitTouchGate[] _pencilTouch = { new(), new() }, _closeTouch = { new(), new() }, _sliderTouch = { new(), new() };
    private readonly bool[] _hover = new bool[2];
    private readonly Vector2 _popupCenter, _popupSize, _closeCenter;
    private readonly float _trackWidth;
    private int _sliderOwner = -1, _lastTick;
    private float _background, _lastPulse;
    private float _displayedBackground = -1;
    private bool _editing, _disposed;
    internal Vector2 PencilCenter { get; }
    internal Vector2 SliderCenter { get; }
    internal bool IsOpen => _open == this && _editing && !_disposed;
    internal bool IsHandBusy(int side) => side >= 0 && side < 2 && _sliderOwner == side;
    internal bool IsPointing(int side) => side >= 0 && side < 2 && (_hover[side] || _sliderOwner == side);

    internal CockpitWidgetAppearanceEditor(RectTransform root, Vector2 size, Action<float> setBackground)
    {
        _setBackground = setBackground;
        PencilCenter = new Vector2(0, size.y * .5f + 62);
        _popupSize = new Vector2(Mathf.Clamp(size.x, 300, 420), 150);
        // Leave the content visible while tuning its background, and keep the
        // contact area below even the shortest widget's resize corners.
        _popupCenter = new Vector2(0, -size.y * .5f - 110);
        SliderCenter = _popupCenter + new Vector2(0, -5);
        _closeCenter = _popupCenter + new Vector2(_popupSize.x * .5f - 24, _popupSize.y * .5f - 24);
        _trackWidth = _popupSize.x - 76;

        _pencilBack = Plate("Edit widget appearance", root, PencilCenter, new Vector2(52, 52), new Color(.04f, .10f, .13f, .9f));
        _pencil = _pencilBack.gameObject;
        _pencilGroup = _pencil.AddComponent<CanvasGroup>();
        _pencilGroup.interactable = _pencilGroup.blocksRaycasts = false;
        var body = Plate("Pencil", _pencil.transform, Vector2.zero, new Vector2(8, 29), new Color(.88f, .96f, 1f, 1));
        body.rectTransform.localRotation = Quaternion.Euler(0, 0, -35);
        var tip = Plate("Pencil tip", _pencil.transform, new Vector2(-9, -13), new Vector2(6, 6), new Color(.88f, .96f, 1f, 1));
        tip.rectTransform.localRotation = Quaternion.Euler(0, 0, -35);

        var popup = Plate("Widget backplate editor", root, _popupCenter, _popupSize, new Color(.025f, .06f, .08f, .95f));
        _popup = popup.gameObject;
        _popupGroup = _popup.AddComponent<CanvasGroup>();
        _popupGroup.interactable = _popupGroup.blocksRaycasts = false;
        _value = Label(_popup.transform, "Backplate 0%", new Vector2(-18, 46), new Vector2(_popupSize.x - 75, 32), 21);
        var close = Plate("Close appearance editor", _popup.transform, _closeCenter - _popupCenter,
            new Vector2(36, 36), new Color(.13f, .25f, .29f, 1));
        for (var i = 0; i < 2; i++)
        {
            var stroke = Plate("Close mark", close.transform, Vector2.zero, new Vector2(3, 20), Color.white);
            stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, i == 0 ? 45 : -45);
        }
        Plate("Opacity track", _popup.transform, new Vector2(0, -5), new Vector2(_trackWidth, 12), new Color(.18f, .36f, .40f, 1));
        _sliderHandle = Plate("Opacity handle", _popup.transform, new Vector2(-_trackWidth * .5f, -5), new Vector2(12, 32), new Color(.65f, .94f, 1, 1));
        Label(_popup.transform, "Clear", new Vector2(-_trackWidth * .5f + 22, -43), new Vector2(60, 28), 17);
        Label(_popup.transform, "Solid", new Vector2(_trackWidth * .5f - 22, -43), new Vector2(60, 28), 17);
        _pencil.SetActive(false); _popup.SetActive(false);
    }

    internal void SetState(bool editing, bool visible, float background, float presentationAlpha = 1)
    {
        if (_disposed) return;
        _editing = editing;
        // Hidden widgets still expose their editor while layout mode is active.
        _pencil.SetActive(editing);
        if (!editing) { ClosePopup(); ReleaseHands(); }
        _popup.SetActive(IsOpen);
        _background = FlightControlMath.IsFinite(background) ? Mathf.Clamp01(background) : 0;
        _pencilGroup.alpha = _popupGroup.alpha = FlightControlMath.IsFinite(presentationAlpha) ? Mathf.Clamp01(presentationAlpha) : 1;
        RefreshValue();
    }

    internal bool HitTest(Vector3 point, float metresPerPixel)
    {
        if (!_editing || _disposed || !Finite(point, metresPerPixel)) return false;
        var depth = -point.z * metresPerPixel;
        if (depth < -.025f || depth > .075f) return false;
        var padding = .008f / metresPerPixel;
        return Inside(point, PencilCenter, new Vector2(52, 52), padding) ||
            IsOpen && Inside(point, _popupCenter, _popupSize, padding);
    }

    internal bool UpdateHand(int side, Vector3 point, bool available, float metresPerPixel, InputDevice device)
    {
        if (side < 0 || side > 1) return false;
        if (!_editing || _disposed || !available || !Finite(point, metresPerPixel))
        { ReleaseHand(side); return false; }
        var depth = -point.z * metresPerPixel;
        var padding = .006f / metresPerPixel;
        var hover = HitTest(point, metresPerPixel);
        if (hover && !_hover[side]) InteractionHaptics.Pulse(device, InteractionCue.Touch);
        _hover[side] = hover;
        var pencil = Inside(point, PencilCenter, new Vector2(52, 52), padding);
        if (_pencilTouch[side].Update(true, pencil, depth))
        {
            if (IsOpen) ClosePopup();
            else
            {
                _open?.ClosePopup(); _open = this;
                _popup.SetActive(true);
                // The popup appearing under a finger is never a new press.
                foreach (var gate in _closeTouch) gate.Reset();
                foreach (var gate in _sliderTouch) gate.Reset();
            }
            InteractionHaptics.Pulse(device, InteractionCue.Click);
            return true;
        }
        _pencilBack.color = pencil && hover ? new Color(.17f, .36f, .40f, 1) : new Color(.04f, .10f, .13f, .9f);
        var close = IsOpen && Inside(point, _closeCenter, new Vector2(36, 36), padding);
        if (_closeTouch[side].Update(IsOpen, close, depth))
        {
            ClosePopup(); InteractionHaptics.Pulse(device, InteractionCue.Click); return true;
        }
        var onTrack = Inside(point, SliderCenter, new Vector2(_trackWidth, 40), padding);
        var touched = _sliderTouch[side].Update(IsOpen, onTrack, depth);
        if (_sliderOwner == side)
        {
            var away = Mathf.Abs(point.y - SliderCenter.y) * metresPerPixel > .035f || depth > .055f || depth < -.025f ||
                Mathf.Abs(point.x - SliderCenter.x) > _trackWidth * .5f + .04f / metresPerPixel;
            if (!IsOpen || away)
            { _sliderOwner = -1; _sliderTouch[side].Reset(); }
            else SetFromFinger(point.x, device);
        }
        else if (_sliderOwner < 0 && touched)
        {
            _sliderOwner = side; _lastTick = Mathf.RoundToInt(_background * 10); _lastPulse = Time.unscaledTime;
            InteractionHaptics.Pulse(device, InteractionCue.Grab);
            SetFromFinger(point.x, device);
        }
        return hover || _sliderOwner == side;
    }

    private void SetFromFinger(float x, InputDevice device)
    {
        var value = Mathf.RoundToInt(Mathf.Clamp01((x - SliderCenter.x) / _trackWidth + .5f) * 100) * .01f;
        if (Mathf.Approximately(value, _background)) return;
        _background = value; _setBackground(value); RefreshValue();
        var tick = Mathf.RoundToInt(value * 10);
        if (tick != _lastTick && Time.unscaledTime - _lastPulse >= .08f)
        { InteractionHaptics.PulseOnly(device, .09f, .015f); _lastPulse = Time.unscaledTime; }
        _lastTick = tick;
    }
    private void RefreshValue()
    {
        if (_displayedBackground == _background) return;
        _displayedBackground = _background;
        _value.text = $"Backplate {_background * 100:0}%";
        _sliderHandle.rectTransform.anchoredPosition = new Vector2((_background - .5f) * _trackWidth, -5);
    }
    private static bool Finite(Vector3 point, float scale) => FlightControlMath.IsFinite(point.x) && FlightControlMath.IsFinite(point.y) &&
        FlightControlMath.IsFinite(point.z) && FlightControlMath.IsFinite(scale) && scale > 0;
    private static bool Inside(Vector3 point, Vector2 center, Vector2 size, float padding) =>
        Mathf.Abs(point.x - center.x) <= size.x * .5f + padding && Mathf.Abs(point.y - center.y) <= size.y * .5f + padding;
    private void ClosePopup()
    {
        if (_open == this) _open = null;
        _popup.SetActive(false); ReleaseHands();
    }
    private void ReleaseHand(int side)
    {
        _pencilTouch[side].Reset(); _closeTouch[side].Reset(); _sliderTouch[side].Reset(); _hover[side] = false;
        if (_sliderOwner == side) _sliderOwner = -1;
    }
    internal void ReleaseHands() { ReleaseHand(0); ReleaseHand(1); }
    public void Dispose()
    {
        if (_disposed) return;
        ClosePopup(); _disposed = true;
        UnityEngine.Object.Destroy(_pencil); UnityEngine.Object.Destroy(_popup);
    }
    private static Image Plate(string name, Transform parent, Vector2 at, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.anchoredPosition = at; rect.sizeDelta = size;
        go.layer = parent.gameObject.layer;
        var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    private static Text Label(Transform parent, string value, Vector2 at, Vector2 size, int fontSize)
    {
        var go = new GameObject("Appearance label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.anchoredPosition = at; rect.sizeDelta = size;
        go.layer = parent.gameObject.layer;
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("Arial.ttf"); text.text = value;
        text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false; return text;
    }
}
