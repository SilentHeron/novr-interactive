using Rewired;

namespace NOVR.VrUi.Native;

internal sealed class NativeBindingEntry
{
    public NativeBindingEntry(
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

    public NativeBindingEntry(
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

internal readonly struct NativeBindingDevice
{
    public NativeBindingDevice(string key, string label)
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
