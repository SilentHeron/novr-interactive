using System;
using Cysharp.Threading.Tasks;
using NuclearOption.Networking;
using NuclearOption.Networking.Lobbies;
using NuclearOption.SavedMission;
using NuclearOption.SceneLoading;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOVR.VrUi.Native;

public partial class NativeMultiplayerPanel
{
    private bool _isHosting;
    private void HostLobbyClicked()
    {
        if (_isHosting)
            return;

        if (_hostSelectedMissionIndex < 0 || _hostSelectedMissionIndex >= _hostMissions.Count)
        {
            SetHostStatus("Select a multiplayer mission first.");
            return;
        }

        var password = _hostPasswordEnabled ? _hostPasswordInput?.text : null;
        if (_hostPasswordEnabled && string.IsNullOrEmpty(password))
        {
            SetHostStatus("Enter a password or disable password hosting.");
            return;
        }

        var entry = _hostMissions[_hostSelectedMissionIndex];
        var lobbyName = _hostNameInput?.text;
        if (string.IsNullOrWhiteSpace(lobbyName))
        {
            lobbyName = $"{entry.Key.Name} [Hosted by {PlayerSettings.playerName_Unsanitized}]";
        }

        HostLobbyAsync(entry, lobbyName ?? entry.Key.Name, _hostMaxPlayers, _hostVisibility, password).Forget();
    }

    private async UniTaskVoid HostLobbyAsync(HostMissionEntry entry, string lobbyName, int maxPlayers, HostLobbyVisibility visibility, string? password)
    {
        if (_isHosting)
            return;
        _isHosting = true;
        HostRequestContext? context = null;
        HostedLobbyInstance? created = null;
        var completed = false;
        try
        {
            var service = SteamLobby.instance;
            var manager = NetworkManagerNuclearOption.i;
            if (this == null || GameManager.gameState != GameState.Menu || !NativeLobbyOwnership.CanBegin(service, manager))
            {
                SetHostStatus("Hosting is unavailable while another session or lobby request is active.");
                return;
            }

            context = new HostRequestContext(this, service, manager);
            if (!entry.Key.TryLoad(out var mission, out var error))
            {
                SetHostStatus(error);
                Debug.LogWarning($"[NOVR] Native multiplayer failed to load host mission '{entry.Key}': {error}");
                return;
            }
            if (!context.IsCurrent)
                return;

            SetHostStatus("Creating Steam lobby.");
            // Native HostLobby has no mission dependency. Keep the current
            // mission untouched until Steam succeeds and this context is valid.
            service.CheckRelayLocationTask();
            created = await service.HostLobby(maxPlayers, GetSteamLobbyType(visibility));
            if (!created.HasValue)
            {
                SetHostStatus("Steam lobby creation failed.");
                return;
            }
            if (!context.IsCurrent || !NativeLobbyOwnership.OwnsIdleLobby(service, manager, created.Value))
            {
                SetHostStatus("Hosting canceled because the session changed.");
                return;
            }

            var lobby = created.Value;
            service.CurrentLobbyName = lobbyName;
            lobby.SetData("name", lobbyName.SanitizeRichText(128));
            lobby.SetData("mission_name", mission.Name.SanitizeRichText(128));
            lobby.SetData("mission_description", mission.missionSettings.description.SanitizeRichText(1000));
            lobby.SetData("mission_pvp_type", MissionTag.GetPvpTypeLobbyString(mission));
            if (TryGetMapName(mission.MapKey, out var mapName))
            {
                lobby.SetData("map_name", mapName);
            }

            var publishedFileId = mission.LoadKey?.WorkshopId;
            if (publishedFileId.HasValue)
            {
                lobby.SetData("mission_workshop_id", publishedFileId.Value.m_PublishedFileId.ToString("X"));
            }

            var hasPassword = !string.IsNullOrEmpty(password);
            if (hasPassword)
            {
                lobby.SetData("short_password", LobbyPassword.GetShortPassword(password));
            }

            var moddedServer = NetworkManagerNuclearOption.ModdedServer == true;
            var allowsEventContent = mission.missionSettings?.allowEventContent == true;
            lobby.SetData("modded_server", LobbyInstance.BoolToTag(moddedServer || allowsEventContent));

            SetHostStatus("Starting host.");
            var options = new HostOptions(SocketType.Steam, GameState.Multiplayer, mission.MapKey)
            {
                MaxConnections = maxPlayers - 1,
                Password = hasPassword ? password : null
            };

            context.SetHostMission(mission);
            // SetMission invokes native listeners synchronously. A listener may
            // enter another session or replace the services before it returns.
            if (!context.IsCurrent || !NativeLobbyOwnership.OwnsIdleLobby(service, manager, lobby))
                return;
            await manager.StartHostAsync(options);
            // Native StartHostAsync returns normally even when map loading fails
            // and it stops the server. That idle lobby still needs our cleanup.
            completed = manager != null && manager.Server != null && manager.Server.Active;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Native multiplayer host failed: {exception}");
            SetHostStatus("Host failed. Check the BepInEx log for details.");
        }
        finally
        {
            context?.Dispose();
            if (!completed && created.HasValue && context != null)
            {
                try
                {
                    if (!NativeLobbyOwnership.TryRelease(context.Service, context.Manager, created.Value))
                        Debug.LogWarning("[NOVR] Unfinished host request no longer owns an idle lobby; newer session state was left untouched.");
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[NOVR] Could not release the unfinished host lobby: {exception.Message}");
                }
            }
            _isHosting = false;
        }
    }


    private static ELobbyType GetSteamLobbyType(HostLobbyVisibility visibility)
    {
        return visibility switch
        {
            HostLobbyVisibility.FriendsOnly => ELobbyType.k_ELobbyTypeFriendsOnly,
            HostLobbyVisibility.Private => ELobbyType.k_ELobbyTypePrivate,
            _ => ELobbyType.k_ELobbyTypePublic
        };
    }

    private static bool TryGetMapName(MapKey mapKey, out string mapName)
    {
        var mapLoaders = Resources.FindObjectsOfTypeAll<MapLoader>();
        for (var index = 0; index < mapLoaders.Length; index++)
        {
            if (mapLoaders[index] != null && mapLoaders[index].TryGetMapName(mapKey, out mapName))
            {
                return true;
            }
        }

        mapName = "";
        return false;
    }

    private void SetHostStatus(string status)
    {
        if (this != null && _wasVisible && _hostStatusText != null)
        {
            _hostStatusText.text = status;
        }
    }

    // This is a single accepted host request, not the panel's visibility.
    // Hiding/reopening the menu keeps it busy; replacing the actual context does
    // not allow an old Steam callback to launch into the new scene or mission.
    private sealed class HostRequestContext : IDisposable
    {
        internal readonly SteamLobby Service;
        internal readonly NetworkManagerNuclearOption Manager;
        private readonly NativeMultiplayerPanel _owner;
        private readonly int _scene;
        private Mission? _mission;
        private bool _changed, _disposed, _observing;

        internal HostRequestContext(NativeMultiplayerPanel owner, SteamLobby service, NetworkManagerNuclearOption manager)
        {
            _owner = owner;
            Service = service;
            Manager = manager;
            _scene = SceneManager.GetActiveScene().handle;
            _mission = MissionManager.CurrentMission;
            GameManager.OnGameStateChanged.AddListener(ContextChanged);
            MissionManager.onMissionLoad += MissionChanged;
            SceneManager.activeSceneChanged += SceneChanged;
            _observing = true;
        }

        internal bool IsCurrent => !_changed && _owner != null && Service != null && Manager != null &&
            ReferenceEquals(SteamLobby.instance, Service) && ReferenceEquals(NetworkManagerNuclearOption.i, Manager) &&
            ReferenceEquals(Service.Manager, Manager) && GameManager.gameState == GameState.Menu &&
            SceneManager.GetActiveScene().handle == _scene && ReferenceEquals(MissionManager.CurrentMission, _mission);

        internal void SetHostMission(Mission mission)
        {
            _mission = mission;
            MissionManager.SetMission(mission, checkIfSame: false);
        }

        private void ContextChanged()
        {
            // Mirage's AddLateEvent replays the current state during AddListener.
            // Only subsequent changes invalidate the captured menu context.
            if (_observing)
                _changed = true;
        }
        private void MissionChanged(Mission mission)
        {
            if (!ReferenceEquals(mission, _mission))
                _changed = true;
        }
        private void SceneChanged(Scene previous, Scene current) => _changed = true;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            GameManager.OnGameStateChanged.RemoveListener(ContextChanged);
            MissionManager.onMissionLoad -= MissionChanged;
            SceneManager.activeSceneChanged -= SceneChanged;
        }
    }

}
