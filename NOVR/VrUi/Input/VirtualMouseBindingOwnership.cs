using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace NOVR.VrUi;

// Owns only the overrides this cursor applies to the native UI actions. Native
// ApplyBindingOverride replaces path, interactions AND processors, including nulls.
// Restore that exact tuple only while it still matches our applied override.
internal sealed class VirtualMouseBindingOwnership
{
    private readonly List<OwnedBinding> _bindings = new();
    private readonly Dictionary<InputAction, InputBinding[]> _snapshots = new();
    private InputSystemUIInputModule? _module;
    private InputAction? _point, _left, _middle, _right, _scroll, _move;
    private bool _acquired, _acquiring, _releasing, _acquisitionCanceled;

    internal bool Matches(InputSystemUIInputModule? module) =>
        _acquired && module != null && ReferenceEquals(module, _module) &&
        ReferenceEquals(module.point?.action, _point) &&
        ReferenceEquals(module.leftClick?.action, _left) &&
        ReferenceEquals(module.middleClick?.action, _middle) &&
        ReferenceEquals(module.rightClick?.action, _right) &&
        ReferenceEquals(module.scrollWheel?.action, _scroll) &&
        ReferenceEquals(module.move?.action, _move);

    internal bool Acquire(InputSystemUIInputModule module, string devicePath)
    {
        if (_acquiring || _releasing)
            return false;
        Release();
        _acquiring = true;
        _acquisitionCanceled = false;
        try
        {
            _module = module;
            _point = module.point?.action;
            _left = module.leftClick?.action;
            _middle = module.middleClick?.action;
            _right = module.rightClick?.action;
            _scroll = module.scrollWheel?.action;
            _move = module.move?.action;
            RestrictPointer(_point, devicePath);
            RestrictPointer(_left, devicePath);
            RestrictPointer(_middle, devicePath);
            RestrictPointer(_right, devicePath);
            RestrictPointer(_scroll, devicePath);
            RestrictNavigation(_move);
            _acquired = !_acquisitionCanceled;
            if (Matches(module))
                return true;
            // Binding modification can notify native listeners synchronously.
            // Do not retain a half-acquired lease if one replaces the module's actions.
            Release();
            return false;
        }
        catch
        {
            Release();
            throw;
        }
        finally
        {
            _acquiring = false;
        }
    }

    internal void Release()
    {
        if (_releasing)
            return;
        _releasing = true;
        _acquisitionCanceled |= _acquiring;
        _acquired = false;
        _module = null;
        _point = _left = _middle = _right = _scroll = _move = null;
        try
        {
            for (var i = _bindings.Count - 1; i >= 0; i--)
            {
                var owned = _bindings[i];
                try
                {
                    var index = FindOwnedBinding(owned);
                    if (index >= 0)
                        owned.Action.ApplyBindingOverride(index, owned.Original);
                }
                catch (Exception exception)
                {
                    // A disposed native action must not prevent other actions restoring.
                    Debug.LogWarning($"[NOVR] Could not restore UI binding on '{owned.Action.name}': {exception}");
                }
            }
        }
        finally
        {
            _bindings.Clear();
            _snapshots.Clear();
            _releasing = false;
        }
    }

    private void RestrictPointer(InputAction? action, string devicePath)
    {
        if (action == null)
            return;
        for (var i = 0; i < action.bindings.Count; i++)
        {
            var path = action.bindings[i].path;
            if (path == null || !path.Contains("<Mouse>"))
                continue;
            var newPath = path.Replace("<Mouse>", devicePath);
            if (!Apply(action, i, newPath))
                return;
            Debug.Log($"[NOVR] Overriding UI binding path: '{path}' -> '{newPath}' for action '{action.name}'");
        }
    }

    private void RestrictNavigation(InputAction? action)
    {
        if (action == null)
            return;
        for (var i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite)
                continue;
            var path = binding.path ?? string.Empty;
            if (path.StartsWith("<Keyboard>") || path.StartsWith("<Gamepad>"))
                continue;
            if (!Apply(action, i, string.Empty))
                return;
            Debug.Log($"[NOVR] Disabled UI navigation binding '{path}' on action '{action.name}'");
        }
    }

    private bool Apply(InputAction action, int index, string path)
    {
        if (_acquisitionCanceled)
            return false;
        var applied = new InputBinding { overridePath = path };
        // Shared actions retain the first native snapshot, but the last restriction
        // still wins, just as it did when the pointer roles were applied separately.
        for (var i = 0; i < _bindings.Count; i++)
        {
            var owned = _bindings[i];
            if (!ReferenceEquals(owned.Action, action) || owned.Index != index)
                continue;
            _bindings[i] = new OwnedBinding(action, index, owned.Snapshot, applied);
            try
            {
                action.ApplyBindingOverride(index, applied);
            }
            catch
            {
                if (!_acquisitionCanceled && SameOverrides(action.bindings[index], owned.Applied))
                    _bindings[i] = owned; // Failure before mutation: the previous override is still ours.
                throw;
            }
            return !_acquisitionCanceled;
        }
        if (!_snapshots.TryGetValue(action, out var snapshot))
        {
            snapshot = new InputBinding[action.bindings.Count];
            for (var i = 0; i < snapshot.Length; i++)
                snapshot[i] = action.bindings[i];
            _snapshots.Add(action, snapshot);
        }
        // Native binding notifications can throw AFTER updating the override.
        _bindings.Add(new OwnedBinding(action, index, snapshot, applied));
        action.ApplyBindingOverride(index, applied);
        return !_acquisitionCanceled;
    }

    private static int FindOwnedBinding(OwnedBinding owned)
    {
        var bindings = owned.Action.bindings;
        var index = -1;
        if (owned.Original.id != Guid.Empty)
        {
            for (var i = 0; i < bindings.Count; i++)
            {
                if (bindings[i].id != owned.Original.id)
                    continue;
                if (index >= 0)
                    return -1; // Ambiguous duplicate ID: never guess which binding to restore.
                index = i;
            }
        }
        else
        {
            // Some hand-authored bindings have no ID. An index is safe only if
            // the entire action's binding structure still matches its snapshot.
            if (bindings.Count != owned.Snapshot.Length)
                return -1;
            for (var i = 0; i < bindings.Count; i++)
                if (!SameBinding(bindings[i], owned.Snapshot[i]))
                    return -1;
            index = owned.Index;
        }
        if (index < 0)
            return -1;
        var current = bindings[index];
        return SameBinding(current, owned.Original) && SameOverrides(current, owned.Applied) ? index : -1;
    }

    private static bool SameBinding(InputBinding left, InputBinding right) =>
        left.id == right.id && left.path == right.path && left.name == right.name &&
        left.groups == right.groups && left.action == right.action &&
        left.interactions == right.interactions && left.processors == right.processors &&
        left.isComposite == right.isComposite && left.isPartOfComposite == right.isPartOfComposite;

    private static bool SameOverrides(InputBinding left, InputBinding right) =>
        left.overridePath == right.overridePath &&
        left.overrideInteractions == right.overrideInteractions &&
        left.overrideProcessors == right.overrideProcessors;

    private readonly struct OwnedBinding
    {
        internal readonly InputAction Action;
        internal readonly int Index;
        internal readonly InputBinding[] Snapshot;
        internal readonly InputBinding Original;
        internal readonly InputBinding Applied;

        internal OwnedBinding(InputAction action, int index, InputBinding[] snapshot, InputBinding applied)
        {
            Action = action;
            Index = index;
            Snapshot = snapshot;
            Original = snapshot[index];
            Applied = applied;
        }
    }
}
