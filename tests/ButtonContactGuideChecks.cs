using NOVR.MotionControls;

internal static class ButtonContactGuideChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("Button guide captures only close to a real button face and limits centering", () =>
        {
            var guide = new ButtonContactGuide();
            truth(!guide.Update(true, 0, 0, .036f, .04f, .02f));
            truth(!guide.Update(true, .041f, 0, .02f, .04f, .02f));
            truth(!guide.Update(true, 0, .021f, .02f, .04f, .02f));
            truth(guide.Update(true, .035f, .01f, .02f, .04f, .02f));
            var shift = MathF.Sqrt(MathF.Pow(.035f - guide.X, 2) + MathF.Pow(.01f - guide.Y, 2));
            truth(MathF.Abs(shift - .02f) < .000001f);
            truth(MathF.Abs(guide.Depth - .02f) < .000001f);
        });
        check("Captured button guide keeps one lateral axis while preserving raw press depth", () =>
        {
            var guide = new ButtonContactGuide();
            truth(guide.Update(true, .01f, -.005f, .03f, .016f, .015f));
            var x = guide.X; var y = guide.Y;
            foreach (var depth in new[] { .025f, .012f, .003f, 0, -.01f, -.024f })
            {
                truth(guide.Update(true, .022f, .008f, depth, .016f, .015f));
                truth(guide.X == x && guide.Y == y && guide.Depth == depth);
                truth(guide.VisualDepth >= -.003f && guide.VisualDepth <= .035f);
                truth(guide.HoldsPress == (depth <= .012f));
            }
        });
        check("Starting a guide on a pressed face never generates a button action", () =>
        {
            var guide = new ButtonContactGuide(); var press = new CockpitTouchGate();
            for (var i = 0; i < 30; i++)
            {
                truth(guide.Update(true, .01f, .005f, .003f, .016f, .015f));
                truth(!press.Update(true, guide.Captured, guide.Depth));
            }
            truth(guide.Update(true, 0, 0, .03f, .016f, .015f));
            truth(!press.Update(true, true, guide.Depth));
            truth(guide.Update(true, .023f, 0, .003f, .016f, .015f));
            truth(press.Update(true, guide.Captured, guide.Depth));
            for (var i = 0; i < 200; i++) truth(!press.Update(true, true, guide.Depth));
        });
        check("A fast genuine approach can press once when the guide first captures", () =>
        {
            var guide = new ButtonContactGuide(); var press = new CockpitTouchGate();
            truth(!press.Update(true, true, .06f)); truth(!press.Update(true, true, .06f));
            truth(guide.Update(true, .005f, 0, .003f, .016f, .015f));
            truth(press.Update(true, guide.Captured, guide.Depth));
            truth(!press.Update(true, guide.Captured, guide.Depth));
        });
        check("Sideways escape releases before a guide can transfer to another button", () =>
        {
            var guide = new ButtonContactGuide();
            truth(guide.Update(true, 0, 0, .025f, .04f, .03f));
            truth(!guide.Update(true, .041f, 0, .003f, .04f, .03f));
            truth(!guide.Captured && guide.WaitingForWithdrawal);
            truth(!guide.HoldsPress);
            truth(!guide.Update(true, .041f, 0, .003f, .04f, .03f));
            truth(!guide.Captured && !guide.WaitingForWithdrawal);
            truth(guide.Update(true, 0, 0, .025f, .04f, .03f));
        });
        check("Retreat releases while excess penetration waits for a real withdrawal", () =>
        {
            var guide = new ButtonContactGuide();
            truth(guide.Update(true, 0, 0, .03f, .02f, .02f));
            truth(!guide.Update(true, 0, 0, -.026f, .02f, .02f));
            truth(!guide.Update(true, 0, 0, .003f, .02f, .02f));
            truth(guide.WaitingForWithdrawal);
            truth(!guide.Update(true, 0, 0, .071f, .02f, .02f));
            truth(!guide.WaitingForWithdrawal);
            truth(guide.Update(true, 0, 0, .03f, .02f, .02f));
            truth(!guide.Update(true, 0, 0, .071f, .02f, .02f));
            truth(!guide.Captured);
        });
        check("Page, menu, tracking and invalid-data cancellation cannot recapture a resting finger", () =>
        {
            foreach (var mode in new[] { "page", "unavailable", "invalid" })
            {
                var guide = new ButtonContactGuide();
                truth(guide.Update(true, 0, 0, .02f, .02f, .02f));
                if (mode == "page") guide.Cancel();
                else truth(!guide.Update(mode != "unavailable", mode == "invalid" ? float.NaN : 0, 0, .003f, .02f, .02f));
                truth(!guide.Captured && guide.WaitingForWithdrawal);
                truth(!guide.Update(true, 0, 0, .003f, .02f, .02f));
                truth(!guide.Update(true, 0, 0, .075f, .02f, .02f));
                truth(guide.Update(true, 0, 0, .03f, .02f, .02f));
            }
        });
        check("Left and right guides have independent capture and cancellation state", () =>
        {
            var left = new ButtonContactGuide(); var right = new ButtonContactGuide();
            truth(left.Update(true, .012f, 0, .025f, .02f, .02f));
            truth(right.Update(true, -.012f, 0, .02f, .02f, .02f));
            left.Cancel(); truth(!left.Captured && right.Captured);
            truth(right.Update(true, .01f, 0, -.002f, .02f, .02f));
            left.Reset(); truth(!left.WaitingForWithdrawal && right.Captured && right.Depth == -.002f);
        });
        check("A key that disables itself still releases its tracked hand on withdrawal", () =>
        {
            foreach (var sideways in new[] { false, true })
            {
                var guide = new ButtonContactGuide();
                truth(guide.Update(true, 0, 0, .02f, .016f, .015f));
                truth(!guide.Update(false, 0, 0, .003f, .016f, .015f));
                truth(!guide.Captured && guide.WaitingForWithdrawal && !guide.HoldsPress);
                truth(!guide.Update(false, sideways ? .05f : 0, 0, sideways ? .003f : .08f, .016f, .015f));
                truth(!guide.Captured && !guide.WaitingForWithdrawal);
                truth(!guide.Update(false, 0, 0, .02f, .016f, .015f));
                truth(!guide.Captured); // Availability loss never authorizes reacquisition.
                truth(guide.Update(true, 0, 0, .02f, .016f, .015f));
            }
        });
        check("Unavailable or lost tracking cannot fake withdrawal using default coordinates", () =>
        {
            var guide = new ButtonContactGuide();
            truth(guide.Update(true, 0, 0, .02f, .016f, .015f));
            truth(!guide.Update(false, .5f, .5f, .5f, .016f, .015f, tracked: false));
            truth(!guide.Captured && guide.WaitingForWithdrawal);
            truth(!guide.Update(false, float.NaN, 0, .08f, .016f, .015f));
            truth(guide.WaitingForWithdrawal);
            truth(!guide.Update(true, 0, 0, .003f, .016f, .015f));
            truth(guide.WaitingForWithdrawal);
            truth(!guide.Update(false, 0, 0, .08f, .016f, .015f, tracked: true));
            truth(!guide.WaitingForWithdrawal);
        });
    }
}
