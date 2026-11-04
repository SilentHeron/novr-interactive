using System;
using System.Collections.Generic;

namespace NOVR.VrUi;

internal enum ViewMenuDestination
{
    FlightActions, FlightControls, Bindings, EditLayout, Seat, Profiles, Recenter,
    LookCue, Hud, Widgets, Hands, Feedback, Performance, MenuOptions
}

// The displayed home screen consumes this layout directly. Common controls are
// destinations, never another generic settings hub. The two commands retain
// their existing layout-preview and recenter behavior.
internal static class WristMenuNavigation
{
    internal const float PanelWidth = 660, PanelHeight = 660;
    internal readonly struct ButtonRect
    {
        internal readonly float X, Y, Width, Height;
        internal ButtonRect(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
    internal static ButtonRect ButtonBounds(int column, int row, int columns = 2)
    {
        var width = columns == 3 ? 196f : 300f;
        return new ButtonRect((column - (columns - 1) * .5f) * (width + 16), 177 - row * 57, width, 49);
    }
    internal readonly struct Item
    {
        internal readonly string Label;
        internal readonly ViewMenuDestination Destination;
        internal readonly int Column, Row;
        internal Item(string label, ViewMenuDestination destination, int column, int row)
        {
            Label = label;
            Destination = destination;
            Column = column;
            Row = row;
        }
        internal bool OpensPage => Destination != ViewMenuDestination.EditLayout && Destination != ViewMenuDestination.Recenter;
    }

    internal static readonly IReadOnlyList<Item> Home = Array.AsReadOnly(new[]
    {
        new Item("Flight controls", ViewMenuDestination.FlightControls, 0, 0),
        new Item("Controller bindings", ViewMenuDestination.Bindings, 0, 1),
        new Item("Flight actions", ViewMenuDestination.FlightActions, 0, 2),
        new Item("Edit layout", ViewMenuDestination.EditLayout, 0, 3),
        new Item("Seat & handles", ViewMenuDestination.Seat, 0, 4),
        new Item("Aircraft profiles", ViewMenuDestination.Profiles, 0, 5),
        new Item("Recenter now", ViewMenuDestination.Recenter, 0, 7),
        new Item("Look direction cue", ViewMenuDestination.LookCue, 1, 0),
        new Item("HUD & markers", ViewMenuDestination.Hud, 1, 1),
        new Item("Widget appearance", ViewMenuDestination.Widgets, 1, 2),
        new Item("Hands & arms", ViewMenuDestination.Hands, 1, 3),
        new Item("Sound & haptics", ViewMenuDestination.Feedback, 1, 4),
        new Item("VR performance", ViewMenuDestination.Performance, 1, 5),
        new Item("Menu options", ViewMenuDestination.MenuOptions, 1, 6)
    });
    internal static ButtonRect HomeBounds(Item item)
    {
        var y = item.Column == 0 ? item.Row < 3 ? 172 - item.Row * 47 : item.Row < 6 ? -17 - (item.Row - 3) * 47 : -208
            : item.Row < 4 ? 172 - item.Row * 47 : -64 - (item.Row - 4) * 47;
        return new ButtonRect(item.Column == 0 ? -160 : 160, y, 300, 42);
    }

    internal static void BuildHome(Action<Item, Action> addButton, IReadOnlyDictionary<ViewMenuDestination, Action> actions)
    {
        foreach (var item in Home)
        {
            if (!actions.TryGetValue(item.Destination, out var action))
                throw new ArgumentException("Missing View menu destination: " + item.Destination, nameof(actions));
            addButton(item, action);
        }
    }
}
