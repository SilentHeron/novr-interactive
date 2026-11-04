using System;
using System.Collections.Generic;
using NOVR.MotionControls;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

// Original controller artwork stays unobstructed. Clickable assignments sit
// in the outside margins and leader lines end on the actual physical controls.
internal sealed class ControllerDiagramView
{
    private sealed class Control
    {
        internal bool Left;
        internal ControllerButton? Value;
        internal Button Button = null!;
        internal Text Name = null!, Action = null!;
        internal Image LiveEdge = null!;
        internal ControllerDiagramActivityRing LiveRing = null!;
        internal ControllerDiagramInput LiveState;
    }
    private readonly List<Control> _controls = new();
    private readonly GameObject[] _hands = new GameObject[2];
    private readonly RawImage[,] _images = new RawImage[2, 2];
    private readonly Image[] _cards = new Image[2];
    private readonly Text[] _tabs = new Text[2];
    private readonly Button[] _tabButtons = new Button[2];
    private readonly ControllerDiagramLeaders[] _leaders = new ControllerDiagramLeaders[2];
    private readonly List<(Vector2 from, Vector2 to)> _segments = new();
    private readonly ControllerBindingProfile[] _profiles = new ControllerBindingProfile[2];
    private bool _left, _initialized, _dirty = true, _lastStickLeft;
    private int _version = -1;
    private bool? _selectedLeft;
    private ControllerButton? _selectedButton;
    internal GameObject Root
    {
        get;
    }

    internal ControllerDiagramView(Transform parent, Font font, Action<bool, ControllerButton?> select)
    {
        Root = new GameObject("Controller diagram", typeof(RectTransform), typeof(ControllerDiagramActivityLifecycle));
        Root.transform.SetParent(parent, false);
        Root.GetComponent<ControllerDiagramActivityLifecycle>().Clear = ClearActivity;
        for (var i = 0; i < 2; i++)
        {
            var index = i;
            var left = i == 0;
            var tab = MakeButton(Root.transform, left ? "Left tab" : "Right tab", new Vector2(left ? -132 : 132, 180), new Vector2(248, 40));
            _tabButtons[i] = tab;
            _tabs[i] = Text(tab.transform, font, "", Vector2.zero, new Vector2(240, 38), 17);
            tab.onClick.AddListener(() => { _left = index == 0; _dirty = true; Refresh(ModConfiguration.Instance.ControllerBindings, ModConfiguration.Instance.MotionControls.LeftHanded.Value); });
            var hand = new GameObject(left ? "Left controller" : "Right controller", typeof(RectTransform));
            hand.transform.SetParent(Root.transform, false);
            _hands[i] = hand;
            var card = new GameObject("Touch artwork light card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(hand.transform, false);
            _cards[i] = card.GetComponent<Image>();
            _cards[i].color = new Color(.96f, .97f, .97f, 1);
            _cards[i].raycastTarget = false;
            _cards[i].rectTransform.sizeDelta = ControllerDiagramImages.Size(false, false) + new Vector2(8, 8);
            for (var view = 0; view < 2; view++)
            {
                var go = new GameObject(view == 0 ? "Controller top artwork" : "Controller trigger artwork", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                go.transform.SetParent(hand.transform, false);
                _images[i, view] = go.GetComponent<RawImage>();
                _images[i, view].raycastTarget = false;
                _images[i, view].color = Color.white;
            }
            var lines = new GameObject("Physical control leaders", typeof(RectTransform), typeof(CanvasRenderer), typeof(ControllerDiagramLeaders));
            lines.transform.SetParent(hand.transform, false);
            _leaders[i] = lines.GetComponent<ControllerDiagramLeaders>();
            _leaders[i].raycastTarget = false;
            foreach (var value in new ControllerButton?[] { ControllerButton.Menu, ControllerButton.StickClick, null,
                ControllerButton.Shoulder, ControllerButton.Trigger, ControllerButton.Top, ControllerButton.Secondary, ControllerButton.Primary, ControllerButton.Outside })
            {
                var button = MakeButton(hand.transform, value?.ToString() ?? "Grip", Vector2.zero, new Vector2(126, 38));
                var item = new Control
                {
                    Left = left,
                    Value = value,
                    Button = button,
                    Name = Text(button.transform, font, "", new Vector2(0, 8), new Vector2(122, 20), 16),
                    Action = Text(button.transform, font, "", new Vector2(0, -10), new Vector2(122, 18), 12)
                };
                var ring = new GameObject((value?.ToString() ?? "Grip") + " live ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(ControllerDiagramActivityRing));
                ring.transform.SetParent(hand.transform, false);
                item.LiveRing = ring.GetComponent<ControllerDiagramActivityRing>();
                item.LiveRing.rectTransform.sizeDelta = new Vector2(18, 18);
                item.LiveRing.raycastTarget = false;
                item.LiveRing.enabled = false;
                var edge = new GameObject("Live input edge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                edge.transform.SetParent(button.transform, false);
                item.LiveEdge = edge.GetComponent<Image>();
                item.LiveEdge.rectTransform.anchoredPosition = new Vector2(-60, 0);
                item.LiveEdge.rectTransform.sizeDelta = new Vector2(3, 32);
                item.LiveEdge.raycastTarget = false;
                item.LiveEdge.enabled = false;
                button.onClick.AddListener(() => { _selectedLeft = left; _selectedButton = value; _dirty = true; select(left, value); });
                _controls.Add(item);
            }
        }
        LayerHelper.SetLayerRecursive(Root.transform, LayerHelper.GetVrUiLayer());
    }

    internal void Refresh(ControllerBindings bindings, bool stickLeft)
    {
        var leftProfile = bindings.ButtonProfile(true);
        var rightProfile = bindings.ButtonProfile(false);
        if (!_initialized)
        {
            _left = stickLeft;
            _initialized = true;
        }
        if (!_dirty && _version == bindings.ChangeVersion && _lastStickLeft == stickLeft && _profiles[0] == leftProfile && _profiles[1] == rightProfile)
        {
            RefreshActivity();
            return;
        }
        _dirty = false;
        _version = bindings.ChangeVersion;
        _lastStickLeft = stickLeft;
        _profiles[0] = leftProfile;
        _profiles[1] = rightProfile;
        for (var i = 0; i < 2; i++)
        {
            var left = i == 0;
            var frame = _profiles[i] == ControllerBindingProfile.Frame;
            _hands[i].SetActive(left == _left);
            _cards[i].gameObject.SetActive(!frame);
            _tabs[i].text = (left ? "LEFT" : "RIGHT") + " · " + (stickLeft == left ? "STICK" : "THROTTLE") + "\n" + (frame ? "Steam Frame" : "Touch / Quest");
            NativeButtonFeedback.SetNormalColor(_tabButtons[i], left == _left ? new Color(.24f, .47f, .56f) : new Color(.1f, .24f, .29f));
            for (var view = 0; view < 2; view++)
            {
                var image = _images[i, view];
                var side = view == 1;
                image.gameObject.SetActive(frame || !side);
                if (!frame && side)
                    continue;
                image.texture = ControllerDiagramImages.Get(frame, left, side);
                image.uvRect = ControllerDiagramImages.Region(frame, left, side);
                image.rectTransform.sizeDelta = ControllerDiagramImages.Size(frame, side);
                image.rectTransform.anchoredPosition = ControllerDiagramImages.Position(frame, side);
            }
            _segments.Clear();
            foreach (var item in _controls)
            {
                if (item.Left != left)
                    continue;
                var supported = item.Value == null || item.Value == ControllerButton.Menu || ControllerBindings.IsButtonAvailable(_profiles[i], left, item.Value.Value);
                item.Button.gameObject.SetActive(supported);
                if (!supported)
                    continue;
                var position = ControllerDiagramLayout.Position(frame, left, item.Value);
                var columnLeft = position.x < 0;
                ((RectTransform)item.Button.transform).anchoredPosition = position;
                var system = !frame && !left && item.Value == ControllerButton.Menu;
                item.Button.interactable = !system;
                item.Name.text = item.Value == null ? "Grip" : system ? "System" : ControllerDiagramModel.ShortButton(item.Value.Value, left, _profiles[i]);
                var assigned = item.Value == null || system ? Array.Empty<ControllerActionId>() : ControllerDiagramModel.Assigned(left, item.Value.Value, _profiles[i], stickLeft, bindings.Get);
                item.Action.text = item.Value == null ? "Fixed · hold handle" : system ? "Headset only" : assigned.Length == 0 ? "Unassigned" :
                    ControllerDiagramModel.ShortAction(assigned[0]) + (assigned.Length > 1 ? " +" : "");
                if (item.Value == ControllerButton.StickClick && assigned.Length == 0)
                    item.Action.text = "Free: wheel";
                NativeButtonFeedback.SetNormalColor(item.Button, _selectedLeft == left && _selectedButton == item.Value ? new Color(.24f, .47f, .56f) : new Color(.1f, .24f, .29f));
                item.LiveRing.rectTransform.anchoredPosition = ControllerDiagramImages.Endpoint(frame, left, item.Value);
                _segments.Add((position + new Vector2(columnLeft ? 63 : -63, 0), ControllerDiagramImages.Endpoint(frame, left, item.Value)));
            }
            _leaders[i].Set(_segments);
        }
        RefreshActivity();
    }
    private void RefreshActivity()
    {
        // The controller reader caches one sample per hand/frame. No allocations
        // or UI rebuild occur while the physical activity state is unchanged.
        var sample = Root.activeInHierarchy ? TrackedControllers.Read(_left) : default;
        foreach (var item in _controls)
        {
            var state = item.Left == _left && item.Button.gameObject.activeInHierarchy
                ? ControllerDiagramActivity.Read(sample, item.Value, item.Left, _profiles[item.Left ? 0 : 1] == ControllerBindingProfile.Frame)
                : ControllerDiagramInput.None;
            SetActivity(item, state);
        }
    }
    internal void ClearActivity()
    {
        foreach (var item in _controls)
            SetActivity(item, ControllerDiagramInput.None);
    }
    private static void SetActivity(Control item, ControllerDiagramInput state)
    {
        if (item.LiveState == state)
            return;
        item.LiveState = state;
        var active = state != ControllerDiagramInput.None;
        item.LiveRing.enabled = item.LiveEdge.enabled = active;
        if (active)
            item.LiveRing.color = item.LiveEdge.color = ControllerDiagramActivity.Color(state);
    }
    private static Button MakeButton(Transform parent, string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var button = go.GetComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        NativeButtonFeedback.Configure(button, new Color(.1f, .24f, .29f));
        return button;
    }
    private static Text Text(Transform parent, Font font, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = value;
        return text;
    }
}

internal sealed class ControllerDiagramLeaders : MaskableGraphic
{
    private readonly List<(Vector2 from, Vector2 to)> _segments = new();
    internal void Set(List<(Vector2 from, Vector2 to)> segments)
    {
        _segments.Clear();
        _segments.AddRange(segments);
        SetVerticesDirty();
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        var color = new Color(.13f, .55f, .65f, .85f);
        foreach (var segment in _segments)
        {
            var direction = (segment.to - segment.from).normalized;
            var normal = new Vector2(-direction.y, direction.x) * .6f;
            var index = vh.currentVertCount;
            vh.AddVert(segment.from - normal, color, Vector2.zero);
            vh.AddVert(segment.from + normal, color, Vector2.zero);
            vh.AddVert(segment.to + normal, color, Vector2.zero);
            vh.AddVert(segment.to - normal, color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
            var dot = new Vector2(1.5f, 1.5f);
            index = vh.currentVertCount;
            vh.AddVert(segment.to - dot, color, Vector2.zero);
            vh.AddVert(segment.to + new Vector2(-dot.x, dot.y), color, Vector2.zero);
            vh.AddVert(segment.to + dot, color, Vector2.zero);
            vh.AddVert(segment.to + new Vector2(dot.x, -dot.y), color, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2);
            vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
