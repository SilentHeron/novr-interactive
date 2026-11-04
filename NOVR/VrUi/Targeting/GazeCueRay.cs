using UnityEngine;
using NOVR.VrUi.HarmonyPatches;

namespace NOVR.MotionControls;

internal static class GazeCueRay
{
    internal static Vector3 Direction(Transform camera)
    {
        // Match the centre of GazeRingGraphic on its head-facing canvas. Use
        // the same sanitized offset so adjusting the cue also adjusts aim.
        var settings = ModConfiguration.Instance.LookCue;
        var local = new Vector3(0, settings.CanvasOffsetY * GazeCueSettings.CanvasScale,
            GazeCueSettings.ViewDepthMeters);
        return camera.TransformDirection(local).normalized;
    }

    internal static void AlignDesignator(Transform designator, Transform camera) =>
        designator.SetPositionAndRotation(
            camera.position + Direction(camera) * VrHudProjection.HudDistance,
            camera.rotation);
}
