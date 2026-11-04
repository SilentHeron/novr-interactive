using System.Collections.Generic;

namespace NOVR.MotionControls;

// One navigation path drives visibility, breadcrumb and Back, including shared
// help pages. Home and reopening a menu discard confirmation-page history.
internal sealed class MenuHistory<T> where T : class
{
    private readonly List<T> _trail;
    internal MenuHistory(T home) => _trail = new List<T> { home };
    internal T Current => _trail[_trail.Count - 1];
    internal IReadOnlyList<T> Trail => _trail;
    internal bool CanBack => _trail.Count > 1;
    internal void Push(T page)
    {
        if (EqualityComparer<T>.Default.Equals(Current, page)) return;
        var index = _trail.IndexOf(page);
        if (index >= 0) _trail.RemoveRange(index + 1, _trail.Count - index - 1);
        else _trail.Add(page);
    }
    internal void Back() { if (CanBack) _trail.RemoveAt(_trail.Count - 1); }
    internal void Home() { if (CanBack) _trail.RemoveRange(1, _trail.Count - 1); }
}
