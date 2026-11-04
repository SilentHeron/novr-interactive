using UnityEngine;

namespace NOVR.VrCamera;

public class VrCamera : StereoCamera
{
    public static VrCamera? HighestDepthVrCamera { get; private set; }

    private void Start() => gameObject.AddComponent<NOVRPoseDriver>();

    private void Update()
    {
        var camera = ParentCamera;
        if (camera == null || !camera.isActiveAndEnabled) return;
        var previous = HighestDepthVrCamera != null ? HighestDepthVrCamera.CameraInUse : null;
        if (previous == null || !previous.isActiveAndEnabled || camera.depth > previous.depth)
            HighestDepthVrCamera = this;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (HighestDepthVrCamera == this) HighestDepthVrCamera = null;
    }
}
