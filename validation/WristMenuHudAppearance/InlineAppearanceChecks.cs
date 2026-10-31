using NOVR.MotionControls;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.XR;

// Production UI/contact logic, with Unity rendering and controller vibration
// replaced at the boundary. No simulated aircraft or scene lifecycle.
internal static class InlineAppearanceChecks
{
    internal static int Run()
    {
        var checks = 0; const float pixel = .0007f * .65f;
        var device = new InputDevice(1); var values = new List<float>();
        var root = (RectTransform)new GameObject("Widget").transform;
        using var editor = new CockpitWidgetAppearanceEditor(root, new Vector2(420, 170), values.Add);
        Check(!editor.HitTest(Point(editor.PencilCenter, 0), pixel), "Editor has no contacts before editing");
        editor.SetState(true, false, .25f);
        Check(editor.HitTest(Point(editor.PencilCenter, .04f), pixel), "Hidden widgets retain an editable pencil and approach zone");
        Check(!editor.HitTest(Point(editor.PencilCenter, .08f), pixel) && !editor.HitTest(Point(editor.PencilCenter, -.03f), pixel),
            "Contact does not extend through cockpit or far into space");
        Check(!editor.HitTest(new Vector3(float.NaN, 0, 0), pixel) && !editor.HitTest(Vector3.zero, 0), "Malformed coordinates reject contact");
        Hand(editor.PencilCenter, .001f);
        Check(!editor.IsOpen && values.Count == 0, "Spawning or recovering with a finger inside does not activate");
        Touch(editor, editor.PencilCenter);
        Check(editor.IsOpen && editor.IsPointing(0), "A deliberate direct fingertip approach opens pencil without a button or grip");
        var clickCount = InteractionHaptics.Cues.Count(x => x == InteractionCue.Click);
        for (var i = 0; i < 20; i++) Hand(editor.PencilCenter, .001f);
        Check(editor.IsOpen && InteractionHaptics.Cues.Count(x => x == InteractionCue.Click) == clickCount, "Held pencil contact neither repeats sound nor toggles");
        Check(values.Count == 0, "Opening an editor does not quantize or change opacity");
        Hand(editor.SliderCenter, .001f);
        Check(!editor.IsHandBusy(0) && values.Count == 0, "New popup cannot capture a finger already in its track");
        Touch(editor, editor.SliderCenter);
        Check(editor.IsHandBusy(0) && editor.IsPointing(0) && values.Last() == .5f, "Fresh fingertip touch captures slider and sets absolute position");
        Time.unscaledTime += .1f;
        Hand(editor.SliderCenter + new Vector2(172, 0), .001f);
        Check(values.Last() == 1, "Dragging to right endpoint makes background solid");
        Time.unscaledTime += .1f;
        Hand(editor.SliderCenter + new Vector2(-172, 0), .001f);
        Check(values.Last() == 0, "Dragging to left endpoint clears background");
        var cues = InteractionHaptics.Cues.Count; var pulses = InteractionHaptics.SilentPulses;
        for (var i = 0; i < 10; i++) Hand(editor.SliderCenter + new Vector2(-170 + i * .01f, 0), .001f);
        Check(InteractionHaptics.Cues.Count == cues && InteractionHaptics.SilentPulses == pulses,
            "Sub-percent jitter produces no repetitive sound or haptic chatter");
        Check(pulses >= 2, "Moving across meaningful slider ranges gives bounded silent haptic ticks");
        var beforeOtherHand = values.Count;
        Touch(editor, editor.SliderCenter + new Vector2(50, 0), 1);
        Check(editor.IsHandBusy(0) && !editor.IsHandBusy(1) && values.Count == beforeOtherHand, "Second hand cannot steal a captured slider");
        Hand(editor.SliderCenter + new Vector2(200, 0), .001f);
        Check(editor.IsHandBusy(0), "Captured slider remains busy slightly beyond its visual rail");
        Hand(editor.SliderCenter, .06f);
        Check(!editor.IsHandBusy(0), "Withdrawing releases slider capture");
        var beforeStale = values.Count;
        editor.UpdateHand(0, Point(editor.SliderCenter, .001f), false, pixel, device);
        Hand(editor.SliderCenter, .001f);
        Check(!editor.IsHandBusy(0) && values.Count == beforeStale, "Menu/tracking loss clears capture and rejects stale contact on return");
        Touch(editor, editor.SliderCenter);
        Check(editor.IsHandBusy(0), "A fresh approach works after recovery");
        editor.ReleaseHands();
        Check(!editor.IsHandBusy(0) && !editor.IsPointing(0), "Explicit release clears hand ownership and pointing");
        using var other = new CockpitWidgetAppearanceEditor(root, new Vector2(260, 135), _ => throw new Exception("Unexpected write"));
        other.SetState(true, true, .75f, .22f);
        Touch(other, other.PencilCenter);
        Check(other.IsOpen && !editor.IsOpen && !editor.IsHandBusy(0), "Opening another editor closes previous popup and capture");
        Touch(other, other.PencilCenter);
        Check(!other.IsOpen, "Touching the pencil again closes the popup after withdrawal");
        Touch(other, other.PencilCenter);
        var close = other.SliderCenter + new Vector2(126, 56);
        Touch(other, close);
        Check(!other.IsOpen, "Direct touch of close icon closes popup");
        Touch(other, other.PencilCenter);
        other.SetState(false, true, .75f);
        Check(!other.IsOpen && !other.HitTest(Point(other.PencilCenter, 0), pixel), "Finishing or cancelling layout removes every edit contact");

        foreach (var size in new[] { new Vector2(420, 80), new Vector2(260, 135), new Vector2(460, 270), new Vector2(300, 300) })
        {
            using var shaped = new CockpitWidgetAppearanceEditor(root, size, _ => { }); shaped.SetState(true, true, 0);
            Touch(shaped, shaped.PencilCenter);
            foreach (var scale in new[] { .35f, .65f, 3f })
            foreach (var x in new[] { -1, 1 }) foreach (var y in new[] { -1, 1 })
                Check(!shaped.HitTest(new Vector3(x * size.x / 2, y * size.y / 2, 0), .0007f * scale),
                    "Popup leaves resize corner free for actual widget dimensions at every allowed scale");
        }
        editor.SetState(true, true, float.NaN, float.NaN);
        Touch(editor, editor.PencilCenter); editor.Dispose();
        Check(!editor.IsOpen && !editor.HitTest(Point(editor.PencilCenter, 0), pixel), "Disposal clears global popup ownership and all contacts");
        return checks;

        Vector3 Point(Vector2 at, float depth) => new(at.x, at.y, -depth / pixel);
        void Hand(Vector2 at, float depth) => editor.UpdateHand(0, Point(at, depth), true, pixel, device);
        void Touch(CockpitWidgetAppearanceEditor target, Vector2 at, int side = 0)
        {
            target.UpdateHand(side, Point(at, .04f), true, pixel, device);
            target.UpdateHand(side, Point(at, .04f), true, pixel, device);
            target.UpdateHand(side, Point(at, .001f), true, pixel, device);
        }
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    }
}
