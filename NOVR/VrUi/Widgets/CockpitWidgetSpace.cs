using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Pilot viewpoint bones can inherit a 100x model-import scale. Widget offsets
// and controller positions are meters, so use the rigid pose, never bone scale.
internal static class CockpitWidgetSpace
{
    internal static Vector3 ToWorld(Vector3 origin, Quaternion rotation, Vector3 local) => origin + rotation * local;
    internal static Vector3 ToLocal(Vector3 origin, Quaternion rotation, Vector3 world) => Quaternion.Inverse(rotation) * (world - origin);
}
