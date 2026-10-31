using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using UnityEngine;

namespace NOVR.MotionControls;

// Handle coordinates are fixed in the cockpit. Seat translation moves only the
// headset and tracked hands, preserving their relative geometry and reach.
[DefaultExecutionOrder(-1300)]
internal sealed class CockpitProfiles : MonoBehaviour
{
    private sealed class Profile
    {
        public ConfigEntry<Vector3> Seat = null!, Stick = null!, Throttle = null!;
        public Vector3 DefaultThrottle;
    }
    private readonly Dictionary<string, Profile> _profiles = new();
    private Profile? _current;
    private string? _key;
    private bool _leftHanded;
    internal static CockpitProfiles? Instance { get; private set; }
    internal static Vector3 SeatOffset => Instance?._current?.Seat.Value ?? Vector3.zero;
    internal static Vector3 StickPosition => CockpitLayout.StickPreview ?? Instance?._current?.Stick.Value ?? ModConfiguration.Instance.MotionControls.StickPosition.Value;
    internal static Vector3 ThrottlePosition => CockpitLayout.ThrottlePreview ?? Instance?._current?.Throttle.Value ?? ModConfiguration.Instance.MotionControls.ThrottlePosition.Value;
    internal string AircraftName { get; private set; } = "No aircraft";

    private void Awake() => Instance = this;
    private void Update() => Refresh();
    internal void Refresh()
    {
        if (CameraStateManager.cameraMode != CameraMode.cockpit || !GameManager.GetLocalAircraft(out var aircraft) || aircraft.definition == null)
        {
            _current = null;
            _key = null;
            AircraftName = "No aircraft";
            return;
        }
        var identity = aircraft.definition.jsonKey;
        if (string.IsNullOrEmpty(identity)) identity = aircraft.definition.name;
        var settings = ModConfiguration.Instance.MotionControls;
        var leftHanded = settings.LeftHanded.Value;
        // One cockpit layout in right-stick coordinates. HandedPosition mirrors
        // the handles when used; the seat remains in aircraft coordinates.
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
        AircraftName = aircraft.definition.unitName;
        if (!_profiles.TryGetValue(identity, out _current))
        {
            var section = Section(identity);
            var config = ModConfiguration.Instance.Config;
            var collective = MotionFlightController.UsesLiftCollective(aircraft);
            var defaultThrottle = settings.ThrottlePosition.Value;
            if (collective) defaultThrottle.y = Mathf.Min(defaultThrottle.y, settings.StickPosition.Value.y - .15f);
            _current = new Profile
            {
                Seat = config.Bind(section, "Seat Offset", Vector3.zero, "Seat translation in meters for " + identity),
                Stick = config.Bind(section, "Stick Position", settings.StickPosition.Value, "Saved stick placement for " + identity),
                Throttle = config.Bind(section, "Throttle Position", defaultThrottle, "Saved throttle/collective travel center for " + identity),
                DefaultThrottle = defaultThrottle
            };
            var cockpitAnchored = config.Bind(section, "Cockpit Anchored Handles Applied", false,
                "One-time conversion of saved seat-relative handles to fixed cockpit positions. Seat adjustment no longer moves the handles.");
            var lowered = collective ? config.Bind(section, "Lower Collective Default Applied", false,
                "One-time migration of untouched helicopter handle defaults. Custom placements are retained.") : null;
            var shared = config.Bind(section, "Shared Handedness Layout Applied", false,
                "Uses one saved seat and handle layout for both hands. The selected legacy layout is copied once; changing stick hand only mirrors the handles.");
            if (!shared.Value)
            {
                // Preserve whichever layout the pilot is using at upgrade time,
                // including its migration state. Keep both legacy sections for
                // recovery instead of modifying or deleting their saved values.
                var legacy = Section(identity + (leftHanded ? ":left" : ":right"));
                _current.Seat.Value = config.Bind(legacy, "Seat Offset", Vector3.zero, "Legacy seat translation for " + identity).Value;
                _current.Stick.Value = config.Bind(legacy, "Stick Position", settings.StickPosition.Value, "Legacy stick placement for " + identity).Value;
                _current.Throttle.Value = config.Bind(legacy, "Throttle Position", defaultThrottle, "Legacy throttle placement for " + identity).Value;
                cockpitAnchored.Value = config.Bind(legacy, "Cockpit Anchored Handles Applied", false, "Legacy cockpit anchoring migration state.").Value;
                if (lowered != null)
                    lowered.Value = config.Bind(legacy, "Lower Collective Default Applied", false, "Legacy collective default migration state.").Value;
            }
            if (collective)
            {
                if (!lowered!.Value)
                {
                    if (_current.Stick.Value == settings.StickPosition.Value && _current.Throttle.Value == settings.ThrottlePosition.Value)
                        _current.Throttle.Value = defaultThrottle;
                    lowered.Value = true;
                }
            }
            _current.Seat.Value = BoundSeat(_current.Seat.Value);
            if (!TrackedControllers.Finite(_current.Stick.Value)) _current.Stick.Value = (Vector3)settings.StickPosition.DefaultValue;
            if (!TrackedControllers.Finite(_current.Throttle.Value)) _current.Throttle.Value = defaultThrottle;
            if (!cockpitAnchored.Value)
            {
                var offset = _current.Seat.Value;
                // A legacy left-hand layout stored unmirrored handle positions,
                // but its seat offset was always in aircraft coordinates.
                if (leftHanded) offset.x = -offset.x;
                _current.Stick.Value += offset; _current.Throttle.Value += offset;
                cockpitAnchored.Value = true;
            }
            shared.Value = true;
            _profiles.Add(identity, _current);
        }
        MotionFlightController.Instance?.ResetGrips();
    }

    internal void SaveHandles(Vector3 stick, Vector3 throttle)
    {
        Refresh();
        if (_current == null) return;
        _current.Stick.Value = stick;
        _current.Throttle.Value = throttle;
        ModConfiguration.Instance.Config.Save();
    }

    internal void MoveSeat(Vector3 delta)
    {
        Refresh();
        if (_current == null) return;
        _current.Seat.Value = BoundSeat(_current.Seat.Value + delta);
        MotionFlightController.Instance?.ResetGrips();
    }

    internal void ResetProfile()
    {
        if (_current == null) return;
        _current.Seat.Value = Vector3.zero;
        _current.Stick.Value = ModConfiguration.Instance.MotionControls.StickPosition.Value;
        _current.Throttle.Value = _current.DefaultThrottle;
        MotionFlightController.Instance?.ResetGrips();
    }

    private static Vector3 BoundSeat(Vector3 value) => !TrackedControllers.Finite(value) ? Vector3.zero :
        new Vector3(Mathf.Clamp(value.x, -0.5f, 0.5f), Mathf.Clamp(value.y, -0.5f, 0.5f), Mathf.Clamp(value.z, -0.5f, 0.5f));
    private static string Section(string identity)
    {
        using var hash = SHA256.Create();
        return "Cockpit " + System.BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").Substring(0, 16);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
