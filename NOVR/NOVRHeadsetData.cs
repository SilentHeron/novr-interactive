using System;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR;

[DefaultExecutionOrder(-100)]
public class NOVRHeadsetData : NOVRBehaviour
{
    private static Func<XRNode, Quaternion>? _trackingRotation;
    private static Func<XRNode, Vector3>? _trackingPosition;
    public static Vector3 TranslationAnchor
    {
        get; private set;
    }
    public static Vector3 Translation
    {
        get; private set;
    }
    public static Vector3 TranslationCalibrationOffset
    {
        get; private set;
    }
    public static Vector3 TranslationError => Quaternion.Inverse(RotationCalibrationOffset) * (Translation - TranslationAnchor) - TranslationCalibrationOffset;

    public static Quaternion Rotation
    {
        get; private set;
    }
    public static Quaternion RotationCalibrationOffset { get; private set; } = Quaternion.identity;
    public static Quaternion RotationError => Quaternion.Inverse(RotationCalibrationOffset) * Rotation;

    // One rigid tracking-space transform for the head, grip and aim poses.
    // Previously only orientations were rotated on recenter, leaving controller
    // positions in the old room axes and moving their markers away from the hands.
    internal static Vector3 MapTrackingPosition(Vector3 position) =>
        TranslationAnchor + RotationCalibrationOffset * (TranslationCalibrationOffset + position);

    public static void SetAnchor(Vector3 anchor)
    {
        TranslationAnchor = anchor;
    }

    public static void CalibrateTranslation(CalibrationAxes calibrationAxes = CalibrationAxes.All, bool overrideExistingInNonCalibratedAxes = false)
    {
        if (_trackingPosition == null)
            return;
        Vector3 currentError = -_trackingPosition(XRNode.CenterEye);
        bool ov = overrideExistingInNonCalibratedAxes;
        TranslationCalibrationOffset = new Vector3(
            (calibrationAxes & CalibrationAxes.X) != 0 ? currentError.x : ov ? TranslationCalibrationOffset.x : 0,
            (calibrationAxes & CalibrationAxes.Y) != 0 ? currentError.y : ov ? TranslationCalibrationOffset.y : 0,
            (calibrationAxes & CalibrationAxes.Z) != 0 ? currentError.z : ov ? TranslationCalibrationOffset.z : 0
        );
    }

    public static void CalibrateRotation(CalibrationAxes calibrationAxes = CalibrationAxes.Yaw, bool overrideExistingInNonCalibratedAxes = false)
    {
        if (_trackingRotation == null)
            return;
        var currentRotation = _trackingRotation(XRNode.CenterEye);
        var currentEuler = currentRotation.eulerAngles;
        Vector3 currentError = new(
            -Mathf.DeltaAngle(0f, currentEuler.x),
            -Mathf.DeltaAngle(0f, currentEuler.y),
            -Mathf.DeltaAngle(0f, currentEuler.z)
        );

        bool ov = overrideExistingInNonCalibratedAxes;
        RotationCalibrationOffset = Quaternion.Euler(
            (calibrationAxes & CalibrationAxes.X) != 0 ? currentError.x : ov ? RotationCalibrationOffset.eulerAngles.x : 0,
            (calibrationAxes & CalibrationAxes.Y) != 0 ? currentError.y : ov ? RotationCalibrationOffset.eulerAngles.y : 0,
            (calibrationAxes & CalibrationAxes.Z) != 0 ? currentError.z : ov ? RotationCalibrationOffset.eulerAngles.z : 0
        );
    }

    protected override void Awake()
    {
        base.Awake();
        // Keep the installed game's existing center-eye API and calibration
        // semantics. Typed delegates avoid boxing two pose structs per sample.
#pragma warning disable CS0618
        _trackingRotation = InputTracking.GetLocalRotation;
        _trackingPosition = InputTracking.GetLocalPosition;
#pragma warning restore CS0618
    }

    protected override void OnBeforeRender()
    {
        base.OnBeforeRender();
        UpdateTransform();
    }

    private void Update()
    {
        UpdateTransform();
    }

    private void LateUpdate()
    {
        UpdateTransform();
    }

    private void UpdateTransform()
    {
        if (_trackingRotation != null && _trackingPosition != null)
        {
            Translation = MapTrackingPosition(_trackingPosition(XRNode.CenterEye));
            Rotation = RotationCalibrationOffset * _trackingRotation(XRNode.CenterEye);
        }
    }
}
