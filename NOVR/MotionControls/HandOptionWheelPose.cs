using System;
using UnityEngine;

namespace NOVR.MotionControls;

internal static class HandOptionWheelPose
{
    private const float TiltDegrees = 25f;
    private static readonly float TiltCos = (float)Math.Cos(TiltDegrees * Math.PI / 180);
    private static readonly float TiltSin = (float)Math.Sin(TiltDegrees * Math.PI / 180);

    internal static void Get(bool left, bool frame, out Vector3 offset, out Vector3 normal, out Vector3 forward)
    {
        // Installed controller render models' thumbstick/openxr_grip transforms,
        // converted from OpenVR to Unity coordinates. Frame's deck mirrors in X.
        var side = left ? 1f : -1f;
        Vector3 cap;
        if (frame)
        {
            cap = new Vector3(side * .027826f, .0110762f, .0537274f);
            normal = new Vector3(side * .1654186f, .7774802f, .6067629f).normalized;
            forward = new Vector3(side * .1007780f, -.6253439f, .7738145f).normalized;
        }
        else
        {
            cap = new Vector3(-side * .006413f, -.0166453f, .0537527f);
            normal = new Vector3(0, .7358035f, .6771951f).normalized;
            forward = new Vector3(0, -.6771951f, .7358035f).normalized;
        }

        offset = cap + normal * .055f;
        // Raise the far edge toward the pilot, keeping the center anchored and
        // the left/right axis intact. This is a fixed -25° local-X UI rotation.
        var deckNormal = normal;
        normal = normal * TiltCos - forward * TiltSin;
        forward = forward * TiltCos + deckNormal * TiltSin;
    }
}
