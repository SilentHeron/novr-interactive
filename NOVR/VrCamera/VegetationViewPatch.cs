using HarmonyLib;
using NuclearOption.Effects;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrCamera;

// The native vegetation pass caches the original, aircraft-facing camera at
// Start. NOVR renders its tracked child instead. Use that child for this pass
// so detailed trees do not disappear when the pilot looks out of a side window.
internal static class VegetationViewPatch
{
    private static readonly Plane[] Planes = new Plane[6];
    private static readonly Vector4[] PlaneVectors = new Vector4[6];

    private static bool IsTrackedView(Camera? camera) => XRSettings.enabled && camera != null &&
        camera.isActiveAndEnabled && camera.GetComponent<NOVRPoseDriver>() != null;

    [HarmonyPatch(typeof(DetailRenderer), "LateUpdate")]
    private static class TrackedVegetationCamera
    {
        [HarmonyPrefix]
        private static void Prefix(ref Camera ___camera, out Camera? __state)
        {
            __state = null;
            var tracked = APIBus.MainCamera;
            if (!IsTrackedView(tracked) || ___camera == tracked)
                return;
            __state = ___camera;
            ___camera = tracked!;
        }

        // Scope the substitution to vegetation, including exceptional exits.
        // CameraStateManager and the non-VR camera cache retain native ownership.
        [HarmonyFinalizer]
        private static void Finalizer(ref Camera ___camera, Camera? __state)
        {
            if (__state != null)
                ___camera = __state;
        }
    }

    [HarmonyPatch(typeof(ShaderGlobalManager), nameof(ShaderGlobalManager.SetCameraPlanes))]
    private static class StereoVegetationPlanes
    {
        [HarmonyPostfix]
        private static void Postfix(Camera camera)
        {
            if (!IsTrackedView(camera) || !camera.stereoEnabled)
                return;
            var leftEye = camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
            var rightEye = camera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
            if (!TryExtents(leftEye, out var left) || !TryExtents(rightEye, out var right))
                return;

            // GeometryUtility's Camera overload uses the mono projection, which
            // can be much narrower than the headset. Bound both eye projections
            // without touching either eye's actual rendering projection or LOD.
            const float edgeMargin = 3f * Mathf.Deg2Rad;
            const float angleLimit = 89f * Mathf.Deg2Rad;
            var xMin = Mathf.Tan(Mathf.Max(-angleLimit, Mathf.Atan(Mathf.Min(left.x, right.x)) - edgeMargin));
            var xMax = Mathf.Tan(Mathf.Min(angleLimit, Mathf.Atan(Mathf.Max(left.y, right.y)) + edgeMargin));
            var yMin = Mathf.Tan(Mathf.Max(-angleLimit, Mathf.Atan(Mathf.Min(left.z, right.z)) - edgeMargin));
            var yMax = Mathf.Tan(Mathf.Min(angleLimit, Mathf.Atan(Mathf.Max(left.w, right.w)) + edgeMargin));
            var near = camera.nearClipPlane;
            var far = camera.farClipPlane;
            if (near <= 0 || far <= near)
                return;
            var projection = Matrix4x4.Frustum(xMin * near, xMax * near, yMin * near, yMax * near, near, far);
            GeometryUtility.CalculateFrustumPlanes(projection * camera.worldToCameraMatrix, Planes);
            for (var i = 0; i < Planes.Length; i++)
            {
                var normal = Planes[i].normal;
                // Keep near vegetation shared by both eyes despite their small
                // lateral offsets. Native per-tree bounds and distance remain.
                PlaneVectors[i] = new Vector4(normal.x, normal.y, normal.z, Planes[i].distance + .08f);
            }
            Shader.SetGlobalVectorArray(ShaderGlobalManager.ID_Global_FrustumPlanes, PlaneVectors);
        }
    }

    private static bool TryExtents(Matrix4x4 projection, out Vector4 extents)
    {
        extents = default;
        if (!MotionControls.FlightControlMath.IsFinite(projection.m00) || !MotionControls.FlightControlMath.IsFinite(projection.m11) ||
            !MotionControls.FlightControlMath.IsFinite(projection.m02) || !MotionControls.FlightControlMath.IsFinite(projection.m12) ||
            projection.m00 <= .01f || projection.m11 <= .01f)
            return false;
        extents = new Vector4((projection.m02 - 1) / projection.m00, (projection.m02 + 1) / projection.m00,
            (projection.m12 - 1) / projection.m11, (projection.m12 + 1) / projection.m11);
        return true;
    }
}
