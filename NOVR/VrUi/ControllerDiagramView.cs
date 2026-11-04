using System;
using System.Collections.Generic;
using System.Linq;
using NOVR.MotionControls;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

// A code-drawn schematic, shared by the cockpit View and native Settings.
// The small labels summarize assignments; selecting a control shows full names.
internal sealed class ControllerDiagramView
{
    private sealed class Control
    {
        internal bool Left;
        internal ControllerButton? Value;
        internal Button Button = null!;
        internal Text Name = null!, Action = null!;
        internal bool FrameOnly, Menu;
    }
    private readonly List<Control> _controls = new();
    private readonly Text[] _headers = new Text[2];
    internal GameObject Root { get; }
    private bool? _selectedLeft;
    private ControllerButton? _selectedButton;

    internal ControllerDiagramView(Transform parent, Font font, Action<bool, ControllerButton?> select)
    {
        Root = new GameObject("Controller diagram", typeof(RectTransform));
        Root.transform.SetParent(parent, false);
        for (var i = 0; i < 2; i++)
        {
            var left = i == 0;
            var hand = new GameObject(left ? "Left controller" : "Right controller", typeof(RectTransform));
            hand.transform.SetParent(Root.transform, false);
            ((RectTransform)hand.transform).anchoredPosition = new Vector2(left ? -132 : 132, 0);
            var body = new GameObject("Controller silhouette", typeof(RectTransform), typeof(CanvasRenderer), typeof(ControllerBodyGraphic));
            body.transform.SetParent(hand.transform, false);
            ((RectTransform)body.transform).sizeDelta = new Vector2(230, 330);
            body.GetComponent<ControllerBodyGraphic>().Left = left;
            _headers[i] = Text(hand.transform, font, "", new Vector2(0, 180), new Vector2(250, 40), 17);
            Add(ControllerButton.Trigger, new Vector2(0, 127), new Vector2(132, 44));
            Add(ControllerButton.Shoulder, new Vector2(0, 79), new Vector2(132, 40), frameOnly: true);
            Add(ControllerButton.StickClick, new Vector2(-52, 27), new Vector2(88, 52));
            Add(ControllerButton.Top, new Vector2(49, 27), new Vector2(48, 44), frameOnly: true);
            Add(ControllerButton.Outside, new Vector2(0, -21), new Vector2(48, 44), frameOnly: true);
            Add(ControllerButton.Secondary, new Vector2(98, -21), new Vector2(48, 44));
            Add(ControllerButton.Primary, new Vector2(49, -69), new Vector2(48, 44));
            Add(ControllerButton.Menu, new Vector2(-53, -68), new Vector2(82, 42), menu: true);
            Add(null, new Vector2(0, -127), new Vector2(134, 44));

            void Add(ControllerButton? value, Vector2 position, Vector2 size, bool frameOnly = false, bool menu = false)
            {
                var go = new GameObject(value?.ToString() ?? "Grip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                var rect = (RectTransform)go.transform; rect.SetParent(hand.transform, false); rect.anchoredPosition = position; rect.sizeDelta = size;
                var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                var item = new Control { Left = left, Value = value, Button = button, FrameOnly = frameOnly, Menu = menu,
                    Name = Text(rect, font, "", new Vector2(0, 9), new Vector2(size.x - 2, 23), 16),
                    Action = Text(rect, font, "", new Vector2(0, -11), new Vector2(size.x + 3, 20), 12) };
                button.onClick.AddListener(() => { _selectedLeft = left; _selectedButton = value; select(left, value); });
                NativeButtonFeedback.Configure(button, new Color(.13f, .24f, .29f));
                _controls.Add(item);
            }
        }
        LayerHelper.SetLayerRecursive(Root.transform, LayerHelper.GetVrUiLayer());
    }

    internal void Refresh(ControllerBindings bindings, bool stickLeft)
    {
        for (var i = 0; i < 2; i++)
        {
            var left = i == 0;
            _headers[i].text = (left ? "LEFT" : "RIGHT") + " · " + (stickLeft == left ? "STICK" : "THROTTLE") +
                "\n" + (bindings.ButtonProfile(left) == ControllerBindingProfile.Frame ? "Steam Frame" : "Touch / Quest");
        }
        foreach (var item in _controls)
        {
            var profile = bindings.ButtonProfile(item.Left); var frame = profile == ControllerBindingProfile.Frame;
            item.Button.gameObject.SetActive(!item.FrameOnly || frame);
            if (!item.Button.gameObject.activeSelf) continue;
            var system = item.Menu && !frame && !item.Left;
            item.Button.interactable = !system;
            item.Name.text = item.Value == null ? "Grip" : system ? "System" : ShortPhysical(item.Value.Value, item.Left, profile);
            var assigned = item.Value == null || system ? Array.Empty<ControllerActionId>() :
                ControllerDiagramModel.Assigned(item.Left, item.Value.Value, profile, stickLeft, bindings.Get);
            item.Action.text = item.Value == null ? "Fixed · hold handle" : system ? "Headset only" : assigned.Length == 0 ? "—" :
                ControllerDiagramModel.ShortAction(assigned[0]) + (assigned.Length > 1 ? " +" : "");
            if (item.Value == ControllerButton.StickClick && assigned.Length == 0) item.Action.text = "Free: wheel";
            NativeButtonFeedback.SetNormalColor(item.Button, _selectedLeft == item.Left && _selectedButton == item.Value ?
                new Color(.24f, .47f, .56f) : new Color(.13f, .24f, .29f));
        }
    }
    private static string ShortPhysical(ControllerButton button, bool left, ControllerBindingProfile profile)
    {
        if (profile == ControllerBindingProfile.Frame && left)
            return button switch { ControllerButton.Primary => "↓", ControllerButton.Secondary => "→", ControllerButton.Top => "↑", ControllerButton.Outside => "←", _ => ControllerDiagramModel.ShortButton(button, left, profile) };
        return ControllerDiagramModel.ShortButton(button, left, profile);
    }
    private static Text Text(Transform parent, Font font, string value, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
        var text = go.GetComponent<Text>(); text.font = font; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white; text.raycastTarget = false; text.text = value;
        return text;
    }
}

internal sealed class ControllerBodyGraphic : MaskableGraphic
{
    internal bool Left;
    private static readonly Vector2[] Shape = {
        new(-.37f,.46f), new(-.1f,.5f), new(.27f,.47f), new(.45f,.30f), new(.49f,.05f),
        new(.35f,-.13f), new(.22f,-.43f), new(.05f,-.5f), new(-.17f,-.48f), new(-.26f,-.28f),
        new(-.48f,-.06f), new(-.49f,.20f)
    };
    protected override void Awake() { base.Awake(); raycastTarget = false; }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var rect = rectTransform.rect;
        var fill = new Color(.06f, .105f, .13f, 1); var edge = new Color(.34f, .48f, .55f, 1);
        vh.AddVert(Vector3.zero, fill, Vector2.zero);
        for (var i = 0; i < Shape.Length; i++)
        {
            var point = new Vector3(Shape[i].x * rect.width * (Left ? -1 : 1), Shape[i].y * rect.height, 0);
            vh.AddVert(point, fill, Vector2.zero);
        }
        for (var i = 0; i < Shape.Length; i++) vh.AddTriangle(0, i + 1, (i + 1) % Shape.Length + 1);
        for (var i = 0; i < Shape.Length; i++)
        {
            var a = new Vector2(Shape[i].x * rect.width * (Left ? -1 : 1), Shape[i].y * rect.height);
            var b = new Vector2(Shape[(i + 1) % Shape.Length].x * rect.width * (Left ? -1 : 1), Shape[(i + 1) % Shape.Length].y * rect.height);
            var direction = (b - a).normalized; var normal = new Vector2(-direction.y, direction.x) * 1.3f;
            var index = vh.currentVertCount;
            vh.AddVert(a - normal, edge, Vector2.zero); vh.AddVert(a + normal, edge, Vector2.zero);
            vh.AddVert(b + normal, edge, Vector2.zero); vh.AddVert(b - normal, edge, Vector2.zero);
            vh.AddTriangle(index, index + 1, index + 2); vh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
