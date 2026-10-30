using System.Collections.Generic;
using UnityEngine;

namespace NOVR.MotionControls;

// Native MFD apps keep refreshing while borrowed by a display. Suppress only
// their native presentation, without competing with their brightness writers.
internal sealed class NativeMfdAppVisibility
{
    private sealed class Lease
    {
        internal readonly Transform Source, Wrapper;
        internal readonly Transform? Parent;
        internal readonly int Index;
        internal bool Retired;
        internal bool WasHidden;
        internal int ChildCount;
        internal readonly List<CanvasGroup> Groups = new();

        internal Lease(Transform source)
        {
            Source = source; Parent = source.parent; Index = source.GetSiblingIndex();
            // Match the parent's entire coordinate space, not the app's rect.
            // The app keeps its anchors, pivot, offsets and local transform, and
            // Dock can borrow/return it without special handling for the wrapper.
            if (Parent is RectTransform parentRect)
            {
                var rect = new GameObject("NOVR native MFD visibility", typeof(RectTransform)).GetComponent<RectTransform>();
                rect.SetParent(Parent, false);
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                rect.pivot = parentRect.pivot; rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition3D = Vector3.zero;
                Wrapper = rect;
            }
            else
            {
                Wrapper = new GameObject("NOVR native MFD visibility").transform;
                Wrapper.SetParent(Parent, false);
            }
            Wrapper.gameObject.layer = source.gameObject.layer;
            Wrapper.SetSiblingIndex(Index);
            source.SetParent(Wrapper, false);
            RefreshGroups();
        }

        internal void RefreshGroups()
        {
            Groups.Clear();
            if (Source == null) return;
            Source.GetComponentsInChildren(true, Groups);
            ChildCount = Source.childCount;
        }

        internal void Release(CanvasGroup? group)
        {
            if (group != null) { group.alpha = 1; group.enabled = false; }
            if (Wrapper == null) return;
            if (Source != null && Source.parent == Wrapper)
            {
                Source.SetParent(Parent, false);
                Source.SetSiblingIndex(Index);
            }
            Object.Destroy(Wrapper.gameObject);
        }
    }

    // Retained as a diagnostic surface: these groups always belong to us, on
    // wrappers, never on a game object that already owns a native CanvasGroup.
    private readonly Dictionary<Transform, CanvasGroup> _groups = new();
    private readonly Dictionary<Transform, Lease> _leases = new();
    private readonly HashSet<Transform> _current = new();
    private readonly List<Transform> _removed = new();
    private readonly Dictionary<CanvasGroup, bool> _ignoreOriginal = new();
    private readonly HashSet<CanvasGroup> _hiddenGroups = new();
    private readonly List<CanvasGroup> _restoreIgnore = new();

    internal void Capture(HUDApp[] apps, Transform? tacticalSource)
    {
        _current.Clear();
        foreach (var app in apps)
        {
            if (app == null) continue;
            var source = app.transform;
            // Protect the radar viewport itself, its children and any app that
            // contains it. Other instrument siblings on its canvas may be hidden.
            if (tacticalSource != null && (source == tacticalSource || source.IsChildOf(tacticalSource) || tacticalSource.IsChildOf(source))) continue;
            _current.Add(source);
            if (_leases.TryGetValue(source, out var existing) && existing.Wrapper != null)
            {
                // Capture runs on the MFD's half-second discovery tick. Include
                // inactive descendants and newly added components as well as
                // hierarchy changes that do not affect the root child count.
                existing.Retired = false; existing.RefreshGroups(); continue;
            }
            var lease = new Lease(source);
            var group = lease.Wrapper.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            _leases[source] = lease; _groups[source] = group;
        }
        _removed.Clear();
        foreach (var entry in _leases)
        {
            if (entry.Key != null && _current.Contains(entry.Key)) continue;
            // Dock remembers Wrapper as the app's original parent. An app list
            // refresh must not destroy that parent while the app is borrowed.
            entry.Value.Retired = true;
            if (entry.Key == null || entry.Value.Wrapper == null || entry.Key.parent == entry.Value.Wrapper)
                _removed.Add(entry.Key!); // Unity-null objects still have managed keys.
        }
        foreach (var source in _removed)
        {
            _leases[source].Release(_groups[source]);
            _leases.Remove(source); _groups.Remove(source);
        }
        _current.Clear(); _removed.Clear();
    }

    internal void Apply(Transform displays)
    {
        _hiddenGroups.Clear();
        foreach (var entry in _leases)
        {
            var source = entry.Key; var group = _groups[source];
            if (source == null || group == null) continue;
            var hidden = !entry.Value.Retired && !source.IsChildOf(displays);
            var alpha = hidden ? 0f : 1f;
            if (group.alpha != alpha) group.alpha = alpha;
            var lease = entry.Value;
            if (hidden && (!lease.WasHidden || lease.ChildCount != source.childCount)) lease.RefreshGroups();
            lease.WasHidden = hidden;
            if (!hidden) continue;
            foreach (var child in lease.Groups)
            {
                // A nested app may have moved to a visible MFD since discovery.
                // Do not retain ownership of its flags through a stale cache.
                if (child == null || child.transform != source && !child.transform.IsChildOf(source)) continue;
                _hiddenGroups.Add(child);
                if (!_ignoreOriginal.ContainsKey(child)) _ignoreOriginal.Add(child, child.ignoreParentGroups);
                if (child.ignoreParentGroups) child.ignoreParentGroups = false;
            }
        }
        // Global ownership matters for nested apps: one lease must not restore
        // a child flag that another hidden ancestor still needs to suppress.
        _restoreIgnore.Clear();
        foreach (var entry in _ignoreOriginal)
            if (entry.Key == null || !_hiddenGroups.Contains(entry.Key)) _restoreIgnore.Add(entry.Key!);
        foreach (var group in _restoreIgnore)
        {
            if (group != null) group.ignoreParentGroups = _ignoreOriginal[group];
            _ignoreOriginal.Remove(group!);
        }
        _restoreIgnore.Clear(); _hiddenGroups.Clear();
    }

    // The caller returns native Docks first, then releases their parent leases.
    internal void Restore()
    {
        foreach (var entry in _ignoreOriginal)
            if (entry.Key != null) entry.Key.ignoreParentGroups = entry.Value;
        _ignoreOriginal.Clear(); _hiddenGroups.Clear(); _restoreIgnore.Clear();
        foreach (var entry in _leases) entry.Value.Release(_groups[entry.Key]);
        _leases.Clear(); _groups.Clear(); _current.Clear(); _removed.Clear();
    }
}
