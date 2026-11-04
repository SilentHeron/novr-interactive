using System;
using System.Collections.Generic;
using Rewired;

namespace NOVR.VrUi.Native;

// Reads the native player's maps without editing them. Rows keep the original
// map/element references so capture, clear and invert act on the same bindings.
internal sealed class NativeBindingCatalog
{
    private readonly List<NativeBindingEntry> _entries = new();
    private readonly List<NativeBindingDevice> _devices = new();

    internal IReadOnlyList<NativeBindingEntry> Entries => _entries;
    internal IReadOnlyList<NativeBindingDevice> Devices => _devices;

    internal static NativeBindingCatalog Read(Player player)
    {
        var catalog = new NativeBindingCatalog();
        catalog.AddDeviceBindingEntries(player.controllers.maps.GetMaps<KeyboardMap>(0), "KEYBOARD", "KEYBOARD");
        catalog.AddDeviceBindingEntries(player.controllers.maps.GetMaps<MouseMap>(0), "MOUSE", "MOUSE");

        var joysticks = player.controllers.Joysticks;
        for (var index = 0; index < joysticks.Count; index++)
        {
            var joystick = joysticks[index];
            if (joystick == null)
                continue;

            var label = NativeBindingNames.GetControllerLabel(joystick, $"CONTROLLER {index + 1}");
            catalog.AddDeviceBindingEntries(player.controllers.maps.GetMaps<JoystickMap>(joystick.id), $"JOYSTICK:{joystick.id}", label);
        }

        catalog._entries.Sort(static (left, right) => string.Compare(left.SortKey, right.SortKey, StringComparison.OrdinalIgnoreCase));
        return catalog;
    }

    private void AddDeviceBindingEntries<TMap>(IEnumerable<TMap> maps, string deviceKey, string controllerLabel)
        where TMap : ControllerMap
    {
        var userAssignableMaps = GetUserAssignableMaps(maps);
        var countBefore = _entries.Count;
        AddAssignedBindingEntriesForMaps(userAssignableMaps, deviceKey, NativeBindingNames.ShortenLabel(controllerLabel, 18));
        AddUnassignedBindingEntriesForMaps(userAssignableMaps, deviceKey, NativeBindingNames.ShortenLabel(controllerLabel, 18));
        if (_entries.Count > countBefore)
        {
            AddDevice(deviceKey, controllerLabel);
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
            var categoryName = NativeBindingNames.GetMapCategoryName(controllerMap);
            foreach (var actionElementMap in controllerMap.AllMaps)
            {
                if (actionElementMap == null || !actionElementMap.enabled)
                    continue;

                var action = ReInput.mapping.GetAction(actionElementMap.actionId);
                if (action == null || !action.userAssignable)
                    continue;

                _entries.Add(new NativeBindingEntry(
                    controllerMap,
                    actionElementMap,
                    action,
                    deviceKey,
                    controllerLabel,
                    categoryName,
                    NativeBindingNames.GetActionDisplayName(action, actionElementMap),
                    NativeBindingNames.GetBindingName(actionElementMap),
                    NativeBindingNames.GetBindingKind(actionElementMap),
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

                _entries.Add(new NativeBindingEntry(
                    controllerMap,
                    action,
                    deviceKey,
                    controllerLabel,
                    NativeBindingNames.GetActionCategoryName(action),
                    NativeBindingNames.GetActionDisplayName(action, actionRange),
                    "UNASSIGNED",
                    NativeBindingNames.GetDefaultBindingKind(action, actionRange),
                    actionRange));
            }
        }
    }

    private void AddDevice(string key, string label)
    {
        for (var index = 0; index < _devices.Count; index++)
        {
            if (_devices[index].Key == key)
            {
                return;
            }
        }

        _devices.Add(new NativeBindingDevice(key, label));
    }

    private static bool IsUserAssignableMap(ControllerMap controllerMap)
    {
        var mapCategory = ReInput.mapping.GetMapCategory(controllerMap.categoryId);
        return mapCategory == null || mapCategory.userAssignable;
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

    internal static AxisRange GetActionRange(ActionElementMap actionElementMap)
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
}
