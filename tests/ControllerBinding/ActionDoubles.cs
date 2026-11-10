// Game/Unity boundaries only. Production ControllerActions, bindings, gates and
// queue execute unchanged; no Rewired/Harmony interception or real input runs.
namespace UnityEngine
{
    public class MonoBehaviour
    {
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder(int order) : Attribute
    {
        public int Order { get; } = order;
    }
    public static class Time
    {
        public static int frameCount; public static float timeScale = 1;
    }
    public static class Cursor
    {
        public static bool visible;
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch(Type type, string method, Type[] arguments) : Attribute
    {
        public (Type, string, Type[]) Target { get; } = (type, method, arguments);
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPostfix : Attribute
    {
    }
}
namespace Rewired
{
    public sealed class Player
    {
    }
}
namespace NuclearOption.Networking
{
    public sealed class Player
    {
    }
}
namespace NuclearOption.UI
{
    public static class LeaderboardMenu
    {
        public static bool IsOpen() => false;
    }
}
namespace NuclearOption.MissionEditorScripts
{
    public static class InputFieldChecker
    {
        public static bool InsideInputField;
    }
}
public sealed class Aircraft
{
    public bool disabled;
}
public sealed class ControlMapper
{
    public bool isOpen;
}
public enum GameState
{
    SinglePlayer, Multiplayer
}
public static class GameManager
{
    public static readonly Rewired.Player playerInput = new();
    public static Aircraft? Aircraft = new();
    public static bool flightControlsEnabled = true;
    public static GameState gameState;
    public static ControlMapper? controlMapper;
    public static NuclearOption.Networking.Player? LocalPlayer;
    public static bool GetLocalAircraft(out Aircraft aircraft)
    {
        aircraft = Aircraft!;
        return aircraft != null;
    }
    public static bool GetLocalPlayer<T>(out T? player) where T : class
    {
        player = LocalPlayer as T;
        return player != null;
    }
}
[Flags]
public enum CursorFlags
{
    None, GameMenu
}
public static class CursorManager
{
    public static CursorFlags Flags;
    public static bool GetFlag(CursorFlags flag) => (Flags & flag) != 0;
}
public enum CameraMode
{
    cockpit, external
}
public sealed class CameraStateManager
{
    public static CameraMode cameraMode;
    public object? followingUnit;
}
public static class SceneSingleton<T> where T : class
{
    public static T? i;
}
public sealed class GameplayUI
{
}
public sealed class MapOptions
{
}
public sealed class DynamicMap
{
    public static bool mapMaximized, AllowedToOpen = true;
    public int Opens, Closes;
    public void Minimize() { mapMaximized = false; Closes++; }
    public void Maximize() { if (AllowedToOpen) { mapMaximized = true; Opens++; } }
}
public sealed class RadialMenuMain
{
    public static bool InUse;
    public bool isActiveAndEnabled = true;
    public static bool IsInUse() => InUse;
}
public sealed class CombatHUD
{
    public int LastCleared, AllCleared;
    public void DeselectLast() => LastCleared++;
    public void DeselectAll() => AllCleared++;
}
namespace NOVR.VrUi
{
    public static class WristMenu
    {
        public static bool IsOpen;
        public static void Close() => IsOpen = false;
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    public static class CockpitHudWidgets
    {
        public static bool Editing;
    }
    public static class CockpitMapInteraction
    {
        public static bool Reserved;
        public static void UpdateNavigation()
        {
        }
        public static bool ReservesButton(bool left, NOVR.MotionControls.ControllerButton button, bool frame) => Reserved;
    }
}
namespace NOVR.VrCamera
{
    // The camera rules are covered by the spectator-input suite. Here the
    // dispatcher only asks what is possible and requests a change.
    public static class PilotView
    {
        public static bool External, CanGoExternal, CanGoCockpit;
        public static int CockpitRequests, ToggleRequests;
        public static bool ToCockpit()
        {
            CockpitRequests++;
            if (!CanGoCockpit)
                return false;
            External = CanGoCockpit = false;
            CanGoExternal = true;
            CameraStateManager.cameraMode = CameraMode.cockpit;
            // The real sampler stops as soon as the camera is in the cockpit.
            NOVR.MotionControls.ExternalFlightInput.Active = false;
            return true;
        }
        public static bool Toggle()
        {
            ToggleRequests++;
            if (CanGoCockpit)
            {
                var done = ToCockpit();
                CockpitRequests--; // counted as a toggle, not as a B press
                return done;
            }
            if (!CanGoExternal)
                return false;
            External = CanGoCockpit = true;
            CanGoExternal = false;
            CameraStateManager.cameraMode = CameraMode.external;
            return true;
        }
        public static void Reset()
        {
            External = CanGoExternal = CanGoCockpit = false;
            CockpitRequests = ToggleRequests = 0;
        }
    }
}
namespace NOVR.MotionControls
{
    // Outside-view thumbstick flight; its own gating has its own suite.
    public static class ExternalFlightInput
    {
        public static bool Active;
        public static bool Sample() => Active;
    }
    public sealed class MotionFlightController
    {
        public static MotionFlightController Instance = new();
        public bool FlightActive = true, StickHeld = true, ThrottleHeld = true;
        public void ResetGrips() => StickHeld = ThrottleHeld = false;
    }
    public static class CockpitLayout
    {
        public static bool Active;
    }
    public static class NativeHandOptionWheel
    {
        public static bool IsOpen;
        public static bool OwnsHand(bool left) => IsOpen;
    }
}
