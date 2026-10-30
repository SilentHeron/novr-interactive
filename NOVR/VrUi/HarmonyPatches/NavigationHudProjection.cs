using NOVR.MotionControls;
using TMPro;
using UnityEngine;

namespace NOVR.VrUi.HarmonyPatches;

// Navigation uses the headset's actual view, independently of the compact
// flight instruments and the existing combat-target edge cue.
internal static class NavigationHudProjection
{
    private static Camera? _camera;
    private static int _frame = -1;
    private static Vector4 _view, _band;

    internal static bool PinToEdge(Vector3 worldPosition, out Vector3 hudPosition, out float arrowAngle)
    {
        hudPosition = Vector3.zero; arrowAngle = 0;
        var main = APIBus.MainCamera; var hud = APIBus.CockpitHudCamera;
        if (main == null || hud == null) return false;
        var direction = main.transform.InverseTransformPoint(worldPosition).normalized;
        if (direction.sqrMagnitude <= Mathf.Epsilon) direction = Vector3.forward;
        RefreshBounds(main);
        var offScreen = direction.z <= 0;
        if (!offScreen)
        {
            var x = direction.x / direction.z; var y = direction.y / direction.z;
            offScreen = x < _view.x || x > _view.y || y < _view.z || y > _view.w;
        }
        if (offScreen)
        {
            var yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            var pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            // A point exactly behind has no unique left/right choice. Keep a
            // deterministic rightward cue instead of collapsing it to center.
            if (Mathf.Abs(yaw) < .001f && Mathf.Abs(pitch) < .001f) yaw = 180;
            arrowAngle = Mathf.Atan2(pitch, yaw);
            var horizontal = yaw < 0 ? _band.x : _band.y;
            var vertical = pitch < 0 ? _band.z : _band.w;
            var ratio = Mathf.Max(Mathf.Abs(yaw) / horizontal, Mathf.Abs(pitch) / vertical);
            yaw /= Mathf.Max(1, ratio); pitch /= Mathf.Max(1, ratio);
            var yawRadians = yaw * Mathf.Deg2Rad; var pitchRadians = pitch * Mathf.Deg2Rad;
            // Bounds are independent projected eye-frustum slopes. Spherical
            // yaw/pitch would increase y/z at diagonal corners by 1/cos(yaw).
            direction = new Vector3(Mathf.Tan(yawRadians), Mathf.Tan(pitchRadians), 1).normalized;
        }
        else
        {
            // The native mission pointer also uses this angle while visible.
            var yaw = Mathf.Atan2(direction.x, direction.z);
            var pitch = Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude);
            arrowAngle = Mathf.Atan2(pitch, yaw);
        }
        hudPosition = hud.transform.position + hud.transform.TransformDirection(direction) * VrHudProjection.HudDistance;
        return offScreen;
    }

    internal static Vector3 LabelPosition(Vector3 marker, TMP_Text label)
    {
        var hud = APIBus.CockpitHudCamera.transform;
        var local = hud.InverseTransformPoint(marker);
        var outward = new Vector2(local.x, local.y).normalized;
        var scale = ModConfiguration.Instance.MarkerLabelScale.Value;
        // Keep the existing text/color/size, but reserve inward space for its
        // scaled footprint. All offsets use HUD axes, never the world origin.
        var halfWidth = Mathf.Min(160, label.preferredWidth * Mathf.Abs(label.transform.lossyScale.x) * scale * .5f);
        var halfHeight = Mathf.Min(60, label.preferredHeight * Mathf.Abs(label.transform.lossyScale.y) * scale * .5f);
        var inset = 28 + Mathf.Abs(outward.x) * halfWidth + Mathf.Abs(outward.y) * halfHeight;
        return marker - (hud.right * outward.x + hud.up * outward.y) * inset;
    }

    private static void RefreshBounds(Camera camera)
    {
        if (_camera == camera && _frame == Time.frameCount) return;
        _camera = camera; _frame = Time.frameCount;
        // Only used while the runtime has not supplied usable stereo matrices.
        var x = Mathf.Tan(50 * Mathf.Deg2Rad); var y = Mathf.Tan(42 * Mathf.Deg2Rad);
        _view = new Vector4(-x, x, -y, y);
        var overlap = _view;
        if (camera.stereoEnabled && TryExtents(camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left), out var left) &&
            TryExtents(camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right), out var right))
        {
            // Anything visible to either eye stays at its true world bearing.
            _view = new Vector4(Mathf.Min(left.x, right.x), Mathf.Max(left.y, right.y), Mathf.Min(left.z, right.z), Mathf.Max(left.w, right.w));
            // Direction cues are inset into the view common to both eyes.
            overlap = new Vector4(Mathf.Max(left.x, right.x), Mathf.Min(left.y, right.y), Mathf.Max(left.z, right.z), Mathf.Min(left.w, right.w));
        }
        _band = new Vector4(Inset(-overlap.x, 42), Inset(overlap.y, 42), Inset(-overlap.z, 34), Inset(overlap.w, 34));
    }

    private static float Inset(float tangent, float limit) => Mathf.Min(limit, Mathf.Max(1, Mathf.Atan(tangent) * Mathf.Rad2Deg - 6));

    private static bool TryExtents(Matrix4x4 projection, out Vector4 extents)
    {
        extents = default;
        if (!FlightControlMath.IsFinite(projection.m00) || !FlightControlMath.IsFinite(projection.m11) ||
            !FlightControlMath.IsFinite(projection.m02) || !FlightControlMath.IsFinite(projection.m12) ||
            projection.m00 <= .01f || projection.m11 <= .01f) return false;
        extents = new Vector4((projection.m02 - 1) / projection.m00, (projection.m02 + 1) / projection.m00,
            (projection.m12 - 1) / projection.m11, (projection.m12 + 1) / projection.m11);
        return extents.x < 0 && extents.y > 0 && extents.z < 0 && extents.w > 0;
    }
}
