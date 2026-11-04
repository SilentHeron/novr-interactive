using System.Reflection;
using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi.HarmonyPatches;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class OffsetRayChecks
{
    internal static void Run(Action<bool, string> check)
    {
        CandidateChecks.ResetContext();
        var settings = ModConfiguration.Instance.LookCue;
        var camera = APIBus.CockpitHudCamera!;
        var ring = GazeRingGraphic.Create(new GameObject("Cue canvas").transform).GetComponent<GazeRingGraphic>();
        var refreshRing = typeof(GazeRingGraphic).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var designator = new GameObject("Offset designator").AddComponent<NOVRTargetDesignatorBehavior>();
        var updateDesignator = typeof(NOVRTargetDesignatorBehavior).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var rotation in new[] { Quaternion.identity, Quaternion.Euler(27, 135, -32), Quaternion.Euler(-18, -165, 40) })
            foreach (var angle in new[] { -8f, -2.25f, -1f, 1f, 3.5f, 8f })
            {
                camera.transform.position = new Vector3(1200, -30, 700);
                camera.transform.rotation = rotation;
                settings.VerticalDegrees.Value = angle;
                refreshRing.Invoke(ring, null);
                // Actual ring placement supplies the expected centre; do not
                // duplicate the ray helper's settings/angle computation.
                var renderedCentre = camera.transform.position + rotation * new Vector3(
                    ring.rectTransform.anchoredPosition.x * GazeCueSettings.CanvasScale,
                    ring.rectTransform.anchoredPosition.y * GazeCueSettings.CanvasScale,
                    GazeCueSettings.ViewDepthMeters);
                var renderedRay = (renderedCentre - camera.transform.position).normalized;
                var aim = GazeCueRay.Direction(camera.transform);
                check((aim - renderedRay).sqrMagnitude < .000001f, "selection ray passes through actual rendered ring centre for signed offset and camera pitch/yaw/roll");
                check(Math.Abs(aim.magnitude - 1) < .00001f, "cue-offset selection direction remains normalized");
                var aligned = CandidateChecks.Marker(camera.transform.position + renderedRay * 1000);
                var headCentre = CandidateChecks.Marker(camera.transform.position + camera.transform.forward * 1000);
                var hud = CandidateChecks.Hud(headCentre, aligned);
                check(TargetCueCandidates.Nearest(hud) == aligned, "cue-aligned target wins over a competing head-centred target");
                check(!GazeTargetSelectionPatch.Prefix(hud, false) && hud.SelectedCalls.SequenceEqual(new[] { aligned.unit }),
                    "actual selection endpoint receives the target beneath the offset cue");
                updateDesignator.Invoke(designator, null);
                check(((designator.transform.position - camera.transform.position).normalized - renderedRay).sqrMagnitude < .000001f,
                    "native designator shares the rendered cue ray instead of the head-centre ray");
                check(Math.Abs((designator.transform.position - camera.transform.position).magnitude - 1000) < .001f &&
                    designator.transform.rotation == rotation, "offset designator retains projection depth and camera-facing rotation");
            }

        CandidateChecks.ResetContext();
        camera = APIBus.CockpitHudCamera!;
        var upper = CandidateChecks.Marker(Quaternion.Euler(-4, 0, 0) * Vector3.forward * 1000);
        var centre = CandidateChecks.Marker(Vector3.forward * 1000);
        var lower = CandidateChecks.Marker(Quaternion.Euler(4, 0, 0) * Vector3.forward * 1000);
        var competing = CandidateChecks.Hud(centre, upper, lower);
        check(TargetCueCandidates.Nearest(competing) == centre, "zero offset starts with head-centred candidate");
        settings.VerticalDegrees.Value = 4;
        check(TargetCueCandidates.Nearest(competing) == upper, "changing cue offset immediately changes candidate without advancing a frame");
        GazeTargetSelectionPatch.Prefix(competing, false);
        settings.VerticalDegrees.Value = -4;
        GazeTargetSelectionPatch.Prefix(competing, false);
        check(competing.SelectedCalls.SequenceEqual(new[] { upper.unit, lower.unit }), "each selection reads the current offset even within the same frame");
        refreshRing.Invoke(ring, null);
        var fixedRay = GazeCueRay.Direction(camera.transform);
        var fixedCentre = ring.rectTransform.anchoredPosition;
        foreach (var shape in Enum.GetValues<GazeCueShape>())
            foreach (var size in new[] { .5f, 1f, 2.5f })
            {
                settings.Shape.Value = shape;
                settings.Size.Value = size;
                refreshRing.Invoke(ring, null);
                check(GazeCueRay.Direction(camera.transform) == fixedRay && ring.rectTransform.anchoredPosition == fixedCentre,
                    "cue size and shape do not shift its rendered centre or targeting ray");
            }
        settings.Reset();
        PaintTimingChecks(check);
    }

    private static void PaintTimingChecks(Action<bool, string> check)
    {
        CandidateChecks.ResetContext();
        var camera = APIBus.CockpitHudCamera!;
        var settings = ModConfiguration.Instance.LookCue;
        var hud = CandidateChecks.Hud();
        var native = hud.targetDesignator.transform;
        GazeCueRay.AlignDesignator(native, camera.transform);
        var oldPosition = native.position;
        camera.transform.position = new Vector3(220, -35, 90);
        camera.transform.rotation = Quaternion.Euler(25, -130, 18);
        settings.VerticalDegrees.Value = -6;
        var ring = GazeRingGraphic.Create(new GameObject("Current paint cue").transform).GetComponent<GazeRingGraphic>();
        var expected = camera.transform.rotation * new Vector3(0,
            ring.rectTransform.anchoredPosition.y * GazeCueSettings.CanvasScale, GazeCueSettings.ViewDepthMeters);
        expected = camera.transform.position + expected.normalized * 1000;
        check(GazeTargetSelectionPatch.Prefix(hud, true), "paint prefix retains native multi-selection ownership");
        check((native.position - expected).sqrMagnitude < .001f && native.position != oldPosition &&
            native.rotation == camera.transform.rotation, "paint updates designator from moved camera and changed offset before any LateUpdate");
        check(hud.SelectedCalls.Count == 0, "paint alignment never invokes the single-target endpoint");

        foreach (var nativeMode in new Action[]
        {
            () => UnityEngine.XR.XRSettings.enabled = false,
            () => CameraStateManager.cameraMode = CameraMode.orbit,
            () => DynamicMap.mapMaximized = true
        })
        {
            CandidateChecks.ResetContext();
            native.position = new Vector3(-8, 17, 19);
            native.rotation = Quaternion.Euler(15, 20, 30);
            var retainedPosition = native.position;
            var retainedRotation = native.rotation;
            nativeMode();
            check(GazeTargetSelectionPatch.Prefix(hud, true) && native.position == retainedPosition && native.rotation == retainedRotation,
                "desktop, external-view and full-map painting never repositions native designator");
            check(hud.SelectedCalls.Count == 0, "native painting modes do not call single selection");
        }
        CandidateChecks.ResetContext();
        var previous = native.position;
        APIBus.CockpitHudCamera = null;
        check(GazeTargetSelectionPatch.Prefix(hud, true) && native.position == previous && hud.SelectedCalls.Count == 0,
            "paint without HUD camera safely retains native ownership and prior designator");
        CandidateChecks.ResetContext();
        hud.targetDesignator = null!;
        check(GazeTargetSelectionPatch.Prefix(hud, true) && hud.SelectedCalls.Count == 0,
            "paint without designator safely retains native ownership without single selection");
        settings.Reset();
    }
}
