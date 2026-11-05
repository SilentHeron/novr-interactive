using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// Noninteractive ghosts show the exact wrist location/size before release.
// They never borrow native content or take controller input.
internal sealed partial class CockpitHudWidgets
{
    private sealed class WristDockPreview
    {
        internal RectTransform Ghost = null!, Prompt = null!;
        internal Canvas GhostCanvas = null!, PromptCanvas = null!;
        internal Image Fill = null!;
        internal readonly Image[] Edges = new Image[4];
        internal Text WristLabel = null!, PromptLabel = null!;
    }
    private readonly WristDockPreview?[] _dockPreviews = new WristDockPreview?[2];

    private RectTransform DockCanvas(string name, out Canvas canvas)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        go.transform.SetParent(transform, false);
        go.SetActive(false);
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var group = go.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        return (RectTransform)go.transform;
    }
    private WristDockPreview CreateWristDockPreview()
    {
        var preview = new WristDockPreview();
        preview.Ghost = DockCanvas("NOVR wrist attachment preview", out preview.GhostCanvas);
        preview.Fill = Plate("Wrist preview tint", preview.Ghost, new Color(.1f, .7f, .8f, .09f));
        for (var i = 0; i < 4; i++)
            preview.Edges[i] = Plate("Wrist preview outline", preview.Ghost, Color.white);
        preview.WristLabel = Label(preview.Ghost, "", Vector2.zero, new Vector2(420, 70), 23);
        preview.Prompt = DockCanvas("NOVR wrist release hint", out preview.PromptCanvas);
        preview.Prompt.sizeDelta = new Vector2(410, 72);
        Plate("Release hint contrast", preview.Prompt, new Color(.025f, .07f, .085f, .92f));
        preview.PromptLabel = Label(preview.Prompt, "", Vector2.zero, new Vector2(400, 68), 23);
        LayerHelper.SetLayerRecursive(preview.Ghost, LayerHelper.GetVrUiLayer());
        LayerHelper.SetLayerRecursive(preview.Prompt, LayerHelper.GetVrUiLayer());
        return preview;
    }

    private static void DockTransform(RectTransform root, Vector3 position, Quaternion rotation, float scale,
        Transform gameRoot, Transform uiRoot, Transform eye)
    {
        root.SetPositionAndRotation(uiRoot.TransformPoint(gameRoot.InverseTransformPoint(
                CockpitWidgetSpace.ToWorld(eye.position, eye.rotation, position))),
            uiRoot.rotation * Quaternion.Inverse(gameRoot.rotation) * eye.rotation * rotation);
        root.localScale = Vector3.one * (PixelSize * scale);
    }
    private void PlaceWristDockPreviews(bool ready, Transform? gameRoot, Transform? uiRoot, Transform? eye)
    {
        for (var index = 0; index < 2; index++)
        {
            var drag = _hands[index];
            var show = ready && _editing && !WristMenu.IsOpen && drag.Id != null && drag.Gate.Held &&
                (drag.Dock.Near || drag.Dock.Detach);
            var preview = _dockPreviews[index];
            if (!show || gameRoot == null || uiRoot == null || eye == null)
            {
                if (preview != null)
                {
                    preview.Ghost.gameObject.SetActive(false);
                    preview.Prompt.gameObject.SetActive(false);
                }
                continue;
            }
            preview ??= _dockPreviews[index] = CreateWristDockPreview();
            var id = drag.Id!;
            var active = ActivePose(id);
            var placed = CockpitPose(id, active);
            var rotation = Quaternion.Euler(placed.Euler);
            var halfHeight = Size(id).y * PixelSize * active.Scale * .5f;
            DockTransform(preview.Prompt, placed.Position + rotation * new Vector3(0, halfHeight + .055f, 0),
                rotation, .6f, gameRoot, uiRoot, eye);
            var wristName = drag.Dock.Target == HudWidgetAnchor.LeftWrist ? "LEFT WRIST" : "RIGHT WRIST";
            preview.PromptLabel.text = drag.Dock.Detach ? "Release to place\nIN COCKPIT" :
                (drag.Dock.Gate.Ready ? "Release to attach\n" : "Hold over wrist\n") + wristName;
            preview.PromptCanvas.worldCamera = APIBus.CockpitHudCamera;
            preview.Prompt.gameObject.SetActive(true);
            var ghost = drag.Dock.Near && TryWristFrame(drag.Dock.Target == HudWidgetAnchor.LeftWrist, out _, out _);
            preview.Ghost.gameObject.SetActive(ghost);
            if (!ghost)
                continue;
            TryWristFrame(drag.Dock.Target == HudWidgetAnchor.LeftWrist, out var origin, out var wristRotation);
            DockTransform(preview.Ghost, origin, wristRotation, DockScale(active), gameRoot, uiRoot, eye);
            var size = Size(id);
            preview.Ghost.sizeDelta = preview.Fill.rectTransform.sizeDelta = size;
            var color = drag.Dock.Gate.Ready ? Color.white : new Color(.35f, .8f, .9f, .7f);
            for (var edgeIndex = 0; edgeIndex < 4; edgeIndex++)
            {
                var edge = preview.Edges[edgeIndex];
                edge.color = color;
                edge.rectTransform.sizeDelta = edgeIndex < 2 ? new Vector2(size.x, 4) : new Vector2(4, size.y);
                edge.rectTransform.anchoredPosition = edgeIndex < 2
                    ? new Vector2(0, (edgeIndex == 0 ? -1 : 1) * size.y * .5f)
                    : new Vector2((edgeIndex == 2 ? -1 : 1) * size.x * .5f, 0);
            }
            preview.WristLabel.rectTransform.anchoredPosition = new Vector2(0, -size.y * .5f - 40);
            preview.WristLabel.text = drag.Dock.Gate.Ready ? "Release to attach\n" + wristName : wristName;
            preview.WristLabel.color = color;
            preview.GhostCanvas.worldCamera = APIBus.CockpitHudCamera;
        }
    }

    private void ReleaseWristDockPreviews()
    {
        for (var i = 0; i < _dockPreviews.Length; i++)
        {
            var preview = _dockPreviews[i];
            if (preview == null)
                continue;
            Destroy(preview.Ghost.gameObject);
            Destroy(preview.Prompt.gameObject);
            _dockPreviews[i] = null;
        }
    }
}
