namespace NOVR.MotionControls;

internal static class NativeRadialMenuState
{
    internal static bool IsBlockingInput
    {
        get
        {
            var menu = SceneSingleton<RadialMenuMain>.i;
            // Native IsInUse reads its child activeSelf. Before a spectator's
            // first spawn, the HUD parent can be inactive while that child is
            // still active; the disabled owner's Update cannot finish hiding it.
            return menu != null && menu.isActiveAndEnabled && RadialMenuMain.IsInUse();
        }
    }
}
