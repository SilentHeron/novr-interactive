using NOVR.MotionControls;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

internal static class TextSizePencilChecks
{
    internal static int Run()
    {
        var checks = 0;
        const float pixel = .0007f * .65f;
        var opacity = new List<float>();
        var text = new List<float>();
        var root = (RectTransform)new GameObject("Text widget").transform;
        using var editor = new CockpitWidgetAppearanceEditor(root, new Vector2(420, 170), opacity.Add,
            _ => throw new Exception("Text editing changed visibility"), text.Add);
        var popup = (RectTransform)root.children.Single(x => x.gameObject.Name == "Widget backplate editor");
        var handle = (RectTransform)popup.children.Single(x => x.gameObject.Name == "Text size handle");
        var label = popup.children.Where(x => x.gameObject.Name == "Appearance label")
            .Select(x => x.GetComponent<Text>()).Single(x => x.text.StartsWith("Text size"));
        var close = popup.children.Single(x => x.gameObject.Name == "Close appearance editor");
        editor.SetState(true, true, .4f, textScale: 1);
        Touch(editor, editor.PencilCenter);
        Check(editor.IsOpen && opacity.Count == 0 && text.Count == 0, "Opening text appearance does not mutate either setting");
        Hand(editor, editor.TextSliderCenter, .001f);
        Check(!editor.IsHandBusy(0) && text.Count == 0, "New text track cannot capture a finger already inside it");
        Touch(editor, editor.TextSliderCenter);
        Check(editor.IsHandBusy(0) && text.Count == 1 && opacity.Count == 0, "A deliberate text-track touch captures only text size");
        Hand(editor, editor.TextSliderCenter + new Vector2(-172, 0), .001f);
        Near(text.Last(), .65f, "Left text endpoint is 65%");
        Check(label.text == "Text size 65%" && handle.anchoredPosition.x == -172, "Text value and handle follow fingertip adjustment");
        Hand(editor, editor.TextSliderCenter + new Vector2(172, 0), .001f);
        Near(text.Last(), 1.5f, "Right text endpoint is 150%");
        Check(label.text == "Text size 150%" && handle.anchoredPosition.x == 172 && opacity.Count == 0,
            "Growing text never adjusts backplate opacity");
        var writes = text.Count;
        Touch(editor, editor.TextSliderCenter, 1);
        Touch(editor, editor.SliderCenter, 1);
        Check(editor.IsHandBusy(0) && !editor.IsHandBusy(1) && text.Count == writes && opacity.Count == 0,
            "Other hand cannot steal either track while text is captured");
        Hand(editor, editor.SliderCenter, .001f);
        Check(!editor.IsHandBusy(0) && opacity.Count == 0 && text.Count == writes,
            "Sliding from text onto opacity releases capture without selecting the neighboring track");
        Hand(editor, editor.SliderCenter, .001f);
        Check(opacity.Count == 0, "Holding contact on the neighboring track still requires a fresh approach");
        Touch(editor, editor.SliderCenter);
        Check(editor.IsHandBusy(0) && opacity.Last() == .5f && text.Count == writes, "Opacity remains independently editable after withdrawal");
        var opacityWrites = opacity.Count;
        Touch(editor, editor.TextSliderCenter, 1);
        Check(opacity.Count == opacityWrites && text.Count == writes && !editor.IsHandBusy(1),
            "Text cannot be captured by another hand while opacity is held");
        editor.UpdateHand(0, Point(editor.SliderCenter, .001f), false, pixel, new InputDevice(0));
        Check(!editor.IsHandBusy(0) && !editor.IsPointing(0), "Tracking loss clears active track and pointing ownership");
        Hand(editor, editor.SliderCenter, .001f);
        Check(!editor.IsHandBusy(0) && opacity.Count == opacityWrites, "Tracking return inside track cannot synthesize a press");
        Touch(editor, editor.TextSliderCenter);
        Check(editor.IsHandBusy(0), "Text can be captured after a fresh approach following tracking loss");
        var textBeforeClose = text.Count;
        Touch(editor, popup.anchoredPosition + ((RectTransform)close).anchoredPosition, 1);
        Check(!editor.IsOpen && !editor.IsHandBusy(0) && !editor.IsHandBusy(1) && text.Count == textBeforeClose,
            "Either hand can close the popup and release capture without changing text");
        Touch(editor, editor.PencilCenter);
        editor.SetState(true, true, .4f, textScale: 1.2f);
        var expectedX = ((1.2f - .65f) / .85f - .5f) * 344;
        Check(label.text == "Text size 120%" && Math.Abs(handle.anchoredPosition.x - expectedX) < .001f,
            "VR-menu or saved-profile changes refresh the open pencil readout and handle");
        Check(opacity.Count == opacityWrites && text.Count == textBeforeClose, "External state refresh is read-only to both callbacks");
        editor.SetState(true, true, .4f, textScale: float.NaN);
        Check(label.text == "Text size 100%", "Invalid external text size displays the default safely");
        foreach (var size in new[] { new Vector2(420, 80), new Vector2(150, 600), new Vector2(600, 150), new Vector2(460, 270) })
        {
            editor.SetSize(size);
            Check(popup.anchoredPosition.y + popup.sizeDelta.y * .5f == -size.y * .5f - 35,
                "Expanded text popup stays fully below the resized content frame");
            Check(editor.TextSliderCenter == popup.anchoredPosition + new Vector2(0, -64),
                "Text hit region remains aligned with the visible track after resizing");
            foreach (var scale in new[] { .35f, .65f, 3f })
                foreach (var x in new[] { -1, 1 })
                    foreach (var y in new[] { -1, 1 })
                        Check(!editor.HitTest(new Vector3(x * size.x * .5f, y * size.y * .5f, 0), .0007f * scale),
                            "Text appearance popup leaves both-hand resize corners available at every widget scale");
            Touch(editor, editor.TextSliderCenter);
            Check(editor.IsHandBusy(0), "Resized text track accepts fresh fingertip contact");
            var beforeResize = text.Count;
            editor.SetSize(new Vector2(size.x, size.y + 2));
            Check(!editor.IsHandBusy(0) && text.Count == beforeResize, "Viewport resize releases text capture without writing a setting");
            Hand(editor, editor.TextSliderCenter, .001f);
            Check(!editor.IsHandBusy(0) && text.Count == beforeResize, "Moving text track under the finger cannot activate it");
        }
        editor.SetState(false, true, .4f, textScale: 1);
        Check(!editor.IsOpen && !editor.HitTest(Point(editor.TextSliderCenter, .001f), pixel),
            "Saving or cancelling layout removes text controls and contacts");
        editor.Dispose();
        Check(popup.gameObject.destroyed && !editor.IsHandBusy(0), "Disposal removes the popup and all text capture");

        var plainRoot = (RectTransform)new GameObject("Diagram widget").transform;
        using var plain = new CockpitWidgetAppearanceEditor(plainRoot, new Vector2(420, 170),
            _ => throw new Exception("Non-text contact changed opacity"), _ => throw new Exception("Unexpected visibility change"));
        var plainPopup = (RectTransform)plainRoot.children.Single(x => x.gameObject.Name == "Widget backplate editor");
        plain.SetState(true, true, .4f);
        Touch(plain, plain.PencilCenter);
        Check(plainPopup.sizeDelta.y == 150 && !plainPopup.children.Any(x => x.gameObject.Name.StartsWith("Text size")),
            "Non-text widgets retain the compact popup without a text handle or track");
        Touch(plain, plain.TextSliderCenter);
        Check(!plain.IsHandBusy(0), "Non-text popup cannot capture a text-size gesture");
        return checks;

        Vector3 Point(Vector2 at, float depth) => new(at.x, at.y, -depth / pixel);
        void Hand(CockpitWidgetAppearanceEditor target, Vector2 at, float depth, int side = 0) =>
            target.UpdateHand(side, Point(at, depth), true, pixel, new InputDevice(side));
        void Touch(CockpitWidgetAppearanceEditor target, Vector2 at, int side = 0)
        {
            Hand(target, at, .04f, side);
            Hand(target, at, .04f, side);
            Hand(target, at, .001f, side);
        }
        void Check(bool ok, string message)
        {
            if (!ok)
                throw new Exception(message);
            checks++;
        }
        void Near(float actual, float expected, string message) => Check(Math.Abs(actual - expected) < .0001f, message);
    }
}
