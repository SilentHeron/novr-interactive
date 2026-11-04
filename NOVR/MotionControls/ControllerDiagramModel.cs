using System;
using System.Collections.Generic;

namespace NOVR.MotionControls;

// Physical controller locations resolve through the same profile/hand rules as
// runtime input. Selecting a diagram never rewrites handle-relative bindings.
internal static class ControllerDiagramModel
{
    internal static ControllerActionId[] Assigned(bool left, ControllerButton button, ControllerBindingProfile profile,
        bool stickLeft, Func<ControllerActionId, ControllerActionBinding> get)
    {
        var result = new List<ControllerActionId>();
        var frame = profile == ControllerBindingProfile.Frame;
        if (!ControllerBindingDefaults.IsButtonAvailable(profile, left, button)) return result.ToArray();
        foreach (var action in ControllerBindingDefaults.Actions)
        {
            var binding = get(action);
            if (ControllerBindingDefaults.ResolveLeft(binding.Hand, stickLeft) == left &&
                ControllerButtonNames.SamePhysicalButton(binding.Button, button, frame)) result.Add(action);
        }
        return result.ToArray();
    }
    internal static ControllerActionBinding Proposed(ControllerActionBinding current, bool left, ControllerButton button, bool stickLeft)
    {
        // Keep Stick/Throttle semantics when the action is already on this
        // physical hand. A deliberate cross-hand assignment is physical.
        var hand = ControllerBindingDefaults.ResolveLeft(current.Hand, stickLeft) == left ? current.Hand :
            left ? ControllerHandRole.Left : ControllerHandRole.Right;
        return new ControllerActionBinding(button, hand);
    }
    internal static string ShortAction(ControllerActionId action) => action switch
    {
        ControllerActionId.Guns => "Guns", ControllerActionId.WeaponRelease => "Weapon", ControllerActionId.SelectTarget => "Lock",
        ControllerActionId.UnlockTarget => "Unlock", ControllerActionId.ClearTargets => "Clear", ControllerActionId.NextWeapon => "Next W",
        ControllerActionId.Countermeasures => "CM", ControllerActionId.NextCountermeasure => "Next CM", ControllerActionId.Brake => "Brake",
        ControllerActionId.Map => "Map", ControllerActionId.WristMenu => "View", ControllerActionId.Gear => "Gear",
        ControllerActionId.Radar => "Radar", ControllerActionId.FlightAssist => "Assist", ControllerActionId.HoverAssist => "Hover",
        _ => action.ToString()
    };
    internal static string ShortButton(ControllerButton button, bool left, ControllerBindingProfile profile)
    {
        var frame = profile == ControllerBindingProfile.Frame;
        button = ControllerButtonNames.Resolve(button, frame);
        if (button == ControllerButton.StickClick) return "Stick click";
        if (frame && left) return button switch
        {
            ControllerButton.Primary => "D-pad ↓", ControllerButton.Secondary => "D-pad →",
            ControllerButton.Top => "D-pad ↑", ControllerButton.Outside => "D-pad ←",
            _ => ControllerButtonNames.Describe(button, left, frame)
        };
        return ControllerButtonNames.Describe(button, left, frame);
    }
}
