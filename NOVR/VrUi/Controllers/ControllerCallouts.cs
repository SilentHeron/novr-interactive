using System;
using System.Collections.Generic;

namespace NOVR.VrUi.Controllers;

// Which screen the controllers are shown on. The labels say what the buttons
// do there, and nothing else.
internal enum ControllerHintContext
{
    None, Menus, AircraftSelection
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
// walking around the aircraft, turning and the quick wheel. Flight bindings
// are not listed: there is nothing to fly on these screens. Part names are the
// ones in SteamVR's model descriptions, so a label always points at the real
// button of the controller in the player's hand.
internal static class ControllerCallouts
{
    // pointerHand: this hand's laser is the one driving the menu. Pressing the
    // other hand's trigger takes the pointer over; at aircraft selection the
    // hand without the pointer walks.
    internal static List<ControllerCallout> Build(IEnumerable<string> parts, ControllerHintContext context, bool left, bool pointerHand)
    {
        var result = new List<ControllerCallout>();
        if (context == ControllerHintContext.None)
            return result;
        var selection = context == ControllerHintContext.AircraftSelection;
        foreach (var part in parts)
        {
            if (part == "trigger")
                result.Add(new ControllerCallout(part, "Trigger", pointerHand ? "Select" : "Take the pointer"));
            if (part != "thumbstick")
                continue;
            var lines = new List<string>();
            if (pointerHand)
                lines.Add("Up / down: scroll");
            else if (selection)
            {
                lines.Add("Push, release: TELEPORT");
                lines.Add("Pull back: bring menu here");
            }
            if (selection)
            {
                // Snap turn is always the left thumbstick.
                if (left)
                    lines.Add("Left / right: snap turn");
                // The quick wheel exists in a mission, not in the main menus.
                lines.Add("Click: quick wheel");
            }
            if (lines.Count > 0)
                result.Add(new ControllerCallout(part, "Thumbstick", string.Join("\n", lines)));
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
