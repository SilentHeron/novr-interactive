namespace NOVR.MotionControls;

internal static class AircraftLayoutIdentity
{
    // Mod aircraft use the same definition identity as stock aircraft. A
    // loading/unnamed definition must never become a shared fallback profile.
    internal static string Resolve(string? jsonKey, string? definitionName) =>
        !string.IsNullOrWhiteSpace(jsonKey) ? jsonKey! :
        !string.IsNullOrWhiteSpace(definitionName) ? definitionName! : "";
}
