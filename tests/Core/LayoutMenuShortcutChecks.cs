using NOVR.VrUi;

internal static class LayoutMenuShortcutChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("Layout menu opens once for a fresh click from either or both controllers", () =>
        {
            foreach (var side in new[] { "left", "right", "both" })
            {
                var shortcut = new LayoutMenuShortcut();
                var left = side != "right";
                var right = side != "left";
                truth(!Update(shortcut));
                truth(Update(shortcut, leftClick: left, rightClick: right));
                truth(!Update(shortcut, leftClick: left, rightClick: right));
                truth(!Update(shortcut));
                truth(Update(shortcut, leftClick: left, rightClick: right));
            }
        });
        check("Held stick clicks at layout entry require a release before opening View", () =>
        {
            var shortcut = new LayoutMenuShortcut();
            truth(!Update(shortcut, editing: false));
            truth(!Update(shortcut, leftClick: true, rightClick: true));
            truth(!Update(shortcut, leftClick: true, rightClick: true));
            truth(!Update(shortcut, rightClick: true));
            truth(Update(shortcut, leftClick: true, rightClick: true));
            truth(!Update(shortcut, leftClick: true, rightClick: true));
        });
        check("Layout exit, unavailable cockpit and an open menu reset shortcut ownership", () =>
        {
            foreach (var context in new[] { "edit exit", "cockpit unavailable", "menu open" })
            {
                var shortcut = new LayoutMenuShortcut();
                truth(!Update(shortcut));
                truth(!Update(shortcut, editing: context != "edit exit", cockpit: context != "cockpit unavailable",
                    menuOpen: context == "menu open", leftClick: true, rightClick: true));
                truth(!Update(shortcut, leftClick: true, rightClick: true));
                truth(!Update(shortcut));
                truth(Update(shortcut, rightClick: true));
            }
        });
        check("Clicks cannot close an open View or immediately reopen it when returning to layout", () =>
        {
            var shortcut = new LayoutMenuShortcut();
            truth(!Update(shortcut));
            truth(Update(shortcut, leftClick: true));
            truth(!Update(shortcut, menuOpen: true, leftClick: true));
            truth(!Update(shortcut, menuOpen: true));
            truth(!Update(shortcut, menuOpen: true, rightClick: true));
            truth(!Update(shortcut, rightClick: true));
            truth(!Update(shortcut));
            truth(Update(shortcut, rightClick: true));
        });
        check("Lost tracking cancels only that hand; regain while pressed cannot open View", () =>
        {
            foreach (var lostLeft in new[] { false, true })
            {
                var shortcut = new LayoutMenuShortcut();
                truth(!Update(shortcut));
                truth(!Update(shortcut, leftValid: !lostLeft, rightValid: lostLeft,
                    leftClick: lostLeft, rightClick: !lostLeft));
                truth(!Update(shortcut, leftClick: lostLeft, rightClick: !lostLeft));
                // The opposite, continuously tracked hand retains its fresh-press gate.
                truth(Update(shortcut, leftClick: true, rightClick: true));
                truth(!Update(shortcut, leftClick: true, rightClick: true));
                truth(!Update(shortcut));
                truth(Update(shortcut, leftClick: lostLeft, rightClick: !lostLeft));
            }
        });
        check("Either tracked hand works alone; explicit reset discards both armed clicks", () =>
        {
            foreach (var left in new[] { false, true })
            {
                var shortcut = new LayoutMenuShortcut();
                truth(!Update(shortcut, leftValid: left, rightValid: !left));
                truth(Update(shortcut, leftValid: left, rightValid: !left, leftClick: left, rightClick: !left));
                truth(!Update(shortcut, leftValid: false, rightValid: false, leftClick: true, rightClick: true));
                truth(!Update(shortcut));
                shortcut.Reset();
                truth(!Update(shortcut, leftClick: true, rightClick: true));
                truth(!Update(shortcut));
                truth(Update(shortcut, leftClick: left, rightClick: !left));
            }
        });
    }

    private static bool Update(LayoutMenuShortcut shortcut, bool editing = true, bool cockpit = true,
        bool menuOpen = false, bool leftValid = true, bool leftClick = false, bool rightValid = true, bool rightClick = false) =>
        shortcut.Update(editing, cockpit, menuOpen, leftValid, leftClick, rightValid, rightClick);
}
