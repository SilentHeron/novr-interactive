using UnityEngine;
using UnityEngine.UI;

// Only native detection storage and unrotated aircraft transforms are doubled.
// The refresh, threat ordering, pin cap and row update are production methods.
public class Unit : MonoBehaviour
{
}
public sealed class Aircraft : Unit
{
}
public sealed class Missile : Unit
{
    public enum SeekerMode
    {
        search, activeLock
    }
    public SeekerMode seekerMode;
    public string Seeker = "IR";
    public string GetSeekerType() => Seeker;
}
public sealed class ThreatItem
{
    public Missile Missile = null!; public Vector3? Notch;
}
public static class UnitConverter
{
    public static string DistanceReading(float distance) => distance.ToString("0") + "m";
}
namespace NOVR.VrUi.HarmonyPatches
{
    internal static class ThreatItemNotchDirectionPatch
    {
        internal static bool TryGetNotchDirection(ThreatItem item, out Vector3 direction)
        {
            direction = item.Notch ?? default;
            return item.Notch.HasValue;
        }
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    internal sealed partial class CockpitAlerts : MonoBehaviour
    {
        internal static bool Active;
        private sealed class Signal
        {
            internal Unit Source = null!; internal bool Jam; internal Vector3 Direction; internal float Expires;
        }
        private Aircraft? _aircraft = null;
        private ThreatList? _list = null;
        private readonly List<Signal> _signals = new();
        private readonly List<ThreatItem> _missiles = new();
        private readonly List<Text> _pins = new(), _rows = new();
        private GameObject? _threatRoot = null;
        private Text? _heading = null, _missileHeading = null;
        private static readonly Color MissileColor = new(1, .34f, .2f), RadarColor = new(1, .74f, .25f), JamColor = new(.45f, .85f, 1);
        private static Dictionary<int, ThreatItem> Items(ThreatList source) => source.Items;
        private static Missile MissileOf(ThreatItem source) => source.Missile;

        internal static int RunThreatChecks()
        {
            var passed = 0;
            void Check(bool value, string message)
            {
                if (!value)
                    throw new Exception(message);
                passed++;
            }
            Text Label() => new GameObject("warning text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            var owner = new CockpitAlerts
            {
                _aircraft = new GameObject("aircraft").AddComponent<Aircraft>(),
                _list = new GameObject("native threats").AddComponent<ThreatList>(),
                _threatRoot = new GameObject("scope"),
                _heading = Label(),
                _missileHeading = Label()
            };
            for (var i = 0; i < 6; i++)
            {
                var pin = Label();
                pin.enabled = false;
                pin.DisableTransitions = 0;
                owner._pins.Add(pin);
            }
            for (var i = 0; i < 4; i++)
                owner._rows.Add(Label());
            void Missile(int id, string seeker, Vector3 position, bool locked)
            {
                var missile = new GameObject(seeker).AddComponent<Missile>();
                missile.Seeker = seeker;
                missile.transform.position = position;
                missile.seekerMode = locked ? global::Missile.SeekerMode.activeLock : global::Missile.SeekerMode.search;
                owner._list.Items.Add(id, new ThreatItem { Missile = missile });
            }
            Missile(1, "IR", new Vector3(0, 0, 10), false);
            Missile(2, "ARH", new Vector3(100, 0, 0), true);
            Missile(3, "SARH", new Vector3(0, 0, -300), true);
            owner._list.Items[3].Notch = new Vector3(-1, 0, 0);
            var emitter = new GameObject("emitter").AddComponent<Unit>();
            owner._signals.Add(new Signal { Source = emitter, Direction = new Vector3(1, 0, 0), Expires = 4 });
            owner._signals.Add(new Signal { Source = emitter, Direction = new Vector3(-1, 0, 0), Expires = 1, Jam = true });
            Time.timeSinceLevelLoad = 0;
            owner.RefreshThreats();
            Check(owner._rows[0].text.StartsWith("ARH") && owner._rows[1].text.StartsWith("SARH") && owner._rows[2].text.StartsWith("IR"), "Active-lock threats remain first, then distance order.");
            Check(owner._rows[1].text.Contains("NOTCH 9h"), "Native notch detail remains in the retained row.");
            Check(owner._pins.Count(p => p.enabled) == 5 && owner._pins.All(p => p.DisableTransitions == 0), "New threats and emitters appear immediately without cycling pin activation.");
            Check(owner._rows[3].text == "RADAR 1  ·  JAM 1" && owner._missileHeading.text == "INCOMING  3", "Summary counts remain complete.");
            var dirties = owner._rows.Sum(r => r.TextDirties) + owner._pins.Sum(p => p.TextDirties);
            var enables = owner._pins.Sum(p => p.EnableTransitions);
            for (var frame = 0; frame < 20; frame++)
                owner.RefreshThreats();
            Check(owner._rows.Sum(r => r.TextDirties) + owner._pins.Sum(p => p.TextDirties) == dirties, "Unchanged refresh does not blank/refill warning text.");
            Check(owner._pins.Sum(p => p.EnableTransitions) == enables && owner._pins.All(p => p.DisableTransitions == 0), "Unchanged refresh never disables or re-enables existing pins.");

            owner._list.Items.Remove(2);
            Time.timeSinceLevelLoad = 1;
            owner.RefreshThreats();
            Check(owner._rows[0].text.StartsWith("SARH") && owner._rows[1].text.StartsWith("IR") && owner._rows[2].text == "", "Fewer missiles preserve sorted rows and clear the stale trailing row.");
            Check(owner._pins.Count(p => p.enabled) == 3 && owner._pins.Take(3).All(p => p.DisableTransitions == 0), "Expiry removes only trailing pins, retaining active slots.");
            Check(owner._pins[3].DisableTransitions == 1 && owner._pins[4].DisableTransitions == 1, "Disappearing pins are disabled once in the same refresh.");
            Check(owner._pins[2].text == "R" && owner._rows[3].text == "RADAR 1  ·  JAM 0", "Reused slot updates its symbol and expired jammer count immediately.");
            owner._list.Items.Clear();
            Time.timeSinceLevelLoad = 4;
            owner.RefreshThreats();
            Check(!owner._threatRoot.activeSelf && owner._pins.All(p => !p.enabled) && owner._rows.Take(3).All(r => r.text == ""), "All threats expiring clears every stale row/pin and hides the scope.");
            Check(owner._rows[3].text == "RADAR 0  ·  JAM 0", "Empty threat counts do not retain old totals.");
            for (var i = 0; i < 8; i++)
                Missile(i, "IR" + i, new Vector3(0, 0, 100 + i), true);
            owner.RefreshThreats();
            Check(owner._threatRoot.activeSelf && owner._pins.All(p => p.enabled), "Reappearing threats fill and activate the bounded pool immediately.");
            Check(owner._rows[0].text.StartsWith("IR0") && owner._rows[2].text.StartsWith("IR2") && owner._missileHeading.text == "INCOMING  8  ·  +5 MORE", "Overflow preserves three nearest rows and reports the full threat count.");
            return passed;
        }
    }
}
