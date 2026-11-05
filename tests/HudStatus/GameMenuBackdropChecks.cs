using System.Reflection;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

internal static class GameMenuBackdropChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        bool Near(float value, float expected) => Math.Abs(value - expected) < .00001f;
        void Call(VrGameMenuBackdrop adapter, string method) => typeof(VrGameMenuBackdrop)
            .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(adapter, null);
        var root = new GameObject("Native pause menu", typeof(RectTransform), typeof(Image), typeof(Canvas));
        var backdrop = root.GetComponent<Image>();
        backdrop.color = new Color(.1f, .2f, .3f, .42f);
        backdrop.raycastTarget = true;
        var canvas = root.GetComponent<Canvas>();
        var group = root.AddComponent<CanvasGroup>();
        group.alpha = .8f;
        var buttonRoot = new GameObject("Native menu button", typeof(RectTransform), typeof(Image));
        buttonRoot.transform.SetParent(root.transform, false);
        var button = buttonRoot.GetComponent<Image>();
        button.color = new Color(.2f, .3f, .4f, .9f);
        button.raycastTarget = true;
        var textRoot = new GameObject("Native button label", typeof(RectTransform), typeof(Text));
        textRoot.transform.SetParent(buttonRoot.transform, false);
        var text = textRoot.GetComponent<Text>();
        text.text = "Resume";
        text.color = new Color(1, 1, 1, 1);
        text.raycastTarget = false;
        var adapter = root.AddComponent<VrGameMenuBackdrop>();

        XRSettings.enabled = false;
        Call(adapter, "OnEnable");
        Check(Near(backdrop.color.a, .42f) && backdrop.canvasRenderer.cullTransparentMesh,
            "Non-VR pause menu retains its original translucent backdrop");
        XRSettings.enabled = true;
        Call(adapter, "LateUpdate");
        Check(backdrop.color.a == 0 && Near(backdrop.color.r, .1f) && Near(backdrop.color.g, .2f) && Near(backdrop.color.b, .3f),
            "VR hides only root backdrop alpha and preserves its color channels");
        Check(!backdrop.canvasRenderer.cullTransparentMesh && !backdrop.canvasRenderer.cull && backdrop.raycastTarget && backdrop.enabled,
            "Transparent root stays enabled and eligible to block clicks outside menu buttons");
        Check(root.activeSelf && canvas.enabled && Near(group.alpha, .8f) && group.interactable && group.blocksRaycasts,
            "Backdrop adapter leaves the menu hierarchy, canvas and canvas-group interaction intact");
        Check(buttonRoot.activeSelf && button.enabled && button.raycastTarget && !button.canvasRenderer.cull &&
            button.canvasRenderer.cullTransparentMesh && Near(button.color.a, .9f),
            "Child button graphics retain opacity, draw state, and raycast eligibility");
        Check(textRoot.activeSelf && text.enabled && text.text == "Resume" && text.color.a == 1 &&
            !text.raycastTarget && !text.canvasRenderer.cull && text.canvasRenderer.cullTransparentMesh,
            "Child labels remain visible without becoming input blockers");

        backdrop.color = new Color(.6f, .5f, .4f, .7f);
        backdrop.canvasRenderer.cullTransparentMesh = true;
        Call(adapter, "LateUpdate");
        Check(backdrop.color.a == 0 && !backdrop.canvasRenderer.cullTransparentMesh,
            "Native backdrop refresh cannot reintroduce the VR veil or transparent-mesh auto-culling");
        XRSettings.enabled = false;
        Call(adapter, "LateUpdate");
        Check(Near(backdrop.color.a, .42f) && backdrop.canvasRenderer.cullTransparentMesh && Near(backdrop.color.r, .6f),
            "Disabling VR restores captured alpha and auto-culling while retaining current native RGB");
        Call(adapter, "LateUpdate");
        Check(Near(backdrop.color.a, .42f), "Repeated non-VR frames do not overwrite restored opacity");

        XRSettings.enabled = true;
        for (var i = 0; i < 25; i++)
        {
            Call(adapter, "OnEnable");
            Check(backdrop.color.a == 0 && !backdrop.canvasRenderer.cullTransparentMesh,
                "Reopening the pause menu suppresses the veil without losing the saved state");
            Call(adapter, "OnDisable");
            Check(Near(backdrop.color.a, .42f) && backdrop.canvasRenderer.cullTransparentMesh,
                "Closing the pause menu restores its original alpha and transparency behavior");
        }
        backdrop.color = new Color(.1f, .2f, .3f, .25f);
        backdrop.canvasRenderer.cullTransparentMesh = false;
        backdrop.raycastTarget = false;
        Call(adapter, "OnEnable");
        Call(adapter, "OnDestroy");
        Call(adapter, "OnDisable");
        Check(Near(backdrop.color.a, .25f) && !backdrop.canvasRenderer.cullTransparentMesh && !backdrop.raycastTarget,
            "Teardown is repeatable and preserves a newly supplied native alpha, culling, and raycast configuration");

        var delayedRoot = new GameObject("Menu before backdrop construction", typeof(RectTransform));
        var delayed = delayedRoot.AddComponent<VrGameMenuBackdrop>();
        Call(delayed, "OnEnable");
        Call(delayed, "LateUpdate");
        Check(delayedRoot.activeSelf && delayed.enabled, "An incomplete native menu is tolerated without disabling its hierarchy");
        var delayedImage = delayedRoot.AddComponent<Image>();
        delayedImage.color = new Color(.2f, .3f, .4f, .65f);
        delayedImage.raycastTarget = true;
        Call(delayed, "LateUpdate");
        Check(delayedImage.color.a == 0 && !delayedImage.canvasRenderer.cullTransparentMesh && delayedImage.raycastTarget,
            "A backdrop constructed after attachment is acquired without altering its raycast state");
        Call(delayed, "OnDestroy");
        Check(Near(delayedImage.color.a, .65f) && delayedImage.canvasRenderer.cullTransparentMesh,
            "Late backdrop acquisition restores correctly on teardown");
        XRSettings.enabled = false;
        return checks;
    }
}
