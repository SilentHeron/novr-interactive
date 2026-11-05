using BepInEx.Configuration;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR
{
    internal sealed partial class ModConfiguration
    {
        internal static ModConfiguration Instance = new();
        internal readonly ConfigEntry<bool> SeatClearanceEnabled = new(true, new(null));
        internal readonly ConfigEntry<float> SeatClearanceRadius = new(Comfort.SeatClearanceRange.Default, new(new AcceptableValueRange<float>(Comfort.SeatClearanceRange.Minimum, Comfort.SeatClearanceRange.Maximum)));
        internal readonly MotionControls.MotionControlSettings MotionControls = new();
    }
}

namespace NOVR.Comfort
{
    internal static class SeatClearance
    {
        internal static string Status = "Off";
    }
}

namespace NOVR.MotionControls
{
    internal sealed class MotionControlSettings
    {
        internal readonly ConfigEntry<bool> LeftHanded = new(false, new(null));
    }
    internal sealed class CockpitProfiles
    {
        internal static CockpitProfiles? Instance;
        internal readonly List<Vector3> SeatMoves = new();
        internal void MoveSeat(Vector3 delta) => SeatMoves.Add(delta);
    }
}

namespace NOVR.VrUi
{
    internal sealed partial class WristMenu
    {
        private GameObject? _seatPage;
        private readonly List<(GameObject page, Action refresh)> _settingRefresh = new();
        internal readonly Dictionary<string, ViewMenuControls.Check> SeatToggles = new();
        internal readonly Dictionary<string, ViewMenuControls.Scalar> SeatSliders = new();
        internal int ResetRequests;
        internal void BuildSeat() => BuildSeatPage();
        internal Text ClearanceHint => _seatPage!.transform.children
            .Single(item => item.gameObject.Name == "Checking seat…").GetComponent<Text>();
        internal void RefreshSeat()
        {
            foreach (var (_, refresh) in _settingRefresh)
                refresh();
        }
        private void AskReset() => ResetRequests++;
        private static string Centimeters(float value) => $"{value * 100:0.#} cm";
        private void Setting(Transform parent, string title, ConfigEntry<bool> entry, int column, float y, Action? after = null) =>
            Setting(parent, title, () => entry.Value, value => { entry.Value = value; after?.Invoke(); }, column, y);
        private void Setting(Transform parent, string title, Func<bool> read, Action<bool> write, int column, float y)
        {
            var control = ViewMenuControls.Checkbox(parent, _font, title, new Vector2(column == 0 ? -160 : 160, y), 300, read, write);
            SeatToggles.Add(title, control);
            _settingRefresh.Add((parent.gameObject, control.Refresh));
        }
        private ViewMenuControls.Scalar Setting(Transform parent, string title, ConfigEntry<float> entry, float y, Func<float, string> format, float step = 0, Action<float>? write = null)
        {
            var control = ViewMenuControls.Slider(parent, _font, title, y, entry, format, step, write);
            SeatSliders.Add(title, control);
            _settingRefresh.Add((parent.gameObject, control.Refresh));
            return control;
        }
    }
}
