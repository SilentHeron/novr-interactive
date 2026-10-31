using System;

namespace NOVR.MotionControls;

internal enum ControllerBindingProfile { Auto, Frame, Touch }
internal enum ControllerHandRole { Stick, Throttle, Left, Right }
internal enum ControllerActionId
{
    Guns, WeaponRelease, SelectTarget, UnlockTarget, ClearTargets, NextWeapon,
    Countermeasures, NextCountermeasure, Brake, Map, WristMenu, Zoom,
    Gear, Radar, FlightAssist, HoverAssist, Eots
}

internal readonly struct ControllerActionBinding
{
    internal readonly ControllerButton Button;
    internal readonly ControllerHandRole Hand;
    internal ControllerActionBinding(ControllerButton button, ControllerHandRole hand) { Button = button; Hand = hand; }
}

// Hardware-independent preset and collision rules, shared by the editor and
// runtime. These never change a saved profile just because a headset connects.
internal static class ControllerBindingDefaults
{
    internal static readonly ControllerActionId[] Actions = (ControllerActionId[])Enum.GetValues(typeof(ControllerActionId));
    private static readonly ControllerButton[] FrameButtons = { ControllerButton.None, ControllerButton.Trigger, ControllerButton.Primary,
        ControllerButton.Secondary, ControllerButton.Top, ControllerButton.Outside, ControllerButton.StickClick, ControllerButton.Shoulder, ControllerButton.Menu };
    private static readonly ControllerButton[] TouchLeftButtons = { ControllerButton.None, ControllerButton.Trigger, ControllerButton.Primary,
        ControllerButton.Secondary, ControllerButton.StickClick, ControllerButton.Menu };
    private static readonly ControllerButton[] TouchRightButtons = { ControllerButton.None, ControllerButton.Trigger, ControllerButton.Primary,
        ControllerButton.Secondary, ControllerButton.StickClick };

    internal static ControllerActionBinding Get(ControllerBindingProfile profile, ControllerActionId action)
    {
        var touch = profile == ControllerBindingProfile.Touch;
        return action switch
        {
            ControllerActionId.Guns => new(ControllerButton.Trigger, ControllerHandRole.Stick),
            ControllerActionId.WeaponRelease => new(ControllerButton.Primary, ControllerHandRole.Stick),
            ControllerActionId.SelectTarget => new(ControllerButton.StickClick, ControllerHandRole.Stick),
            ControllerActionId.UnlockTarget => new(touch ? ControllerButton.Secondary : ControllerButton.OutsideOrNone, ControllerHandRole.Stick),
            ControllerActionId.ClearTargets => new(ControllerButton.None, ControllerHandRole.Stick),
            ControllerActionId.NextWeapon => new(touch ? ControllerButton.Secondary : ControllerButton.TopOrSecondary,
                touch ? ControllerHandRole.Throttle : ControllerHandRole.Stick),
            ControllerActionId.Countermeasures => new(ControllerButton.Primary, ControllerHandRole.Throttle),
            ControllerActionId.NextCountermeasure => new(touch ? ControllerButton.None : ControllerButton.Secondary, ControllerHandRole.Throttle),
            ControllerActionId.Brake => new(ControllerButton.Trigger, ControllerHandRole.Throttle),
            ControllerActionId.Map => new(ControllerButton.OutsideOrSecondary, ControllerHandRole.Stick),
            ControllerActionId.WristMenu => new(ControllerButton.Menu, ControllerHandRole.Left),
            ControllerActionId.Zoom => new(touch ? ControllerButton.StickClick : ControllerButton.Shoulder,
                touch ? ControllerHandRole.Throttle : ControllerHandRole.Right),
            _ => new(ControllerButton.None, ControllerHandRole.Throttle)
        };
    }

    internal static bool ResolveLeft(ControllerHandRole role, bool leftHanded) => role switch
    { ControllerHandRole.Left => true, ControllerHandRole.Right => false, ControllerHandRole.Stick => leftHanded, _ => !leftHanded };
    internal static bool RequiresGrip(ControllerActionId action) => action != ControllerActionId.Map &&
        action != ControllerActionId.WristMenu && action != ControllerActionId.Zoom;
    internal static bool ShareContext(ControllerActionId first, ControllerActionId second) =>
        !(first == ControllerActionId.Map && RequiresGrip(second) || second == ControllerActionId.Map && RequiresGrip(first));
    internal static bool Conflict(ControllerActionId first, ControllerActionBinding a, ControllerActionId second,
        ControllerActionBinding b, bool leftHanded, bool frame) => first != second && ShareContext(first, second) &&
        ResolveLeft(a.Hand, leftHanded) == ResolveLeft(b.Hand, leftHanded) && ControllerButtonNames.SamePhysicalButton(a.Button, b.Button, frame);

    internal static ControllerButton[] AvailableButtons(ControllerBindingProfile profile, bool left) =>
        (ControllerButton[])(profile == ControllerBindingProfile.Frame ? FrameButtons : left ? TouchLeftButtons : TouchRightButtons).Clone();
    internal static bool IsButtonAvailable(ControllerBindingProfile profile, bool left, ControllerButton button)
    {
        var resolved = ControllerButtonNames.Resolve(button, profile == ControllerBindingProfile.Frame);
        var buttons = profile == ControllerBindingProfile.Frame ? FrameButtons : left ? TouchLeftButtons : TouchRightButtons;
        return Array.IndexOf(buttons, resolved) >= 0;
    }
    internal static bool IsBindingAvailable(ControllerBindingProfile profile, bool left, ControllerActionId action, ControllerButton button) =>
        IsButtonAvailable(profile, left, button) && !((action == ControllerActionId.Map || action == ControllerActionId.WristMenu) &&
            ControllerButtonNames.Resolve(button, profile == ControllerBindingProfile.Frame) == ControllerButton.Trigger);
    internal static string ActionLabel(ControllerActionId action) => action switch
    {
        ControllerActionId.Guns => "Guns", ControllerActionId.WeaponRelease => "Missile / weapon release",
        ControllerActionId.SelectTarget => "Select target", ControllerActionId.UnlockTarget => "Unselect latest target",
        ControllerActionId.ClearTargets => "Clear all targets", ControllerActionId.NextWeapon => "Next weapon",
        ControllerActionId.Countermeasures => "Countermeasures", ControllerActionId.NextCountermeasure => "Next CM type",
        ControllerActionId.Brake => "Wheel / air brake", ControllerActionId.Map => "Map / respawn",
        ControllerActionId.WristMenu => "View menu", ControllerActionId.Zoom => "Inspection zoom",
        ControllerActionId.Gear => "Landing gear", ControllerActionId.Radar => "Radar",
        ControllerActionId.FlightAssist => "Flight assist", ControllerActionId.HoverAssist => "Hover assist",
        ControllerActionId.Eots => "EOTS HUD", _ => action.ToString()
    };
    internal static string ContextLabel(ControllerActionId action) => action switch
    {
        ControllerActionId.Map => "Free hand or recovery view",
        ControllerActionId.WristMenu => "Either grip state",
        ControllerActionId.Zoom => "Hold; either grip state",
        ControllerActionId.Guns or ControllerActionId.WeaponRelease or ControllerActionId.Countermeasures or ControllerActionId.Brake => "Hold while gripping the assigned handle",
        _ => "Release while gripping the assigned handle"
    };
}
