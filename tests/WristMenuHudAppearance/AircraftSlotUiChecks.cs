using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;

internal static class AircraftSlotUiChecks
{
    internal static int Run()
    {
        var checks = 0;
        var manager = new CockpitHudWidgets(); CockpitHudWidgets.Instance = manager;
        var menu = new WristMenu(); menu.Build(); menu.Profiles(); menu.Refresh();
        var profileButtons = menu.Buttons.Where(b => b.Key.page == "AIRCRAFT PROFILES").ToArray();
        Check(profileButtons.Length == 7, "Profiles contains exactly three load/save pairs and Edit current slot");
        for (var slot = 1; slot <= 3; slot++)
        {
            var load = menu.Cell("AIRCRAFT PROFILES", 0, slot - 1).text;
            var save = menu.Cell("AIRCRAFT PROFILES", 1, slot - 1).text;
            Check(load == $"Load Slot {slot}" + (slot == 1 ? " · active" : ""), "Load labels identify each distinct slot and the active slot");
            Check(save == $"Save FS-20 Revoker\nSlot {slot}", "Every save label identifies the aircraft and destination slot");
            Check(menu.Enabled("AIRCRAFT PROFILES", load) && !menu.Enabled("AIRCRAFT PROFILES", save),
                "Outside edit, loading is enabled and saving is disabled for each slot");
        }
        Check(menu.Cell("LAYOUT", 0, 0).text == "Save FS-20 Revoker\nSlot 1", "Primary layout save identifies current aircraft and slot");
        Check(!menu.Enabled("LAYOUT", menu.Cell("LAYOUT", 0, 0).text), "Primary layout save is disabled outside editing");
        Check(menu.ProfilesInfo.text.Contains("Three separate layouts for this aircraft only") &&
            menu.ProfilesInfo.text.Contains("Seat, handles and HUD widgets save together"), "Profile instructions describe per-aircraft unified layouts");
        Check(!menu.Buttons.Keys.Any(b => b.text.Contains("shared", StringComparison.OrdinalIgnoreCase) ||
            b.text.Contains("global", StringComparison.OrdinalIgnoreCase) || b.text.Contains("all aircraft", StringComparison.OrdinalIgnoreCase)),
            "No shared/global/all-aircraft layout buttons remain");

        // Each closure must retain its own loop slot, not all point to Slot 3.
        foreach (var slot in new[] { 3, 1, 2 })
        {
            var closes = WristMenu.Closes;
            menu.Click("AIRCRAFT PROFILES", menu.Cell("AIRCRAFT PROFILES", 0, slot - 1).text); menu.Refresh();
            Check(manager.LoadRequests.Last() == slot && manager.SuccessfulLoads.Last() == slot && manager.ProfileSlot == slot,
                "Each load callback routes the corresponding slot");
            Check(menu.Cell("AIRCRAFT PROFILES", 0, slot - 1).text.EndsWith(" · active") &&
                menu.Cell("LAYOUT", 0, 0).text == $"Save FS-20 Revoker\nSlot {slot}", "Loading refreshes active label and primary save destination");
            Check(WristMenu.Closes == closes, "Loading a slot leaves the profiles page available");
        }
        var saves = manager.SuccessfulSaves.Count; var beforeCloses = WristMenu.Closes;
        for (var slot = 1; slot <= 3; slot++)
            menu.ForceCallback("AIRCRAFT PROFILES", menu.Cell("AIRCRAFT PROFILES", 1, slot - 1).text);
        menu.ForceCallback("LAYOUT", menu.Cell("LAYOUT", 0, 0).text);
        Check(manager.SuccessfulSaves.Count == saves && WristMenu.Closes == beforeCloses,
            "Late save callbacks outside editing neither save nor close the menu");

        var begins = manager.Begins;
        menu.Click("AIRCRAFT PROFILES", "Edit current slot"); menu.Refresh();
        Check(manager.Begins == begins + 1 && CockpitHudWidgets.Editing && WristMenu.Closes == beforeCloses + 1,
            "Edit current slot opens one preview and closes the menu to allow movement");
        var loadedSlot = manager.ProfileSlot; var loads = manager.SuccessfulLoads.Count;
        for (var slot = 1; slot <= 3; slot++)
        {
            var load = menu.Cell("AIRCRAFT PROFILES", 0, slot - 1).text;
            var save = menu.Cell("AIRCRAFT PROFILES", 1, slot - 1).text;
            Check(!menu.Enabled("AIRCRAFT PROFILES", load) && menu.Enabled("AIRCRAFT PROFILES", save),
                "During edit all three load buttons disable and save buttons enable");
            menu.Click("AIRCRAFT PROFILES", load); menu.ForceCallback("AIRCRAFT PROFILES", load);
        }
        Check(manager.SuccessfulLoads.Count == loads && manager.ProfileSlot == loadedSlot && CockpitHudWidgets.Editing,
            "Both disabled clicks and late load callbacks preserve an active preview");
        Check(menu.ProfilesInfo.text.Contains("Loading is disabled while editing"), "Instructions explain the edit/load restriction");

        foreach (var slot in new[] { 2, 3, 1 })
        {
            manager.Begin(); menu.Refresh(); var closes = WristMenu.Closes;
            menu.Click("AIRCRAFT PROFILES", menu.Cell("AIRCRAFT PROFILES", 1, slot - 1).text); menu.Refresh();
            Check(manager.SaveRequests.Last() == slot && manager.SuccessfulSaves.Last() == slot && manager.ProfileSlot == slot,
                "Each save callback routes its own selected destination slot");
            Check(!CockpitHudWidgets.Editing && WristMenu.Closes == closes + 1, "Successful slot save ends edit and closes menu");
        }
        manager.LoadSlot(3); manager.Begin(); menu.Refresh();
        menu.Click("LAYOUT", menu.Cell("LAYOUT", 0, 0).text);
        Check(manager.SuccessfulSaves.Last() == 3 && !CockpitHudWidgets.Editing, "Primary layout save follows the active slot instead of always Slot 1");

        manager.ProfileAircraftName = "SAH-46 Chicane"; manager.ProfileSlot = 2; menu.Refresh();
        Check(menu.Cell("LAYOUT", 0, 0).text == "Save SAH-46 Chicane\nSlot 2" &&
            Enumerable.Range(1, 3).All(slot => menu.Cell("AIRCRAFT PROFILES", 1, slot - 1).text == $"Save SAH-46 Chicane\nSlot {slot}"),
            "Aircraft change updates all save labels without rebuilding menu");
        Check(menu.ProfilesInfo.text.StartsWith("SAH-46 Chicane · Slot 2"), "Profile summary follows the current aircraft and slot");
        manager.Available = false; menu.Refresh();
        for (var slot = 1; slot <= 3; slot++)
            Check(!menu.Enabled("AIRCRAFT PROFILES", menu.Cell("AIRCRAFT PROFILES", 0, slot - 1).text) &&
                !menu.Enabled("AIRCRAFT PROFILES", menu.Cell("AIRCRAFT PROFILES", 1, slot - 1).text),
                "No-aircraft state disables all load and save rows");
        saves = manager.SuccessfulSaves.Count; loads = manager.SuccessfulLoads.Count; beforeCloses = WristMenu.Closes;
        menu.ForceCallback("AIRCRAFT PROFILES", menu.Cell("AIRCRAFT PROFILES", 0, 0).text);
        menu.ForceCallback("AIRCRAFT PROFILES", menu.Cell("AIRCRAFT PROFILES", 1, 0).text);
        menu.Click("AIRCRAFT PROFILES", "Edit current slot");
        Check(manager.SuccessfulSaves.Count == saves && manager.SuccessfulLoads.Count == loads && WristMenu.Closes == beforeCloses,
            "Unavailable late callbacks and edit action cannot change profiles or close menu");
        return checks;
        void Check(bool pass, string message) { if (!pass) throw new Exception(message); checks++; }
    }
}
