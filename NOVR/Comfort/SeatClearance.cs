using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace NOVR.Comfort;

// Render-scoped ownership: only the current pilot's separate seat meshes are
// substituted, only while the VR cockpit camera draws. Every other view sees
// the originals. This does not alter physics, seat animation or ejection logic.
[DefaultExecutionOrder(12000)]
internal sealed class SeatClearance : MonoBehaviour
{
    internal static string Status { get; private set; } = "Off";
    private readonly List<SeatClearanceMesh> _seats = new();
    private readonly HashSet<MeshFilter> _examined = new();
    private readonly List<Material> _materials = new();
    private Aircraft? _aircraft;
    private Pilot? _pilot;
    private float _nextDiscovery;
    private int _discoveryAttempts;
    private bool _reported;

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += BeforeCamera;
        RenderPipelineManager.endCameraRendering += AfterCamera;
        RenderPipelineManager.endFrameRendering += AfterFrame;
    }

    private void LateUpdate()
    {
        Restore(); // Also recovers if a render pass was interrupted last frame.
        if (!TryGetContext(out var aircraft, out var pilot))
        {
            Release();
            Status = ModConfiguration.Instance.SeatClearanceEnabled.Value ? "Waiting for cockpit" : "Off";
            return;
        }
        if (_aircraft != aircraft || _pilot != pilot)
        {
            Release();
            _aircraft = aircraft;
            _pilot = pilot;
        }
        if (_discoveryAttempts < 5 && Time.unscaledTime >= _nextDiscovery)
        {
            _discoveryAttempts++;
            _nextDiscovery = Time.unscaledTime + 1;
            Discover(pilot!);
        }
    }

    private static bool TryGetContext(out Aircraft? aircraft, out Pilot? pilot)
    {
        aircraft = null;
        pilot = null;
        if (!ModConfiguration.Instance.SeatClearanceEnabled.Value || !XRSettings.enabled ||
            CameraStateManager.cameraMode != CameraMode.cockpit ||
            !GameManager.GetLocalAircraft(out var local) || local == null || local.cockpit == null ||
            local.cockpit.IsDetached() || local.HasEjected() ||
            SceneSingleton<CameraStateManager>.i?.followingUnit != local)
            return false;
        // CameraCockpitState uses pilots[0]. All crew share aircraft.Player,
        // so player ownership alone cannot distinguish the occupied seat.
        if (local.pilots == null || local.pilots.Length == 0)
            return false;
        pilot = local.pilots[0];
        if (pilot == null || pilot.dead || pilot.ejected || !GameManager.IsLocalPlayer(pilot.player))
            return false;
        aircraft = local;
        return true;
    }

    private void Discover(Pilot pilot)
    {
        foreach (var filter in pilot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter == null || filter.sharedMesh == null || _examined.Contains(filter))
                continue;
            _examined.Add(filter);
            var renderer = filter.GetComponent<MeshRenderer>();
            var mesh = filter.sharedMesh;
            var size = mesh.bounds.size;
            var transform = filter.transform;
            var dimensions = new Vector3(size.x * transform.TransformVector(Vector3.right).magnitude,
                size.y * transform.TransformVector(Vector3.up).magnitude,
                size.z * transform.TransformVector(Vector3.forward).magnitude);
            if (!SeatCandidatePolicy.IsEligible(filter.name, mesh.name,
                filter.GetComponentInParent<Pilot>() == pilot,
                renderer != null && !renderer.isPartOfStaticBatch && filter.GetComponent<SkinnedMeshRenderer>() == null,
                mesh.vertexCount, dimensions.x, dimensions.y, dimensions.z))
                continue;
            renderer!.GetSharedMaterials(_materials);
            if (!SeatMaterialPolicy.AreEligible(_materials, mesh.subMeshCount))
                continue;
            try
            {
                _seats.Add(new SeatClearanceMesh(filter, renderer!));
            }
            catch (Exception error) { Debug.LogWarning("[NOVR] Seat clearance left a seat unchanged: " + error.Message); }
        }
        Status = _seats.Count > 0 ? "Seat detected" : _discoveryAttempts < 5 ? "Finding seat…" : "Seat not supported";
        if (!_reported && (_seats.Count > 0 || _discoveryAttempts >= 5))
        {
            _reported = true;
            Debug.Log(_seats.Count > 0
                ? $"[NOVR] Head clearance ready for {_seats.Count} separate local seat mesh(es)."
                : "[NOVR] Head clearance: no safely identifiable local seat mesh; cockpit left unchanged.");
        }
    }

    private void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        Restore();
        if (_seats.Count == 0 || camera == null || !MotionControls.TrackedControllers.Available ||
            !TryGetContext(out var aircraft, out var pilot) ||
            aircraft != _aircraft || pilot != _pilot)
            return;
        var main = APIBus.MainCamera;
        var cockpit = SceneSingleton<CameraStateManager>.i?.cockpitCamRender;
        if (main == null || (camera != main && camera != cockpit) ||
            camera.cameraType != CameraType.Game || camera.stereoTargetEye == StereoTargetEyeMask.None)
            return;
        if (!SeatClearanceRange.TryClamp(ModConfiguration.Instance.SeatClearanceRadius.Value, out var radius))
            return;
        for (var i = _seats.Count - 1; i >= 0; i--)
        {
            var seat = _seats[i];
            try
            {
                seat.Prepare(main.transform.position, radius);
                seat.Apply();
            }
            catch (Exception error)
            {
                seat.Dispose();
                _seats.RemoveAt(i);
                if (_seats.Count == 0)
                    Status = "Seat not supported";
                Debug.LogWarning("[NOVR] Seat clearance stopped for an unsupported seat; original restored: " + error.Message);
            }
        }
    }

    private void AfterCamera(ScriptableRenderContext context, Camera camera) => Restore();
    private void AfterFrame(ScriptableRenderContext context, Camera[] cameras) => Restore();
    private void Restore()
    {
        foreach (var seat in _seats)
            seat.Restore();
    }
    private void Release()
    {
        foreach (var seat in _seats)
            seat.Dispose();
        _seats.Clear();
        _examined.Clear();
        _materials.Clear();
        _aircraft = null;
        _pilot = null;
        _discoveryAttempts = 0;
        _nextDiscovery = 0;
        _reported = false;
    }
    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        RenderPipelineManager.endCameraRendering -= AfterCamera;
        RenderPipelineManager.endFrameRendering -= AfterFrame;
        Release();
        Status = "Off";
    }
    private void OnDestroy() => OnDisable();
}
