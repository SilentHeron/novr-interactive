using NuclearOption.Networking.Lobbies;
using Steamworks;
using UnityEngine;

namespace NOVR.VrUi.Native;

public partial class NativeMultiplayerPanel
{
    // Rows are rebuilt every second and can change order as native metadata
    // arrives. Keep the join destination by identity, never by its old row.
    private LobbyInstance? _selectedLobby;
    private CSteamID _selectedLobbyId;

    private void ResetLobbySelection()
    {
        _selectedIndex = -1;
        _selectedLobby = null;
        _selectedLobbyId = default;
    }

    private int FindSelectedLobbyIndex()
    {
        if (_selectedLobby == null)
            return _visibleLobbies.Count > 0 ? 0 : -1;

        for (var index = 0; index < _visibleLobbies.Count; index++)
        {
            var lobby = _visibleLobbies[index].Lobby;
            // Native player lobbies and dedicated servers both expose LobbyId.
            // An unresolved/invalid ID must not match a different unresolved row.
            if (_selectedLobbyId.m_SteamID != 0
                ? lobby.LobbyId == _selectedLobbyId
                : ReferenceEquals(lobby, _selectedLobby))
                return index;
        }

        // Retain the intended identity through an empty/partial refresh. Joining
        // stays disabled until that server returns or the user selects another.
        return -1;
    }

    private void ApplyFiltersAndSort()
    {
        _visibleLobbies.Clear();
        for (var index = 0; index < _lobbies.Count; index++)
        {
            var lobby = _lobbies[index];
            if (!MatchesFilters(lobby))
                continue;

            _visibleLobbies.Add(lobby);
        }

        _visibleLobbies.Sort(CompareLobbies);
        _page = Mathf.Clamp(_page, 0, GetLastPage());
        _selectedIndex = FindSelectedLobbyIndex();
        if (_selectedIndex >= 0)
            _page = _selectedIndex / PageSize;

        UpdateFilterLabels();
        RefreshList();
        SelectLobby(_selectedIndex);

        if (_statusText != null)
        {
            _statusText.text = _visibleLobbies.Count == 0
                ? $"No lobbies match current filters. Total lobbies visible in game: {_lobbies.Count}."
                : $"{_visibleLobbies.Count} lobbies shown. Click a column header to sort; click again to reverse.";
        }
    }

    private void ReapplyFiltersFromFirstPage()
    {
        _page = 0;
        ResetLobbySelection();
        ApplyFiltersAndSort();
    }

    private void SelectLobby(int index)
    {
        if (index < 0 || index >= _visibleLobbies.Count)
        {
            _selectedIndex = -1;
            if (_titleText != null)
                _titleText.text = "";
            if (_summaryText != null)
                _summaryText.text = "";
            if (_descriptionText != null)
                _descriptionText.text = "";
            if (_joinButton != null)
                _joinButton.interactable = false;
            RefreshList();
            return;
        }

        _selectedIndex = index;
        var lobby = _visibleLobbies[_selectedIndex];
        _selectedLobby = lobby.Lobby;
        _selectedLobbyId = lobby.Lobby.LobbyId;
        if (_titleText != null)
            _titleText.text = lobby.LobbyName;
        if (_summaryText != null)
        {
            _summaryText.text = $"{lobby.MissionName}\n{lobby.MapName}   {lobby.PlayerCountText}   {lobby.PingText}   {lobby.StatusText}";
        }

        if (_descriptionText != null)
        {
            _descriptionText.text = lobby.Description;
        }

        if (_joinButton != null)
        {
            _joinButton.interactable = !lobby.IsFull;
        }

        RefreshList();
    }

    private void JoinSelectedLobby()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _visibleLobbies.Count)
            return;

        var lobby = _visibleLobbies[_selectedIndex];
        if (lobby.HasPassword)
        {
            ShowPasswordPanel(lobby);
            return;
        }

        if (_actions?.TryJoinLobby(lobby.Lobby, null, promptIfPasswordNeeded: false) == true)
        {
            SetStatus($"Joining {lobby.LobbyName}.");
        }
        else
        {
            SetStatus($"Could not join {lobby.LobbyName}.");
        }
    }

    private void SubmitJoinPassword()
    {
        if (!_pendingPasswordLobby.HasValue)
            return;

        var lobby = _pendingPasswordLobby.Value;
        var password = _joinPasswordInput?.text;
        if (string.IsNullOrEmpty(password))
        {
            if (_passwordStatusText != null)
            {
                _passwordStatusText.text = "Enter the server password.";
            }

            return;
        }

        if (!LobbyPassword.TestShortPassword(lobby.Lobby, password))
        {
            if (_passwordStatusText != null)
            {
                _passwordStatusText.text = "Password incorrect.";
            }

            return;
        }

        HidePasswordPanel();
        if (_actions?.TryJoinLobby(lobby.Lobby, password, promptIfPasswordNeeded: false) == true)
        {
            SetStatus($"Joining {lobby.LobbyName}.");
        }
        else
        {
            SetStatus($"Could not join {lobby.LobbyName}.");
        }
    }
}
