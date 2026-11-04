using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.UI;

internal static class MenuAvailabilityChecks
{
    internal static int Run()
    {
        var passed = 0;
        void Check(bool value, string description)
        { if (!value) throw new Exception(description); passed++; }
        var adapter = new NativeGameActionAdapter();
        Check(!adapter.IsTopLevelMainMenuAvailable, "No native canvas means no available main menu");
        var root = new GameObject();
        var menu = new Button { Label = "SINGLE PLAYER", IsMainMenu = true };
        menu.gameObject.Parent = root; root.Components.Add(menu); adapter.Source = root;
        Check(adapter.IsTopLevelMainMenuAvailable && root.HierarchySearches == 1,
            "Active native menu remains available through the existing button matching path");
        root.activeSelf = false;
        Check(!adapter.IsTopLevelMainMenuAvailable && !adapter.IsTopLevelMainMenuAvailable && root.HierarchySearches == 1,
            "Closed native canvas skips all button discovery on repeated frames");
        root.activeSelf = true;
        Check(adapter.IsTopLevelMainMenuAvailable && root.HierarchySearches == 2,
            "Reopening immediately resumes discovery without a cached closed result");

        var canvas = new Canvas { gameObject = root, enabled = false };
        var group = new CanvasGroup { gameObject = root, alpha = 0, interactable = false, blocksRaycasts = false };
        root.Components.Add(canvas); root.Components.Add(group);
        Check(adapter.IsTopLevelMainMenuAvailable,
            "NOVR rendering/input suppression leaves the active native menu available");
        var parent = new GameObject { activeSelf = false }; root.Parent = parent;
        var scans = root.HierarchySearches;
        Check(!adapter.IsTopLevelMainMenuAvailable && root.HierarchySearches == scans,
            "An inactive scene parent also bypasses menu enumeration");
        parent.activeSelf = true;
        Check(adapter.IsTopLevelMainMenuAvailable, "Parent reactivation restores availability immediately");
        menu.gameObject.activeSelf = false;
        Check(!adapter.IsTopLevelMainMenuAvailable, "A closed native menu subtree is still filtered by discovery");
        var late = new Button { Label = "SINGLE PLAYER", IsMainMenu = true };
        late.gameObject.Parent = root; root.Components.Add(late);
        Check(adapter.IsTopLevelMainMenuAvailable, "A newly created native button is found on the same query");
        root.Destroyed = true; scans = root.HierarchySearches;
        Check(!adapter.IsTopLevelMainMenuAvailable && root.HierarchySearches == scans,
            "Destroyed Unity canvas wrappers are rejected without touching their hierarchy");
        var next = new GameObject(); adapter.Source = next;
        Check(!adapter.IsTopLevelMainMenuAvailable && next.HierarchySearches == 1,
            "Scene replacement does not retain old menu availability");
        var replacement = new Button { Label = "SINGLE PLAYER", IsMainMenu = true };
        replacement.gameObject.Parent = next; next.Components.Add(replacement);
        Check(adapter.IsTopLevelMainMenuAvailable, "The replacement canvas can become available without a retry delay");
        return passed;
    }
}

namespace UnityEngine.UI
{
    public sealed class Button : MonoBehaviour { public string Label = ""; public bool IsMainMenu; }
}

namespace NOVR.VrUi.Native
{
    internal enum NativeGameAction { SinglePlayer }

    // The actual availability query and GetButtons are source-linked. Only
    // unrelated action routing and the existing label/path matchers are doubled.
    public partial class NativeGameActionAdapter
    {
        private GameObject? _originalMainCanvas;
        private readonly Dictionary<NativeGameAction, string[]> _mainMenuActionLabels = new()
        { [NativeGameAction.SinglePlayer] = new[] { "SINGLE PLAYER" } };
        internal GameObject Source { set => _originalMainCanvas = value; }
        private static bool IsTopLevelMainMenuButton(Button button) => button.IsMainMenu;
        private static bool ButtonMatches(Button button, IEnumerable<string> labels) => labels.Contains(button.Label);
    }
}
