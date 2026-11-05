internal static partial class Program
{
    private static void Main()
    {
        var passed = CandidateChecks.Run();
        passed += MaterialChecks.Run();
        RunGeometryChecks(ref passed);
        passed += RuntimeChecks.Run();
        Console.WriteLine($"PASS {passed} source-linked seat-clearance checks (production coordinator with render/geometry boundaries; no Unity rendering).");
    }

    // The clipping fixture supplies this implementation when its source lands.
    static partial void RunGeometryChecks(ref int passed);
}
