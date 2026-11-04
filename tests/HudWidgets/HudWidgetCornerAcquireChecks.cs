using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class HudWidgetCornerAcquireChecks
{
    internal static void Run(Action<bool, string> check)
    {
        const float w = .12f, h = .08f;
        for (var first = 0; first < 4; first++)
        {
            var fx = (first & 1) == 0 ? -w : w;
            var fy = (first & 2) == 0 ? -h : h;
            Pair(first, fx, fy, 0, -fx, -fy, 0, w, h, first ^ 3, first, "Every opposite diagonal supports either hand arriving first");
            Pair(first, fx, fy, 0, -fx, -fy, .1199f, w, h, first ^ 3, first, "Held corner provides twelve-centimeter depth tolerance");
            Reject(first, fx, fy, 0, -fx, -fy, .1201f, w, h, "Second grip beyond assisted depth cannot acquire");
            check(HudWidgetGestureMath.Corner(fx, fy, .0899f, w, h) == first, "General corner accepts nine-centimeter depth margin");
            check(HudWidgetGestureMath.Corner(fx, fy, .0901f, w, h) == -1, "General corner rejects outside nine-centimeter depth margin");
            check(HudWidgetGestureMath.Corner(fx, fy, .11f, w, h) == -1, "Second-hand assistance does not enlarge ordinary first-grab capture");
            Pair(first, fx, fy, 0, -fx, -fy, .11f, w, h, first ^ 3, first, "Existing diagonal wins in expanded second-hand capture");
            for (var second = 0; second < 4; second++)
            {
                if (second == (first ^ 3))
                    continue;
                var x = (second & 1) == 0 ? -.06f : .06f;
                var y = (second & 2) == 0 ? -.04f : .04f;
                Reject(first, fx * .5f, fy * .5f, 0, x, y, 0, .06f, .04f, "Same or adjacent corners cannot steal resize even on a small widget");
            }
        }
        Pair(-1, -w, 0, 0, w, h, 0, w, h, 3, 0, "Left-edge body hold promotes to bottom-left for upper-right second grip");
        Pair(-1, -w, 0, 0, w, -h, 0, w, h, 1, 2, "Left-edge body hold supports the other diagonal");
        Pair(-1, w, 0, 0, -w, h, 0, w, h, 2, 1, "Right-edge body hold supports mirrored diagonal");
        Pair(-1, 0, h, 0, w, -h, 0, w, h, 1, 2, "Upper-edge body hold chooses opposite lower corner");
        Pair(-1, 0, -h, 0, -w, h, 0, w, h, 2, 1, "Lower-edge body hold chooses opposite upper corner");
        Pair(2, -w, 0, 0, w, h, 0, w, h, 3, 0, "Ambiguous edge-center first corner may change vertical identity");
        Pair(3, 0, h, 0, w, -h, 0, w, h, 1, 2, "Ambiguous edge-center first corner may change horizontal identity");
        Pair(2, -w, h * .349f, 0, w, h, 0, w, h, 3, 0, "Small midline drift still allows deliberate diagonal choice");
        Reject(2, -w, h * .351f, 0, w, h, 0, w, h, "Established top corner cannot be remapped across the midline band");
        Reject(-1, 0, 0, 0, w, h, 0, w, h, "Central body hold cannot become a two-corner resize");
        Reject(-1, w * .449f, h * .449f, 0, -w, -h, 0, w, h, "Inside normalized central rectangle remains a body hold");
        Pair(-1, -w * .451f, 0, 0, w, h, 0, w, h, 3, 0, "Outside central rectangle near a real edge can promote");
        Reject(0, -w, -h, 0, 0, 0, 0, w, h, "Second grip in middle cannot acquire a corner");
        Reject(0, -w, -h, 0, w * .449f, h * .449f, 0, w, h, "Second central-body contact is excluded despite large assisted radius");
        Reject(0, -.012f, -.008f, 0, .012f, .008f, 0, .03f, .02f, "Physically overlapping hands cannot acquire even in opposite quadrants");
        Reject(0, -.0249f, -.001f, 0, .0249f, .001f, 0, .025f, .02f, "Pair just below five-centimeter separation is rejected");
        Pair(0, -.0251f, -.001f, 0, .0251f, .001f, 0, .025f, .02f, 3, 0, "Pair just above five-centimeter separation can acquire");
        Reject(0, -w, -h, 0, -w * .251f, h, 0, w, h, "Second hand clearly across desired corner's horizontal side cannot acquire");
        Reject(0, -w, -h, 0, -w * .249f, .04f, 0, .04f, .04f, "Far outside a smaller widget's desired corner cannot borrow the larger widget's tolerance");
        Reject(0, -w, -h, .121f, w, h, 0, w, h, "First grip must also remain near its corner in depth");
        foreach (var invalid in new[] { int.MinValue, -2, 4, int.MaxValue })
            Reject(invalid, -w, -h, 0, w, h, 0, w, h, "Invalid first-corner identity rejects and clears promotion");
        foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Reject(0, invalid, -h, 0, w, h, 0, w, h, "Invalid first x rejected");
            Reject(0, -w, invalid, 0, w, h, 0, w, h, "Invalid first y rejected");
            Reject(0, -w, -h, invalid, w, h, 0, w, h, "Invalid first depth rejected");
            Reject(0, -w, -h, 0, invalid, h, 0, w, h, "Invalid second x rejected");
            Reject(0, -w, -h, 0, w, invalid, 0, w, h, "Invalid second y rejected");
            Reject(0, -w, -h, 0, w, h, invalid, w, h, "Invalid second depth rejected");
            Reject(0, -w, -h, 0, w, h, 0, invalid, h, "Invalid width rejected");
            Reject(0, -w, -h, 0, w, h, 0, w, invalid, "Invalid height rejected");
        }
        foreach (var invalid in new[] { 0f, -.01f })
        {
            Reject(0, -w, -h, 0, w, h, 0, invalid, h, "Nonpositive width rejected");
            Reject(0, -w, -h, 0, w, h, 0, w, invalid, "Nonpositive height rejected");
        }
        foreach (var aspect in new[] { .5f, 1f, 2f })
        {
            var pose = new HudWidgetPose(new Vector3(.14f, -.21f, .46f), new Vector3(17, -32, 0), .8f, .7f, .23f, false, aspect);
            var second = HudWidgetGestureMath.PairCorner(-1, -w, 0, 0, w, h, 0, w, h, out var first);
            check(first == 0 && second == 3, "Edge promotion establishes opposite corners before starting actual two-hand gesture");
            var rotation = Quaternion.Euler(pose.Euler.x, pose.Euler.y, 0);
            var left = pose.Position + rotation * new Vector3(-w, 0, 0);
            var right = pose.Position + rotation * new Vector3(w, h, 0);
            var leftRotation = Quaternion.Euler(-8, 153, 13);
            var rightRotation = Quaternion.Euler(14, -117, -37);
            var gesture = new HudWidgetTwoHandGesture(pose, left, right, leftRotation, rightRotation, true);
            var joined = gesture.Move(pose, left, right, leftRotation, rightRotation);
            check((joined.Position - pose.Position).magnitude < .00002f, "Promoted edge grip preserves displayed position at second-hand acquisition");
            check((joined.Euler - pose.Euler).magnitude < .00002f, "Promoted edge grip preserves displayed pitch and yaw");
            check(Math.Abs(joined.Scale - pose.Scale) < .00002f && Math.Abs(joined.Aspect - pose.Aspect) < .00002f, "Promoted edge grip preserves map width/height without acquisition jump");
            check(joined.Visible == pose.Visible && joined.Opacity == pose.Opacity && joined.Background == pose.Background, "Corner promotion cannot change unrelated appearance");
        }
        void Pair(int first, float fx, float fy, float fz, float x, float y, float z, float halfWidth, float halfHeight, int expected, int promoted, string why)
        {
            var result = HudWidgetGestureMath.PairCorner(first, fx, fy, fz, x, y, z, halfWidth, halfHeight, out var actualFirst);
            check(result == expected && actualFirst == promoted, why);
            if (result >= 0)
                check(HudWidgetGestureMath.Opposite(actualFirst, result), "Accepted pair always contains valid opposite corner identities");
        }
        void Reject(int first, float fx, float fy, float fz, float x, float y, float z, float halfWidth, float halfHeight, string why) =>
            Pair(first, fx, fy, fz, x, y, z, halfWidth, halfHeight, -1, -1, why);
    }
}
