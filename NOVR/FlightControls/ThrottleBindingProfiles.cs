namespace NOVR.MotionControls;

internal enum ThrottleBindingLayout { CountermeasureTrigger, BrakeTrigger, Custom, VtolStyle }

internal readonly struct ThrottleButtons
{
    internal readonly ControllerButton Brake, Countermeasures, Target, NextCountermeasure;
    internal ThrottleButtons(ControllerButton brake, ControllerButton countermeasures, ControllerButton target, ControllerButton next)
    { Brake = brake; Countermeasures = countermeasures; Target = target; NextCountermeasure = next; }
}

internal static class ThrottleBindingProfiles
{
    internal static ThrottleButtons Get(ThrottleBindingLayout layout) => layout switch
    {
        ThrottleBindingLayout.VtolStyle => new(ControllerButton.Trigger, ControllerButton.Primary,
            ControllerButton.None, ControllerButton.Secondary),
        ThrottleBindingLayout.CountermeasureTrigger => new(ControllerButton.PrimaryOrStickClick, ControllerButton.Trigger,
            ControllerButton.ShoulderOrPrimary, ControllerButton.Secondary),
        ThrottleBindingLayout.BrakeTrigger => new(ControllerButton.Trigger, ControllerButton.ShoulderOrPrimary,
            ControllerButton.PrimaryOrSecondary, ControllerButton.SecondaryOrStickClick),
        _ => throw new System.ArgumentException("Custom bindings do not have preset values.")
    };
    internal static ThrottleBindingLayout Identify(ThrottleButtons buttons)
    {
        foreach (var layout in new[] { ThrottleBindingLayout.VtolStyle, ThrottleBindingLayout.CountermeasureTrigger, ThrottleBindingLayout.BrakeTrigger })
        {
            var preset = Get(layout);
            if (buttons.Brake == preset.Brake && buttons.Countermeasures == preset.Countermeasures &&
                buttons.Target == preset.Target && buttons.NextCountermeasure == preset.NextCountermeasure) return layout;
        }
        return ThrottleBindingLayout.Custom;
    }
    internal static bool SharesButton(ControllerButton candidate, bool frame, params ControllerButton[] existing)
    {
        var resolved = ControllerButtonNames.Resolve(candidate, frame);
        if (resolved == ControllerButton.None) return false;
        foreach (var button in existing)
            if (ControllerButtonNames.Resolve(button, frame) == resolved) return true;
        return false;
    }
    internal static bool Conflicts(ThrottleButtons buttons, bool frame) =>
        SharesButton(buttons.Brake, frame, buttons.Countermeasures, buttons.Target, buttons.NextCountermeasure) ||
        SharesButton(buttons.Countermeasures, frame, buttons.Target, buttons.NextCountermeasure) ||
        SharesButton(buttons.Target, frame, buttons.NextCountermeasure);
}
