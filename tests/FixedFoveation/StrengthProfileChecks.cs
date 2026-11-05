using NOVR.Rendering;

internal static class StrengthProfileChecks
{
    internal static void Run(Action<bool, string> check)
    {
        foreach (var (level, expectedInner, expectedOuter) in new[]
        {
            (FixedFoveationLevel.Mild, .70f, .95f),
            (FixedFoveationLevel.Balanced, .55f, .85f),
            (FixedFoveationLevel.Strong, .40f, .75f)
        })
        {
            FixedFoveationPolicy.Radii(level, out var inner, out var outer);
            check(inner == expectedInner && outer == expectedOuter,
                "Adding test strengths preserves the existing " + level + " protected regions.");
        }
        var previousInner = 1f;
        var previousOuter = 1f;
        foreach (var level in Enum.GetValues<FixedFoveationLevel>())
        {
            FixedFoveationPolicy.Radii(level, out var inner, out var outer);
            check(inner > 0 && outer > inner && outer <= 1, "Every profile has a nonempty ordered full-rate/transition region.");
            if (level != FixedFoveationLevel.Off)
            {
                check(inner < previousInner && outer < previousOuter,
                    "Each successive strength visibly reduces both protected-region radii.");
                previousInner = inner;
                previousOuter = outer;
            }
            foreach (var eyeTracking in new[] { false, true })
                foreach (var tracked in new[] { false, true })
                {
                    var effective = FixedFoveationPolicy.WithGazeFallback(level, eyeTracking, tracked);
                    var expected = eyeTracking && !tracked && level > FixedFoveationLevel.Strong ? FixedFoveationLevel.Strong : level;
                    check(effective == expected,
                        "Only aggressive eye-tracked strengths reduce to Strong when current gaze is unavailable.");
                }
        }
        FixedFoveationPolicy.Radii(FixedFoveationLevel.Strong, out var strongInner, out _);
        FixedFoveationPolicy.Radii(FixedFoveationLevel.Extreme, out var extremeInner, out var extremeOuter);
        FixedFoveationPolicy.Radii(FixedFoveationLevel.Maximum, out var maximumInner, out var maximumOuter);
        check(extremeInner * extremeInner < strongInner * strongInner * .60f && extremeOuter <= .60f,
            "Extreme materially shrinks the full-rate area and transition boundary compared with Strong.");
        check(maximumInner * maximumInner < strongInner * strongInner * .25f && maximumOuter <= .40f,
            "Maximum retains under one quarter of Strong's full-rate area for an obvious test difference.");
        FixedFoveationPolicy.Radii(FixedFoveationLevel.Off, out var offInner, out var offOuter);
        check(offInner == .70f && offOuter == .95f, "Off diagnostic packets retain a valid conservative profile while VRS stays disabled.");
    }
}
