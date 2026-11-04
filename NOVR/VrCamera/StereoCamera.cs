using UnityEngine;

namespace NOVR.VrCamera;

public class StereoCamera : NOVRBehaviour
{
    public Camera? ParentCamera
    {
        get; private set;
    }
    public Camera? CameraInUse => ParentCamera;

    protected override void Awake()
    {
        base.Awake();
        ParentCamera = GetComponent<Camera>();
    }
}
