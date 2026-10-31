using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using NOVR;
using NOVR.MotionControls;
using UnityEngine;

var assertions = 0;
void Yes(bool value) { assertions++; if (!value) throw new Exception("Profile migration assertion " + assertions + " failed."); }
void Near(Vector3 a, Vector3 b) => Yes(Math.Abs(a.x-b.x)<1e-6 && Math.Abs(a.y-b.y)<1e-6 && Math.Abs(a.z-b.z)<1e-6);
string Section(string identity) => "Cockpit " + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..16];
void Entry<T>(string identity, string key, T value) => ModConfiguration.Instance.Config.Bind(Section(identity), key, value, "").Value = value;
T Read<T>(string identity, string key, T fallback) => ModConfiguration.Instance.Config.Bind(Section(identity), key, fallback, "").Value;
CockpitProfiles Start(bool left, bool collective = false, bool fresh = true)
{
    if (fresh) ModConfiguration.Instance = new();
    ModConfiguration.Instance.MotionControls.LeftHanded.Value = left;
    GameManager.Current = new Aircraft { Collective = collective };
    var profiles = new CockpitProfiles();
    typeof(CockpitProfiles).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(profiles, null);
    return profiles;
}
var seat = new Vector3(.08f, .10f, -.04f);
var stick = new Vector3(.31f, -.27f, .43f);
var throttle = new Vector3(-.29f, -.34f, .38f);

// Migrating the selected legacy side must neither choose the opposite side nor
// reinterpret already cockpit-anchored coordinates.
foreach (var selectedLeft in new[] { false, true })
{
    var profiles = Start(selectedLeft);
    var legacy = "SmallFighter1" + (selectedLeft ? ":left" : ":right");
    var other = "SmallFighter1" + (selectedLeft ? ":right" : ":left");
    Entry(legacy, "Seat Offset", seat); Entry(legacy, "Stick Position", stick); Entry(legacy, "Throttle Position", throttle);
    Entry(legacy, "Cockpit Anchored Handles Applied", true);
    Entry(other, "Seat Offset", new Vector3(-.3f, -.3f, -.3f));
    profiles.Refresh(); Near(CockpitProfiles.SeatOffset, seat); Near(CockpitProfiles.StickPosition, stick); Near(CockpitProfiles.ThrottlePosition, throttle);
    var resets = MotionFlightController.Instance!.Resets;
    ModConfiguration.Instance.MotionControls.LeftHanded.Value = !selectedLeft;
    profiles.Refresh(); Near(CockpitProfiles.SeatOffset, seat); Near(CockpitProfiles.StickPosition, stick); Near(CockpitProfiles.ThrottlePosition, throttle);
    Yes(MotionFlightController.Instance.Resets == resets + 1);
    var changed = stick + new Vector3(.02f, -.01f, .03f);
    profiles.SaveHandles(changed, throttle);
    profiles = Start(selectedLeft, fresh:false); profiles.Refresh();
    Near(CockpitProfiles.StickPosition, changed); Near(CockpitProfiles.SeatOffset, seat);
    Near(Read(legacy, "Stick Position", Vector3.zero), stick); // originals retained
    Near(Read(other, "Seat Offset", Vector3.zero), new(-.3f, -.3f, -.3f));
    Console.WriteLine("PASS " + (selectedLeft ? "left" : "right") + " legacy migration, hand swap, save/reload, original retention");
}

// Very old seat-relative layouts need their original side's X conversion once.
foreach (var left in new[] { false, true })
{
    var profiles = Start(left); var legacy = "SmallFighter1" + (left ? ":left" : ":right");
    Entry(legacy, "Seat Offset", seat); Entry(legacy, "Stick Position", stick); Entry(legacy, "Throttle Position", throttle);
    profiles.Refresh(); var offset = seat; if (left) offset.x = -offset.x;
    Near(CockpitProfiles.StickPosition, stick + offset); Near(CockpitProfiles.ThrottlePosition, throttle + offset);
    profiles = Start(!left, fresh:false); profiles.Refresh();
    Near(CockpitProfiles.StickPosition, stick + offset); Near(CockpitProfiles.ThrottlePosition, throttle + offset); Near(CockpitProfiles.SeatOffset, seat);
    Yes(!Read(legacy, "Cockpit Anchored Handles Applied", false));
}
Console.WriteLine("PASS seat-relative conversion uses original selected side exactly once");

foreach (var custom in new[] { false, true })
{
    var profiles = Start(false, collective:true);
    var settings = ModConfiguration.Instance.MotionControls;
    Entry("SmallFighter1:right", "Throttle Position", custom ? throttle : settings.ThrottlePosition.Value);
    Entry("SmallFighter1:right", "Cockpit Anchored Handles Applied", true);
    profiles.Refresh(); var expected = settings.ThrottlePosition.Value; expected.y = settings.StickPosition.Value.y - .15f;
    Near(CockpitProfiles.ThrottlePosition, custom ? throttle : expected);
    profiles.ResetProfile(); Near(CockpitProfiles.ThrottlePosition, expected);
    profiles = Start(true, collective:true, fresh:false); profiles.Refresh(); Near(CockpitProfiles.ThrottlePosition, expected);
}
Console.WriteLine("PASS collective defaults lower once, custom placement retained, reset preserved");
Console.WriteLine(assertions + " profile migration assertions passed.");
