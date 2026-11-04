namespace NOVR.VrUi.Native;

// Lifetime belongs to the menu operation, not its visibility: closing or
// re-showing a panel must not permit a second launch while the first awaits.
internal sealed class MissionLaunchGate
{
    internal bool IsBusy { get; private set; }

    internal bool TryBegin(bool sessionActive)
    {
        if (IsBusy || sessionActive) return false;
        IsBusy = true;
        return true;
    }

    internal void Finish() => IsBusy = false;
}
