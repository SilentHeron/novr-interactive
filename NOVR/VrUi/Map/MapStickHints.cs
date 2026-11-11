using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// The full-screen map is moved with one thumbstick and zoomed with the other,
// which nothing on the map itself says. While the laser is on the map, a small
// label sits above each thumbstick naming what it does. It uses the quick
// wheel's controller pose, so it needs nothing from the map's own layout.
internal sealed class MapStickHints : MonoBehaviour
{
    private readonly GameObject?[] _views = new GameObject?[2];
    private readonly Canvas?[] _canvases = new Canvas?[2];
    private readonly Text?[] _labels = new Text?[2];

    internal static string Caption(bool zoomHand) => zoomHand ? "STICK UP / DOWN\nZOOM MAP" : "STICK\nMOVE MAP";

    // Same rule as ControllerMapPatch: the pointer (stick) hand zooms, the
    // other hand moves the map.
    internal static bool ZoomHand(bool left, bool leftHanded) => left == leftHanded;

    private void LateUpdate()
    {
        var camera = APIBus.CockpitHudCamera;
        var show = XRSettings.enabled && DynamicMap.mapMaximized && camera != null && TrackedControllers.Available &&
            VrUiCursor.I?.ControllerOverMap == true && !WristMenu.IsOpen && !NativeHandOptionWheel.IsOpen;
        var leftHanded = show && ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        for (var side = 0; side < 2; side++)
        {
            var left = side == 0;
            var hand = show ? TrackedControllers.Read(left) : default;
            // A raised controller shows its own labels, the thumbstick's among them.
            if (!show || !hand.Valid || !hand.HasAxis || NativeHandOptionWheel.OwnsHand(left) || Controllers.MenuControllerModels.LabelsVisible(left))
            {
                if (_views[side] != null && _views[side]!.activeSelf)
                    _views[side]!.SetActive(false);
                continue;
            }
            Ensure(side);
            var text = Caption(ZoomHand(left, leftHanded));
            if (_labels[side]!.text != text)
                _labels[side]!.text = text;
            var trackingRoot = camera!.transform.parent;
            var origin = trackingRoot != null ? trackingRoot.position : Vector3.zero;
            var basis = trackingRoot != null ? trackingRoot.rotation : Quaternion.identity;
            var gripRotation = basis * hand.Rotation;
            HandOptionWheelPose.Get(left, hand.NativeFrame, out var offset, out var normal, out var forward);
            _views[side]!.transform.SetPositionAndRotation(origin + basis * hand.Position + gripRotation * offset,
                gripRotation * Quaternion.LookRotation(-normal, forward));
            _canvases[side]!.worldCamera = camera;
            if (!_views[side]!.activeSelf)
                _views[side]!.SetActive(true);
        }
    }

    private void Ensure(int side)
    {
        if (_views[side] != null)
            return;
        var view = new GameObject("NOVR Map Stick Hint", typeof(RectTransform), typeof(Canvas));
        view.transform.localScale = Vector3.one * .00065f;
        ((RectTransform)view.transform).sizeDelta = new Vector2(230, 70);
        view.layer = (int)LayerHelper.GetVrUiLayer();
        var canvas = view.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 470;
        var backing = new GameObject("Backing", typeof(RectTransform), typeof(Image));
        backing.transform.SetParent(view.transform, false);
        backing.layer = view.layer;
        ((RectTransform)backing.transform).sizeDelta = new Vector2(230, 70);
        var image = backing.GetComponent<Image>();
        image.color = new Color(.03f, .05f, .07f, .82f);
        image.raycastTarget = false;
        var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
        label.transform.SetParent(view.transform, false);
        label.layer = view.layer;
        var text = label.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 22;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.rectTransform.sizeDelta = new Vector2(220, 64);
        view.SetActive(false);
        _views[side] = view;
        _canvases[side] = canvas;
        _labels[side] = text;
    }

    private void OnDisable()
    {
        foreach (var view in _views)
            if (view != null)
                view.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (var view in _views)
            if (view != null)
                Destroy(view);
    }
}
