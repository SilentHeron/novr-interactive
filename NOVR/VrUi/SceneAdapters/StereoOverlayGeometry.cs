using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Bound the actual two eye frusta in the camera's local forward plane. A mono
// FOV/aspect rectangle misses asymmetric lenses, eye offsets and canted views.
internal static class StereoOverlayGeometry
{
    internal static bool TryFit(Camera camera, float depth, out Vector2 center, out Vector2 size)
    {
        center = size = default;
        if (!camera.stereoEnabled || !Finite(depth) || depth <= 0)
            return false;
        var bounds = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (var eyeIndex = 0; eyeIndex < 2; eyeIndex++)
        {
            var eye = eyeIndex == 0 ? Camera.StereoscopicEye.Left : Camera.StereoscopicEye.Right;
            var projection = camera.GetStereoProjectionMatrix(eye);
            var view = camera.GetStereoViewMatrix(eye);
            if (!Finite(projection.determinant) || Mathf.Abs(projection.determinant) < .000001f ||
                !Finite(view.determinant) || Mathf.Abs(view.determinant) < .000001f)
                return false;
            var fromClip = projection.inverse;
            var eyeToHead = camera.transform.worldToLocalMatrix * view.inverse;
            var origin = eyeToHead.MultiplyPoint(Vector3.zero);
            for (var corner = 0; corner < 4; corner++)
            {
                var point = fromClip.MultiplyPoint(new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, 0));
                var direction = eyeToHead.MultiplyVector(point);
                if (!Finite(origin.x) || !Finite(origin.y) || !Finite(origin.z) ||
                    !Finite(direction.x) || !Finite(direction.y) || !Finite(direction.z) || direction.z <= .00001f)
                    return false;
                var along = (depth - origin.z) / direction.z;
                if (!Finite(along) || along <= 0)
                    return false;
                var x = origin.x + direction.x * along;
                var y = origin.y + direction.y * along;
                if (!Finite(x) || !Finite(y))
                    return false;
                bounds.x = Mathf.Min(bounds.x, x);
                bounds.y = Mathf.Min(bounds.y, y);
                bounds.z = Mathf.Max(bounds.z, x);
                bounds.w = Mathf.Max(bounds.w, y);
            }
        }
        center = new Vector2((bounds.x + bounds.z) * .5f, (bounds.y + bounds.w) * .5f);
        // Slight overscan keeps texture/mesh borders outside both visible eyes.
        size = new Vector2((bounds.z - bounds.x) * 1.04f, (bounds.w - bounds.y) * 1.04f);
        return Finite(size.x) && Finite(size.y) && size.x > .001f && size.y > .001f;
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
