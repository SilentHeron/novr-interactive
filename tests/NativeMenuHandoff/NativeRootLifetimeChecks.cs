using NOVR.VrUi.Native;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class NativeRootLifetimeChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
            checks++;
        }
        var subscribers = SceneManager.Subscribers;
        var root = new NativeVrUiRoot();
        root.CreatePresentationForCheck();
        root.EnableForCheck();
        root.QueueRecenterForCheck();
        root.DisableForCheck();
        Check(!root.RecenterPendingForCheck, "Owner disable must discard the queued recenter");
        Check(!RootLifetimeBoundary.ProjectionActive && !root.Root.activeSelf && !root.UtilityVisibleForCheck,
            "Owner disable releases projection and hides both presentation canvases");
        Check(!root.Suppressed && !NOUIManager.I!.StartupVisible, "Owner disable restores the stock menu and startup camera");
        Check(root.ActionsForCheck.Subscribers == 0 && SceneManager.Subscribers == subscribers,
            "Disabled owner must not retain adapter or scene subscriptions");
        var calls = root.ActionCallbacksForCheck;
        root.ActionsForCheck.Raise(NativeGameAction.SinglePlayer);
        Check(root.ActionCallbacksForCheck == calls && !root.Root.activeSelf && !root.Suppressed,
            "A retained adapter cannot reactivate or suppress menus after owner disable");

        root.EnableForCheck();
        Check(root.ActionsForCheck.Subscribers == 1 && SceneManager.Subscribers == subscribers + 3,
            "Reenable restores exactly one action callback and scene observer");
        root.ActionsForCheck.Raise(NativeGameAction.SinglePlayer);
        Check(root.ActionCallbacksForCheck == calls + 1 && root.Root.activeSelf && root.Panels[1].Visible,
            "An explicit action after reenable still reaches the retained panel");
        Check(!root.RecenterPendingForCheck, "Reenable cannot revive an expired recenter request");
        root.DisableForCheck();
        root.EnableForCheck();
        Check(root.ActionsForCheck.Subscribers == 1 && SceneManager.Subscribers == subscribers + 3,
            "Repeated disable/enable cycles do not multiply subscriptions");

        root.ShowForCheck(2);
        var panelUpdates = root.Panels.Sum(panel => panel.Updates);
        root.QueueRecenterForCheck();
        root.SuspendForCheck();
        Check(!root.Root.activeSelf && !root.RecenterPendingForCheck && !RootLifetimeBoundary.ProjectionActive,
            "Temporary native handoff cancels queued recenter and releases the projection override");
        Check(root.Panels[1].Visible && root.Panels.Sum(panel => panel.Updates) == panelUpdates,
            "Suspension preserves the selected panel and its edited state");
        Check(root.ActionsForCheck.Subscribers == 1 && root.ActionCallbacksForCheck == calls + 1,
            "Temporary handoff neither synthesizes an action nor unbinds the enabled owner");
        root.Root.SetActive(true);
        Check(!root.RecenterPendingForCheck, "Returning from a temporary dialog cannot trigger the old recenter");
        root.QueueRecenterForCheck();
        root.SuspendForCheck(stockFallback: true);
        Check(!root.Root.activeSelf && root.UtilityVisibleForCheck && !root.RecenterPendingForCheck,
            "Native-UI-off suspension keeps only the explicit stock recovery control");

        root.QueueRecenterForCheck();
        RootLifetimeBoundary.Events.Clear();
        root.DestroyForCheck();
        Check(root.ActionsForCheck.Subscribers == 0 && SceneManager.Subscribers == subscribers,
            "Direct destruction releases callbacks even if no disable callback arrived");
        Check(!root.Suppressed && !RootLifetimeBoundary.ProjectionActive && !NOUIManager.I!.StartupVisible && !root.RecenterPendingForCheck,
            "Direct destruction releases every outstanding presentation override");
        var events = RootLifetimeBoundary.Events;
        Check(events.IndexOf("restore") >= 0 && events.IndexOf("restore") < events.IndexOf("splash release") &&
            events.IndexOf("splash release") < events.IndexOf("startup hidden") &&
            events.IndexOf("startup hidden") < events.IndexOf("destroy"),
            "Canvas restore, splash release and startup shutdown precede root destruction");
        root.DestroyForCheck();
        root.DisableForCheck();
        Check(root.ActionsForCheck.Subscribers == 0 && SceneManager.Subscribers == subscribers,
            "Repeated destroy/disable cleanup cannot restore subscriptions");

        var early = new NativeVrUiRoot(false);
        early.EnableForCheck();
        early.DisableForCheck();
        early.DestroyForCheck();
        Check(SceneManager.Subscribers == subscribers && early.ActionsForCheck.Subscribers == 0,
            "Disable/destroy before presentation construction still releases owner subscriptions");
        return checks;
    }
}

internal static class RootLifetimeBoundary
{
    internal static bool ProjectionActive;
    internal static readonly List<string> Events = new();
}

namespace NOVR
{
    // The owning MonoBehaviour's lifecycle is production-linked. Configuration
    // events and Unity object destruction are boundaries for this source-only suite.
    public class NOVRBehaviour : MonoBehaviour
    {
        protected virtual void OnEnable()
        {
        }
        protected virtual void OnDisable()
        {
        }
        protected static void Destroy(UnityEngine.Object value)
        {
            value.Destroyed = true;
            RootLifetimeBoundary.Events.Add("destroy");
        }
    }
}
namespace NOVR.VrUi
{
    public sealed class NOUIManager
    {
        public static NOUIManager? I = new();
        internal bool StartupVisible;
        internal void SetStartupPresentation(bool visible)
        {
            StartupVisible = visible;
            RootLifetimeBoundary.Events.Add(visible ? "startup shown" : "startup hidden");
        }
    }
}
namespace NOVR.VrUi.Native
{
    internal sealed class NativeStartupSplash
    {
        internal void Dispose() => RootLifetimeBoundary.Events.Add("splash release");
    }
    public partial class NativeGameActionAdapter
    {
        internal event Action<NativeGameAction>? ActionInvoked;
        internal int Subscribers => ActionInvoked?.GetInvocationList().Length ?? 0;
        internal void Raise(NativeGameAction action) => ActionInvoked?.Invoke(action);
    }
    public partial class NativeVrUiRoot
    {
        private readonly NativeGameActionAdapter _actions = new();
        private readonly NativeMainCanvasDiscovery _mainCanvasDiscovery = new();
        private NativeStartupSplash? _startupSplash;
        private GameObject? _recenterWidgetRoot;
        private bool _recenterPending;
        private UtilityWidgetMode _utilityWidgetMode;
        private enum UtilityWidgetMode
        {
            Hidden, Recenter, EnableNativeUi
        }
        internal NativeGameActionAdapter ActionsForCheck => _actions;
        internal bool RecenterPendingForCheck => _recenterPending;
        internal bool UtilityVisibleForCheck => _utilityWidgetMode != UtilityWidgetMode.Hidden;
        internal int ActionCallbacksForCheck;
        internal void EnableForCheck() => OnEnable();
        internal void DisableForCheck() => OnDisable();
        internal void DestroyForCheck() => OnDestroy();
        internal void SuspendForCheck(bool stockFallback = false) => SuspendNativePresentation(stockFallback ? UtilityWidgetMode.EnableNativeUi : UtilityWidgetMode.Hidden);
        internal void CreatePresentationForCheck()
        {
            _startupSplash = new();
            _recenterWidgetRoot = new();
        }
        internal void QueueRecenterForCheck()
        {
            _recenterPending = true;
            RootLifetimeBoundary.ProjectionActive = true;
            _utilityWidgetMode = UtilityWidgetMode.Recenter;
            Suppressed = true;
            NOUIManager.I!.StartupVisible = true;
        }
        private void OnNativeActionInvoked(NativeGameAction action)
        {
            // Routing is a boundary; the production lifecycle decides whether
            // a native callback can reach any routing after presentation teardown.
            ActionCallbacksForCheck++;
            ActivatePanel(MenuPanel.SinglePlayer);
        }
        private void RestoreOriginalMainCanvas()
        {
            Suppressed = false;
            RootLifetimeBoundary.Events.Add("restore");
        }
        private static void ClearNativeCursorProjectionReference() => RootLifetimeBoundary.ProjectionActive = false;
        private void SetUtilityWidgetMode(UtilityWidgetMode mode) => _utilityWidgetMode = mode;
    }
}
