using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class CockpitWidgetSpaceChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var origin = new Vector3(12, 3, -8);
        var yaw = Quaternion.Euler(0, 90, 0);
        var forward = new Vector3(0, 0, .44f);
        var world = CockpitWidgetSpace.ToWorld(origin, yaw, forward);
        Near(world, origin + new Vector3(.44f, 0, 0), "44 cm offset follows aircraft rotation in world meters");
        Length(world - origin, .44f, "Widget remains 44 cm from the viewpoint");

        // FS-20 helmetCamPoint (28500) and KR-67 helmetCamPoint (31447)
        // both inherit pilot_armature scale (100,100,100) in installed assets.
        // Reproduce TransformPoint's scaled offset to expose the original bug.
        foreach (var aircraft in new[] { "FS-20", "KR-67" })
        {
            var oldScaledWorld = origin + yaw * (forward * 100);
            Length(oldScaledWorld - origin, 44, aircraft + " old bone TransformPoint places the widget 44 meters away");
            Length(CockpitWidgetSpace.ToWorld(origin, yaw, forward) - origin, .44f,
                aircraft + " rigid viewpoint conversion ignores imported 100x scale");
            Near(CockpitWidgetSpace.ToLocal(origin, yaw, world), forward,
                aircraft + " physical hand at widget is detected at 44 cm, not 0.0044 units");
        }

        foreach (var rotation in new[] { Quaternion.identity, yaw, Quaternion.Euler(-18, 143, 27) })
        foreach (var position in new[] { Vector3.zero, origin, new Vector3(-93, 42, 71) })
        {
            var hand = new Vector3(-.23f, -.31f, .46f);
            var handWorld = CockpitWidgetSpace.ToWorld(position, rotation, hand);
            Near(CockpitWidgetSpace.ToLocal(position, rotation, handWorld), hand,
                "Hand and widget share the same meter-space under aircraft translation/rotation");
            var saved = new Vector3(.28f, -.40f, .28f);
            var fixedWidget = CockpitWidgetSpace.ToWorld(position, rotation, saved);
            foreach (var seat in new[] { new Vector3(.12f, .08f, -.16f), new Vector3(-.2f, -.15f, .25f) })
            {
                var seatedHand = CockpitWidgetSpace.ToWorld(position, rotation, hand + seat);
                Near(CockpitWidgetSpace.ToLocal(position, rotation, seatedHand), hand + seat,
                    "Seat translation changes mapped controller coordinates in meters");
                Near(CockpitWidgetSpace.ToWorld(position, rotation, saved), fixedWidget,
                    "Seat translation does not drag the fixed widget");
                var seatedEye = position + rotation * seat;
                Near(Quaternion.Inverse(rotation) * (fixedWidget - seatedEye), saved - seat,
                    "Seat movement changes viewer-to-widget distance without changing saved placement");
            }
        }

        void Near(Vector3 a, Vector3 b, string label) => check((a - b).magnitude < .00003f, label);
        void Length(Vector3 value, float expected, string label) => check(MathF.Abs(value.magnitude - expected) < .00003f, label);
    }
}
