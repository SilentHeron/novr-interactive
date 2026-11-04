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

{
    var profiles = Start(false); profiles.Refresh();
    var factoryStick = CockpitProfiles.StickPosition;
    var factoryThrottle = CockpitProfiles.ThrottlePosition;
    Yes(CockpitProfiles.ActiveSlot == 1);
    Yes(profiles.SaveSlot(1, seat, stick, throttle));
    var seatTwo = new Vector3(-.07f, .02f, .12f);
    var stickTwo = stick + new Vector3(.03f, .02f, -.04f);
    var throttleTwo = throttle + new Vector3(-.02f, .03f, -.05f);
    Yes(profiles.SaveSlot(2, seatTwo, stickTwo, throttleTwo));
    Yes(CockpitProfiles.ActiveSlot == 1); Near(CockpitProfiles.SeatOffset, seat); Near(CockpitProfiles.StickPosition, stick);
    Yes(profiles.SelectSlot(2)); Near(CockpitProfiles.SeatOffset, seatTwo); Near(CockpitProfiles.StickPosition, stickTwo); Near(CockpitProfiles.ThrottlePosition, throttleTwo);
    profiles = Start(false, fresh:false); profiles.Refresh();
    Yes(CockpitProfiles.ActiveSlot == 2); Near(CockpitProfiles.StickPosition, stickTwo);
    Yes(profiles.SelectSlot(3)); Near(CockpitProfiles.SeatOffset, Vector3.zero); Near(CockpitProfiles.StickPosition, factoryStick); Near(CockpitProfiles.ThrottlePosition, factoryThrottle);
    Yes(profiles.SaveSlot(3, new Vector3(.04f, .05f, -.02f), stick + new Vector3(.1f, 0, 0), throttle));
    Yes(profiles.SelectSlot(1)); Near(CockpitProfiles.SeatOffset, seat); Near(CockpitProfiles.StickPosition, stick);

    // Editing seat placement is temporary. Save-as captures it into a different
    // slot without replacing the source when the coordinator cancels its preview.
    profiles.BeginPreview(); profiles.MoveSeat(new Vector3(.01f, -.02f, .03f));
    var previewSeat = CockpitProfiles.SeatOffset;
    Yes(previewSeat != seat); Near(Read("SmallFighter1", "Seat Offset", Vector3.zero), seat);
    profiles.BeginPreview(); Near(CockpitProfiles.SeatOffset, previewSeat); // idempotent
    profiles.EndPreview(); Near(CockpitProfiles.SeatOffset, seat);
    profiles.BeginPreview(); profiles.MoveSeat(new Vector3(.02f, .02f, 0)); previewSeat = CockpitProfiles.SeatOffset;
    Yes(profiles.SaveSlot(3, previewSeat, stickTwo, throttleTwo)); profiles.EndPreview();
    Yes(CockpitProfiles.ActiveSlot == 1); Near(CockpitProfiles.SeatOffset, seat); Near(CockpitProfiles.StickPosition, stick);
    Yes(profiles.SelectSlot(3)); Near(CockpitProfiles.SeatOffset, previewSeat); Near(CockpitProfiles.StickPosition, stickTwo);

    // No aircraft, slot, or altered global setting can leak into a new layout.
    ModConfiguration.Instance.MotionControls.StickPosition.Value = new Vector3(2, 2, 2);
    ModConfiguration.Instance.MotionControls.ThrottlePosition.Value = new Vector3(-2, 2, 2);
    GameManager.Current = new Aircraft { definition = new() { jsonKey = "Multirole1", unitName = "KR-67" } }; profiles.Refresh();
    Yes(CockpitProfiles.ActiveSlot == 1); Near(CockpitProfiles.SeatOffset, Vector3.zero); Near(CockpitProfiles.StickPosition, factoryStick);
    Yes(profiles.SelectSlot(2)); Near(CockpitProfiles.StickPosition, factoryStick); Near(CockpitProfiles.ThrottlePosition, factoryThrottle);
    Yes(profiles.SaveSlot(2, Vector3.zero, factoryStick + new Vector3(.04f, 0, 0), factoryThrottle));
    GameManager.Current = new Aircraft(); profiles.Refresh();
    Yes(CockpitProfiles.ActiveSlot == 3); Near(CockpitProfiles.SeatOffset, previewSeat); Near(CockpitProfiles.StickPosition, stickTwo);
    Yes(profiles.SelectSlot(2)); Near(CockpitProfiles.SeatOffset, seatTwo); Near(CockpitProfiles.ThrottlePosition, throttleTwo);
    foreach (var invalid in new[] { -1, 0, 4 })
    { Yes(!profiles.SelectSlot(invalid)); Yes(!profiles.SaveSlot(invalid, seat, stick, throttle)); }
    Yes(!profiles.SaveSlot(2, new Vector3(float.NaN, 0, 0), stick, throttle));
    Yes(!profiles.SaveSlot(2, seat, new Vector3(float.PositiveInfinity, 0, 0), throttle));
    Yes(CockpitProfiles.ActiveSlot == 2); Near(CockpitProfiles.SeatOffset, seatTwo); Near(CockpitProfiles.StickPosition, stickTwo);
    profiles.BeginPreview(); profiles.MoveSeat(new Vector3(.3f, 0, 0));
    GameManager.Current = null; profiles.Refresh(); profiles.EndPreview();
    Yes(CockpitProfiles.ActiveSlot == 1); Near(CockpitProfiles.SeatOffset, Vector3.zero);
    Console.WriteLine("PASS three independent aircraft slots, active-slot reload, factory isolation, cancel and save-as seat previews");
}
{
    var profiles=Start(false);profiles.Refresh();
    Yes(profiles.SaveSlot(1,seat,stick,throttle));
    GameManager.Current!.definition=new AircraftDefinition{jsonKey="",name=" "};profiles.Refresh();
    Yes(profiles.AircraftIdentity=="" && profiles.AircraftName=="Aircraft loading");
    Yes(!profiles.SelectSlot(2) && !profiles.SaveSlot(1,seat,stick,throttle));
    GameManager.Current.definition.jsonKey="third-party/late-plane";profiles.Refresh();
    Yes(profiles.AircraftIdentity=="third-party/late-plane" && profiles.SelectSlot(3));
    Yes(profiles.SaveSlot(3,seat,stick,throttle));
    GameManager.Current.definition=new AircraftDefinition{jsonKey="",name=""};profiles.Refresh();
    Yes(profiles.AircraftIdentity=="" && !profiles.SaveSlot(1,seat,stick,throttle));
    GameManager.Current.definition=new AircraftDefinition{jsonKey="another-author/plane",name=""};profiles.Refresh();
    Yes(profiles.AircraftIdentity=="another-author/plane" && CockpitProfiles.ActiveSlot==1);
    Near(CockpitProfiles.SeatOffset,Vector3.zero);
    GameManager.Current.definition.jsonKey="third-party/late-plane";profiles.Refresh();
    Yes(CockpitProfiles.ActiveSlot==3);Near(CockpitProfiles.SeatOffset,seat);
    Yes(AircraftLayoutIdentity.Resolve(null,null)=="" && AircraftLayoutIdentity.Resolve(" ","\t")=="");
    Yes(AircraftLayoutIdentity.Resolve("", "ModPrefab") == "ModPrefab");
    Yes(AircraftLayoutIdentity.Resolve("author/new-key", "ModPrefab") == "author/new-key");
    Console.WriteLine("PASS late mod identities initialize independent slots; unnamed definitions cannot save shared fallback profiles");
}
Console.WriteLine(assertions + " profile migration and slot assertions passed.");
