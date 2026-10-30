using NOVR.MotionControls;

var passed = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    Console.WriteLine("PASS " + message); passed++;
}
bool Near(float a, float b, float tolerance = .00001f) => Math.Abs(a-b) <= tolerance;

Check(HandCalibrationMath.Correction(0, .15f) == 0, "Default per-hand correction preserves legacy fit");
Check(Near(HandCalibrationMath.Correction(.005f, .15f), .005f), "Position correction preserves a 5 mm adjustment");
Check(Near(HandCalibrationMath.Correction(-.1f, .15f), -.1f), "Signed correction does not mirror the independently fitted hand");
Check(Near(HandCalibrationMath.Correction(3, .15f), .15f), "Accidental large position correction is bounded");
Check(Near(HandCalibrationMath.Correction(-3, .15f), -.15f), "Negative position correction is bounded");
Check(HandCalibrationMath.Correction(300, 180) == 180, "Rotation correction is bounded");
Check(HandCalibrationMath.Correction(float.NaN, .15f) == 0, "Malformed NaN correction safely retains baseline");
Check(HandCalibrationMath.Correction(float.PositiveInfinity, .15f) == 0, "Infinite correction safely retains baseline");
Check(HandCalibrationMath.Correction(.05f, float.NaN) == 0, "Invalid correction limit is rejected");

var normal = ArmReachMath.Evaluate(.3f, .27f, .45f);
Check(normal.Valid && normal.Opacity == 1 && normal.Scale == 1 && Near(normal.Distance, .45f), "Normal reach stays opaque and does not stretch");
var oldCutoff = ArmReachMath.Evaluate(.3f, .27f, .568f);
Check(oldCutoff.Valid && oldCutoff.Opacity > .8f, "Sleeve remains visible at the former hard reach cutoff");
var extended = ArmReachMath.Evaluate(.3f, .27f, .6f);
Check(extended.Valid && extended.Opacity > 0 && extended.Opacity < 1 && extended.Scale > 1, "A small extra reach keeps a connected cuff with partial fade");
Check(Near(extended.Distance, .6f), "Fading sleeve follows the wrist while within its extended range");
var outer = ArmReachMath.Evaluate(.3f, .27f, .625f);
Check(outer.Valid && Near(outer.Opacity, 0) && outer.Scale <= 1.10001f, "Outer reach is invisible before any further sleeve stretching");
var tooFar = ArmReachMath.Evaluate(.3f, .27f, 9);
Check(tooFar.Valid && tooFar.Opacity == 0 && tooFar.Scale <= 1.10001f && tooFar.Distance <= .62501f, "Unreachable controller never stretches a sleeve across the cockpit");
Check(ArmReachMath.Evaluate(.3f,.27f,float.NaN).Valid == false, "NaN tracking distance cannot enter the IK solver");
Check(ArmReachMath.Evaluate(.3f,.27f,float.PositiveInfinity).Valid == false, "Infinite tracking distance cannot enter the IK solver");
Check(ArmReachMath.Evaluate(.01f,.27f,.2f).Valid == false, "Missing or unusably short arm bones are rejected");
Check(ArmReachMath.Evaluate(float.MaxValue,float.MaxValue,1).Valid == false, "Overflowing rig dimensions are rejected");
Check(ArmReachMath.Evaluate(.3f,.27f,0).Valid == false, "Zero shoulder-to-wrist direction is rejected");
Check(ArmReachMath.Evaluate(.3f,.27f,.032f).Opacity <= .00001f, "Impossible inner reach is faded rather than snapping into an invalid triangle");
Check(ArmReachMath.Evaluate(.3f,.27f,.055f).Opacity > .9f, "Sleeve recovers smoothly outside the small inner reach band");
var previous = 1f;
var continuous = true; var monotonic = true; var finite = true; var triangle = true;
for (var i = 0; i <= 2000; i++)
{
    var distance = .54f + i*.00005f;
    var reach = ArmReachMath.Evaluate(.3f,.27f,distance);
    continuous &= Math.Abs(reach.Opacity-previous) < .002f;
    monotonic &= reach.Opacity <= previous + .000001f;
    finite &= float.IsFinite(reach.Opacity) && float.IsFinite(reach.Scale) && reach.Opacity >= 0 && reach.Opacity <= 1;
    triangle &= reach.Distance < (.3f + .27f)*reach.Scale && reach.Distance > Math.Abs(.3f-.27f)*reach.Scale;
    previous = reach.Opacity;
}
Check(continuous, "Reach fade stays continuous across 2,001 boundary samples");
Check(monotonic, "Increasing reach never makes a fading sleeve reappear");
Check(finite, "Reach sweep returns finite, bounded opacity and scale");
Check(triangle, "Reach sweep retains a solvable two-bone triangle");
var large = ArmReachMath.Evaluate(.6f,.5f,1.13f);
Check(large.Scale < 1.06f, "Large pilot rigs still limit added reach to six centimeters");
Console.WriteLine($"{passed} hand comfort checks passed.");
