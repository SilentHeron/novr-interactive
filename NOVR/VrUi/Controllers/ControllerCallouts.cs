using System;
using System.Collections.Generic;
using NOVR.MotionControls;

namespace NOVR.VrUi.Controllers;

internal readonly struct ControllerCallout
{
    internal readonly string Part, Title, Detail;
    internal ControllerCallout(string part, string title, string detail)
    {
        Part = part;
        Title = title;
        Detail = detail;
    }
}

// What each physical button on the shown controller does, taken from the
// player's own bindings, and where its label goes. Part names are the ones in
// SteamVR's model descriptions, so a label always points at the real button of
// the controller in the player's hand.
internal static class ControllerCallouts
{
    private static readonly (string arrow, ControllerButton button)[] DpadDirections =
    {
        ("↑", ControllerButton.Top), ("→", ControllerButton.Secondary),
        ("↓", ControllerButton.Primary), ("←", ControllerButton.Outside)
    };

    // The mod's name for a model part, or false for parts that are not a
    // bindable button on this controller (the body, the system button).
    internal static bool TryGetButton(string part, bool frame, bool left, out ControllerButton button)
    {
        button = part switch
        {
            "trigger" => ControllerButton.Trigger,
            "thumbstick" => ControllerButton.StickClick,
            "bumper" when frame => ControllerButton.Shoulder,
            "button_view" when frame && left => ControllerButton.Menu,
            "button_menu" when frame && !left => ControllerButton.Menu,
            "button_a" when !left => ControllerButton.Primary,
            "button_x" when frame && !left => ControllerButton.Secondary,
            "button_y" when frame && !left => ControllerButton.Top,
            "button_b" when frame && !left => ControllerButton.Outside,
            "button_b" when !frame && !left => ControllerButton.Secondary,
            "button_x" when !frame && left => ControllerButton.Primary,
            "button_y" when !frame && left => ControllerButton.Secondary,
            // Touch: the left menu button is the application's; the right one is the system's.
            "button_enter" when !frame && left => ControllerButton.Menu,
            _ => ControllerButton.None
        };
        return button != ControllerButton.None;
    }

    // teleportHand: at aircraft selection, the hand that is not holding the
    // menu pointer walks the player around the aircraft with its thumbstick.
    internal static List<ControllerCallout> Build(IEnumerable<string> parts, bool frame, bool left, bool stickLeft,
        Func<ControllerActionId, ControllerActionBinding> get, bool teleportHand = false)
    {
        var profile = frame ? ControllerBindingProfile.Frame : ControllerBindingProfile.Touch;
        string Actions(ControllerButton button)
        {
            var names = new List<string>();
            foreach (var action in ControllerDiagramModel.Assigned(left, button, profile, stickLeft, get))
                names.Add(ControllerDiagramModel.ShortAction(action));
            return string.Join(" · ", names);
        }
        var result = new List<ControllerCallout>();
        foreach (var part in parts)
        {
            if (part == "button_grip")
            {
                result.Add(new ControllerCallout(part, "Grip", "Hold stick / throttle"));
                continue;
            }
            if (part == "dpad" && frame && left)
            {
                var bound = new List<string>();
                foreach (var (arrow, direction) in DpadDirections)
                {
                    var actions = Actions(direction);
                    if (actions.Length > 0)
                        bound.Add(arrow + " " + actions);
                }
                if (bound.Count > 0)
                    result.Add(new ControllerCallout(part, "D-pad", string.Join("  ", bound)));
                continue;
            }
            if (!TryGetButton(part, frame, left, out var button))
                continue;
            var detail = Actions(button);
            // In the menus the trigger clicks and the thumbstick scrolls,
            // whatever they are bound to in the cockpit.
            if (button == ControllerButton.Trigger)
                detail = detail.Length > 0 ? "Menu click · " + detail : "Menu click";
            else if (button == ControllerButton.StickClick)
            {
                // The walking hand's label is about walking; the rest is on the other hand.
                detail = teleportHand ? "Push, release: TELEPORT\nPull back: return to menu"
                    : detail.Length > 0 ? "Scroll · click: wheel · " + detail : "Scroll · click: wheel";
            }
            if (detail.Length == 0)
                continue;
            var title = button == ControllerButton.Trigger ? "Trigger" : button == ControllerButton.StickClick ? "Thumbstick"
                : ControllerButtonNames.Describe(button, left, frame);
            result.Add(new ControllerCallout(part, title, string.Equals(title, detail, StringComparison.OrdinalIgnoreCase) ? "VR menu" : detail));
        }
        return result;
    }

    // Labels go in two columns beside the controller, each in the order of the
    // buttons it points at, so leader lines never cross within a column and no
    // two labels overlap. Anchors are (across, along) on the label plane.
    internal static (float x, float y)[] Layout(IReadOnlyList<(float x, float y)> anchors, float columnX, float rowHeight)
    {
        var result = new (float x, float y)[anchors.Count];
        if (anchors.Count == 0)
            return result;
        var order = new List<int>();
        for (var i = 0; i < anchors.Count; i++)
            order.Add(i);
        // Split at the median so both columns are used even when every button
        // is on one half of the controller.
        order.Sort((a, b) => anchors[a].x != anchors[b].x ? anchors[a].x.CompareTo(anchors[b].x) : a.CompareTo(b));
        var leftCount = (anchors.Count + 1) / 2;
        for (var side = 0; side < 2; side++)
        {
            var column = order.GetRange(side == 0 ? 0 : leftCount, side == 0 ? leftCount : anchors.Count - leftCount);
            if (column.Count == 0)
                continue;
            column.Sort((a, b) => anchors[a].y != anchors[b].y ? anchors[b].y.CompareTo(anchors[a].y) : a.CompareTo(b));
            var centre = 0f;
            foreach (var index in column)
                centre += anchors[index].y;
            centre /= column.Count;
            for (var row = 0; row < column.Count; row++)
                result[column[row]] = (side == 0 ? -columnX : columnX, centre + ((column.Count - 1) * .5f - row) * rowHeight);
        }
        return result;
    }

    // Labels are shown while the controller is raised and turned toward the
    // face, the way you look at a controller to find a button. Lowered or
    // pointed at the menu, only the model remains.
    internal static bool LookedAt(float normalDotToHead, float distanceMetres) =>
        !float.IsNaN(normalDotToHead) && !float.IsNaN(distanceMetres) && normalDotToHead >= .5f && distanceMetres <= .8f;
}
