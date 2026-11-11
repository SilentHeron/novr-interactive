using System;
using System.Collections.Generic;
using NOVR.MotionControls;

namespace NOVR.VrUi.Controllers;

// Which screen the controllers are shown on. The labels say what the buttons
// do there, and nothing else.
internal enum ControllerHintContext
{
    None, Menus, AircraftSelection, Map
}

// What a hand is pressing right now, as plain flags: enough to light up the
// matching part of the shown controller.
internal struct ControllerPresses
{
    internal bool Trigger, Grip, StickClick, Primary, Secondary, Top, Outside, Menu, Shoulder;
}

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

// What the buttons of the shown controller do on the current screen. In the
// menus that is pointing, selecting and scrolling; aircraft selection adds
// walking around the aircraft, turning and the quick wheel; the full-screen
// map has its two thumbsticks and the button that closes it. Flight bindings
// are not listed: there is nothing to fly on these screens. Part names are the
// ones in SteamVR's model descriptions, so a label always points at the real
// button of the controller in the player's hand.
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

    // Parts of the shown controller that can be pressed and read: the grip,
    // the Frame's D-pad and every bindable button. The body, the status light
    // and the system button are not.
    internal static bool Pressable(string part, bool frame, bool left) =>
        part == "button_grip" || part == "dpad" && frame && left || TryGetButton(part, frame, left, out _);

    // Whether that part is being pressed.
    internal static bool Pressed(string part, bool frame, bool left, in ControllerPresses presses)
    {
        if (part == "button_grip")
            return presses.Grip;
        // The D-pad is one part on the model and four buttons to the game.
        if (part == "dpad" && frame && left)
            return presses.Top || presses.Secondary || presses.Primary || presses.Outside;
        if (!TryGetButton(part, frame, left, out var button))
            return false;
        return button switch
        {
            ControllerButton.Trigger => presses.Trigger,
            ControllerButton.StickClick => presses.StickClick,
            ControllerButton.Primary => presses.Primary,
            ControllerButton.Secondary => presses.Secondary,
            ControllerButton.Top => presses.Top,
            ControllerButton.Outside => presses.Outside,
            ControllerButton.Menu => presses.Menu,
            ControllerButton.Shoulder => presses.Shoulder,
            _ => false
        };
    }

    // pointerHand: this hand's laser is the one driving the menu. Pressing the
    // other hand's trigger takes the pointer over; at aircraft selection the
    // hand without the pointer walks. frame, stickLeft (a left-handed cockpit)
    // and get (the player's bindings) are only needed on the map, to find the
    // button that closes it and which thumbstick zooms.
    internal static List<ControllerCallout> Build(IEnumerable<string> parts, ControllerHintContext context, bool left, bool pointerHand,
        bool frame = false, bool stickLeft = false, Func<ControllerActionId, ControllerActionBinding>? get = null)
    {
        var result = new List<ControllerCallout>();
        if (context == ControllerHintContext.None)
            return result;
        var selection = context == ControllerHintContext.AircraftSelection;
        var map = context == ControllerHintContext.Map;
        var profile = frame ? ControllerBindingProfile.Frame : ControllerBindingProfile.Touch;
        bool ClosesMap(ControllerButton button)
        {
            if (!map || get == null)
                return false;
            foreach (var action in ControllerDiagramModel.Assigned(left, button, profile, stickLeft, get))
                if (action == ControllerActionId.Map)
                    return true;
            return false;
        }
        foreach (var part in parts)
        {
            if (part == "trigger")
            {
                result.Add(new ControllerCallout(part, "Trigger", pointerHand ? "Select" : "Take the pointer"));
                continue;
            }
            if (part == "thumbstick")
            {
                var lines = new List<string>();
                // Same rule as ControllerMapPatch: the stick hand zooms the
                // map, the other hand moves it.
                if (map)
                    lines.Add(left == stickLeft ? "Up / down: zoom map" : "Move map");
                else if (pointerHand)
                    lines.Add("Up / down: scroll");
                else if (selection)
                {
                    lines.Add("Push, release: TELEPORT");
                    lines.Add("Pull back: bring menu here");
                }
                // Snap turn is always the left thumbstick.
                if (selection && left)
                    lines.Add("Left / right: snap turn");
                // The quick wheel exists in a mission, not in the main menus.
                if (selection || map)
                    lines.Add("Click: quick wheel");
                if (lines.Count > 0)
                    result.Add(new ControllerCallout(part, "Thumbstick", string.Join("\n", lines)));
                continue;
            }
            if (!map)
                continue;
            // The one binding that matters on the map: the button that closes it.
            if (part == "dpad" && frame && left)
            {
                foreach (var (arrow, direction) in DpadDirections)
                    if (ClosesMap(direction))
                    {
                        result.Add(new ControllerCallout(part, "D-pad " + arrow, "Close map"));
                        break;
                    }
                continue;
            }
            if (TryGetButton(part, frame, left, out var button) && ClosesMap(button))
                result.Add(new ControllerCallout(part, ControllerButtonNames.Describe(button, left, frame), "Close map"));
        }
        return result;
    }

    // Lines of text in a label, title included.
    internal static int Lines(ControllerCallout callout)
    {
        var lines = 2;
        foreach (var c in callout.Detail)
            if (c == '\n')
                lines++;
        return lines;
    }

    // Labels go in two columns beside the controller, each in the order of the
    // buttons it points at, so leader lines never cross within a column and no
    // two labels overlap. Anchors are (across, along) on the label plane.
    internal static (float x, float y)[] Layout(IReadOnlyList<(float x, float y)> anchors, float columnX, float rowHeight) =>
        Layout(anchors, -columnX, columnX, rowHeight);

    // The same with each column placed on its own: a controller is rarely
    // centred under its thumbstick, and each column has to clear its side.
    internal static (float x, float y)[] Layout(IReadOnlyList<(float x, float y)> anchors, float leftX, float rightX, float rowHeight)
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
                result[column[row]] = (side == 0 ? leftX : rightX, centre + ((column.Count - 1) * .5f - row) * rowHeight);
        }
        return result;
    }

    // Labels are shown while the controller is raised and turned toward the
    // face, the way you look at a controller to find a button. Lowered or
    // pointed at the menu, only the model remains.
    internal static bool LookedAt(float normalDotToHead, float distanceMetres) =>
        !float.IsNaN(normalDotToHead) && !float.IsNaN(distanceMetres) && normalDotToHead >= .5f && distanceMetres <= .8f;
}
