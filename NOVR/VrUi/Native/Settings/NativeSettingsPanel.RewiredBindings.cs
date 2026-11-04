using System;
using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace NOVR.VrUi.Native;

// Native binding refresh, edits and mapping callbacks. Catalog discovery and
// naming are independent of the UI; NativeBindingCaptureSession still owns
// delayed starts and global mapper lifetime. NOVR controller bindings are separate.
public partial class NativeSettingsPanel
{
    private const float BindingCaptureDelaySeconds = 0.25f;
    private const float BindingCaptureTimeoutSeconds = 8f;
    private readonly List<NativeBindingEntry> _bindingEntries = new();
    private readonly List<NativeBindingDevice> _bindingDeviceFilters = new();
    private readonly NativeBindingCaptureSession<NativeBindingEntry> _bindingCapture = new();
    private bool _bindingEntriesDirty = true;
    private string _bindingStatus = "Select REMAP, release the mouse, then press the new input.";


    private void EnsureBindingEntriesLoaded()
    {
        if (!_bindingEntriesDirty)
            return;

        var selectedDeviceKey = GetSelectedBindingDeviceKey();
        _bindingEntriesDirty = false;
        _bindingEntries.Clear();
        _bindingDeviceFilters.Clear();
        _bindingDeviceFilterIndex = 0;

        try
        {
            if (!ReInput.isReady || GameManager.playerInput == null)
            {
                return;
            }

            var catalog = NativeBindingCatalog.Read(GameManager.playerInput);
            _bindingEntries.AddRange(catalog.Entries);
            _bindingDeviceFilters.AddRange(catalog.Devices);
            RestoreBindingDeviceFilter(selectedDeviceKey);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to load control bindings: {exception}");
            _bindingEntries.Clear();
            _bindingDeviceFilters.Clear();
            _bindingDeviceFilterIndex = 0;
        }
    }

    private string? GetSelectedBindingDeviceKey()
    {
        if (_bindingDeviceFilters.Count == 0)
        {
            return null;
        }

        _bindingDeviceFilterIndex = ClampInt(_bindingDeviceFilterIndex, 0, _bindingDeviceFilters.Count - 1);
        return _bindingDeviceFilters[_bindingDeviceFilterIndex].Key;
    }

    private void RestoreBindingDeviceFilter(string? selectedDeviceKey)
    {
        if (_bindingDeviceFilters.Count == 0)
        {
            _bindingDeviceFilterIndex = 0;
            return;
        }

        if (!string.IsNullOrWhiteSpace(selectedDeviceKey))
        {
            for (var index = 0; index < _bindingDeviceFilters.Count; index++)
            {
                if (_bindingDeviceFilters[index].Key != selectedDeviceKey)
                    continue;

                _bindingDeviceFilterIndex = index;
                return;
            }
        }

        _bindingDeviceFilterIndex = ClampInt(_bindingDeviceFilterIndex, 0, _bindingDeviceFilters.Count - 1);
    }

    private void QueueBindingCapture(NativeBindingEntry entry)
    {
        _bindingCapture.Queue(entry, Time.unscaledTime + BindingCaptureDelaySeconds);
        _bindingStatus = $"Release the mouse, then press a new input or move an axis for {entry.DisplayName}.";
        RenderCurrentTab();
    }

    private void ClearBinding(NativeBindingEntry entry)
    {
        CancelBindingCapture(null);
        if (!entry.IsAssigned || entry.ActionElementMap == null)
        {
            return;
        }

        try
        {
            entry.ControllerMap.DeleteElementMap(entry.ActionElementMap.id);
            SaveRewiredBindings();
            _bindingEntriesDirty = true;
            _focusedBindingKey = null;
            _lastActiveBindingKey = null;
            _bindingStatus = $"Cleared {entry.DisplayName} binding {entry.BindingName}.";
        }
        catch (Exception exception)
        {
            _bindingStatus = $"Could not clear {entry.DisplayName}.";
            Debug.LogWarning($"[NOVR] Native settings failed to clear binding '{entry.DisplayName}' on '{entry.ControllerLabel}': {exception}");
        }

        RenderCurrentTab();
    }

    private void StartBindingCapture(NativeBindingEntry entry)
    {
        if (!ReInput.isReady)
        {
            _bindingStatus = "Cannot remap because Rewired is not ready.";
            RenderCurrentTab();
            return;
        }

        var mapper = new InputMapper
        {
            options = new InputMapper.Options
            {
                allowAxes = true,
                allowButtons = true,
                allowButtonsOnFullAxisAssignment = true,
                timeout = BindingCaptureTimeoutSeconds,
                checkForConflicts = true,
                checkForConflictsWithAllPlayers = false,
                checkForConflictsWithSelf = true,
                checkForConflictsWithSystemPlayer = true,
                defaultActionWhenConflictFound = InputMapper.ConflictResponse.Replace,
                ignoreMouseXAxis = true,
                ignoreMouseYAxis = true,
                allowKeyboardKeysWithModifiers = true,
                allowKeyboardModifierKeyAsPrimary = true
            }
        };

        mapper.InputMappedEvent += OnBindingMapped;
        mapper.CanceledEvent += OnBindingCanceled;
        mapper.ErrorEvent += OnBindingError;
        mapper.TimedOutEvent += OnBindingTimedOut;
        mapper.ConflictFoundEvent += OnBindingConflictFound;

        _bindingCapture.Attach(entry, mapper);
        _bindingStatus = $"Listening for {entry.DisplayName}. Press a button, key, or move an axis.";

        var context = new InputMapper.Context
        {
            actionId = entry.ActionId,
            controllerMap = entry.ControllerMap,
            actionElementMapToReplace = entry.ActionElementMap,
            actionRange = entry.ActionRange
        };

        if (!mapper.Start(context))
        {
            _bindingCapture.Release();
            _bindingStatus = $"Could not start remapping {entry.DisplayName}.";
        }

        RenderCurrentTab();
    }

    private void ToggleBindingInvert(NativeBindingEntry entry)
    {
        CancelBindingCapture(null);
        if (!entry.CanInvert)
        {
            return;
        }

        if (entry.ActionElementMap == null)
            return;

        entry.ActionElementMap.invert = !entry.ActionElementMap.invert;
        SaveRewiredBindings();
        _bindingEntriesDirty = true;
        _bindingStatus = $"{entry.DisplayName} axis invert {(entry.ActionElementMap.invert ? "enabled" : "disabled")}.";
        RenderCurrentTab();
    }

    private void OnBindingMapped(InputMapper.InputMappedEventData eventData)
    {
        var mappedName = eventData.actionElementMap != null
            ? NativeBindingNames.GetBindingName(eventData.actionElementMap)
            : "input";
        var actionName = eventData.actionElementMap != null
            ? NativeBindingNames.GetActionDisplayName(ReInput.mapping.GetAction(eventData.actionElementMap.actionId), eventData.actionElementMap)
            : _bindingCapture.Active?.DisplayName ?? "Binding";

        _bindingCapture.Release();
        SaveRewiredBindings();
        _bindingEntriesDirty = true;
        _bindingStatus = $"{actionName} mapped to {mappedName}.";
        RenderCurrentTab();
    }

    private void OnBindingCanceled(InputMapper.CanceledEventData eventData)
    {
        var message = string.IsNullOrWhiteSpace(eventData.message) ? "Binding capture canceled." : eventData.message;
        _bindingCapture.Release();
        _bindingStatus = message;
        RenderCurrentTab();
    }

    private void OnBindingError(InputMapper.ErrorEventData eventData)
    {
        var message = string.IsNullOrWhiteSpace(eventData.message) ? "Binding capture failed." : eventData.message;
        _bindingCapture.Release();
        _bindingStatus = message;
        RenderCurrentTab();
    }

    private void OnBindingTimedOut(InputMapper.TimedOutEventData eventData)
    {
        _bindingCapture.Release();
        _bindingStatus = "Binding capture timed out.";
        RenderCurrentTab();
    }

    private void OnBindingConflictFound(InputMapper.ConflictFoundEventData eventData)
    {
        _bindingStatus = "Replacing conflicting binding.";
        eventData.responseCallback(InputMapper.ConflictResponse.Replace);
    }

    private static void SaveRewiredBindings()
    {
        try
        {
            ReInput.userDataStore?.Save();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[NOVR] Native settings failed to save Rewired bindings: {exception}");
        }
    }

}
