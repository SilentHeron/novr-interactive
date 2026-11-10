using UnityEngine;

namespace NOVR.VrUi.HarmonyPatches;

internal static class VrHudProjection
{
    public const float HudDistance = 1000.0f;
    private const float VrViewportHorizontalDegrees = 50.0f;
    private const float VrViewportVerticalDegrees = 50.0f;
    private const float VrViewportHalfHorizontalDegrees = VrViewportHorizontalDegrees * 0.5f;
    private const float VrViewportHalfVerticalDegrees = VrViewportVerticalDegrees * 0.5f;

    // Both camera poses, read once. A loop that places many symbols in one
    // call (every unit marker, each frame) captures this once instead of
    // asking Unity for the same transforms again for each symbol. Capture it
    // inside the call that uses it: the head pose is updated several times a
    // frame, and a pose kept across calls would lag it.
    internal readonly struct HudView
    {
        internal readonly Vector3 MainPosition, MainForward, HudPosition;
        internal readonly Quaternion HudRotation;
        private readonly Quaternion _mainInverseRotation;
        private readonly Matrix4x4 _worldToMain;

        private HudView(Transform main, Transform hud)
        {
            var mainRotation = main.rotation;
            MainPosition = main.position;
            MainForward = mainRotation * Vector3.forward;
            _mainInverseRotation = Quaternion.Inverse(mainRotation);
            _worldToMain = main.worldToLocalMatrix;
            HudPosition = hud.position;
            HudRotation = hud.rotation;
        }

        internal static bool TryCapture(out HudView view)
        {
            var mainCamera = APIBus.MainCamera;
            var cockpitHudCamera = APIBus.CockpitHudCamera;
            if (mainCamera == null || cockpitHudCamera == null)
            {
                view = default;
                return false;
            }
            view = new HudView(mainCamera.transform, cockpitHudCamera.transform);
            return true;
        }

        internal bool TryProject(Vector3 worldPosition, out Vector3 hudPosition)
        {
            var mainCameraLocal = _worldToMain.MultiplyPoint3x4(worldPosition);
            if (mainCameraLocal.z <= 0.0f)
            {
                hudPosition = Vector3.zero;
                return false;
            }
            hudPosition = HudPosition + HudRotation * mainCameraLocal.normalized * HudDistance;
            return true;
        }

        internal bool TryProjectDirection(Vector3 worldPosition, out Vector3 hudPosition)
        {
            var mainCameraLocal = _worldToMain.MultiplyPoint3x4(worldPosition);
            if (mainCameraLocal.sqrMagnitude <= Mathf.Epsilon)
            {
                hudPosition = Vector3.zero;
                return false;
            }
            hudPosition = HudPosition + HudRotation * mainCameraLocal.normalized * HudDistance;
            return true;
        }

        internal bool PinToScreenEdge(Vector3 worldPosition, out Vector3 hudPosition, out float arrowAngle)
        {
            arrowAngle = 0.0f;
            var directionToTarget = worldPosition - MainPosition;
            if (directionToTarget.sqrMagnitude <= Mathf.Epsilon)
            {
                hudPosition = HudPosition + HudRotation * Vector3.forward * HudDistance;
                return false;
            }

            var mainCameraLocalDirection = _mainInverseRotation * directionToTarget.normalized;
            var targetYawDegrees = Mathf.Atan2(mainCameraLocalDirection.x, mainCameraLocalDirection.z) * Mathf.Rad2Deg;
            var targetPitchDegrees = Mathf.Atan2(
                mainCameraLocalDirection.y,
                new Vector2(mainCameraLocalDirection.x, mainCameraLocalDirection.z).magnitude) * Mathf.Rad2Deg;

            var horizontalRatio = targetYawDegrees / VrViewportHalfHorizontalDegrees;
            var verticalRatio = targetPitchDegrees / VrViewportHalfVerticalDegrees;
            var ellipseDistance = Mathf.Sqrt(horizontalRatio * horizontalRatio + verticalRatio * verticalRatio);
            var screenEdge = mainCameraLocalDirection.z <= 0.0f || ellipseDistance > 1.0f;

            var pinnedYawDegrees = targetYawDegrees;
            var pinnedPitchDegrees = targetPitchDegrees;
            if (screenEdge && ellipseDistance > Mathf.Epsilon)
            {
                pinnedYawDegrees /= ellipseDistance;
                pinnedPitchDegrees /= ellipseDistance;
            }

            var pinnedLocalDirection = DirectionFromYawPitch(pinnedYawDegrees, pinnedPitchDegrees);
            hudPosition = HudPosition + (HudRotation * pinnedLocalDirection).normalized * HudDistance;
            arrowAngle = Mathf.Atan2(targetPitchDegrees, targetYawDegrees);
            return screenEdge;
        }
    }

    public static bool TryProjectToCockpitHud(Vector3 worldPosition, out Vector3 hudPosition)
    {
        hudPosition = Vector3.zero;
        return HudView.TryCapture(out var view) && view.TryProject(worldPosition, out hudPosition);
    }

    public static bool TryProjectDirectionToCockpitHud(Vector3 worldPosition, out Vector3 hudPosition)
    {
        hudPosition = Vector3.zero;
        return HudView.TryCapture(out var view) && view.TryProjectDirection(worldPosition, out hudPosition);
    }

    public static bool PinToScreenEdge(Vector3 worldPosition, out Vector3 hudPosition, out float arrowAngle)
    {
        hudPosition = Vector3.zero;
        arrowAngle = 0.0f;
        return HudView.TryCapture(out var view) && view.PinToScreenEdge(worldPosition, out hudPosition, out arrowAngle);
    }

    public static bool PinToScreenEdge(Vector3 worldPosition, out Vector3 hudPosition) =>
        PinToScreenEdge(worldPosition, out hudPosition, out _);

    public static void SetVerticalLine(Transform line, Vector3 start, Vector3 end, Camera cockpitHudCamera, float lengthOffset = 0.0f)
    {
        var lineVector = end - start;
        if (lineVector.sqrMagnitude <= Mathf.Epsilon)
            return;

        var lineLength = Mathf.Max(0.0f, lineVector.magnitude * GetScreenHeightScale() + lengthOffset);
        if (lineLength <= Mathf.Epsilon)
            return;

        var lineMidpoint = Vector3.Lerp(start, end, 0.5f);
        line.position = start;
        line.rotation = Quaternion.LookRotation(lineMidpoint - cockpitHudCamera.transform.position, lineVector);
        line.localScale = Vector3.one + Vector3.up * lineLength;
    }

    public static void SetHorizontalLine(Transform line, Vector3 start, Vector3 end, Camera cockpitHudCamera, float lengthOffset = 0.0f)
    {
        var lineVector = end - start;
        if (lineVector.sqrMagnitude <= Mathf.Epsilon)
            return;

        var lineLength = Mathf.Max(0.0f, lineVector.magnitude + lengthOffset);
        if (lineLength <= Mathf.Epsilon)
            return;

        var lineMidpoint = Vector3.Lerp(start, end, 0.5f);
        line.position = lineMidpoint;
        line.rotation = Quaternion.LookRotation(lineMidpoint - cockpitHudCamera.transform.position, lineVector);
        line.localScale = new Vector3(lineLength, 1.0f, 1.0f);
    }

    public static void SetHorizontalLocalLine(Transform line, Vector3 startLocal, Vector3 endLocal, float lengthOffset = 0.0f)
    {
        var lineVector = endLocal - startLocal;
        if (lineVector.sqrMagnitude <= Mathf.Epsilon)
            return;

        var lineLength = Mathf.Max(0.0f, lineVector.magnitude * GetScreenHeightScale() + lengthOffset);
        if (lineLength <= Mathf.Epsilon)
            return;

        line.localPosition = Vector3.Lerp(startLocal, endLocal, 0.5f);
        line.localRotation = Quaternion.Euler(0.0f, 0.0f, Mathf.Atan2(lineVector.y, lineVector.x) * Mathf.Rad2Deg);
        line.localScale = new Vector3(lineLength, 1.0f, 1.0f);
    }

    public static Quaternion GetRotationAlongHudSegment(Vector3 start, Vector3 end, Camera cockpitHudCamera)
    {
        var segment = end - start;
        if (segment.sqrMagnitude <= Mathf.Epsilon)
            return cockpitHudCamera.transform.rotation;

        var midpoint = Vector3.Lerp(start, end, 0.5f);
        return Quaternion.LookRotation(midpoint - cockpitHudCamera.transform.position, segment);
    }

    public static float ReferencePixelsToHudDistance(float pixels) => pixels / GetScreenHeightScale();

    private static float GetScreenHeightScale() => 1080.0f / Mathf.Max(1, Screen.height);

    private static Vector3 DirectionFromYawPitch(float yawDegrees, float pitchDegrees)
    {
        var yawRadians = yawDegrees * Mathf.Deg2Rad;
        var pitchRadians = pitchDegrees * Mathf.Deg2Rad;
        var pitchCosine = Mathf.Cos(pitchRadians);

        return new Vector3(
            Mathf.Sin(yawRadians) * pitchCosine,
            Mathf.Sin(pitchRadians),
            Mathf.Cos(yawRadians) * pitchCosine);
    }
}
