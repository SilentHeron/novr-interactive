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
        var checks = 0;
        const float pixel = .0007f * .65f;
        var device = new InputDevice(1);
        var values = new List<float>();
        var root = (RectTransform)new GameObject("Widget").transform;
        using var editor = new CockpitWidgetAppearanceEditor(root, new Vector2(420, 170), values.Add, _ => throw new Exception("Unexpected visibility write"));
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
        for (var i = 0; i < 20; i++)
            Hand(editor.PencilCenter, .001f);
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
        var cues = InteractionHaptics.Cues.Count;
        var pulses = InteractionHaptics.SilentPulses;
        for (var i = 0; i < 10; i++)
            Hand(editor.SliderCenter + new Vector2(-170 + i * .01f, 0), .001f);
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
        using var other = new CockpitWidgetAppearanceEditor(root, new Vector2(260, 135), _ => throw new Exception("Unexpected write"), _ => throw new Exception("Unexpected visibility write"));
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
            using var shaped = new CockpitWidgetAppearanceEditor(root, size, _ => { }, _ => { });
            shaped.SetState(true, true, 0);
            Touch(shaped, shaped.PencilCenter);
            foreach (var scale in new[] { .35f, .65f, 3f })
                foreach (var x in new[] { -1, 1 })
                    foreach (var y in new[] { -1, 1 })
                        Check(!shaped.HitTest(new Vector3(x * size.x / 2, y * size.y / 2, 0), .0007f * scale),
                            "Popup leaves resize corner free for actual widget dimensions at every allowed scale");
        }
        editor.SetState(true, true, float.NaN, float.NaN);
        Touch(editor, editor.PencilCenter);
        editor.Dispose();
        Check(!editor.IsOpen && !editor.HitTest(Point(editor.PencilCenter, 0), pixel), "Disposal clears global popup ownership and all contacts");
        return checks + VisibilityChecks() + ResizeChecks() + HoverOwnershipChecks() + TextSizePencilChecks.Run();

        Vector3 Point(Vector2 at, float depth) => new(at.x, at.y, -depth / pixel);
        void Hand(Vector2 at, float depth) => editor.UpdateHand(0, Point(at, depth), true, pixel, device);
        void Touch(CockpitWidgetAppearanceEditor target, Vector2 at, int side = 0)
        {
            target.UpdateHand(side, Point(at, .04f), true, pixel, device);
            target.UpdateHand(side, Point(at, .04f), true, pixel, device);
            target.UpdateHand(side, Point(at, .001f), true, pixel, device);
        }
        void Check(bool ok, string message)
        {
            if (!ok)
                throw new Exception(message);
            checks++;
        }
    }

    private static int HoverOwnershipChecks()
    {
        const float pixel = .0007f * .65f;
        var checks = 0;
        var writes = 0;
        var root = (RectTransform)new GameObject("Two-hand appearance feedback").transform;
        using var editor = new CockpitWidgetAppearanceEditor(root, new Vector2(420, 170), _ => writes++, _ => writes++);
        var pencil = root.children.Single(x => x.gameObject.Name == "Edit widget appearance").GetComponent<UnityEngine.UI.Image>();
        var eye = root.children.Single(x => x.gameObject.Name == "Show or hide widget").GetComponent<UnityEngine.UI.Image>();
        var idle = new Color(.04f, .10f, .13f, .9f);
        var lit = new Color(.17f, .36f, .40f, 1);
        editor.SetState(true, true, .37f);
        foreach (var first in new[] { 0, 1 })
        {
            var second = 1 - first;
            editor.ReleaseHands();
            Hover(first, editor.PencilCenter);
            Hover(second, new Vector2(500, 500));
            Check(pencil.color == lit && eye.color == idle, "An idle second hand cannot erase the first hand's pencil hover");
            Hover(second, editor.EyeCenter);
            Check(pencil.color == lit && eye.color == lit, "Both hands can independently highlight different appearance controls");
            Hover(first, editor.PencilCenter, false);
            Check(pencil.color == idle && eye.color == lit, "Tracking loss clears only that hand's highlight");
            Hover(first, editor.EyeCenter);
            Hover(second, editor.EyeCenter, false);
            Check(eye.color == lit, "Shared eye hover persists until the final hand withdraws");
            Hover(first, new Vector2(500, 500));
            Check(pencil.color == idle && eye.color == idle, "Withdrawal clears both highlights without another press");
            Hover(first, editor.PencilCenter);
            editor.ReleaseHands();
            Check(pencil.color == idle && eye.color == idle && !editor.IsPointing(first), "Explicit release clears visual and contact ownership together");
        }
        Hover(0, editor.EyeCenter);
        editor.SetState(false, true, .37f);
        editor.SetState(true, true, .37f);
        Check(pencil.color == idle && eye.color == idle, "Closing and reopening layout cannot restore a stale highlight");
        Check(writes == 0 && !editor.IsOpen, "Hover and release never change visibility, opacity, or popup state");
        editor.Dispose();
        Hover(0, editor.PencilCenter);
        Check(!editor.IsPointing(0), "A late sample after disposal cannot retain hand ownership");
        return checks;

        void Hover(int side, Vector2 at, bool available = true) => editor.UpdateHand(side,
            new Vector3(at.x, at.y, -.04f / pixel), available, pixel, new InputDevice(side));
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
            checks++;
        }
    }

    private static int ResizeChecks()
    {
        const float pixel = .0007f * .8f;
        var checks = 0;
        var writes = new List<float>();
        var root = (RectTransform)new GameObject("Rectangular map").transform;
        using var editor = new CockpitWidgetAppearanceEditor(root, new Vector2(300, 300), writes.Add, _ => throw new Exception("Resize changed visibility"));
        editor.SetState(true, true, .4f);
        var oldPencil = editor.PencilCenter;
        foreach (var size in new[] { new Vector2(600, 150), new Vector2(150, 600), new Vector2(300, 300) })
        {
            editor.SetSize(size);
            Check(editor.PencilCenter.y == size.y * .5f + 62 && editor.EyeCenter.y == editor.PencilCenter.y,
                "Pencil and eye move above the reshaped map edge");
            Check(editor.SliderCenter.y == -size.y * .5f - 115, "Opacity popup contact follows the lower map edge");
            var pencil = (RectTransform)root.children.Single(x => x.gameObject.Name == "Edit widget appearance");
            var eye = (RectTransform)root.children.Single(x => x.gameObject.Name == "Show or hide widget");
            Check(pencil.anchoredPosition.y == editor.PencilCenter.y && eye.anchoredPosition.y == editor.EyeCenter.y,
                "Rendered edit icons and fingertip contacts remain aligned after reshape");
            Check(editor.HitTest(Point(editor.PencilCenter, .04f), pixel), "Pencil hit testing follows the new location");
            Hand(editor.PencilCenter, .04f);
            Hand(editor.PencilCenter, .04f);
            Hand(editor.PencilCenter, .001f);
            Check(editor.IsOpen, "Moved pencil opens the appearance popup");
            var popup = (RectTransform)root.children.Single(x => x.gameObject.Name == "Widget backplate editor");
            Check(popup.anchoredPosition.y == -size.y * .5f - 110, "Rendered popup follows its new contact region");
            Hand(editor.SliderCenter, .04f);
            Hand(editor.SliderCenter, .04f);
            Hand(editor.SliderCenter, .001f);
            Check(editor.IsHandBusy(0), "Moved slider accepts deliberate fingertip contact");
            var count = writes.Count;
            editor.SetSize(new Vector2(size.x, size.y + 2));
            Check(!editor.IsHandBusy(0) && writes.Count == count, "Reshape releases slider capture without changing opacity");
            Hand(editor.SliderCenter, .001f);
            Check(!editor.IsHandBusy(0) && writes.Count == count, "Moving a popup under a finger cannot synthesize a press");
            editor.SetState(false, true, .4f);
            editor.SetState(true, true, .4f);
        }
        editor.SetSize(new Vector2(150, 600));
        Check(!editor.HitTest(Point(oldPencil, 0), pixel), "Old pencil position no longer captures touches after reshaping");
        return checks;
        Vector3 Point(Vector2 at, float depth) => new(at.x, at.y, -depth / pixel);
        void Hand(Vector2 at, float depth) => editor.UpdateHand(0, Point(at, depth), true, pixel, new InputDevice(1));
        void Check(bool ok, string label)
        {
            if (!ok)
                throw new Exception(label);
            checks++;
        }
    }

    private static int VisibilityChecks()
    {
        const float pixel = .0007f * .35f;
        var checks = 0;
        var changes = new List<bool>();
        var backgrounds = new List<float>();
        var root = (RectTransform)new GameObject("Visibility widget").transform;
        using var editor = new CockpitWidgetAppearanceEditor(root, new Vector2(260, 135), backgrounds.Add, changes.Add);
        var eye = root.children.Single(x => x.gameObject.Name == "Show or hide widget").gameObject;
        var open = eye.transform.children.Single(x => x.gameObject.Name == "Eye open").gameObject;
        var closed = eye.transform.children.Single(x => x.gameObject.Name == "Eye closed").gameObject;
        var pencil = root.children.Single(x => x.gameObject.Name == "Edit widget appearance").gameObject;
        Check(!eye.activeSelf && !editor.HitTest(Point(editor.EyeCenter, 0), pixel), "Visibility icon has no interactions outside editing");
        editor.SetState(true, true, .37f, .22f);
        Check(eye.activeSelf && open.activeSelf && !closed.activeSelf, "Visible widget shows an open eye with pupil");
        Check(eye.GetComponent<CanvasGroup>().alpha == .22f && pencil.GetComponent<CanvasGroup>().alpha == .22f,
            "Both edit icons share surrounding-widget temporary dimming");
        Hand(editor.EyeCenter, .001f);
        Check(changes.Count == 0, "Eye appearing under a fingertip cannot hide a widget");
        Approach(editor.EyeCenter);
        Hand(editor.EyeCenter, .001f);
        Check(changes.SequenceEqual(new[] { false }) && !open.activeSelf && closed.activeSelf,
            "Direct eye touch hides readout and immediately shows closed lid and lashes");
        Check(editor.HitTest(Point(editor.EyeCenter, 0), pixel) && editor.HitTest(Point(editor.PencilCenter, 0), pixel) &&
            eye.activeSelf && pencil.activeSelf, "Hidden widget keeps both eye and pencil available");
        Check(!editor.IsOpen && backgrounds.Count == 0 && editor.IsPointing(0), "Eye consumes contact without opening pencil or changing background");
        var clickCount = InteractionHaptics.Cues.Count(x => x == InteractionCue.Click);
        for (var i = 0; i < 20; i++)
            Hand(editor.EyeCenter, .001f);
        Check(changes.Count == 1 && InteractionHaptics.Cues.Count(x => x == InteractionCue.Click) == clickCount,
            "Holding eye contact toggles once and emits one click");
        Hand(editor.PencilCenter, .001f);
        Check(!editor.IsOpen && changes.Count == 1, "Sliding a pressed finger from eye to pencil does not activate the neighbor");
        Time.unscaledTime += .3f;
        Approach(editor.EyeCenter);
        Hand(editor.EyeCenter, .001f);
        Check(changes.SequenceEqual(new[] { false, true }) && open.activeSelf && !closed.activeSelf,
            "Withdrawing and touching again restores readout and open-eye state");
        Time.unscaledTime += .3f;
        Approach(editor.EyeCenter, 0);
        Approach(editor.EyeCenter, 1);
        Hand(editor.EyeCenter, .001f, 0);
        Hand(editor.EyeCenter, .001f, 1);
        Check(changes.Count == 3 && changes.Last() == false, "Simultaneous touches from both hands do not immediately undo visibility");
        Approach(editor.EyeCenter, 1);
        Hand(editor.EyeCenter, .001f, 1);
        Check(changes.Count == 3, "Short cooldown suppresses immediate second-hand reflip");
        Time.unscaledTime += .3f;
        Approach(editor.EyeCenter, 1);
        Hand(editor.EyeCenter, .001f, 1);
        Check(changes.Count == 4 && changes.Last(), "Either hand can restore a hidden widget after withdrawal and cooldown");
        editor.UpdateHand(0, Point(editor.EyeCenter, .001f), false, pixel, new InputDevice(1));
        Time.unscaledTime += .3f;
        Hand(editor.EyeCenter, .001f);
        Check(changes.Count == 4, "Tracking or menu interruption requires a fresh approach to visibility icon");
        Approach(editor.PencilCenter);
        Hand(editor.PencilCenter, .001f);
        Check(editor.IsOpen, "Pencil still opens alongside the new eye control");
        Approach(editor.EyeCenter);
        Hand(editor.EyeCenter, .001f);
        Check(editor.IsOpen && changes.Count == 5 && backgrounds.Count == 0,
            "Visibility can change while opacity popup stays open without mutating backplate value");
        editor.SetState(true, false, .37f, 1);
        Check(closed.activeSelf && eye.GetComponent<CanvasGroup>().alpha == 1,
            "Parent visibility and focus changes refresh icon and restore normal brightness");
        editor.SetState(false, false, .37f);
        Check(!eye.activeSelf && !pencil.activeSelf && !editor.IsOpen && !editor.HitTest(Point(editor.EyeCenter, 0), pixel),
            "Saving or cancelling hides both edit controls and clears contact");
        editor.Dispose();
        Check(eye.destroyed && pencil.destroyed, "Disposal destroys visibility icon along with existing controls");
        return checks;

        Vector3 Point(Vector2 at, float depth) => new(at.x, at.y, -depth / pixel);
        void Hand(Vector2 at, float depth, int side = 0) => editor.UpdateHand(side, Point(at, depth), true, pixel, new InputDevice(side));
        void Approach(Vector2 at, int side = 0)
        {
            Hand(at, .04f, side);
            Hand(at, .04f, side);
        }
        void Check(bool ok, string message)
        {
            if (!ok)
                throw new Exception(message);
            checks++;
        }
    }
}
