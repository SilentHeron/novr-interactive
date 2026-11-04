using System;
using Cysharp.Threading.Tasks;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOVR.VrUi.Native;

public partial class NativeSinglePlayerMissionPanel
{
    private void StartSelectedMission()
    {
        if (_launch.IsBusy || _selectedIndex < 0 || _selectedIndex >= _filteredMissions.Count)
            return;
        if (!BlueprinterContentReadiness.Read().CanLaunch)
        {
            SetIdleLaunchStatus();
            RefreshLaunchButtons();
            return;
        }
        var manager = NetworkManagerNuclearOption.i;
        if (manager == null)
        {
            SetLaunchStatus("The game is still preparing. Please try again in a moment.");
            return;
        }
        if (!_launch.TryBegin(HasActiveSession()))
        {
            SetLaunchStatus("A session is already open. Return to the main menu before starting another mission.");
            RefreshLaunchButtons();
            return;
        }

        // Lock before loading or yielding, including direct event invocations.
        _launchStartedAt = Time.unscaledTime;
        _launchPhase = "Preparing mission";
        _nextLaunchStatusTime = 0;
        LockLaunchButtons();
        StartMissionAsync(_filteredMissions[_selectedIndex].Key, manager).Forget();
    }

    private async UniTaskVoid StartMissionAsync(MissionKey missionKey, NetworkManagerNuclearOption manager)
    {
        var scene = SceneManager.GetActiveScene().handle;
        var previousMission = MissionManager.CurrentMission;
        try
        {
            SetLaunchStatus("Preparing mission…");
            // Let the disabled buttons and status render before synchronous IO.
            await UniTask.NextFrame(cancellationToken: this.GetCancellationTokenOnDestroy());
            // The loader may have started in the yielded frame. Never race its
            // temporary asset-preload host even if a click was already accepted.
            if (!BlueprinterContentReadiness.Read().CanLaunch)
            {
                SetIdleLaunchStatus();
                return;
            }
            if (!CanStartMission(manager, scene, previousMission))
            {
                SetLaunchStatus("The session changed. Return to the mission menu and try again.");
                return;
            }
            // Customize edits the native selected mission in memory. Use that
            // instance when it matches this row, falling back to disk only when
            // its picker is unavailable.
            Mission? mission = null;
            var fromPicker = _actions?.TryGetOriginalSelectedMission(missionKey, out mission) == true;
            if (!fromPicker && !missionKey.TryLoad(out mission, out var error))
            {
                SetLaunchStatus("Couldn't open this mission. Choose another mission or try again.\n" + error);
                Debug.LogWarning($"[NOVR] Native single player failed to load mission '{missionKey}': {error}");
                return;
            }
            if (mission == null)
            {
                SetLaunchStatus("No mission selected. Choose a mission and try again.");
                return;
            }
            if (!CanStartMission(manager, scene, previousMission))
                return;

            _launchPhase = "Loading map";
            SetLaunchStatus("Loading map…");
            MissionManager.SetMission(mission, checkIfSame: false);
            // Native mission listeners run synchronously. Do not launch if one
            // replaced the mission, manager, or menu context while handling it.
            if (!CanStartMission(manager, scene, mission))
                return;
            await manager.StartHostAsync(new HostOptions(SocketType.Offline, GameState.SinglePlayer, mission.MapKey));
            if (this != null)
                SetLaunchStatus(manager != null && manager.Server.Active
                    ? "Mission ready." : "The map couldn't load. Choose another mission or try again.");
        }
        catch (OperationCanceledException)
        {
            if (this != null)
                SetLaunchStatus("Mission loading stopped. Return to the main menu to try again.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Native single player launch failed: {exception}");
            if (this != null)
                SetLaunchStatus("Couldn't start the mission. Return to the main menu and try again.");
        }
        finally
        {
            _launch.Finish();
            if (this != null)
                RestoreLaunchButtons();
        }
    }

    private bool CanStartMission(NetworkManagerNuclearOption manager, int scene, Mission? expectedMission) =>
        this != null && manager != null && ReferenceEquals(NetworkManagerNuclearOption.i, manager) &&
        GameManager.gameState == GameState.Menu && SceneManager.GetActiveScene().handle == scene &&
        ReferenceEquals(MissionManager.CurrentMission, expectedMission) &&
        manager.Server != null && manager.Client != null && !manager.Server.Active && !manager.Client.Active;

    private static bool HasActiveSession()
    {
        var manager = NetworkManagerNuclearOption.i;
        return manager != null && ((manager.Server != null && manager.Server.Active) ||
                                   (manager.Client != null && manager.Client.Active));
    }

}
