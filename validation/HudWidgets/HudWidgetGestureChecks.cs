using NOVR.VrUi.SpecialBehavior;

internal static class HudWidgetGestureChecks
{
    internal static void Run(Action<bool, string> check)
    {
        for (var corner = 0; corner < 4; corner++)
        {
            var x = (corner & 1) == 0 ? -.12f : .12f;
            var y = (corner & 2) == 0 ? -.08f : .08f;
            check(HudWidgetGestureMath.Corner(x, y, 0, .12f, .08f) == corner, "Each physical corner has a stable identity");
            check(HudWidgetGestureMath.Corner(x, y, .0699f, .12f, .08f) == corner, "Corner capture accepts nearby fingertip depth");
            check(HudWidgetGestureMath.Corner(x, y, .0701f, .12f, .08f) == -1, "Corner capture rejects outside its seven-centimeter radius");
            for (var other = 0; other < 4; other++)
                check(HudWidgetGestureMath.Opposite(corner, other) == ((corner ^ other) == 3),
                    "Only opposite corners can form a resize pair; adjacent/same corners cannot");
        }
        check(HudWidgetGestureMath.Corner(.12f + .049f, .08f + .049f, 0, .12f, .08f) == 3, "Corner tolerance is radial in the display plane");
        check(HudWidgetGestureMath.Corner(.12f + .05f, .08f + .05f, 0, .12f, .08f) == -1, "Two near-axis offsets outside radius do not capture");
        check(HudWidgetGestureMath.Corner(0, 0, 0, .12f, .08f) == -1, "Grabbing the middle does not masquerade as a resize corner");
        foreach (var invalid in new[] { -1, -2, 4, 7, int.MaxValue })
        {
            check(!HudWidgetGestureMath.Opposite(invalid, invalid ^ 3), "Out-of-range corner identities cannot resize");
            check(!HudWidgetGestureMath.Opposite(0, invalid), "Missing/invalid second corner cannot resize");
        }
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            check(HudWidgetGestureMath.Corner(invalid, .08f, 0, .12f, .08f) == -1, "Invalid corner x rejected");
            check(HudWidgetGestureMath.Corner(.12f, invalid, 0, .12f, .08f) == -1, "Invalid corner y rejected");
            check(HudWidgetGestureMath.Corner(.12f, .08f, invalid, .12f, .08f) == -1, "Invalid corner depth rejected");
            check(HudWidgetGestureMath.Corner(.12f, .08f, 0, invalid, .08f) == -1, "Invalid width rejected");
            check(HudWidgetGestureMath.Corner(.12f, .08f, 0, .12f, invalid) == -1, "Invalid height rejected");
            Near(HudWidgetGestureMath.Resize(.65f, invalid, .2f, .3f, .4f, .9f), .9f, "Invalid start x holds last scale");
            Near(HudWidgetGestureMath.Resize(.65f, .2f, invalid, .3f, .4f, .9f), .9f, "Invalid start y holds last scale");
            Near(HudWidgetGestureMath.Resize(invalid, .2f, .2f, .3f, .4f, .9f), .9f, "Invalid initial scale holds last scale");
            Near(HudWidgetGestureMath.Resize(.65f, .2f, .2f, invalid, .4f, .9f), .9f, "Invalid current x holds last scale");
            Near(HudWidgetGestureMath.Resize(.65f, .2f, .2f, .3f, invalid, .9f), .9f, "Invalid current y holds last scale");
            var recovered = HudWidgetGestureMath.Resize(.65f, .2f, .2f, float.NaN, .4f, invalid);
            check(float.IsFinite(recovered) && recovered >= .35f && recovered <= 3, "Invalid fallback scale still produces bounded finite output");
        }
        foreach (var invalid in new[] { 0f, -.01f })
        {
            check(HudWidgetGestureMath.Corner(0, .08f, 0, invalid, .08f) == -1, "Nonpositive width cannot form a corner");
            check(HudWidgetGestureMath.Corner(.12f, 0, 0, .12f, invalid) == -1, "Nonpositive height cannot form a corner");
        }
        Near(HudWidgetGestureMath.Resize(.65f, .2f, .1f, .2f, .1f, 2f), .65f, "Starting second hand produces no size jump");
        Near(HudWidgetGestureMath.Resize(.65f, .2f, .1f, .3f, .15f, .65f), .975f, "Uniform diagonal expansion uses fixed-plane span");
        Near(HudWidgetGestureMath.Resize(1.2f, .2f, .1f, .1f, .05f, 1.2f), .6f, "Uniform diagonal contraction uses fixed-plane span");
        Near(HudWidgetGestureMath.Resize(.8f, .2f, .1f, -.2f, -.1f, 1.3f), 1.3f, "Reversed hands cannot invert or shrink the display");
        Near(HudWidgetGestureMath.Resize(.8f, .2f, .1f, -.1f, .2f, 1.3f), 1.3f, "Crossed projected span holds prior scale");
        Near(HudWidgetGestureMath.Resize(.8f, .2f, .1f, 0, 0, 1.3f), 1.3f, "Coincident hands hold prior scale");
        Near(HudWidgetGestureMath.Resize(.8f, .049f, 0, .1f, 0, 1.3f), 1.3f, "Too-small initial span cannot amplify tracking noise");
        Near(HudWidgetGestureMath.Resize(.8f, .0501f, 0, .1002f, 0, 1.3f), 1.6f, "Usable initial span supports scaling");
        Near(HudWidgetGestureMath.Resize(.8f, .2f, .1f, 20, 10, .8f), 3, "Upper size bound");
        Near(HudWidgetGestureMath.Resize(.8f, .2f, .1f, .002f, .001f, .8f), .35f, "Lower size bound");
        var overflow = HudWidgetGestureMath.Resize(.65f, float.MaxValue, float.MaxValue, 1, 1, .9f);
        check(float.IsFinite(overflow) && overflow >= .35f && overflow <= 3, "Extreme finite span cannot overflow output");
        Near(HudWidgetGestureMath.Resize(1.7f, .3f, .4f, .3f, .4f, .65f), 1.7f, "Regrab begins from latest captured scale, not old gesture baseline");

        // Current rendered footprints. Test every grab corner, not only the
        // panel center: both hands must be able to reach the resize affordance.
        var footprints = new Dictionary<string, (float width, float height)>
        {
            ["weapons"] = (420, 170), ["countermeasures"] = (260, 135), ["power"] = (420, 80),
            ["threats"] = (460, 190), ["ew"] = (260, 260), ["feed"] = (460, 270), ["map"] = (300, 300)
        };
        foreach (var pair in HudWidgetProfiles.FactoryDefaults)
        {
            var pose = pair.Value; var size = footprints[pair.Key];
            for (var corner = 0; corner < 4; corner++)
            {
                var x = pose.Position.x + ((corner & 1) == 0 ? -1 : 1) * size.width * .0007f * pose.Scale * .5f;
                var y = pose.Position.y + ((corner & 2) == 0 ? -1 : 1) * size.height * .0007f * pose.Scale * .5f;
                var z = pose.Position.z;
                var nearShoulderX = x < 0 ? -.16f : .16f;
                var distance = Math.Sqrt((x - nearShoulderX) * (x - nearShoulderX) + (y + .18f) * (y + .18f) + z * z);
                check(distance <= .60, "Default " + pair.Key + " corner is within natural shoulder reach");
            }
            for (var diagonal = 0; diagonal < 2; diagonal++)
            {
                var halfWidth = size.width * .0007f * pose.Scale * .5f;
                var halfHeight = size.height * .0007f * pose.Scale * .5f;
                var leftX = pose.Position.x - halfWidth + .16f;
                var rightX = pose.Position.x + halfWidth - .16f;
                var leftY = pose.Position.y + .18f + (diagonal == 0 ? -halfHeight : halfHeight);
                var rightY = pose.Position.y + .18f + (diagonal == 0 ? halfHeight : -halfHeight);
                var z = pose.Position.z;
                check(Math.Sqrt(leftX * leftX + leftY * leftY + z * z) <= .60 &&
                    Math.Sqrt(rightX * rightX + rightY * rightY + z * z) <= .60,
                    "Default " + pair.Key + " opposite-corner pair is reachable with left and right hands together");
            }
        }
        void Near(float actual, float expected, string label) => check(Math.Abs(actual - expected) < .00001f, label);
    }
}
