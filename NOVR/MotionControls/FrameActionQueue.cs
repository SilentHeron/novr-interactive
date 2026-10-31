using System.Collections.Generic;

namespace NOVR.MotionControls;

// Physical controls run before the game's input update. Keep actions due now
// separate from newly requested actions due next frame, including repeat pulls.
internal sealed class FrameActionQueue<TContext> where TContext : class
{
    private readonly HashSet<string>[] _actions = { new(), new() };
    private readonly int[] _frames = { -1, -1 };
    private readonly TContext?[] _contexts = new TContext?[2];

    internal void Enqueue(string action, TContext context, int currentFrame)
    {
        var due = unchecked(currentFrame + 1);
        var slot = due & 1;
        if (_frames[slot] != due || !ReferenceEquals(_contexts[slot], context))
            _actions[slot].Clear();
        _frames[slot] = due; _contexts[slot] = context;
        _actions[slot].Add(action);
    }

    internal bool Contains(string action, TContext context, int currentFrame)
    {
        var slot = currentFrame & 1;
        return _frames[slot] == currentFrame && ReferenceEquals(_contexts[slot], context) &&
            _actions[slot].Contains(action);
    }

    internal void Clear()
    {
        for (var i = 0; i < 2; i++)
        { _actions[i].Clear(); _contexts[i] = null; _frames[i] = -1; }
    }
}
