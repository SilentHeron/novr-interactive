using Cysharp.Threading.Tasks;
using NuclearOption.SavedMission;

namespace Cysharp.Threading.Tasks
{
    public static class UniTask
    {
        public static TaskCompletionSource<bool>? PendingFrame;
        public static int Frames;
        public static Task NextFrame(CancellationToken cancellationToken)
        {
            Frames++;
            PendingFrame = new();
            return PendingFrame.Task;
        }
    }
    public static class DestroyTokenBoundary
    {
        public static CancellationToken GetCancellationTokenOnDestroy(this UnityEngine.MonoBehaviour owner) => CancellationToken.None;
    }
}

namespace UnityEngine
{
    public static class Time
    {
        public static float unscaledTime;
    }
}

namespace NOVR.VrUi.Native
{
    internal static class BlueprinterContentReadiness
    {
        internal static bool CanLaunch = true;
        internal static Content Read() => new(CanLaunch);
        internal readonly record struct Content(bool CanLaunch);
    }

    public partial class NativeGameActionAdapter
    {
        internal Mission? PickerMission;
        internal MissionKey PickerKey;
        internal bool TryGetOriginalSelectedMission(MissionKey key, out Mission? mission)
        {
            mission = key.Equals(PickerKey) ? PickerMission : null;
            return mission != null;
        }
    }

    // UI construction is a boundary. StartSelectedMission, the async launch,
    // context checks and the duplicate-launch gate are the actual source.
    public partial class NativeSinglePlayerMissionPanel : UnityEngine.MonoBehaviour
    {
        private readonly List<MissionEntry> _filteredMissions = new();
        private int _selectedIndex;
        private readonly MissionLaunchGate _launch = new();
        private readonly NativeGameActionAdapter _actions = new();
        private float _launchStartedAt, _nextLaunchStatusTime;
        private string _launchPhase = "";
        internal string Status = "";
        internal int Locks, Restores;
        internal bool Visible = true;
        internal bool Busy => _launch.IsBusy;
        internal (float started, float next, string phase) Progress => (_launchStartedAt, _nextLaunchStatusTime, _launchPhase);
        internal void SetPicker(MissionKey key, Mission mission)
        {
            _actions.PickerKey = key;
            _actions.PickerMission = mission;
        }
        internal Task ClickStart(MissionKey key)
        {
            _filteredMissions.Clear();
            _filteredMissions.Add(new(key, new()));
            _selectedIndex = 0;
            var before = UniTaskVoid.Started.Count;
            StartSelectedMission();
            return UniTaskVoid.Started.Count == before ? Task.CompletedTask : UniTaskVoid.Started[^1];
        }
        private void SetLaunchStatus(string status) => Status = status;
        private void SetIdleLaunchStatus() => Status = "Content is not ready.";
        private void RefreshLaunchButtons() { }
        private void LockLaunchButtons() => Locks++;
        private void RestoreLaunchButtons() => Restores++;
        private readonly record struct MissionEntry(MissionKey Key, MissionQuickLoad Mission);
    }
}
