using NOVR.VrUi.Native;
using Rewired;
using UnityEngine;

internal static class Program
{
    private static int _checks;

    private static void Main()
    {
        DiscoveryAndReferenceOwnership();
        AxisSlots();
        PreferredMapAndDisabledAssignments();
        NamesAndDescriptorRules();
        Console.WriteLine($"Native binding catalog checks passed ({_checks} assertions).");
    }

    private static void Check(bool value, string message)
    {
        if (!value)
            throw new InvalidOperationException(message);
        _checks++;
    }

    private static Player Reset(params InputAction[] actions)
    {
        ReInput.mapping = new Mapping();
        ReInput.mapping.UserAssignableActions.AddRange(actions);
        return new Player();
    }

    private static InputAction Action(int id, bool axis = false, string negative = "", string positive = "") => new()
    {
        id = id,
        categoryId = id / 10,
        name = $"Action {id}",
        type = axis ? InputActionType.Axis : InputActionType.Button,
        negativeDescriptiveName = negative,
        positiveDescriptiveName = positive
    };

    private static ActionElementMap Binding(int action, ControllerElementType type = ControllerElementType.Button,
        AxisRange range = AxisRange.Full, Pole pole = Pole.Positive) => new()
        {
            id = 100 + action,
            actionId = action,
            elementType = type,
            axisRange = range,
            axisContribution = pole
        };

    private static void DiscoveryAndReferenceOwnership()
    {
        var action = Action(1);
        var hidden = Action(2);
        hidden.userAssignable = false;
        var player = Reset(action, hidden, null!);
        var keyboard = new KeyboardMap { id = 7 };
        var binding = Binding(1);
        keyboard.AllMaps.AddRange(new[] { binding, Binding(2), Binding(999), null! });
        var excluded = new KeyboardMap { id = 8, categoryId = 3 };
        excluded.AllMaps.Add(Binding(1));
        ReInput.mapping.MapCategories[3] = new Category { userAssignable = false };
        player.controllers.maps.Set(0, keyboard, excluded, null!);
        var mouse = new MouseMap { id = 9 };
        player.controllers.maps.Set(0, mouse);
        player.controllers.Joysticks.AddRange(new[]
        {
            new Joystick { id = 4, name = " A very long controller name " },
            null!,
            new Joystick { id = 9 },
            new Joystick { id = 10, name = "Empty controller" }
        });
        var joystick = new JoystickMap { id = 10 };
        player.controllers.maps.Set(4, joystick);
        player.controllers.maps.Set(9, new JoystickMap { id = 11 });
        var catalog = NativeBindingCatalog.Read(player);
        Check(catalog.Devices.Select(device => device.Key).SequenceEqual(new[] { "KEYBOARD", "MOUSE", "JOYSTICK:4", "JOYSTICK:9" }),
            "Device filters retain discovery order and omit devices with no usable maps");
        Check(catalog.Devices[2].Label == " A very long controller name " && catalog.Devices[3].Label == "CONTROLLER 3",
            "Device filter labels retain full controller names and original index-based fallback");
        Check(catalog.Entries.Count == 4, "Only user-assignable actions/maps contribute entries; empty usable maps offer assignments");
        Check(catalog.Entries.Select(row => row.SortKey).SequenceEqual(catalog.Entries.Select(row => row.SortKey).OrderBy(key => key, StringComparer.OrdinalIgnoreCase)),
            "Rows retain ordinal case-insensitive sorting independently of filter order");
        var assigned = catalog.Entries.Single(row => row.IsAssigned);
        Check(ReferenceEquals(assigned.ControllerMap, keyboard) && ReferenceEquals(assigned.ActionElementMap, binding),
            "Assigned rows retain native map and element identities for clear/remap/invert");
        Check(assigned.Key == "KEYBOARD-1-101", "Assigned identity remains device/action/element, without category or label changes");
        var unassigned = catalog.Entries.Single(row => row.DeviceKey == "MOUSE");
        Check(ReferenceEquals(unassigned.ControllerMap, mouse) && unassigned.ActionElementMap == null && unassigned.ActionRange == AxisRange.Positive,
            "Unassigned button keeps its destination map and positive capture range");
        Check(unassigned.Key == "MOUSE-1-Unassigned-Positive-9", "Unassigned identity retains range and native map id");
        Check(catalog.Entries.Single(row => row.DeviceKey == "JOYSTICK:4").ControllerLabel == "A very long con...",
            "Row controller labels retain the 18-character trimmed limit");
        Check(keyboard.AllMaps.Count == 4 && keyboard.AllMaps[0].enabled && !keyboard.AllMaps[0].invert,
            "Catalog reads never edit the native bindings");
    }

    private static void AxisSlots()
    {
        // Use each stored axisRange for key/button inputs: axisContribution,
        // not the stored range, determines which action half is occupied.
        foreach (var storedRange in new[] { AxisRange.Full, AxisRange.Positive, AxisRange.Negative })
            foreach (var pole in new[] { Pole.Negative, Pole.Positive })
            {
                var player = Reset(Action(1, true, "Pitch down", "Pitch up"));
                var map = new KeyboardMap();
                var binding = Binding(1, ControllerElementType.Button, storedRange, pole);
                map.AllMaps.Add(binding);
                player.controllers.maps.Set(0, map);
                var rows = NativeBindingCatalog.Read(player).Entries;
                var used = pole == Pole.Negative ? AxisRange.Negative : AxisRange.Positive;
                var free = used == AxisRange.Negative ? AxisRange.Positive : AxisRange.Negative;
                Check(rows.Count == 2 && rows.Single(row => row.IsAssigned).ActionRange == used,
                    "Button contribution assigns only the intended half of an axis");
                Check(rows.Single(row => !row.IsAssigned).ActionRange == free,
                    "Partial binding offers only its unoccupied half, never a conflicting full axis");
                Check(rows.Single(row => row.IsAssigned).BindingKind == "BTN", "Axis action on a button remains labeled BTN");
            }

        foreach (var range in new[] { AxisRange.Full, AxisRange.Positive, AxisRange.Negative })
        {
            var player = Reset(Action(1, true, "Down", "Up"));
            var map = new JoystickMap();
            var binding = Binding(1, ControllerElementType.Axis, range, Pole.Negative);
            map.AllMaps.Add(binding);
            player.controllers.maps.Set(1, map);
            player.controllers.Joysticks.Add(new Joystick { id = 1 });
            var rows = NativeBindingCatalog.Read(player).Entries;
            Check(rows.Single(row => row.IsAssigned).ActionRange == range, "Real axes trust the native full/half range before contribution");
            Check(rows.Count == (range == AxisRange.Full ? 1 : 2), "Full axes occupy both halves; half axes leave exactly the other half");
        }

        foreach (var names in new[] { ("", ""), ("Down", ""), ("", "Up"), ("Down", "Up") })
        {
            var player = Reset(Action(1, true, names.Item1, names.Item2));
            player.controllers.maps.Set(0, new KeyboardMap());
            var ranges = NativeBindingCatalog.Read(player).Entries.Select(row => row.ActionRange).ToHashSet();
            Check(ranges.Contains(AxisRange.Full), "A wholly unassigned axis always offers a full-axis slot");
            Check(ranges.Contains(AxisRange.Negative) == (names.Item1 != "") && ranges.Contains(AxisRange.Positive) == (names.Item2 != ""),
                "Only explicitly named half-axis directions are offered");
        }
        var unknown = Binding(1, ControllerElementType.Axis, (AxisRange)99, Pole.Negative);
        Check(NativeBindingCatalog.GetActionRange(unknown) == AxisRange.Negative, "Unrecognized native axis ranges fall back to contribution");
        unknown.axisContribution = (Pole)99;
        Check(NativeBindingCatalog.GetActionRange(unknown) == (AxisRange)99, "Unknown range and contribution retain the original range");
    }

    private static void PreferredMapAndDisabledAssignments()
    {
        var mapped = Action(11, true, "Down", "Up");
        var sameCategory = Action(12);
        var fallbackAction = Action(21);
        var player = Reset(mapped, sameCategory, fallbackAction);
        var fallback = new KeyboardMap { id = 3, categoryId = 10, name = "Fallback" };
        var preferred = new KeyboardMap { id = 4, categoryId = 20, name = "Preferred" };
        var disabled = Binding(11);
        disabled.enabled = false;
        preferred.AllMaps.Add(disabled);
        player.controllers.maps.Set(0, fallback, preferred);
        ReInput.mapping.ActionCategories[1] = new Category { descriptiveName = "Flight" };
        var catalog = NativeBindingCatalog.Read(player);
        Check(catalog.Entries.All(row => !row.IsAssigned), "Disabled bindings do not appear assigned or consume any action slot");
        Check(catalog.Entries.Where(row => row.ActionId is 11 or 12).All(row => ReferenceEquals(row.ControllerMap, preferred)),
            "Disabled mappings still identify the preferred native map for their action category");
        Check(ReferenceEquals(catalog.Entries.Single(row => row.ActionId == 21).ControllerMap, fallback),
            "Categories without any existing mappings keep the first usable map as fallback");
        Check(catalog.Entries.Where(row => row.ActionId is 11 or 12).All(row => row.CategoryName == "Flight"),
            "Unassigned rows use action-category names, not map-category names");
        Check(catalog.Entries.Count(row => row.ActionId == 11) == 3, "Disabled whole/half bindings leave all named axis slots available");
        var duplicate = Binding(11, ControllerElementType.Axis, AxisRange.Full);
        fallback.AllMaps.Add(duplicate);
        var refreshed = NativeBindingCatalog.Read(player);
        Check(refreshed.Entries.Count(row => row.ActionId == 11) == 1, "A full binding in another map suppresses every unassigned slot on the device");
        Check(ReferenceEquals(refreshed.Entries.Single(row => row.ActionId == 12).ControllerMap, fallback),
            "The first encountered map for an action category remains preferred");
        Check(catalog.Entries.Count(row => row.ActionId == 11) == 3, "Refreshing a catalog does not mutate an earlier snapshot's row collection");
        Check(ReferenceEquals(refreshed.Entries.Single(row => row.IsAssigned).ActionElementMap, duplicate), "Refresh still retains the live native assignment reference");
    }

    private static void NamesAndDescriptorRules()
    {
        var action = Action(5, true, "Lower", "Raise");
        var player = Reset(action);
        var map = new KeyboardMap { id = 22, categoryId = 42, name = "Map fallback" };
        player.controllers.maps.Set(0, map);
        var binding = Binding(5, ControllerElementType.Axis);
        map.AllMaps.Add(binding);
        ReInput.mapping.MapCategories[42] = new Category { descriptiveName = "Assigned category", name = "Internal category" };
        Check(NativeBindingCatalog.Read(player).Entries.Single().CategoryName == "Assigned category", "Assigned rows prefer map category descriptive name");
        ReInput.mapping.MapCategories[42].descriptiveName = " ";
        Check(NativeBindingNames.GetMapCategoryName(map) == "Internal category", "Map category falls back to internal name");
        ReInput.mapping.MapCategories[42].name = "";
        Check(NativeBindingNames.GetMapCategoryName(map) == "Map fallback", "Unnamed category falls back to map name");
        map.name = " ";
        Check(NativeBindingNames.GetMapCategoryName(map) == "General" && NativeBindingNames.GetActionCategoryName(action) == "General", "Missing category metadata remains General");
        ReInput.mapping.ActionCategories[action.categoryId] = new Category { descriptiveName = "Action category", name = "Internal" };
        Check(NativeBindingNames.GetActionCategoryName(action) == "Action category", "Action category prefers descriptive name");
        ReInput.mapping.ActionCategories[action.categoryId].descriptiveName = "";
        Check(NativeBindingNames.GetActionCategoryName(action) == "Internal", "Action category falls back to internal name");
        action.descriptiveName = "Pitch";
        binding.actionDescriptiveName = "Native element name";
        Check(NativeBindingNames.GetActionDisplayName(action, binding) == "Native element name", "Element's native action label has highest priority");
        binding.actionDescriptiveName = " ";
        binding.axisRange = AxisRange.Negative;
        Check(NativeBindingNames.GetActionDisplayName(action, binding) == "Lower", "Assigned half-axis uses its direction label");
        Check(NativeBindingNames.GetActionDisplayName(action, AxisRange.Positive) == "Raise", "Unassigned half-axis uses the same direction label");
        Check(NativeBindingNames.GetActionDisplayName(action, AxisRange.Full) == "Pitch", "Full axis uses the action label");
        action.descriptiveName = "";
        Check(NativeBindingNames.GetActionDisplayName(action) == "Action 5", "Action label falls back to internal name");
        action.name = "";
        Check(NativeBindingNames.GetActionDisplayName(action) == "Action 5" && NativeBindingNames.GetActionDisplayName(null, binding) == "Action 5", "Nameless/missing actions retain id fallback");
        binding.elementIdentifierName = "Hat up";
        binding.keyCode = KeyCode.Space;
        Check(NativeBindingNames.GetBindingName(binding) == "Hat up", "Native element label precedes key code");
        binding.elementIdentifierName = " ";
        Check(NativeBindingNames.GetBindingName(binding) == "Space", "Keyboard binding falls back to key code");
        binding.keyCode = KeyCode.None;
        binding.elementIdentifierId = 77;
        Check(NativeBindingNames.GetBindingName(binding) == "Element 77", "Nameless binding falls back to element id");
        foreach (var range in new[] { AxisRange.Full, AxisRange.Negative, AxisRange.Positive })
            foreach (var inverted in new[] { false, true })
            {
                binding.axisRange = range;
                binding.invert = inverted;
                var row = NativeBindingCatalog.Read(player).Entries.Single(entry => entry.IsAssigned);
                Check(row.CanInvert == (range == AxisRange.Full) && row.IsInverted == inverted && row.CanShowAxisValue,
                    "Only physical full axes permit inversion; assigned axis actions keep live values");
                Check(row.BindingKind == (range == AxisRange.Full ? inverted ? "AXIS INV" : "AXIS" : range == AxisRange.Negative ? "AXIS -" : "AXIS +"),
                    "Full/half/inverted labels remain unchanged");
                binding.invert = !inverted;
                Check(row.IsInverted == !inverted, "Inversion reads the native element live, without copying mutable state");
            }
        Check(NativeBindingNames.ShortenLabel("  short  ", 8) == "short" && NativeBindingNames.ShortenLabel("  ", 8) == "",
            "Display shortening retains trim/empty behavior");
        Check(NativeBindingNames.ShortenLabel("12345678", 8) == "12345678" && NativeBindingNames.ShortenLabel("123456789", 8) == "12345...",
            "Exact-length labels and ellipsis boundary remain stable");
    }
}
