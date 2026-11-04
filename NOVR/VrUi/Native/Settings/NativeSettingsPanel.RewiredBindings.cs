using System;
using System.Collections.Generic;
using Rewired;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

// Rewired binding catalog, edits and mapping callbacks. The settings view
// owns messages and persistence; NativeBindingCaptureSession owns delayed starts
// and global mapper lifetime. This does not change NOVR controller bindings.
public partial class NativeSettingsPanel
{
    private const float BindingCaptureDelaySeconds = 0.25f;
    private const float BindingCaptureTimeoutSeconds = 8f;
    private readonly List<BindingEntry> _bindingEntries = new();
    private readonly List<BindingDeviceFilter> _bindingDeviceFilters = new();
    private readonly NativeBindingCaptureSession<BindingEntry> _bindingCapture = new();
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

            AddDeviceBindingEntries(GameManager.playerInput.controllers.maps.GetMaps<KeyboardMap>(0), "KEYBOARD", "KEYBOARD");
            AddDeviceBindingEntries(GameManager.playerInput.controllers.maps.GetMaps<MouseMap>(0), "MOUSE", "MOUSE");

            var joysticks = GameManager.playerInput.controllers.Joysticks;
            for (var index = 0; index < joysticks.Count; index++)
            {
                var joystick = joysticks[index];
                if (joystick == null)
                    continue;

                var label = GetControllerLabel(joystick, $"CONTROLLER {index + 1}");
                AddDeviceBindingEntries(GameManager.playerInput.controllers.maps.GetMaps<JoystickMap>(joystick.id), $"JOYSTICK:{joystick.id}", label);
            }

            _bindingEntries.Sort(static (left, right) => string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase));
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

    private void AddDeviceBindingEntries<TMap>(IEnumerable<TMap> maps, string deviceKey, string controllerLabel)
        where TMap : ControllerMap
    {
        var userAssignableMaps = GetUserAssignableMaps(maps);
        var countBefore = _bindingEntries.Count;
        AddAssignedBindingEntriesForMaps(userAssignableMaps, deviceKey, ShortenLabel(controllerLabel, 18));
        AddUnassignedBindingEntriesForMaps(userAssignableMaps, deviceKey, ShortenLabel(controllerLabel, 18));
        if (_bindingEntries.Count > countBefore)
        {
            AddBindingDeviceFilter(deviceKey, controllerLabel);
        }
    }

    private static List<ControllerMap> GetUserAssignableMaps<TMap>(IEnumerable<TMap> maps)
        where TMap : ControllerMap
    {
        var userAssignableMaps = new List<ControllerMap>();
        foreach (var controllerMap in maps)
        {
            if (controllerMap == null || !IsUserAssignableMap(controllerMap))
                continue;
            userAssignableMaps.Add(controllerMap);
        }

        return userAssignableMaps;
    }

    private void AddAssignedBindingEntriesForMaps(IList<ControllerMap> maps, string deviceKey, string controllerLabel)
    {
        foreach (var controllerMap in maps)
        {
            var categoryName = GetMapCategoryName(controllerMap);
            foreach (var actionElementMap in controllerMap.AllMaps)
            {
                if (actionElementMap == null || !actionElementMap.enabled)
                    continue;

                var action = ReInput.mapping.GetAction(actionElementMap.actionId);
                if (action == null || !action.userAssignable)
                    continue;

                _bindingEntries.Add(new BindingEntry(
                    controllerMap,
                    actionElementMap,
                    action,
                    deviceKey,
                    controllerLabel,
                    categoryName,
                    GetActionDisplayName(action, actionElementMap),
                    GetBindingName(actionElementMap),
                    GetBindingKind(actionElementMap),
                    GetActionRange(actionElementMap)));
            }
        }
    }

    private void AddUnassignedBindingEntriesForMaps(IList<ControllerMap> maps, string deviceKey, string controllerLabel)
    {
        if (maps.Count == 0)
            return;

        var assignedActionSlots = new HashSet<string>();
        var preferredMapByActionCategory = new Dictionary<int, ControllerMap>();
        var fallbackMap = maps[0];

        for (var mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            var controllerMap = maps[mapIndex];
            foreach (var actionElementMap in controllerMap.AllMaps)
            {
                if (actionElementMap == null)
                    continue;

                var mappedAction = ReInput.mapping.GetAction(actionElementMap.actionId);
                if (mappedAction == null || !mappedAction.userAssignable)
                    continue;

                if (actionElementMap.enabled)
                {
                    var assignedActionRange = mappedAction.type == InputActionType.Axis
                        ? GetActionRange(actionElementMap)
                        : AxisRange.Positive;
                    AddAssignedActionSlot(assignedActionSlots, mappedAction.id, assignedActionRange);
                }

                if (!preferredMapByActionCategory.ContainsKey(mappedAction.categoryId))
                {
                    preferredMapByActionCategory.Add(mappedAction.categoryId, controllerMap);
                }
            }
        }

        foreach (var action in ReInput.mapping.UserAssignableActions)
        {
            if (action == null || !action.userAssignable)
                continue;

            if (!preferredMapByActionCategory.TryGetValue(action.categoryId, out var controllerMap))
            {
                controllerMap = fallbackMap;
            }

            var actionRanges = GetAssignableActionRanges(action);
            for (var index = 0; index < actionRanges.Count; index++)
            {
                var actionRange = actionRanges[index];
                if (IsActionRangeAssigned(assignedActionSlots, action.id, actionRange))
                    continue;

                // Do not offer a whole axis while a half (e.g. a button for pitch up) is already bound.
                if (actionRange == AxisRange.Full && action.type == InputActionType.Axis &&
                    (assignedActionSlots.Contains(GetBindingActionSlotKey(action.id, AxisRange.Negative)) ||
                     assignedActionSlots.Contains(GetBindingActionSlotKey(action.id, AxisRange.Positive))))
                {
                    continue;
                }

                _bindingEntries.Add(new BindingEntry(
                    controllerMap,
                    action,
                    deviceKey,
                    controllerLabel,
                    GetActionCategoryName(action),
                    GetActionDisplayName(action, actionRange),
                    "UNASSIGNED",
                    GetDefaultBindingKind(action, actionRange),
                    actionRange));
            }
        }
    }

    private void AddBindingDeviceFilter(string key, string label)
    {
        for (var index = 0; index < _bindingDeviceFilters.Count; index++)
        {
            if (_bindingDeviceFilters[index].Key == key)
            {
                return;
            }
        }

        _bindingDeviceFilters.Add(new BindingDeviceFilter(key, label));
    }

    private void QueueBindingCapture(BindingEntry entry)
    {
        _bindingCapture.Queue(entry, Time.unscaledTime + BindingCaptureDelaySeconds);
        _bindingStatus = $"Release the mouse, then press a new input or move an axis for {entry.DisplayName}.";
        RenderCurrentTab();
    }

    private void ClearBinding(BindingEntry entry)
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

    private void StartBindingCapture(BindingEntry entry)
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

    private void ToggleBindingInvert(BindingEntry entry)
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
            ? GetBindingName(eventData.actionElementMap)
            : "input";
        var actionName = eventData.actionElementMap != null
            ? GetActionDisplayName(ReInput.mapping.GetAction(eventData.actionElementMap.actionId), eventData.actionElementMap)
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

    private static bool IsUserAssignableMap(ControllerMap controllerMap)
    {
        var mapCategory = ReInput.mapping.GetMapCategory(controllerMap.categoryId);
        return mapCategory == null || mapCategory.userAssignable;
    }

    private static string GetMapCategoryName(ControllerMap controllerMap)
    {
        var mapCategory = ReInput.mapping.GetMapCategory(controllerMap.categoryId);
        if (mapCategory != null)
        {
            if (!string.IsNullOrWhiteSpace(mapCategory.descriptiveName))
            {
                return mapCategory.descriptiveName;
            }

            if (!string.IsNullOrWhiteSpace(mapCategory.name))
            {
                return mapCategory.name;
            }
        }

        return string.IsNullOrWhiteSpace(controllerMap.name) ? "General" : controllerMap.name;
    }

    private static string GetActionCategoryName(InputAction action)
    {
        var actionCategory = ReInput.mapping.GetActionCategory(action.categoryId);
        if (actionCategory != null)
        {
            if (!string.IsNullOrWhiteSpace(actionCategory.descriptiveName))
            {
                return actionCategory.descriptiveName;
            }

            if (!string.IsNullOrWhiteSpace(actionCategory.name))
            {
                return actionCategory.name;
            }
        }

        return "General";
    }

    private static string GetActionDisplayName(InputAction action)
    {
        if (!string.IsNullOrWhiteSpace(action.descriptiveName))
        {
            return action.descriptiveName;
        }

        if (!string.IsNullOrWhiteSpace(action.name))
        {
            return action.name;
        }

        return $"Action {action.id}";
    }

    private static string GetActionDisplayName(InputAction action, AxisRange actionRange)
    {
        if (action.type == InputActionType.Axis)
        {
            if (actionRange == AxisRange.Negative && !string.IsNullOrWhiteSpace(action.negativeDescriptiveName))
            {
                return action.negativeDescriptiveName;
            }

            if (actionRange == AxisRange.Positive && !string.IsNullOrWhiteSpace(action.positiveDescriptiveName))
            {
                return action.positiveDescriptiveName;
            }
        }

        return GetActionDisplayName(action);
    }

    private static string GetActionDisplayName(InputAction? action, ActionElementMap actionElementMap)
    {
        if (!string.IsNullOrWhiteSpace(actionElementMap.actionDescriptiveName))
        {
            return actionElementMap.actionDescriptiveName;
        }

        if (action != null)
        {
            if (action.type == InputActionType.Axis)
            {
                var actionRange = GetActionRange(actionElementMap);
                if (actionRange == AxisRange.Negative && !string.IsNullOrWhiteSpace(action.negativeDescriptiveName))
                {
                    return action.negativeDescriptiveName;
                }

                if (actionRange == AxisRange.Positive && !string.IsNullOrWhiteSpace(action.positiveDescriptiveName))
                {
                    return action.positiveDescriptiveName;
                }
            }

            if (!string.IsNullOrWhiteSpace(action.descriptiveName))
            {
                return action.descriptiveName;
            }

            if (!string.IsNullOrWhiteSpace(action.name))
            {
                return action.name;
            }
        }

        return $"Action {actionElementMap.actionId}";
    }

    private static string GetBindingName(ActionElementMap actionElementMap)
    {
        if (!string.IsNullOrWhiteSpace(actionElementMap.elementIdentifierName))
        {
            return actionElementMap.elementIdentifierName;
        }

        var keyCode = actionElementMap.keyCode;
        if (keyCode != KeyCode.None)
        {
            return keyCode.ToString();
        }

        return $"Element {actionElementMap.elementIdentifierId}";
    }

    private static string GetBindingKind(ActionElementMap actionElementMap)
    {
        if (actionElementMap.elementType != ControllerElementType.Axis)
        {
            return "BTN";
        }

        var actionRange = GetActionRange(actionElementMap);
        if (actionRange == AxisRange.Negative)
        {
            return "AXIS -";
        }

        if (actionRange == AxisRange.Positive && actionElementMap.axisRange != AxisRange.Full)
        {
            return "AXIS +";
        }

        return actionElementMap.invert ? "AXIS INV" : "AXIS";
    }

    private static string GetDefaultBindingKind(InputAction action, AxisRange actionRange)
    {
        if (action.type != InputActionType.Axis)
        {
            return "BTN";
        }

        return actionRange switch
        {
            AxisRange.Negative => "AXIS -",
            AxisRange.Positive => "AXIS +",
            _ => "AXIS"
        };
    }

    private static List<AxisRange> GetAssignableActionRanges(InputAction action)
    {
        var ranges = new List<AxisRange>();
        if (action.type != InputActionType.Axis)
        {
            ranges.Add(AxisRange.Positive);
            return ranges;
        }

        var hasNegative = !string.IsNullOrWhiteSpace(action.negativeDescriptiveName);
        var hasPositive = !string.IsNullOrWhiteSpace(action.positiveDescriptiveName);

        if (hasNegative || hasPositive)
        {
            // Split axes (e.g. pitch up/down) can also take a whole stick axis, which is how the defaults bind them.
            ranges.Add(AxisRange.Full);

            if (hasNegative)
            {
                ranges.Add(AxisRange.Negative);
            }

            if (hasPositive)
            {
                ranges.Add(AxisRange.Positive);
            }

            return ranges;
        }

        ranges.Add(AxisRange.Full);
        return ranges;
    }

    private static void AddAssignedActionSlot(HashSet<string> assignedActionSlots, int actionId, AxisRange actionRange)
    {
        assignedActionSlots.Add(GetBindingActionSlotKey(actionId, actionRange));
        if (actionRange != AxisRange.Full)
            return;

        assignedActionSlots.Add(GetBindingActionSlotKey(actionId, AxisRange.Negative));
        assignedActionSlots.Add(GetBindingActionSlotKey(actionId, AxisRange.Positive));
    }

    private static bool IsActionRangeAssigned(HashSet<string> assignedActionSlots, int actionId, AxisRange actionRange)
    {
        return assignedActionSlots.Contains(GetBindingActionSlotKey(actionId, actionRange))
            || assignedActionSlots.Contains(GetBindingActionSlotKey(actionId, AxisRange.Full));
    }

    private static string GetBindingActionSlotKey(int actionId, AxisRange actionRange)
    {
        return $"{actionId}:{actionRange}";
    }

    private static AxisRange GetActionRange(ActionElementMap actionElementMap)
    {
        // For buttons and keys axisRange is not the direction (it is stored as Full or Positive either way);
        // the direction is axisContribution. Trusting axisRange marked the wrong half, or both, as taken.
        if (actionElementMap.elementType != ControllerElementType.Axis)
        {
            return actionElementMap.axisContribution == Pole.Negative ? AxisRange.Negative : AxisRange.Positive;
        }

        if (actionElementMap.axisRange == AxisRange.Full)
        {
            return AxisRange.Full;
        }

        if (actionElementMap.axisRange == AxisRange.Positive || actionElementMap.axisRange == AxisRange.Negative)
        {
            return actionElementMap.axisRange;
        }

        if (actionElementMap.axisContribution == Pole.Negative)
        {
            return AxisRange.Negative;
        }

        if (actionElementMap.axisContribution == Pole.Positive)
        {
            return AxisRange.Positive;
        }

        return actionElementMap.axisRange;
    }

    private static string GetControllerLabel(Controller controller, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(controller.name))
        {
            return controller.name;
        }

        return fallback;
    }

    private static string ShortenLabel(string label, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }

        var trimmed = label.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed.Substring(0, Mathf.Max(0, maxLength - 3)) + "...";
    }

    private sealed class BindingEntry
    {
        public BindingEntry(
            ControllerMap controllerMap,
            ActionElementMap actionElementMap,
            InputAction action,
            string deviceKey,
            string controllerLabel,
            string categoryName,
            string displayName,
            string bindingName,
            string bindingKind,
            AxisRange actionRange)
        {
            ControllerMap = controllerMap;
            ActionElementMap = actionElementMap;
            ActionId = action.id;
            ActionType = action.type;
            DeviceKey = deviceKey;
            ControllerLabel = controllerLabel;
            CategoryName = categoryName;
            DisplayName = displayName;
            BindingName = bindingName;
            BindingKind = bindingKind;
            ActionRange = actionRange;
            IsAssigned = true;
            SortKey = $"{DeviceKey}|{CategoryName}|{DisplayName}|{ControllerLabel}|{BindingName}";
            Key = $"{DeviceKey}-{ActionId}-{ActionElementMap.id}";
        }

        public BindingEntry(
            ControllerMap controllerMap,
            InputAction action,
            string deviceKey,
            string controllerLabel,
            string categoryName,
            string displayName,
            string bindingName,
            string bindingKind,
            AxisRange actionRange)
        {
            ControllerMap = controllerMap;
            ActionId = action.id;
            ActionType = action.type;
            DeviceKey = deviceKey;
            ControllerLabel = controllerLabel;
            CategoryName = categoryName;
            DisplayName = displayName;
            BindingName = bindingName;
            BindingKind = bindingKind;
            ActionRange = actionRange;
            IsAssigned = false;
            SortKey = $"{DeviceKey}|{CategoryName}|{DisplayName}|{ControllerLabel}|{BindingName}";
            Key = $"{DeviceKey}-{ActionId}-Unassigned-{ActionRange}-{ControllerMap.id}";
        }

        public ControllerMap ControllerMap
        {
            get;
        }
        public ActionElementMap? ActionElementMap
        {
            get;
        }
        public int ActionId
        {
            get;
        }
        public InputActionType ActionType
        {
            get;
        }
        public string DeviceKey
        {
            get;
        }
        public string ControllerLabel
        {
            get;
        }
        public string CategoryName
        {
            get;
        }
        public string DisplayName
        {
            get;
        }
        public string BindingName
        {
            get;
        }
        public string BindingKind
        {
            get;
        }
        public AxisRange ActionRange
        {
            get;
        }
        public bool IsAssigned
        {
            get;
        }
        public string SortKey
        {
            get;
        }
        public string Key
        {
            get;
        }
        public bool CanInvert => ActionElementMap != null && ActionElementMap.elementType == ControllerElementType.Axis && ActionElementMap.axisRange == AxisRange.Full;
        public bool CanShowAxisValue => IsAssigned && ActionType == InputActionType.Axis;
        public bool IsInverted => ActionElementMap != null && ActionElementMap.invert;
    }

    private readonly struct BindingDeviceFilter
    {
        public BindingDeviceFilter(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key
        {
            get;
        }
        public string Label
        {
            get;
        }
    }
}
