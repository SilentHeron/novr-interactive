using NOVR.MotionControls;
using V = NOVR.MotionControls.ArmPoseMath.Vector;

var passed = 0;
void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
    passed++;
    Console.WriteLine("PASS " + message);
}
float Angle(V a, V b) => (float)(Math.Acos(Math.Clamp(V.Dot(a.Unit, b.Unit), -1, 1)) * 180 / Math.PI);
V Mirror(V v) => new(-v.X, v.Y, v.Z);
V Rotate(V v, V axis, float degrees)
{
    var radians = degrees * Math.PI / 180;
    return v * (float)Math.Cos(radians) + V.Cross(axis, v) * (float)Math.Sin(radians) + axis * (V.Dot(axis, v) * (1 - (float)Math.Cos(radians)));
}

var direction = new V(-.15f, -.28f, .35f).Unit;
var neutral = new V(-.35f, -.5f, -.2f).Unit.OnPlane(direction).Unit;
var free = new ArmPoseMath();
Check(Angle(free.Bend(true, direction, neutral, false), neutral) < .05f, "Aligned free-hand cuff preserves the natural elbow plane");
Check(V.Dot(free.Bend(true, direction, neutral * -1, false), neutral) > .999f, "An inverted free wrist cannot pull its elbow through the anatomical plane");
var freeSweep = true;
var maxFreeStep = 0f;
var maxElbowSwivel = 0f;
V last = default;
for (var i = 0; i <= 3600; i++)
{
    var cuff = Rotate(neutral, direction, i * .1f);
    var bend = free.Bend(true, direction, cuff, false);
    freeSweep &= bend.Finite && Math.Abs(bend.Length - 1) < .0001f && Math.Abs(V.Dot(bend, direction)) < .0001f && V.Dot(bend, neutral) > .2f;
    maxElbowSwivel = Math.Max(maxElbowSwivel, Angle(neutral, bend));
    if (i > 0)
        maxFreeStep = Math.Max(maxFreeStep, Angle(last, bend));
    last = bend;
}
Check(freeSweep, "3,601 wrist orientations retain a finite perpendicular elbow outside the torso-facing hemisphere");
Check(maxFreeStep < 1, "Free wrist rotation has no elbow reversal or branch-cut snap");
Check(maxElbowSwivel < 22, $"Wrist-only flexion cannot swing the elbow more than 22 degrees (measured {maxElbowSwivel:F2})");

foreach (var collective in new[] { false, true })
{
    var solver = new ArmPoseMath();
    last = default;
    var maxStep = 0f;
    var valid = true;
    for (var i = 0; i <= 2000; i++)
    {
        // Cross the exact degeneracy where cuff points down the arm axis.
        var cuff = (direction * -1 + neutral * ((i - 1000) * .0005f)).Unit;
        var bend = solver.Bend(true, direction, cuff, collective);
        valid &= bend.Finite && Math.Abs(V.Dot(bend, direction)) < .0001f && Math.Abs(bend.Length - 1) < .0001f;
        if (i > 0)
            maxStep = Math.Max(maxStep, Angle(last, bend));
        last = bend;
    }
    Check(valid, $"{(collective ? "Collective" : "Free")} cuff-axis crossing never produces a zero or invalid elbow");
    Check(maxStep < 8, $"{(collective ? "Collective" : "Free")} cuff-axis crossing is continuous (largest step {maxStep:F3} degrees)");
}

// A collective grip must never override the anatomical side, even if a
// relocated handle or wrist roll makes its authored cuff oppose that side.
var mirrorGood = true;
var collectiveSweep = true;
var maxCollectiveSwivel = 0f;
foreach (var reach in new[] { new V(-.15f, -.28f, .35f), new V(-.2f, -.16f, .2f), new V(-.2f, -.4f, .1f), new V(-.05f, -.05f, .3f) })
{
    var d = reach.Unit;
    var collectivePole = new V(-.35f, -.5f, -.2f).Unit.OnPlane(d).Unit;
    var leftSolver = new ArmPoseMath();
    var rightSolver = new ArmPoseMath();
    V previous = default;
    for (var i = 0; i <= 3600; i++)
    {
        var cuff = Rotate(collectivePole, d, i * .1f);
        var bend = leftSolver.Bend(true, d, cuff, true);
        var opposite = rightSolver.Bend(false, Mirror(d), Mirror(cuff), true);
        mirrorGood &= (Mirror(bend) - opposite).Length < .0001f;
        collectiveSweep &= bend.Finite && Math.Abs(bend.Length - 1) < .0001f && Math.Abs(V.Dot(bend, d)) < .0001f &&
            V.Dot(bend, collectivePole) > .819f && (i == 0 || Angle(previous, bend) < 1);
        maxCollectiveSwivel = Math.Max(maxCollectiveSwivel, Angle(bend, collectivePole));
        previous = bend;
    }
}
Check(mirrorGood, "Left/right collective geometry mirrors without handed elbow bias");
Check(collectiveSweep && maxCollectiveSwivel < 35.01f, $"14,404 collective cuff samples retain the anatomical elbow without reversal (maximum guidance {maxCollectiveSwivel:F2} degrees)");
var hinge = ArmPoseMath.HingeDorsal(true, direction, neutral);
Check(hinge.Finite && Math.Abs(hinge.Length - 1) < .00001f && Math.Abs(V.Dot(hinge, direction)) < .00001f &&
    Math.Abs(V.Dot(hinge, neutral)) < .00001f, "Shared arm reference is a finite normal to the solved elbow plane");
Check((Mirror(hinge) - ArmPoseMath.HingeDorsal(false, Mirror(direction), Mirror(neutral))).Length < .00001f,
    "The shared elbow frame mirrors between hands without reversing wrist-roll convention");

var transitionSolver = new ArmPoseMath();
var raisedCuff = Rotate(neutral, direction, 70);
var freeTarget = new ArmPoseMath().Bend(true, direction, raisedCuff, false);
var collectiveTarget = new ArmPoseMath().Bend(true, direction, raisedCuff, true);
last = transitionSolver.Bend(true, direction, raisedCuff, true);
var transitionGood = true;
var maxTransitionStep = 0f;
for (var i = 0; i <= 100; i++)
{
    var progress = i / 100f;
    progress = progress * progress * (3 - 2 * progress);
    var bend = transitionSolver.Bend(true, direction, raisedCuff, false, progress);
    maxTransitionStep = Math.Max(maxTransitionStep, Angle(last, bend));
    last = bend;
    transitionGood &= bend.Finite && Math.Abs(V.Dot(bend, direction)) < .0001f;
}
Check(transitionGood && maxTransitionStep < 3, "Collective release shares the finite glove blend without a one-frame elbow reversal");
Check(Angle(last, freeTarget) < .05f, "Completed release reaches the exact free elbow plane without residual smoothing lag");
maxTransitionStep = 0;
for (var i = 0; i <= 100; i++)
{
    var progress = i / 100f;
    progress = progress * progress * (3 - 2 * progress);
    var bend = transitionSolver.Bend(true, direction, raisedCuff, true, progress);
    maxTransitionStep = Math.Max(maxTransitionStep, Angle(last, bend));
    last = bend;
}
Check(maxTransitionStep < 3 && Angle(last, collectiveTarget) < .05f, "Reacquiring the collective smoothly restores its anatomically bounded elbow plane");

var poleSolver = new ArmPoseMath();
var pole = new V(-.35f, -.5f, -.2f).Unit;
var tangent = V.Cross(pole, new V(0, 0, 1)).Unit;
last = default;
var maxPoleStep = 0f;
var poleGood = true;
for (var i = 0; i <= 2000; i++)
{
    var reachDirection = (pole + tangent * ((i - 1000) * .0005f)).Unit;
    var bend = poleSolver.Bend(true, reachDirection, reachDirection * -1, false);
    poleGood &= bend.Finite && Math.Abs(bend.Length - 1) < .0001f && Math.Abs(V.Dot(bend, reachDirection)) < .0001f;
    if (i > 0)
        maxPoleStep = Math.Max(maxPoleStep, Angle(last, bend));
    last = bend;
}
Check(poleGood && maxPoleStep < 8, "Passing the anatomical-pole singularity preserves a continuous usable elbow plane");

var rollSolver = new ArmPoseMath();
var axis = new V(0, 0, 1);
var dorsal = new V(0, 1, 0);
var previousRoll = 0f;
var rollGood = true;
var neutralAfterTurn = true;
for (var i = 0; i <= 10800; i++)
{
    var angle = i * .1f;
    var roll = rollSolver.ForearmRoll(axis, dorsal, Rotate(dorsal, axis, angle));
    rollGood &= float.IsFinite(roll) && Math.Abs(roll) <= 180.001f && (i == 0 || Math.Abs(ArmPoseMath.DeltaDegrees(previousRoll, roll)) < .15f);
    if (i % 3600 == 0)
        neutralAfterTurn &= Math.Abs(roll) < .001f;
    previousRoll = roll;
}
Check(rollGood, "Forearm pronation stays continuous through three full turns without a partial-twist wraparound");
Check(neutralAfterTurn, "Every complete wrist turn returns the sleeve to neutral with no accumulated winding");
var reversingGood = true;
for (var i = 0; i <= 7200; i++)
{
    var angle = 90 * (float)Math.Sin(i * Math.PI / 1200);
    var roll = rollSolver.ForearmRoll(axis, dorsal, Rotate(dorsal, axis, angle));
    reversingGood &= Math.Abs(roll - angle * .8f) < .001f;
}
Check(reversingGood, "Ordinary back-and-forth pronation has no twist hysteresis or residual lag");
rollSolver.Reset();
Check(Math.Abs(rollSolver.ForearmRoll(axis, dorsal, Rotate(dorsal, axis, 90)) - 72) < .001f, "Forearm shares 80% of normal wrist roll while the hand completes orientation");
var stable = rollSolver.ForearmRoll(axis, dorsal, axis);
Check(Math.Abs(stable - 72) < .001f, "A dorsal/forearm-axis singularity retains the last useful twist");
Check(Math.Abs(rollSolver.ForearmRoll(axis, dorsal, new V(float.NaN, 0, 0)) - stable) < .001f, "Invalid visual orientation cannot poison the saved wrist twist");
rollSolver.Reset();
Check(rollSolver.ForearmRoll(axis, dorsal, axis) == 0, "Reset clears previous aircraft/hidden-hand twist history");

var reachGood = true;
var maxVelocityChange = 0f;
float? previousHeight = null;
float? previousVelocity = null;
for (var i = 0; i <= 6800; i++)
{
    var distance = .53f + i * .000025f;
    var reach = ArmReachMath.Evaluate(.3f, .27f, distance);
    var ok = ArmReachMath.Triangle(.3f * reach.Scale, .27f * reach.Scale, reach.Distance, out var along, out var height);
    reachGood &= ok && Math.Abs(Math.Sqrt(along * along + height * height) - .3f * reach.Scale) < .000002f &&
        Math.Abs(Math.Sqrt((reach.Distance - along) * (reach.Distance - along) + height * height) - .27f * reach.Scale) < .000002f;
    if (distance > .553f && distance < .582f && previousHeight.HasValue)
    {
        var velocity = (height - previousHeight.Value) / .000025f;
        if (previousVelocity.HasValue)
            maxVelocityChange = Math.Max(maxVelocityChange, Math.Abs(velocity - previousVelocity.Value));
        previousVelocity = velocity;
    }
    previousHeight = height;
}
Check(reachGood, "6,801 reaches preserve both bone lengths through rigid reach, smooth extension and fade");
Check(maxVelocityChange < .05f, "Elbow velocity changes smoothly at the sleeve-extension threshold");
Check(!ArmReachMath.Triangle(.3f, .27f, float.NaN, out _, out _) && !ArmReachMath.Triangle(.3f, .27f, .7f, out _, out _), "Invalid and impossible IK triangles are rejected before transforms change");
// Torso translation is independent of the user's exact seat adjustment.
var torsoContinuous = true;
var torsoPrevious = 0f;
var torsoMirror = true;
for (var i = 0; i <= 1000; i++)
{
    var displacement = new V(i * .001f, .03f, -.025f);
    var lean = PilotTorsoMath.Lean(displacement);
    torsoContinuous &= lean.Finite && lean.Length <= .120001f && lean.X >= torsoPrevious && lean.X - torsoPrevious < .00036f;
    torsoMirror &= (Mirror(lean) - PilotTorsoMath.Lean(Mirror(displacement))).Length < .000001f;
    torsoPrevious = lean.X;
}
Check(torsoContinuous, "Head lean moves shoulders smoothly with bounded travel instead of dragging the whole chest");
Check(torsoMirror, "Torso follow is identical for left/right lean");
Check(PilotTorsoMath.Lean(new V(0, 0, 0)).Length == 0 && PilotTorsoMath.Lean(new V(float.NaN, 0, 0)).Length == 0, "Recenter/invalid head lean cannot displace the torso");
Check(PilotTorsoMath.Lean(new V(.1f, 0, 0)).X > .03f && PilotTorsoMath.Lean(new V(.1f, 0, 0)).X < .035f, "Ordinary head movement has modest shoulder follow rather than a fixed body or head-glued shoulders");
Console.WriteLine($"{passed} arm pose checks passed.");
