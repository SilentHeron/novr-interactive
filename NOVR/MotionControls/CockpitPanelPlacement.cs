using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace NOVR.MotionControls;

// Placement is aircraft geometry, not a seat offset. The UI camera lives in a
// separate scene space, so convert the physical dashboard through the game rig.
[DefaultExecutionOrder(-1275)]
internal sealed class CockpitPanelPlacement : MonoBehaviour
{
    private sealed class Profile
    {
        internal ConfigEntry<bool> Manual = null!;
        internal ConfigEntry<Vector3> Position = null!, Angles = null!;
        internal ConfigEntry<float> Scale = null!, Aspect = null!;
    }
    private readonly Dictionary<string, Profile> _profiles = new();
    private Profile? _profile;
    private Aircraft? _aircraft;
    private DashboardSurface? _surface;
    private float _nextSearch;
    private int _searchAttempts;
    internal static CockpitPanelPlacement? Instance { get; private set; }
    internal static Vector3 Position { get { GetPose(out var p, out _, out _); return p; } }
    internal static Quaternion Rotation { get { GetPose(out _, out var r, out _); return r; } }
    internal static float Scale { get { GetPose(out _, out _, out var s); return s; } }
    internal static float Aspect => Instance?._profile?.Manual.Value == true ? Instance._profile.Aspect.Value : Instance?._surface?.Aspect ?? 1f;
    internal static string Status => Instance?._profile?.Manual.Value == true ? "Saved placement for this aircraft" :
        Instance?._surface?.ClearanceLimited == true ? "Tight dashboard • use Edit layout to refine" :
        Instance?._surface?.Renderer != null ? "Anchored to the aircraft dashboard" : "No usable dashboard surface • free placement";

    internal static bool IsVt7(Aircraft aircraft) => (aircraft.definition != null &&
        (aircraft.definition.name == "VTOLTrainer1" || aircraft.definition.jsonKey == "VTOLTrainer1")) || aircraft.name == "VTOLTrainer1";
    private void Awake() => Instance = this;
    private void Update() => Refresh();
    internal void Refresh()
    {
        if (CameraStateManager.cameraMode != CameraMode.cockpit || !GameManager.GetLocalAircraft(out var aircraft) ||
            aircraft.cockpit == null || aircraft.cockpit.IsDetached() || aircraft.definition == null)
        { _aircraft = null; _profile = null; _surface = null; return; }
        if (_aircraft != aircraft)
        {
            _aircraft = aircraft; _surface = null; _nextSearch = 0; _searchAttempts = 0;
            var key = aircraft.definition.jsonKey;
            if (string.IsNullOrEmpty(key)) key = aircraft.definition.name;
            if (!_profiles.TryGetValue(key, out _profile))
            {
                using var hash = SHA256.Create();
                var section = "Console " + System.BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(key))).Replace("-", "").Substring(0, 16);
                var config = ModConfiguration.Instance.Config;
                _profile = new Profile
                {
                    Manual = config.Bind(section, "Manual Placement", false, "Use the saved panel placement for " + key + "; otherwise fit to the native dashboard. Shared between handedness layouts."),
                    Position = config.Bind(section, "Position", Vector3.zero, "Saved panel center relative to the aircraft cockpit viewpoint, independent of seat adjustment."),
                    Angles = config.Bind(section, "Rotation", Vector3.zero, "Saved panel rotation relative to the aircraft cockpit viewpoint."),
                    Aspect = config.Bind(section, "Aspect", 1f, new ConfigDescription("Saved dashboard width relative to its height.", new AcceptableValueRange<float>(.85f, 1.75f))),
                    Scale = config.Bind(section, "Scale", 1f, new ConfigDescription("Saved panel scale.", new AcceptableValueRange<float>(.4f, 1.8f)))
                };
                if (!TrackedControllers.Finite(_profile.Position.Value) || !TrackedControllers.Finite(_profile.Angles.Value)) _profile.Manual.Value = false;
                if (IsVt7(aircraft))
                {
                    var enlarged = config.Bind(section, "VT7 Readability Upgrade", false, "Tracks the requested larger VT-7 panel; leaves other aircraft unchanged.");
                    if (!enlarged.Value)
                    {
                        if (_profile.Manual.Value)
                        {
                            var previous = _profile.Scale.Value; _profile.Scale.Value = Mathf.Min(1.8f, previous * 1.25f);
                            _profile.Position.Value += Quaternion.Euler(_profile.Angles.Value) * Vector3.back * (.0395f * (_profile.Scale.Value - previous) + .02f);
                        }
                        enlarged.Value = true;
                    }
                }
                _profiles.Add(key, _profile);
            }
        }
        if (_surface?.Renderer != null || _searchAttempts >= 5 || Time.unscaledTime < _nextSearch) return;
        _searchAttempts++;
        _nextSearch = Time.unscaledTime + 2;
        // This serialized reference is the actual screen mesh, not the offscreen
        // Canvas or render camera (whose transforms do not describe the dash).
        foreach (var cockpit in aircraft.cockpit.GetComponentsInChildren<Cockpit>(true))
        {
            var screen = AccessTools.Field(typeof(Cockpit), "tacScreenRender")?.GetValue(cockpit) as Renderer;
            if (screen != null && (_surface = DashboardSurface.Find(screen, aircraft.cockpitViewPoint)) != null) { _surface.FitClearance(aircraft); return; }
        }
        var materials = new HashSet<Material>();
        foreach (var tac in aircraft.cockpit.GetComponentsInChildren<TacScreen>(true))
            if (AccessTools.Field(typeof(TacScreen), "screenMaterial")?.GetValue(tac) is Material material) materials.Add(material);
        // Some cockpits leave tacScreenRender unassigned. Match the actual native
        // screen material instead of guessing from object names or cockpit bounds.
        foreach (var hierarchy in new[] { aircraft.transform, aircraft.cockpit.transform })
        foreach (var renderer in hierarchy.GetComponentsInChildren<MeshRenderer>(true))
        foreach (var material in renderer.sharedMaterials)
            if (materials.Contains(material) && (_surface = DashboardSurface.Find(renderer, aircraft.cockpitViewPoint)) != null) { _surface.FitClearance(aircraft); return; }
    }

    internal static void GetPose(out Vector3 position, out Quaternion rotation, out float scale)
    {
        var config = ModConfiguration.Instance;
        position = config.CockpitPanelPosition.Value;
        rotation = Quaternion.Euler(config.CockpitPanelTilt.Value, config.CockpitPanelYaw.Value, config.CockpitPanelRoll.Value);
        scale = config.CockpitPanelScale.Value;
        var self = Instance; self?.Refresh();
        var root = APIBus.MainCamera?.transform.parent;
        var eye = self?._aircraft?.cockpitViewPoint;
        if (root == null || eye == null || self!._profile == null) return;
        Vector3 world; Quaternion orientation;
        if (self._profile.Manual.Value)
        {
            world = eye.position + eye.rotation * self._profile.Position.Value;
            orientation = eye.rotation * Quaternion.Euler(self._profile.Angles.Value); scale = self._profile.Scale.Value;
        }
        else
        {
            if (self._surface?.Renderer == null) return;
            self._surface.Pose(out world, out orientation, out scale);
        }
        position = root.InverseTransformPoint(world); rotation = Quaternion.Inverse(root.rotation) * orientation;
    }

    internal void Save(Vector3 position, Quaternion rotation, float scale)
    {
        Refresh(); var root = APIBus.MainCamera?.transform.parent; var eye = _aircraft?.cockpitViewPoint;
        if (_profile == null || root == null || eye == null || !TrackedControllers.Finite(position)) return;
        _profile.Position.Value = Quaternion.Inverse(eye.rotation) * (root.TransformPoint(position) - eye.position);
        _profile.Angles.Value = (Quaternion.Inverse(eye.rotation) * root.rotation * rotation).eulerAngles;
        _profile.Aspect.Value = Aspect;
        _profile.Scale.Value = Mathf.Clamp(scale, .4f, 1.8f); _profile.Manual.Value = true;
        ModConfiguration.Instance.Config.Save();
    }
    internal void ResetToDashboard()
    {
        CockpitLayout.Instance?.Finish(false); Refresh();
        if (_profile != null) { _profile.Manual.Value = false; ModConfiguration.Instance.Config.Save(); }
    }
    internal static void Adjust(Vector3 delta, float tilt = 0, float resize = 0)
    {
        CockpitLayout.Instance?.Finish(true);
        GetPose(out var position, out var rotation, out var scale);
        Instance?.Save(CockpitLayout.BoundPanel(position + delta), rotation * Quaternion.Euler(tilt, 0, 0), scale + resize);
    }
    private void OnDestroy() { if (Instance == this) Instance = null; }
}
