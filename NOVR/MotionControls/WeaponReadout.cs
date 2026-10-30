namespace NOVR.MotionControls;

// A selected target is not a lock. Readiness here means ammunition/reload state;
// only a visible native HUD cue may supply guidance such as SHOOT or NOT LASED.
internal static class WeaponReadout
{
    internal static (string text, bool caution) Describe(int selected, bool fitted, bool safety,
        int ammo, bool reloading, bool ready, string? nativeCue)
    {
        var cue = !fitted ? "NO WEAPON" : safety ? "SAFETY ON" : ammo <= 0 ? "EMPTY" :
            reloading && !ready ? "RELOADING" : !ready ? "NOT READY" :
            selected > 0 && !string.IsNullOrWhiteSpace(nativeCue) ? nativeCue!.Trim() : "LOADED";
        var caution = fitted && (safety || ammo <= 0 || !ready ||
            selected > 0 && !string.IsNullOrWhiteSpace(nativeCue) && cue != "SHOOT");
        return ((selected < 0 ? 0 : selected) + " SELECTED · " + cue, caution);
    }
}
