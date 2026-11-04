using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVR.MotionControls;

// Each aircraft owns three independent seat/handle slots. Coordinates remain
// cockpit-fixed: translating the seat moves only the headset and tracked hands.
[DefaultExecutionOrder(-1300)]
internal sealed class CockpitProfiles : MonoBehaviour
{
    internal const int SlotCount = 3;
    private sealed class Profile
    {
        public ConfigEntry<Vector3> Seat = null!, Stick = null!, Throttle = null!;
        public Vector3 DefaultStick, DefaultThrottle;
    }
    private sealed class AircraftSlots
    {
        internal ConfigEntry<int> Active = null!;
        internal readonly Profile[] Slots = new Profile[SlotCount];
    }
    private readonly Dictionary<string, AircraftSlots> _profiles = new();
    private AircraftSlots? _slots;
    private Profile? _current;
    private string? _key;
    private bool _leftHanded;
    private Vector3? _seatPreview;
    internal static CockpitProfiles? Instance
    {
        get; private set;
    }
    internal static int ActiveSlot => Instance?._slots?.Active.Value ?? 1;
    internal static Vector3 SeatOffset => Instance?._seatPreview ?? Instance?._current?.Seat.Value ?? Vector3.zero;
    internal static Vector3 StickPosition => CockpitLayout.StickPreview ?? Instance?._current?.Stick.Value ?? (Vector3)ModConfiguration.Instance.MotionControls.StickPosition.DefaultValue;
    internal static Vector3 ThrottlePosition => CockpitLayout.ThrottlePreview ?? Instance?._current?.Throttle.Value ?? (Vector3)ModConfiguration.Instance.MotionControls.ThrottlePosition.DefaultValue;
    internal string AircraftName { get; private set; } = "No aircraft";
    internal string AircraftIdentity => _key ?? "";

    private void Awake() => Instance = this;
    private void Update() => Refresh();
    internal void Refresh()
    {
        if (CameraStateManager.cameraMode != CameraMode.cockpit || !GameManager.GetLocalAircraft(out var aircraft) || aircraft.definition == null)
        {
            _current = null;
            _slots = null;
            _key = null;
            _seatPreview = null;
            AircraftName = "No aircraft";
            return;
        }
        var identity = AircraftLayoutIdentity.Resolve(aircraft.definition.jsonKey, aircraft.definition.name);
        if (identity.Length == 0)
        {
            _current = null;
            _slots = null;
            _key = null;
            _seatPreview = null;
            AircraftName = "Aircraft loading";
            return;
        }
        var leftHanded = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        if (_key == identity)
        {
            if (_leftHanded != leftHanded)
            {
                _leftHanded = leftHanded;
                MotionFlightController.Instance?.ResetGrips();
            }
            return;
        }
        _key = identity;
        _leftHanded = leftHanded;
        _seatPreview = null;
        AircraftName = string.IsNullOrEmpty(aircraft.definition.unitName) ? identity : aircraft.definition.unitName;
        if (!_profiles.TryGetValue(identity, out _slots))
        {
            var section = Section(identity);
            var config = ModConfiguration.Instance.Config;
            _slots = new AircraftSlots
            {
                Active = config.Bind(section, "Active Layout Slot", 1, "Selected layout slot (1, 2 or 3) for " + identity + ". Slots never inherit another aircraft's layout.")
            };
            if (!ValidSlot(_slots.Active.Value))
                _slots.Active.Value = 1;
            var collective = MotionFlightController.UsesLiftCollective(aircraft);
            _slots.Slots[0] = LegacyProfile(identity, leftHanded, collective);
            for (var slot = 2; slot <= SlotCount; slot++)
                _slots.Slots[slot - 1] = BindProfile(section + " Slot " + slot, identity, collective);
            _profiles.Add(identity, _slots);
        }
        _current = _slots.Slots[_slots.Active.Value - 1];
        MotionFlightController.Instance?.ResetGrips();
    }

    private static Profile BindProfile(string section, string identity, bool collective)
    {
        var settings = ModConfiguration.Instance.MotionControls;
        // Factory values only. Old global placements must not become defaults
        // for a new aircraft or silently leak into its second/third slots.
        var stick = (Vector3)settings.StickPosition.DefaultValue;
        var throttle = (Vector3)settings.ThrottlePosition.DefaultValue;
        if (collective)
            throttle.y = Mathf.Min(throttle.y, stick.y - .15f);
        var config = ModConfiguration.Instance.Config;
        var profile = new Profile
        {
            Seat = config.Bind(section, "Seat Offset", Vector3.zero, "Seat translation in meters for " + identity),
            Stick = config.Bind(section, "Stick Position", stick, "Saved stick placement for " + identity),
            Throttle = config.Bind(section, "Throttle Position", throttle, "Saved throttle/collective travel center for " + identity),
            DefaultStick = stick,
            DefaultThrottle = throttle
        };
        profile.Seat.Value = BoundSeat(profile.Seat.Value);
        if (!TrackedControllers.Finite(profile.Stick.Value))
            profile.Stick.Value = stick;
        if (!TrackedControllers.Finite(profile.Throttle.Value))
            profile.Throttle.Value = throttle;
        return profile;
    }

    private static Profile LegacyProfile(string identity, bool leftHanded, bool collective)
    {
        var section = Section(identity);
        var config = ModConfiguration.Instance.Config;
        var profile = BindProfile(section, identity, collective);
        var anchored = config.Bind(section, "Cockpit Anchored Handles Applied", false,
            "One-time conversion of old seat-relative handles to fixed cockpit coordinates.");
        var lowered = collective ? config.Bind(section, "Lower Collective Default Applied", false,
            "One-time lowering of untouched helicopter collective defaults.") : null;
        var sharedHandedness = config.Bind(section, "Shared Handedness Layout Applied", false,
            "One layout per slot is mirrored when handedness changes. This migration does not share layouts between aircraft.");
        if (!sharedHandedness.Value)
        {
            var legacy = Section(identity + (leftHanded ? ":left" : ":right"));
            profile.Seat.Value = config.Bind(legacy, "Seat Offset", Vector3.zero, "Legacy seat translation for " + identity).Value;
            profile.Stick.Value = config.Bind(legacy, "Stick Position", profile.DefaultStick, "Legacy stick placement for " + identity).Value;
            profile.Throttle.Value = config.Bind(legacy, "Throttle Position", profile.DefaultThrottle, "Legacy throttle placement for " + identity).Value;
            anchored.Value = config.Bind(legacy, "Cockpit Anchored Handles Applied", false, "Legacy cockpit anchoring migration state.").Value;
            if (lowered != null)
                lowered.Value = config.Bind(legacy, "Lower Collective Default Applied", false, "Legacy collective default migration state.").Value;
        }
        if (lowered != null && !lowered.Value)
        {
            if (profile.Stick.Value == profile.DefaultStick && profile.Throttle.Value == (Vector3)ModConfiguration.Instance.MotionControls.ThrottlePosition.DefaultValue)
                profile.Throttle.Value = profile.DefaultThrottle;
            lowered.Value = true;
        }
        profile.Seat.Value = BoundSeat(profile.Seat.Value);
        if (!TrackedControllers.Finite(profile.Stick.Value))
            profile.Stick.Value = profile.DefaultStick;
        if (!TrackedControllers.Finite(profile.Throttle.Value))
            profile.Throttle.Value = profile.DefaultThrottle;
        if (!anchored.Value)
        {
            var offset = profile.Seat.Value;
            if (leftHanded)
                offset.x = -offset.x;
            profile.Stick.Value += offset;
            profile.Throttle.Value += offset;
            anchored.Value = true;
        }
        sharedHandedness.Value = true;
        return profile;
    }

    internal bool SelectSlot(int slot)
    {
        if (!ValidSlot(slot))
            return false;
        Refresh();
        if (_slots == null)
            return false;
        _slots.Active.Value = slot;
        _current = _slots.Slots[slot - 1];
        _seatPreview = null;
        MotionFlightController.Instance?.ResetGrips();
        ModConfiguration.Instance.Config.Save();
        return true;
    }

    // Save-as writes only the destination. The layout coordinator switches slots
    // after both HUD and handle snapshots are saved, and discards preview state.
    internal bool SaveSlot(int slot, Vector3 seat, Vector3 stick, Vector3 throttle)
    {
        if (!ValidSlot(slot) || !TrackedControllers.Finite(seat) || !TrackedControllers.Finite(stick) || !TrackedControllers.Finite(throttle))
            return false;
        Refresh();
        if (_slots == null)
            return false;
        var target = _slots.Slots[slot - 1];
        target.Seat.Value = BoundSeat(seat);
        target.Stick.Value = stick;
        target.Throttle.Value = throttle;
        ModConfiguration.Instance.Config.Save();
        return true;
    }

    internal void SaveHandles(Vector3 stick, Vector3 throttle)
    {
        Refresh();
        if (_current != null)
            SaveSlot(ActiveSlot, SeatOffset, stick, throttle);
    }
    internal void BeginPreview()
    {
        Refresh();
        if (_current != null && !_seatPreview.HasValue)
            _seatPreview = _current.Seat.Value;
    }
    internal void EndPreview()
    {
        if (!_seatPreview.HasValue)
            return;
        _seatPreview = null;
        MotionFlightController.Instance?.ResetGrips();
    }
    internal void MoveSeat(Vector3 delta)
    {
        Refresh();
        if (_current == null)
            return;
        if (_seatPreview.HasValue)
            _seatPreview = BoundSeat(_seatPreview.Value + delta);
        else
            _current.Seat.Value = BoundSeat(_current.Seat.Value + delta);
        MotionFlightController.Instance?.ResetGrips();
    }
    internal void ResetProfile()
    {
        Refresh();
        if (_current == null)
            return;
        _current.Seat.Value = Vector3.zero;
        _current.Stick.Value = _current.DefaultStick;
        _current.Throttle.Value = _current.DefaultThrottle;
        MotionFlightController.Instance?.ResetGrips();
        ModConfiguration.Instance.Config.Save();
    }
    private static bool ValidSlot(int slot) => slot >= 1 && slot <= SlotCount;
    private static Vector3 BoundSeat(Vector3 value) => !TrackedControllers.Finite(value) ? Vector3.zero :
        new Vector3(Mathf.Clamp(value.x, -0.5f, 0.5f), Mathf.Clamp(value.y, -0.5f, 0.5f), Mathf.Clamp(value.z, -0.5f, 0.5f));
    private static string Section(string identity)
    {
        using var hash = SHA256.Create();
        return "Cockpit " + System.BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").Substring(0, 16);
    }
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
