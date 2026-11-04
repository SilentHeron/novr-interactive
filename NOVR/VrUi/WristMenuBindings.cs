using System;
using NOVR.MotionControls;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private ControllerBindingPanel? _bindingPanel;
    private int _bindingVersion = -1;
    private static readonly ControllerActionId[] BindingActions = (ControllerActionId[])Enum.GetValues(typeof(ControllerActionId));
    private static ControllerBindings Bindings => ModConfiguration.Instance.ControllerBindings;
    private static bool StickLeft => ModConfiguration.Instance.MotionControls.LeftHanded.Value;
    private void CreateBindingPages()
    {
        _bindingsPage = Page("CONTROLLER BINDINGS");
        _bindingPanel = new ControllerBindingPanel(_bindingsPage.transform, _font!, Back);
    }
    private void RefreshBindingEditor() => _bindingPanel?.Refresh();
    private static string DescribeBinding(ControllerActionBinding binding)
    {
        var left = ControllerBindings.ResolveLeft(binding.Hand, StickLeft);
        var text = ControllerButtonNames.Describe(binding.Button, left, Bindings.ButtonProfile(left) == ControllerBindingProfile.Frame);
        return binding.Button == ControllerButton.None ? "Unassigned" : (left ? "L · " : "R · ") + text;
    }

    private string HandHelp(bool stick)
    {
        var left = stick ? StickLeft : !StickLeft;
        var text = (stick ? "STICK" : CollectiveControls ? "COLLECTIVE" : "THROTTLE") + (left ? " · LEFT\n\n" : " · RIGHT\n\n");
        var count = 0;
        foreach (var action in BindingActions)
        {
            var binding = Bindings.Get(action);
            if (binding.Button == ControllerButton.None || action == ControllerActionId.Map || action == ControllerActionId.WristMenu ||
                ControllerBindings.ResolveLeft(binding.Hand, StickLeft) != left) continue;
            if (++count > 7) { text += "More: Controller bindings"; break; }
            text += ControllerBindings.ActionLabel(action) + ": " +
                ControllerButtonNames.Describe(binding.Button, left, Bindings.ButtonProfile(left) == ControllerBindingProfile.Frame) + "\n\n";
        }
        return text.TrimEnd();
    }
}
