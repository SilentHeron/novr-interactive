using System;
using System.Collections;

namespace NOVR.VrUi.Native;

internal enum MissionContentPhase
{
    Ready, Preparing, Failed, Unverified
}

// No clock or log-file polling: a supported loader must report completion during
// its observed operation. Unknown loaders retain the game's existing behavior.
internal sealed class MissionContentState
{
    internal MissionContentPhase Phase { get; private set; } = MissionContentPhase.Ready;
    internal bool CanLaunch => Phase != MissionContentPhase.Preparing && Phase != MissionContentPhase.Failed;
    internal int Revision
    {
        get; private set;
    }
    internal string Status => Phase switch
    {
        MissionContentPhase.Preparing => "Preparing additional aircraft and missions…\nYou can browse while content loads. Start will unlock when ready.",
        MissionContentPhase.Failed => "Additional content did not finish loading.\nReturn to the main menu, check the mod log, then restart the game.",
        MissionContentPhase.Unverified => "Content readiness is unavailable for this loader.\nWait for its loading screen to finish before starting a mission.",
        _ => ""
    };
    private bool _observing;
    private bool _completionSeen;

    internal void Preparing() => SetPhase(MissionContentPhase.Preparing);
    internal void Unverified() => SetPhase(MissionContentPhase.Unverified);
    internal void Begin()
    {
        _observing = true;
        _completionSeen = false;
        Preparing();
    }

    internal void ObserveSuccess()
    {
        if (_observing)
            _completionSeen = true;
    }
    internal void End(bool completedNormally)
    {
        if (!_observing)
            return;
        _observing = false;
        SetPhase(completedNormally && _completionSeen ? MissionContentPhase.Ready : MissionContentPhase.Failed);
    }

    private void SetPhase(MissionContentPhase phase)
    {
        if (Phase == phase)
            return;
        Phase = phase;
        Revision++;
    }
}

// Wrap only the loader's own root enumerator. Yield objects (including its
// nested work and WaitUntil objects) are passed unchanged to Unity.
internal sealed class ObservedContentRoutine : IEnumerator, IDisposable
{
    private readonly IEnumerator _inner;
    private readonly Action<bool> _finished;
    private bool _ended;
    internal ObservedContentRoutine(IEnumerator inner, Action<bool> finished)
    {
        _inner = inner;
        _finished = finished;
    }
    public object Current => _inner.Current;
    public bool MoveNext()
    {
        if (_ended)
            return false;
        try
        {
            if (_inner.MoveNext())
                return true;
            End(true);
            return false;
        }
        catch { End(false); throw; }
    }
    public void Reset() => throw new NotSupportedException();
    public void Dispose()
    {
        try
        {
            (_inner as IDisposable)?.Dispose();
        }
        finally { End(false); }
    }
    private void End(bool normal)
    {
        if (_ended)
            return;
        _ended = true;
        _finished(normal);
    }
}
