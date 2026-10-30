using NOVR.MotionControls;

internal static class SwitchGrabChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("Switch capture follows a bounded cap-centered volume", () =>
        {
            truth(SwitchGrabGeometry.Contains(0, 0, 0, .03f));
            truth(SwitchGrabGeometry.Contains(.039f, 0, .04f, .05f));
            truth(SwitchGrabGeometry.Contains(0, -.059f, .04f, .05f));
            truth(SwitchGrabGeometry.Contains(.025f, .03f, .074f, .08f));
            truth(!SwitchGrabGeometry.Contains(.041f, 0, .04f, .05f));
            truth(!SwitchGrabGeometry.Contains(0, .061f, .04f, .05f));
            truth(!SwitchGrabGeometry.Contains(.035f, .045f, .04f, .05f));
            truth(SwitchGrabGeometry.Contains(0, 0, .099f, .12f));
            truth(!SwitchGrabGeometry.Contains(0, 0, .101f, .12f));
            truth(!SwitchGrabGeometry.Contains(0, 0, -.026f, .02f));
        });
        check("Switch capture cannot reach behind a plate or accept invalid tracking coordinates", () =>
        {
            truth(SwitchGrabGeometry.Contains(0, 0, -.019f, -.003f));
            truth(!SwitchGrabGeometry.Contains(0, 0, 0, -.009f));
            foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                truth(!SwitchGrabGeometry.Contains(value, 0, 0, .03f));
                truth(!SwitchGrabGeometry.Contains(0, value, 0, .03f));
                truth(!SwitchGrabGeometry.Contains(0, 0, value, .03f));
                truth(!SwitchGrabGeometry.Contains(0, 0, 0, value));
            }
        });
        check("A light switch squeeze can arrive just before contact, but is consumed once", () =>
        {
            var gate = new SwitchGripGate();
            gate.Update(true, 0, 0); gate.Update(true, .46f, .1f);
            truth(gate.Held && gate.CanAcquire(.1f) && gate.CanAcquire(.39f));
            truth(!gate.CanAcquire(.41f));
            gate.Update(true, 0, .5f); gate.Update(true, .46f, .6f);
            truth(gate.CanAcquire(.6f)); gate.Consume(); truth(!gate.CanAcquire(.61f));
            gate.Update(true, .3f, .7f); truth(gate.Held);
            gate.Update(true, .2f, .8f); truth(!gate.Held);
        });
        check("Switch recovery never turns an already-held squeeze into a grab", () =>
        {
            var gate = new SwitchGripGate();
            gate.Update(true, .9f, 0); truth(!gate.Held && !gate.CanAcquire(0));
            gate.Update(true, 0, .1f); gate.Update(true, .46f, .2f); truth(gate.Held);
            gate.Update(false, .46f, .3f); gate.Update(true, .46f, .4f);
            truth(!gate.Held && !gate.CanAcquire(.4f));
            gate.Update(true, 0, .5f); gate.Update(true, .46f, .6f); truth(gate.CanAcquire(.6f));
            gate.Update(true, float.NaN, .7f); truth(!gate.Held);
        });
        check("Switch cap capture keeps the nearest brake edge and neighboring switch outside its lateral reach", () =>
        {
            // 74 mm plate, cap shifted 31% to the right; BRAKE is 102 mm
            // right of GEAR. Even BRAKE's left edge is beyond cap capture.
            var capOffset = .074f * .31f;
            var brakeEdgeFromGearCap = .102f - .074f * .5f - capOffset;
            truth(brakeEdgeFromGearCap > .04f);
            foreach (var tilt in new[] { -25f, 0, 25f })
            {
                // Tilt is about X, so this separation holds throughout travel.
                truth(!SwitchGrabGeometry.Contains(brakeEdgeFromGearCap, tilt * .0001f, .02f, .05f));
                truth(!SwitchGrabGeometry.Contains(.102f, 0, .02f, .05f));
            }
        });
        check("A fresh near-switch grip works, but an already-held far grip cannot acquire on entry", () =>
        {
            var gate = new ControllerButtonGate();
            gate.Update(true, 0); gate.Update(true, 1);
            truth(gate.Pressed && SwitchGrabGeometry.Contains(0, 0, .04f, .06f));
            gate.Update(true, 0); gate.Update(true, 1); // A new press far away.
            truth(gate.Pressed && !SwitchGrabGeometry.Contains(0, 0, .20f, .22f));
            gate.Update(true, 1); // Entering the cap volume while still held.
            truth(!gate.Pressed && gate.Held);
            gate.Update(true, 0); gate.Update(true, 1);
            truth(gate.Pressed);
        });
        check("Switch acquisition requires release after tracking or interaction availability returns", () =>
        {
            foreach (var invalid in new[] { false, true })
            {
                var gate = new ControllerButtonGate();
                gate.Update(true, 0); gate.Update(true, 1); truth(gate.Pressed);
                gate.Update(!invalid, invalid ? 1 : float.NaN); truth(!gate.Held);
                gate.Update(true, 1); truth(!gate.Pressed && !gate.Held);
                gate.Update(true, 0); gate.Update(true, 1); truth(gate.Pressed);
            }
        });
    }
}
