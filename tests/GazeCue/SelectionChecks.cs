using System.Reflection;
using NOVR;
using NOVR.VrUi;
using NOVR.VrUi.HarmonyPatches;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.XR;

internal static class SelectionChecks
{
    internal static void Run(Action<bool, string> check)
    {
        CombatHUD Fresh(out HUDUnitMarker marker)
        {
            CandidateChecks.ResetContext();
            marker = CandidateChecks.Marker(new Vector3(0, 0, 1000));
            return CandidateChecks.Hud(marker);
        }
        var hud = Fresh(out var marker);
        var offset = CandidateChecks.Marker(new Vector3(70, 0, 1000));
        hud.Add(offset);
        check(!GazeTargetSelectionPatch.Prefix(hud, false), "VR cockpit single selection suppresses native priority fallback");
        check(hud.SelectedCalls.SequenceEqual(new[] { marker.unit }) && marker.selected &&
            hud.aircraft!.weaponManager!.Targets.SequenceEqual(new[] { marker.unit }), "one nearest-target selection reaches native endpoint exactly once");
        GazeTargetSelectionPatch.Prefix(hud, false);
        check(hud.SelectedCalls.SequenceEqual(new[] { marker.unit, offset.unit }), "next press can select an unlocked alternative without duplicating the selected target");
        GazeTargetSelectionPatch.Prefix(hud, false);
        check(hud.SelectedCalls.Count == 2 && hud.aircraft!.weaponManager!.Targets.Count == 2, "all selected targets cannot create duplicate endpoint calls");
        hud = Fresh(out marker);
        hud.aircraft!.weaponManager!.Targets.Add(marker.unit!);
        check(!marker.selected && !GazeTargetSelectionPatch.Prefix(hud, false) && hud.SelectedCalls.Count == 0,
            "authoritative target list blocks duplicate native calls even before marker presentation updates");

        foreach (var allowNative in new Action[]
        {
            () => XRSettings.enabled = false,
            () => CameraStateManager.cameraMode = CameraMode.orbit,
            () => DynamicMap.mapMaximized = true
        })
        {
            hud = Fresh(out marker);
            allowNative();
            check(GazeTargetSelectionPatch.Prefix(hud, false) && hud.SelectedCalls.Count == 0,
                "desktop, external view and full tactical map retain native selection ownership");
        }
        hud = Fresh(out marker);
        check(GazeTargetSelectionPatch.Prefix(hud, true) && hud.SelectedCalls.Count == 0, "paint selection retains native multi-target behavior");

        foreach (var invalidate in new Action<CombatHUD, HUDUnitMarker>[]
        {
            (h, _) => h.aircraft = null,
            (h, _) => h.aircraft!.disabled = true,
            (h, _) => h.aircraft!.weaponManager = null,
            (h, _) => h.aircraft!.cockpit = null,
            (h, _) => h.aircraft!.cockpit!.Detached = true,
            (_, m) => m.image!.transform.position = new Vector3(1000, 0, 1000),
            (_, _) => APIBus.CockpitHudCamera = null,
            (_, _) => SceneSingleton<TargetListSelector>.i = null
        })
        {
            hud = Fresh(out marker);
            invalidate(hud, marker);
            check(!GazeTargetSelectionPatch.Prefix(hud, false) && hud.SelectedCalls.Count == 0,
                "unavailable cockpit/context or empty gaze cannot fall back to off-centre selection");
        }

        // Invoke the real designator method, with only Transform/camera access
        // replaced. This verifies pose assignment, not Unity execution ordering.
        CandidateChecks.ResetContext();
        var designator = new GameObject("Native designator behaviour").AddComponent<NOVRTargetDesignatorBehavior>();
        var update = typeof(NOVRTargetDesignatorBehavior).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var camera = APIBus.CockpitHudCamera!;
        foreach (var yaw in new[] { -170f, -70f, 0f, 45f, 165f })
        {
            camera.transform.position = new Vector3(250, -30, 70);
            camera.transform.rotation = Quaternion.Euler(17, yaw, -12);
            update.Invoke(designator, null);
            check(designator.transform.rotation == camera.transform.rotation, "designator rotation matches actual HUD camera without amplification or smoothing");
            check((designator.transform.position - camera.transform.position - camera.transform.forward * 1000).sqrMagnitude < .001f,
                "designator sits on exact head ray at native projection depth in translated/rotated scene");
        }
        var lastPosition = designator.transform.position;
        var lastRotation = designator.transform.rotation;
        APIBus.CockpitHudCamera = null;
        update.Invoke(designator, null);
        check(designator.transform.position == lastPosition && designator.transform.rotation == lastRotation, "missing HUD camera leaves designator untouched");
        APIBus.CockpitHudCamera = camera;
        camera.transform.position = new Vector3(1, 2, 3);
        NOUIManager.I = null;
        update.Invoke(designator, null);
        check(designator.transform.position == lastPosition, "missing UI manager does not update an orphaned designator");
    }
}
