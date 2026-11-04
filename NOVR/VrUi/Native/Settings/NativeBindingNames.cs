using System;
using Rewired;
using UnityEngine;

namespace NOVR.VrUi.Native;

// Native labels and fallbacks shared by catalog rows and mapper completion messages.
internal static class NativeBindingNames
{
    internal static string GetMapCategoryName(ControllerMap controllerMap)
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

    internal static string GetActionCategoryName(InputAction action)
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

    internal static string GetActionDisplayName(InputAction action)
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

    internal static string GetActionDisplayName(InputAction action, AxisRange actionRange)
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

    internal static string GetActionDisplayName(InputAction? action, ActionElementMap actionElementMap)
    {
        if (!string.IsNullOrWhiteSpace(actionElementMap.actionDescriptiveName))
        {
            return actionElementMap.actionDescriptiveName;
        }

        if (action != null)
        {
            if (action.type == InputActionType.Axis)
            {
                var actionRange = NativeBindingCatalog.GetActionRange(actionElementMap);
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

    internal static string GetBindingName(ActionElementMap actionElementMap)
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

    internal static string GetBindingKind(ActionElementMap actionElementMap)
    {
        if (actionElementMap.elementType != ControllerElementType.Axis)
        {
            return "BTN";
        }

        var actionRange = NativeBindingCatalog.GetActionRange(actionElementMap);
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

    internal static string GetDefaultBindingKind(InputAction action, AxisRange actionRange)
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

    internal static string GetControllerLabel(Controller controller, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(controller.name))
        {
            return controller.name;
        }

        return fallback;
    }

    internal static string ShortenLabel(string label, int maxLength)
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
}
