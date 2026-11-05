using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;

internal static class GameMenuChecks
{
    internal static readonly List<string> Events = new();

    internal static void Run(Action<bool, string> check)
    {
        MotionFlightController.Instance = new();
        ControllerActions.Instance = new();
        var ui = new GameObject("Native gameplay UI").AddComponent<GameplayUI>();
        SceneSingleton<GameplayUI>.i = ui;
        GameplayUI.AllowPauseKeybind = true;
        foreach (var mode in new[] { GameState.SinglePlayer, GameState.Multiplayer })
        {
            foreach (var side in new[] { 0, 1 })
            {
                GameplayUI.GameIsPaused = false;
                GameManager.gameState = mode;
                Events.Clear();
                var ownership = new HandOptionWheelOwnership();
                ownership.Open(side, 10);
                check(NativeWheelMenuActions.Open(NativeWheelMenuAction.GameMenu, () => { ownership.Close(11); Events.Add("close wheel"); }), "Game menu opens for either wheel hand and game mode");
                check(string.Join(",", Events) == "close wheel,reset grips,reset buttons,native pause", "Borrowed UI and held flight input release before native menu callback");
                check(ownership.Owner == -1 && ownership.Owns(side, 11) && !ownership.Owns(side, 12), "Menu commit consumes its wheel hand through closing frame only");
                check(GameplayUI.GameIsPaused, "Menu invocation uses native pause entry point");

                GameplayUI.GameIsPaused = false;
                Events.Clear();
                ownership.Open(side, 20);
                check(NativeWheelMenuActions.Open(NativeWheelMenuAction.VrMenu, () => { ownership.Close(21); Events.Add("close wheel"); }),
                    "VR menu opens from either hand and game mode");
                check(string.Join(",", Events) == "close wheel,reset grips,reset buttons,VR menu",
                    "Wheel and flight input release before existing View menu opens");
                check(!GameplayUI.GameIsPaused && ownership.Owner == -1 && ownership.Owns(side, 21),
                    "VR menu neither pauses the game nor leaks closing-frame input");
            }
        }
        foreach (var invalid in new[] { "missing", "disabled", "forbidden", "already open", "not playing" })
        {
            GameplayUI.GameIsPaused = invalid == "already open";
            GameplayUI.AllowPauseKeybind = invalid != "forbidden";
            GameManager.gameState = invalid == "not playing" ? GameState.Menu : GameState.SinglePlayer;
            SceneSingleton<GameplayUI>.i = invalid == "missing" ? null : ui;
            ui.gameObject.SetActive(invalid != "disabled");
            Events.Clear();
            check(!NativeWheelMenuActions.Open(NativeWheelMenuAction.GameMenu, () => Events.Add("close wheel")), "Invalid native menu context rejected: " + invalid);
            check(string.Join(",", Events) == "close wheel,reset grips,reset buttons", "Invalid menu context still safely releases flight input");
        }
        ui.gameObject.SetActive(true);
        SceneSingleton<GameplayUI>.i = ui;
        GameplayUI.AllowPauseKeybind = true;
        GameplayUI.GameIsPaused = false;
        GameManager.gameState = GameState.SinglePlayer;
        Events.Clear();
        check(!NativeWheelMenuActions.Open(NativeWheelMenuAction.GameMenu, () => { Events.Add("close wheel"); SceneSingleton<GameplayUI>.i = null; }), "Context is rechecked after wheel disposal callbacks");
        check(!Events.Contains("native pause"), "Destroyed menu during release is never invoked");
        Events.Clear();
        check(NativeWheelMenuActions.Open(NativeWheelMenuAction.VrMenu, () => Events.Add("close wheel")) && Events.Last() == "VR menu",
            "VR menu does not depend on native game menu availability");
        NOVR.VrUi.WristMenu.Available = false;
        check(!NativeWheelMenuActions.Open(NativeWheelMenuAction.VrMenu, () => { }), "VR menu opener failure is reported without native pause fallback");
        NOVR.VrUi.WristMenu.Available = true;
        Events.Clear();
        check(!NativeWheelMenuActions.Open(NativeWheelMenuAction.None, () => Events.Add("close wheel")) &&
            string.Join(",", Events) == "close wheel,reset grips,reset buttons", "Unknown or empty selection cannot invoke either menu");
        GameplayUI.GameIsPaused = true;
        Events.Clear();
        check(!NativeWheelMenuActions.Open(NativeWheelMenuAction.VrMenu, () => Events.Add("close wheel")) &&
            string.Join(",", Events) == "close wheel,reset grips,reset buttons", "An already-open game pause menu cannot acquire a second VR menu");
        GameplayUI.GameIsPaused = false;

        GameplayUI.AllowPauseKeybind = false;
        CursorManager.Flags = CursorFlags.SelectionMenu;
        SceneSingleton<GameplayUI>.i = ui;
        check(NativeWheelMenuActions.Open(NativeWheelMenuAction.GameMenu, () => { }) && GameplayUI.GameIsPaused && !GameplayUI.AllowPauseKeybind,
            "Explicit Game Menu works during aircraft selection without modifying its reserved Escape keybind.");
        GameplayUI.GameIsPaused = false;
        foreach (var blocked in new[] { CursorFlags.Chat, CursorFlags.Dialogue, CursorFlags.Loading, CursorFlags.GameMenu })
        {
            CursorManager.Flags = CursorFlags.SelectionMenu | blocked;
            Events.Clear();
            check(!NativeWheelMenuActions.Open(NativeWheelMenuAction.GameMenu, () => { }) && !Events.Contains("native pause"),
                "Selection-screen exception cannot bypass blocking cursor context: " + blocked);
        }
        CursorManager.Flags = CursorFlags.None;
        GameplayUI.AllowPauseKeybind = true;

        foreach (var blocked in new[] { "pause menu", "chat", "native lock during release", "map policy during release" })
        {
            DynamicMap.mapMaximized = false;
            DynamicMap.AllowedToOpen = true;
            ControllerActions.MapBlocked = false;
            Events.Clear();
            check(!NativeWheelMenuActions.Open(NativeWheelMenuAction.Map, () =>
            {
                Events.Add("close wheel");
                if (blocked == "pause menu") GameplayUI.GameIsPaused = true;
                if (blocked == "chat") CursorManager.Flags = CursorFlags.Chat;
                if (blocked == "native lock during release") DynamicMap.AllowedToOpen = false;
                if (blocked == "map policy during release") ControllerActions.MapBlocked = true;
            }), "Map action rechecks context after release: " + blocked);
            check(!DynamicMap.mapMaximized && string.Join(",", Events) == "close wheel,reset grips,reset buttons",
                "A newly blocking context cannot receive a map callback.");
            GameplayUI.GameIsPaused = false;
            CursorManager.Flags = CursorFlags.None;
        }
        DynamicMap.AllowedToOpen = true;
        ControllerActions.MapBlocked = false;

        foreach (var count in new[] { 0, 1, 5, 8, 11 })
        {
            var parent = new GameObject("Main page");
            var template = new GameObject("Sector template");
            template.AddComponent<Image>();
            var objects = new List<GameObject>();
            for (var i = 0; i < count; i++)
            {
                var icon = new GameObject("Icon " + i);
                var sector = new GameObject("Sector " + i);
                icon.transform.SetParent(parent.transform, false);
                sector.transform.SetParent(parent.transform, false);
                icon.transform.localPosition = new(i, 90, 0);
                sector.transform.localRotation = Quaternion.Euler(0, 0, i * 20);
                sector.AddComponent<Image>().fillAmount = .25f;
                objects.Add(icon);
                objects.Add(sector);
            }
            var before = objects.Select(x => (x.transform.localPosition, x.transform.localRotation, x.GetComponent<Image>()?.fillAmount)).ToArray();
            for (var opening = 0; opening < 2; opening++)
            {
                using (var entry = new NativeWheelMenuEntries(parent.transform, objects, count, template))
                {
                    check(entry.Index == count && parent.transform.Children.Count == count * 2 + 4, "Exactly two menu sectors and labels supplement any aircraft option count");
                    for (var i = 0; i < count; i++)
                    {
                        var p = objects[i * 2].transform.localPosition;
                        check(HandOptionWheelGesture.Sector(p.x, p.y, count + 2) == i, "Native icon placement matches expanded sector selection");
                        check(Math.Abs(objects[i * 2 + 1].GetComponent<Image>().fillAmount - 1f / (count + 2)) < .00001f, "Every borrowed sector occupies its matching selection range");
                    }
                    for (var menu = 0; menu < 2; menu++)
                    {
                        var menuLabel = parent.GetComponent<RectTransform>().Children[count * 2 + menu * 2 + 1].localPosition;
                        var menuIndex = count + menu;
                        var action = menu == 0 ? NativeWheelMenuAction.GameMenu : NativeWheelMenuAction.VrMenu;
                        check(entry.ActionAt(menuIndex) == action, "Each added index maps only to its distinct menu action");
                        check(HandOptionWheelGesture.Sector(menuLabel.x, menuLabel.y, count + 2) == menuIndex, "Menu label aligns with appended sector");
                        var gesture = new HandOptionWheelGesture();
                        gesture.Update(true, false, 0, 0, 0, count + 2);
                        gesture.Update(true, true, 0, 0, 0, count + 2);
                        gesture.Update(true, false, 0, menuLabel.x / 90, menuLabel.y / 90, count + 2);
                        check(gesture.Update(true, false, 0, 0, 0, count + 2) == HandOptionWheelEvent.Commit && gesture.Committed == menuIndex,
                            "Normal stick-return selection commits either added menu sector");
                    }
                }
                check(parent.transform.Children.Count == objects.Count, "Repeated opening leaves no owned presentation objects");
                check(objects.Select(x => (x.transform.localPosition, x.transform.localRotation, x.GetComponent<Image>()?.fillAmount)).SequenceEqual(before), "All native geometry restores exactly");
            }
            var mismatch = false;
            try
            {
                using var entry = new NativeWheelMenuEntries(parent.transform, objects, count + 2, template);
            }
            catch (InvalidOperationException) { mismatch = true; }
            check(mismatch && parent.transform.Children.Count == objects.Count &&
                objects.Select(x => (x.transform.localPosition, x.transform.localRotation, x.GetComponent<Image>()?.fillAmount)).SequenceEqual(before),
                "Unexpected native layout fails before mutating borrowed entries");
        }
    }
}

public class SceneSingleton<T> where T : class
{
    public static T? i;
}
public enum GameState
{
    Menu, SinglePlayer, Multiplayer
}
public static class GameManager
{
    public static GameState gameState;
    public static bool flightControlsEnabled = true;
    public static ControlMapper? controlMapper;
    public static Aircraft? Aircraft;
    public static bool GetLocalAircraft(out Aircraft aircraft) { aircraft = Aircraft!; return aircraft != null; }
}
public sealed class GameplayUI : MonoBehaviour
{
    public static bool AllowPauseKeybind, GameIsPaused;
    public void PauseGame()
    {
        GameMenuChecks.Events.Add("native pause");
        GameIsPaused = true;
    }
}
namespace NOVR.MotionControls
{
    internal sealed class MotionFlightController
    {
        internal static MotionFlightController? Instance;
        internal bool StickHeld, ThrottleHeld;
        internal void ResetGrips() => GameMenuChecks.Events.Add("reset grips");
    }
    internal sealed class ControllerActions
    {
        internal static ControllerActions? Instance;
        internal static bool MapBlocked;
        internal static Action? OnToggleMap;
        internal static bool MapAccessAvailable => !MapBlocked && !NativeHandOptionWheel.IsOpen &&
            !NativeRadialMenuState.IsBlockingInput &&
            (DynamicMap.mapMaximized || DynamicMap.AllowedToOpen);
        internal static void ToggleMap()
        {
            if (!MapAccessAvailable) throw new InvalidOperationException("Map invoked before releasing wheel or during native lock.");
            OnToggleMap?.Invoke();
            DynamicMap.mapMaximized = !DynamicMap.mapMaximized;
            CursorManager.Flags = DynamicMap.mapMaximized ? CursorFlags.Map : CursorFlags.None;
            GameMenuChecks.Events.Add(DynamicMap.mapMaximized ? "open map" : "close map");
        }
        internal void ResetButtons() => GameMenuChecks.Events.Add("reset buttons");
    }
}
namespace NOVR.VrUi
{
    internal static class WristMenu
    {
        internal static bool Available = true;
        internal static bool IsOpen;
        internal static bool OpenFromHandWheel()
        {
            if (!Available)
                return false;
            GameMenuChecks.Events.Add("VR menu");
            return true;
        }
    }
}
