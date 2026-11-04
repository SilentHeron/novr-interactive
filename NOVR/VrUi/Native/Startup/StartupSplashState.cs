namespace NOVR.VrUi.Native;

// A startup-only presentation gate. Readiness comes from the observed loader,
// never from a timer, loading-screen disappearance, or temporary offline host.
internal sealed class StartupSplashState
{
    // Listening is a configuration flag and defaults true on an inactive Mirage
    // server. Offline loader hosts explicitly clear it, including while their local
    // client shuts down. Neither case is an actual multiplayer session.
    internal static bool IsMultiplayerSession(bool multiplayerMode, bool serverActive, bool serverListening, bool clientActive) =>
        multiplayerMode || (serverListening && (serverActive || clientActive));

    private bool _observedLoading;
    internal bool Finished
    {
        get; private set;
    }
    internal bool Visible
    {
        get; private set;
    }
    internal bool ExitConfirmation
    {
        get; private set;
    }
    internal bool Failed
    {
        get; private set;
    }

    internal bool Update(MissionContentPhase phase, bool vrEnabled, bool inAircraft, bool multiplayer)
    {
        if (Finished)
            return Visible = false;
        if (inAircraft || multiplayer)
        {
            Finished = true;
            ExitConfirmation = false;
            return Visible = false;
        }
        if (!vrEnabled)
            ExitConfirmation = false;
        Failed = phase == MissionContentPhase.Failed;
        if (phase == MissionContentPhase.Preparing || Failed)
        {
            _observedLoading = true;
            return Visible = vrEnabled;
        }
        if (_observedLoading || phase == MissionContentPhase.Unverified)
            Finished = true;
        ExitConfirmation = false;
        return Visible = false;
    }

    internal void ToggleExit()
    {
        if (Visible)
            ExitConfirmation = !ExitConfirmation;
    }
    internal void CancelExit() => ExitConfirmation = false;
    internal bool CanQuit => Visible && ExitConfirmation;
}
