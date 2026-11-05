using NOVR.Comfort;

internal static class CandidateChecks
{
    internal static int Run()
    {
        var passed = 0;
        void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            passed++;
        }
        bool Eligible(string? objectName = "EjectionSeat", string? meshName = "ejectionSeat",
            bool local = true, bool separate = true, int vertices = 3806,
            float width = .53839946f, float height = 1.2963212f, float depth = .99190587f) =>
            SeatCandidatePolicy.IsEligible(objectName, meshName, local, separate, vertices, width, height, depth);

        Check(Eligible(), "The inventoried separate native ejection seat qualifies.");
        Check(Eligible(meshName: "fixedSeat_armor"), "The utility helicopter's fixed seat qualifies without an ejection-capability gate.");
        Check(Eligible(width: .53839946f * .9f), "The modded seat's physical nonuniform scale remains eligible.");
        Check(Eligible("Headrest", "headrest", width: .25f, height: .18f, depth: .1f), "A separately identified headrest qualifies.");
        foreach (var label in new[] { "seat", "PilotSeat", "FIXED SEAT", "Ejection_Seat", "ejection-seat", " EjectionSeat (Clone) ", "EjectionSeat_LOD0", "Seat LOD12(Clone)" })
            Check(Eligible(label, label), "Explicit identifier variants qualify: " + label);

        foreach (var label in new string?[] { null, "", " ", "cockpit", "cockpit_int", "cockpit_seat", "seat_cockpit", "canopy", "EjectionSeatPanel", "Seat+Cockpit", "mesh", "pilot", "Seat.001", "Seat(Clone)(Clone)", "Seat_LOD", "Seat_LOD0_cockpit", new string('s', 161) })
        {
            Check(!Eligible(objectName: label), "Unknown or combined object names remain untouched: " + label);
            Check(!Eligible(meshName: label), "A seat-labelled object cannot authorize an unknown/combined mesh: " + label);
        }
        Check(!Eligible(local: false), "Copilot, remote aircraft and out-of-subtree candidates are rejected.");
        Check(!Eligible(separate: false), "The runtime must establish a separate static renderer before using the name hints.");
        Check(!Eligible(local: false, separate: false), "No ownership evidence fails closed even with native names.");
        Check(Eligible(vertices: 3) && Eligible(vertices: 30000), "The finite triangle-mesh budget includes its boundaries.");
        foreach (var count in new[] { -1, 0, 2, 30001, int.MaxValue })
            Check(!Eligible(vertices: count), "Invalid/excessive geometry is rejected: " + count);
        foreach (var dimension in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity, -1f, 0f, .029f })
        {
            Check(!Eligible(width: dimension), "Invalid width is rejected.");
            Check(!Eligible(height: dimension), "Invalid height is rejected.");
            Check(!Eligible(depth: dimension), "Invalid depth is rejected.");
        }
        Check(Eligible(width: .03f, height: .03f, depth: .03f), "Small but measurable standalone components can qualify.");
        Check(Eligible(width: 1.4f, height: 2.2f, depth: 1.8f), "Physical-size limits are inclusive.");
        Check(!Eligible(width: 1.401f), "An oversized cockpit-width mesh is rejected.");
        Check(!Eligible(height: 2.201f), "An oversized cockpit-height mesh is rejected.");
        Check(!Eligible(depth: 1.801f), "An oversized cockpit-depth mesh is rejected.");
        return passed;
    }
}
