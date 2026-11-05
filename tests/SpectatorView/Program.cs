using NOVR.VrCamera;

var checks = 0;
void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
bool Near(float actual, float expected) => Math.Abs(actual - expected) < .0001f;
var input = new SpectatorStickInput();
SpectatorStickMotion Read(float lx = 0, float ly = 0, float rx = 0, float ry = 0, float dt = .02f,
    bool active = true, bool left = true, bool right = true) => input.Update(active, left, right, lx, ly, rx, ry, dt);
Check(!Read(ry: 1).HasMotion, "Entering spectator with a deflected stick does not rotate the view");
Check(!Read(ry: 1).HasMotion, "A held stick cannot arm itself over time");
Check(!Read().HasMotion, "Neutral arms without generating movement");
var move = Read(rx: 1, ly: 1);
Check(Near(move.YawDegrees, 1.5f) && move.PitchDegrees == 0, "Right stick right rotates the native view right");
Check(Near(move.Distance, -.03f) && Near(move.PanForward, .02f), "Left stick forward moves closer in orbit or forward in free view");
Check(Read(ry: 1).PitchDegrees < 0 && Read(ry: -1).PitchDegrees > 0, "Right stick up pitches the viewing direction upward");
Check(Read(lx: 1).PanX > 0 && Read(lx: -1).PanX < 0, "Free camera lateral pan follows left stick direction");
Check(!Read(rx: .1f, ly: -.1f).HasMotion, "Normal stick drift is ignored");
Check(Read(rx: .21f).YawDegrees > 0 && Read(rx: .21f).YawDegrees < Read(rx: .6f).YawDegrees,
    "Movement grows continuously beyond the deadzone");
var diagonal = Read(rx: 1, ry: 1);
Check(Near(MathF.Sqrt(MathF.Pow(diagonal.YawDegrees / 75, 2) + MathF.Pow(diagonal.PitchDegrees / 60, 2)), .02f),
    "Diagonal movement is bounded to the stick's unit radius");
Check(Near(Read(rx: 1, dt: 10).YawDegrees, 3.75f), "A stalled frame cannot cause an uncontrolled orbit jump");
Check(!Read(active: false, rx: 1).HasMotion && !Read(rx: 1).HasMotion, "Menu ownership resets the neutral gate");
Read();
Check(!Read(right: false, rx: 1).HasMotion && !Read(rx: 1).HasMotion, "Tracking loss requires a fresh neutral position");
Read();
Check(Read(left: false, rx: 1).YawDegrees > 0, "One tracked hand remains usable when the other is absent");
foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    Read();
    Check(!Read(rx: invalid).HasMotion && !Read(rx: 1).HasMotion, "Invalid axis disarms its hand safely");
    Read();
    Check(!Read(rx: 1, dt: invalid).HasMotion && !Read(rx: 1).HasMotion, "Invalid frame time resets input");
}
Read();
Check(float.IsFinite(Read(rx: float.MaxValue, ry: float.MaxValue).YawDegrees), "Malformed finite values cannot overflow stick math");
foreach (var dt in new[] { 1f / 60, 1f / 90, 1f / 144 })
{
    input.Reset(); Read();
    var total = 0f;
    var frames = (int)MathF.Round(1 / dt);
    for (var i = 0; i < frames; i++) total += Read(rx: 1, dt: dt).YawDegrees;
    Check(Math.Abs(total - 75) < .002f, "Orbit speed is independent of supported headset refresh rates");
}
checks += NativeInputChecks.Run();
checks += SelectionViewChecks.Run();
Console.WriteLine($"{checks} spectator policy/input checks passed (native/Unity boundary doubles; no in-headset rendering claim).");
