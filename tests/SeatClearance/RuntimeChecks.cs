using System.Reflection;
using NOVR;
using NOVR.Comfort;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

internal static class RuntimeChecks
{
    internal static int Run()
    {
        var passed = 0;
        void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            passed++;
        }
        static void Invoke(SeatClearance target, string method) => typeof(SeatClearance)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, null);
        static Pilot PilotWithSeat()
        {
            var pilot = new Pilot { player = GameManager.LocalPlayer };
            pilot.Filters = new[] { new MeshFilter { ParentPilot = pilot } };
            return pilot;
        }

        ModConfiguration.Instance = new();
        Time.unscaledTime = 0;
        XRSettings.enabled = true;
        TrackedControllers.Available = true;
        var pilot = PilotWithSeat();
        var otherPilot = PilotWithSeat(); // Native multicrew pilots share one player.
        // A nested crew mesh must not inherit pilot[0]'s eligibility either.
        pilot.Filters = pilot.Filters.Concat(otherPilot.Filters).ToArray();
        var glassFilter = new MeshFilter { ParentPilot = pilot }; // Deliberately misleading Seat/Seat names.
        glassFilter.Renderer.Materials[0].name = "UtilityHelo1_glass_int";
        glassFilter.Renderer.Materials[0].shader!.name = "Shader Graphs/canopy_int";
        glassFilter.Renderer.Materials[0].renderQueue = 2950;
        var mixedFilter = new MeshFilter { ParentPilot = pilot };
        mixedFilter.sharedMesh!.subMeshCount = 2;
        mixedFilter.Renderer.Materials.Add(glassFilter.Renderer.Materials[0]);
        pilot.Filters = pilot.Filters.Concat(new[] { glassFilter, mixedFilter }).ToArray();
        var aircraft = new Aircraft { pilots = new[] { pilot, otherPilot } };
        GameManager.LocalAircraft = aircraft;
        var main = new Camera();
        main.transform.position = new(1, 2, 3);
        var cockpit = new Camera();
        var external = new Camera();
        APIBus.MainCamera = main;
        CameraStateManager.cameraMode = CameraMode.cockpit;
        SceneSingleton<CameraStateManager>.i = new()
        {
            followingUnit = aircraft,
            cockpitCamRender = cockpit
        };
        var runtime = new SeatClearance();
        ModConfiguration.Instance.SeatClearanceEnabled.Value = false;
        Invoke(runtime, "OnEnable");
        try
        {
            Check(RenderPipelineManager.Subscriptions == 3, "Enable owns one subscription for each rendering boundary.");
            Invoke(runtime, "LateUpdate");
            RenderPipelineManager.Begin(main);
            Check(pilot.DiscoveryReads == 0 && SeatClearanceMesh.Created.Count == 0 && SeatClearance.Status == "Off",
                "Explicitly disabled clearance never scans the pilot or acquires mesh resources.");
            ModConfiguration.Instance.SeatClearanceEnabled.Value = true;
            Invoke(runtime, "LateUpdate");
            var seat = SeatClearanceMesh.Created.Single();
            Check(ReferenceEquals(seat.Filter, pilot.Filters[0]) && otherPilot.DiscoveryReads == 0,
                "Only the first occupied pilot's own mesh is acquired, even when crew share player identity.");
            Check(SeatClearanceMesh.Created.All(item => item.Filter != glassFilter && item.Filter != mixedFilter),
                "Glass and mixed glass/seat renderers are rejected before any mesh lease is acquired.");
            Check(SeatClearance.Status == "Seat detected", "Supported discovery reports a recognized seat.");
            RenderPipelineManager.Begin(main);
            Check(seat.Applied && seat.PreparedCenter == main.transform.position && seat.PreparedRadius == .25f,
                "Main stereo camera uses the head position and configured local radius.");
            RenderPipelineManager.End(main);
            Check(!seat.Applied, "End camera restores the original immediately.");
            RenderPipelineManager.Begin(cockpit);
            Check(seat.Applied && seat.PreparedCenter == main.transform.position,
                "The cockpit camera shares the main headset center.");
            var applies = seat.Applies;
            RenderPipelineManager.Begin(external);
            Check(!seat.Applied && seat.Applies == applies, "Another camera restores the seat and never applies clearance.");
            RenderPipelineManager.Begin(main);
            RenderPipelineManager.EndFrame();
            Check(!seat.Applied, "End frame restores even when an end-camera callback was missed.");
            RenderPipelineManager.Begin(main);
            Invoke(runtime, "LateUpdate");
            Check(!seat.Applied, "LateUpdate recovers an interrupted prior render pass.");

            applies = seat.Applies;
            main.stereoTargetEye = StereoTargetEyeMask.None;
            RenderPipelineManager.Begin(main);
            main.stereoTargetEye = StereoTargetEyeMask.Both;
            main.cameraType = CameraType.SceneView;
            RenderPipelineManager.Begin(main);
            main.cameraType = CameraType.Game;
            Check(!seat.Applied && seat.Applies == applies, "Mono and non-game cameras never substitute a mesh.");
            ModConfiguration.Instance.SeatClearanceRadius.Value = 1;
            RenderPipelineManager.Begin(main);
            Check(seat.PreparedRadius == .40f, "Runtime independently caps a malformed large radius.");
            ModConfiguration.Instance.SeatClearanceRadius.Value = .01f;
            RenderPipelineManager.Begin(main);
            Check(seat.PreparedRadius == .05f, "Runtime independently bounds a malformed small radius.");
            foreach (var validRadius in new[] { .05f, .12f, .25f, .40f })
            {
                ModConfiguration.Instance.SeatClearanceRadius.Value = validRadius;
                RenderPipelineManager.Begin(main);
                Check(seat.Applied && seat.PreparedRadius == validRadius,
                    "Runtime honors minimum, custom, default and maximum radii without the old 20 cm cap.");
            }
            applies = seat.Applies;
            foreach (var invalidRadius in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                ModConfiguration.Instance.SeatClearanceRadius.Value = invalidRadius;
                RenderPipelineManager.Begin(main);
                Check(!seat.Applied && seat.Applies == applies, "An invalid radius restores rather than retaining a previous cut.");
            }
            ModConfiguration.Instance.SeatClearanceRadius.Value = .25f;

            RenderPipelineManager.Begin(main);
            applies = seat.Applies;
            TrackedControllers.Available = false;
            RenderPipelineManager.Begin(main);
            Check(!seat.Applied && seat.Applies == applies, "Tracking loss restores clearance before using a stale head pose.");
            TrackedControllers.Available = true;
            RenderPipelineManager.Begin(main);
            Check(seat.Applied, "Fresh tracking permits the same owned seat lease to render again.");
            CameraStateManager.cameraMode = CameraMode.external;
            RenderPipelineManager.Begin(main);
            Check(!seat.Applied, "External view restores immediately before LateUpdate notices the change.");
            Invoke(runtime, "LateUpdate");
            Check(seat.Disposed && SeatClearance.Status == "Waiting for cockpit", "External context releases the private seat lease.");

            CameraStateManager.cameraMode = CameraMode.cockpit;
            Invoke(runtime, "LateUpdate");
            seat = SeatClearanceMesh.Created.Last();
            RenderPipelineManager.Begin(main);
            var replacementPilot = PilotWithSeat();
            aircraft.pilots = new[] { replacementPilot, otherPilot };
            RenderPipelineManager.Begin(main);
            Check(!seat.Applied, "Changing the occupied pilot invalidates render ownership immediately.");
            Invoke(runtime, "LateUpdate");
            Check(seat.Disposed && SeatClearanceMesh.Created.Last().Filter == replacementPilot.Filters[0],
                "Pilot replacement releases the old lease and discovers only the new occupied seat.");
            seat = SeatClearanceMesh.Created.Last();
            var replacementAircraft = new Aircraft { pilots = new[] { PilotWithSeat() } };
            GameManager.LocalAircraft = replacementAircraft;
            SceneSingleton<CameraStateManager>.i.followingUnit = replacementAircraft;
            RenderPipelineManager.Begin(main);
            Check(!seat.Applied, "Aircraft changes cannot render a stale seat lease.");
            Invoke(runtime, "LateUpdate");
            Check(seat.Disposed, "Aircraft changes dispose previous private resources.");
            seat = SeatClearanceMesh.Created.Last();
            RenderPipelineManager.Begin(main);
            replacementAircraft.Ejected = true;
            RenderPipelineManager.Begin(main);
            Check(!seat.Applied, "Ejection restores before any further cockpit render.");
            Invoke(runtime, "LateUpdate");
            Check(seat.Disposed, "Ejection releases the private mesh resources.");

            replacementAircraft.Ejected = false;
            Invoke(runtime, "LateUpdate");
            seat = SeatClearanceMesh.Created.Last();
            RenderPipelineManager.Begin(main);
            ModConfiguration.Instance.SeatClearanceEnabled.Value = false;
            RenderPipelineManager.Begin(main);
            Check(!seat.Applied, "Turning the option off restores immediately.");
            Invoke(runtime, "LateUpdate");
            Check(seat.Disposed && SeatClearance.Status == "Off", "Turning the option off releases owned resources.");
            ModConfiguration.Instance.SeatClearanceEnabled.Value = true;
            Invoke(runtime, "LateUpdate");
            seat = SeatClearanceMesh.Created.Last();
            RenderPipelineManager.Begin(main);
            Invoke(runtime, "OnDisable");
            Check(!seat.Applied && seat.Disposed && RenderPipelineManager.Subscriptions == 0,
                "Component disable restores and disposes the seat, then leaves no render callbacks subscribed.");
            applies = seat.Applies;
            RenderPipelineManager.Begin(main);
            Check(seat.Applies == applies, "A disabled component cannot apply clearance from later render callbacks.");
            Invoke(runtime, "OnEnable");
            Invoke(runtime, "LateUpdate");
            Check(RenderPipelineManager.Subscriptions == 3 && !SeatClearanceMesh.Created.Last().Disposed,
                "Reenable establishes one new lease and one subscription per boundary.");
        }
        finally
        {
            Invoke(runtime, "OnDisable");
            Invoke(runtime, "OnDestroy");
        }
        Check(RenderPipelineManager.Subscriptions == 0 && SeatClearanceMesh.Created.All(seat => seat.Disposed && !seat.Applied),
            "Disable followed by destruction is idempotent and leaves every lease restored.");
        return passed;
    }
}
