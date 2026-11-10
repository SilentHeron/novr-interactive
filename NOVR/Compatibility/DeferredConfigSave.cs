using System;

namespace NOVR.Compatibility;

// BepInEx rewrites the whole configuration file on every Bind and on every
// changed value. With several hundred settings that costs seconds at startup
// and a file write for each step of a dragged slider. The plugin turns that
// off; this keeps the file current instead: one write shortly after the last
// change, and one when the game closes.
internal sealed class DeferredConfigSave
{
    internal const float DelaySeconds = 1f;
    private readonly Action _save;
    private readonly Action<string> _report;
    private bool _dirty, _failed;
    private float _saveAt;

    internal DeferredConfigSave(Action save, Action<string> report)
    {
        _save = save;
        _report = report;
    }

    internal bool Pending => _dirty;

    // Each change restarts the wait, so a drag saves once after it ends.
    internal void Changed(float now)
    {
        _dirty = true;
        _saveAt = now + DelaySeconds;
    }

    internal void Tick(float now)
    {
        if (_dirty && now >= _saveAt)
            Flush();
    }

    // "always" is for closing the game: settings bound for the first time
    // during play (a newly flown aircraft's profile) raise no change event,
    // and should still reach the file.
    internal void Flush(bool always = false)
    {
        if (!_dirty && !always)
            return;
        _dirty = false;
        try
        {
            _save();
            _failed = false;
        }
        catch (Exception error)
        {
            // Keep playing with the in-memory settings; report the first failure only.
            if (!_failed)
                _report("[NOVR] Could not save settings: " + error.Message);
            _failed = true;
        }
    }
}
