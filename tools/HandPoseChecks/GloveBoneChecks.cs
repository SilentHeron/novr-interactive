using NOVR.MotionControls;

internal static class GloveBoneChecks
{
    internal static void Run(Action<bool, string> check)
    {
        // The names must point at the shipped rig, not merely agree with each other.
        foreach (var left in new[] { false, true })
        {
            var suffix = left ? "l" : "r";
            var expected = new (int Index, string Name)[]
            {
                (GloveBones.Root, "Root"),
                (GloveBones.Wrist, $"wrist_{suffix}"),
                (GloveBones.ThumbMetacarpal, $"finger_thumb_0_{suffix}"),
                (GloveBones.ThumbProximal, $"finger_thumb_1_{suffix}"),
                (GloveBones.ThumbDistal, $"finger_thumb_2_{suffix}"),
                (GloveBones.ThumbTip, $"finger_thumb_{suffix}_end"),
                (GloveBones.IndexMetacarpal, $"finger_index_meta_{suffix}"),
                (GloveBones.IndexProximal, $"finger_index_0_{suffix}"),
                (GloveBones.IndexIntermediate, $"finger_index_1_{suffix}"),
                (GloveBones.IndexDistal, $"finger_index_2_{suffix}"),
                (GloveBones.IndexTip, $"finger_index_{suffix}_end"),
                (GloveBones.MiddleMetacarpal, $"finger_middle_meta_{suffix}"),
                (GloveBones.MiddleProximal, $"finger_middle_0_{suffix}"),
                (GloveBones.MiddleIntermediate, $"finger_middle_1_{suffix}"),
                (GloveBones.MiddleDistal, $"finger_middle_2_{suffix}"),
                (GloveBones.MiddleTip, $"finger_middle_{suffix}_end"),
                (GloveBones.RingMetacarpal, $"finger_ring_meta_{suffix}"),
                (GloveBones.RingProximal, $"finger_ring_0_{suffix}"),
                (GloveBones.RingIntermediate, $"finger_ring_1_{suffix}"),
                (GloveBones.RingDistal, $"finger_ring_2_{suffix}"),
                (GloveBones.RingTip, $"finger_ring_{suffix}_end"),
                (GloveBones.LittleMetacarpal, $"finger_pinky_meta_{suffix}"),
                (GloveBones.LittleProximal, $"finger_pinky_0_{suffix}"),
                (GloveBones.LittleIntermediate, $"finger_pinky_1_{suffix}"),
                (GloveBones.LittleDistal, $"finger_pinky_2_{suffix}"),
                (GloveBones.LittleTip, $"finger_pinky_{suffix}_end"),
                (GloveBones.ThumbAuxiliary, $"finger_thumb_{suffix}_aux"),
                (GloveBones.IndexAuxiliary, $"finger_index_{suffix}_aux"),
                (GloveBones.MiddleAuxiliary, $"finger_middle_{suffix}_aux"),
                (GloveBones.RingAuxiliary, $"finger_ring_{suffix}_aux"),
                (GloveBones.LittleAuxiliary, $"finger_pinky_{suffix}_aux")
            };
            var asset = RiggedGlove.Load(left);
            check(asset.bones.Length == GloveBones.Count, "Named glove rig count differs from asset");
            check(expected.Select(entry => entry.Index).Distinct().Count() == GloveBones.Count,
                "Named glove joints duplicate or omit an index");
            foreach (var entry in expected)
                check(asset.bones[entry.Index].name == entry.Name, $"Glove joint {entry.Name} points at the wrong asset bone");
        }
    }
}
