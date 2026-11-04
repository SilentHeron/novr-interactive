using NOVR.MotionControls;

var checks = 0;
void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
HandOptionWheelGesture Open(float x = 0, float y = 0, float trigger = 0)
{
    var g = new HandOptionWheelGesture();
    Check(g.Update(true, false, 0, 0, 0, 8) == HandOptionWheelEvent.None, "released sample arms click");
    Check(g.Update(true, true, trigger, x, y, 8) == HandOptionWheelEvent.Open && g.IsOpen, "fresh click opens");
    return g;
}
void Highlight(HandOptionWheelGesture g, float x = 1, float y = 0, int expected = 2)
{
    Check(g.Update(true, false, 0, x, y, 8) == HandOptionWheelEvent.None, "direction never activates immediately");
    Check(g.IsOpen && g.Hovered == expected, "direction highlights native clockwise sector");
}

var g = Open();
Highlight(g);
Check(g.Update(true, false, 0, .3f, 0, 8) == HandOptionWheelEvent.None && g.Hovered == 2, "deadzone hysteresis retains selection");
Check(g.Update(true, false, 0, .21f, 0, 8) == HandOptionWheelEvent.Commit && g.Committed == 2 && !g.IsOpen, "return to centre commits exactly once");
Check(g.Update(true, false, 0, 0, 0, 8) == HandOptionWheelEvent.None && g.Committed == -1, "centred holding never repeats");

g = Open();
Highlight(g);
Check(g.Update(true, true, 0, 0, 0, 8) == HandOptionWheelEvent.Cancel && g.Committed == -1, "click on centre-return cancels before commit");
Check(g.Update(true, true, 0, 0, 0, 8) == HandOptionWheelEvent.None, "held cancel click cannot reopen");
g.Update(true, false, 0, 0, 0, 8);
Check(g.Update(true, true, 0, 0, 0, 8) == HandOptionWheelEvent.Open, "release plus fresh click reopens");

g = Open();
Highlight(g);
Check(g.Update(true, true, 0, 1, 0, 8) == HandOptionWheelEvent.Cancel && g.Committed == -1, "click while hovering cancels without selection");
g = Open();
g.Update(true, false, 0, 0, 0, 8);
Check(g.Update(true, true, 0, 0, 0, 8) == HandOptionWheelEvent.Cancel, "click empty wheel also closes");

foreach (var trigger in new[] { 0f, 1f })
{
    g = Open(1, 0, trigger);
    Check(g.Update(true, false, trigger, 1, 0, 8) == HandOptionWheelEvent.None && g.Hovered == -1, "deflected opening requires neutral");
    Check(g.Update(true, false, trigger, 0, 0, 8) == HandOptionWheelEvent.None && g.IsOpen, "first neutral cannot activate opening direction");
    Highlight(g);
    Check(g.Update(true, false, 0, 0, 0, 8) == HandOptionWheelEvent.Commit, "new deliberate direction after neutral commits");
}

g = Open();
Highlight(g);
Check(g.Update(true, false, 1, 1, 0, 8) == HandOptionWheelEvent.ChangePage && g.Hovered == -1, "trigger clears highlight and requests native page");
Check(g.Update(true, false, 1, 1, 0, 6) == HandOptionWheelEvent.None && g.Hovered == -1, "held trigger cannot cycle repeatedly or inherit old sector");
Check(g.Update(true, false, 0, 0, 0, 6) == HandOptionWheelEvent.None && g.Committed == -1, "page-change centring cannot commit");
Check(g.Update(true, false, 0, 0, 1, 6) == HandOptionWheelEvent.None && g.Hovered == 0, "new direction selects new native page");
Check(g.Update(true, false, 1, 0, 0, 6) == HandOptionWheelEvent.ChangePage && g.Committed == -1, "page change beats centre commit");
g.Update(true, false, 0, 0, 0, 8);
Highlight(g);
Check(g.Update(true, true, 1, 0, 0, 8) == HandOptionWheelEvent.Cancel && g.Committed == -1, "cancel beats page change and commit together");

g = Open();
Highlight(g);
Check(g.Update(false, false, 0, 0, 0, 8) == HandOptionWheelEvent.Cancel && g.Committed == -1, "tracking/context loss cancels, never release-commits");
Check(g.Update(true, true, 0, 0, 0, 8) == HandOptionWheelEvent.None && !g.IsOpen, "held button on tracking restore must release first");
foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    foreach (var channel in new[] { 0, 1, 2 })
    {
        g = Open();
        Highlight(g);
        Check(g.Update(true, false, channel == 0 ? bad : 0, channel == 1 ? bad : 0, channel == 2 ? bad : 0, 8) == HandOptionWheelEvent.Cancel && g.Committed == -1,
            "nonfinite tracking/trigger cannot commit");
    }
}
g = Open();
Highlight(g);
g.Reset();
Check(!g.IsOpen && g.Hovered == -1 && g.Committed == -1, "external close clears all pending work");
Check(g.Update(true, true, 0, 0, 0, 8) == HandOptionWheelEvent.None, "external close requires button release before reopen");

g = Open();
for (var i = 0; i < 8; i++) Check(g.Update(true, false, 0, .15f, -.15f, 8) == HandOptionWheelEvent.None && g.Hovered == -1, "centre jitter cannot select");
g.Update(true, false, 0, 1, 0, 0);
Check(g.Hovered == -1 && g.Update(true, false, 0, 0, 0, 0) == HandOptionWheelEvent.None, "empty native page never commits");
Highlight(g);
Check(g.Update(true, false, 0, 0, 0, 1) == HandOptionWheelEvent.None && g.Committed == -1, "removed native index cannot commit stale selection");

foreach (var count in new[] { 1, 2, 3, 4, 6, 7, 8, 11, 16 })
    for (var sector = 0; sector < count; sector++)
    {
        var angle = sector * 2 * Math.PI / count;
        Check(HandOptionWheelGesture.Sector((float)Math.Sin(angle), (float)Math.Cos(angle), count) == sector, "all native action counts align with actual clockwise render positions");
    }
Check(HandOptionWheelGesture.Sector(0, 0, 8) == -1 && HandOptionWheelGesture.Sector(1, 0, 0) == -1, "undefined sector rejected");
Check(HandOptionWheelGesture.Sector(float.NaN, 1, 8) == -1, "invalid sector rejected");
LeaseChecks.Run(Check);
PoseChecks.Run(Check);
EjectConfirmationChecks.Run(Check);
Console.WriteLine($"PASS: {checks} native option-wheel gesture, ownership, lease, pose and eject confirmation assertions.");
