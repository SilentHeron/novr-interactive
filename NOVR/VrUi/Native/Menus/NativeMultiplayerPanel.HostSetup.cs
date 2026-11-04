using System;
using System.Collections.Generic;
using NuclearOption.SavedMission;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public partial class NativeMultiplayerPanel
{
    private const int HostMissionPageSize = 9;

    private readonly List<HostMissionEntry> _hostMissions = new();

    private readonly List<Button> _hostMissionButtons = new();

    private readonly List<Text> _hostMissionButtonTexts = new();

    private RectTransform? _hostPanel;

    private Text? _hostMissionTitleText;

    private Text? _hostMissionDescriptionText;

    private Text? _hostMissionPageText;

    private Text? _hostPlayersText;

    private Text? _hostVisibilityText;

    private Text? _hostPasswordToggleText;

    private Text? _hostStatusText;

    private InputField? _hostNameInput;

    private InputField? _hostPasswordInput;

    private int _hostSelectedMissionIndex;

    private int _hostMissionPage;

    private int _hostMaxPlayers = 8;

    private HostLobbyVisibility _hostVisibility = HostLobbyVisibility.Public;

    private bool _hostPasswordEnabled;

    private void BuildHostSetupPanel()
    {
        if (_container == null)
            return;

        _hostPanel = CreatePanel("Host Setup Panel", _container, PanelColor, new Vector2(0f, -15f), new Vector2(1600f, 950f));
        CreateText("Host Header", _hostPanel, "CREATE MULTIPLAYER LOBBY", new Vector2(0f, 420f), new Vector2(1000f, 34f), 22, TextAnchor.MiddleCenter, Color.white);

        var missionPanel = CreatePanel("Host Mission Panel", _hostPanel, new Color(0.035f, 0.043f, 0.048f, 0.95f), new Vector2(-420f, -10f), new Vector2(700f, 780f));
        CreateText("Host Mission Header", missionPanel, "MISSION", new Vector2(0f, 345f), new Vector2(640f, 30f), 18, TextAnchor.MiddleCenter, Color.white);
        for (var index = 0; index < HostMissionPageSize; index++)
        {
            var rowIndex = index;
            var button = CreateMenuButton(
                $"Host Mission {index}",
                missionPanel,
                new Vector2(0f, 280f - index * 55f),
                new Vector2(640f, 42f),
                ButtonColor,
                () => SelectHostMission(_hostMissionPage * HostMissionPageSize + rowIndex),
                13,
                TextAnchor.MiddleLeft);
            _hostMissionButtons.Add(button);
            _hostMissionButtonTexts.Add(button.GetComponentInChildren<Text>());
        }

        CreateMenuButton("<", missionPanel, new Vector2(-95f, -330f), new Vector2(68f, 34f), ButtonColor, PreviousHostMissionPage, 16);
        _hostMissionPageText = CreateText("Host Mission Page", missionPanel, "", new Vector2(0f, -330f), new Vector2(130f, 34f), 13, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", missionPanel, new Vector2(95f, -330f), new Vector2(68f, 34f), ButtonColor, NextHostMissionPage, 16);

        var settingsPanel = CreatePanel("Host Settings Panel", _hostPanel, new Color(0.035f, 0.043f, 0.048f, 0.95f), new Vector2(410f, -10f), new Vector2(760f, 780f));
        _hostMissionTitleText = CreateText("Host Mission Title", settingsPanel, "", new Vector2(0f, 330f), new Vector2(700f, 36f), 20, TextAnchor.MiddleCenter, Color.white);
        _hostMissionDescriptionText = CreateText("Host Mission Description", settingsPanel, "", new Vector2(0f, 230f), new Vector2(700f, 140f), 13, TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.90f, 1f));

        CreateText("Lobby Name Label", settingsPanel, "LOBBY NAME", new Vector2(-250f, 115f), new Vector2(180f, 28f), 13, TextAnchor.MiddleLeft, Color.white);
        _hostNameInput = CreateInputField("Host Lobby Name", settingsPanel, new Vector2(105f, 115f), new Vector2(440f, 36f), "Lobby name");

        CreateText("Players Label", settingsPanel, "MAX PLAYERS", new Vector2(-250f, 60f), new Vector2(180f, 28f), 13, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton("-", settingsPanel, new Vector2(20f, 60f), new Vector2(48f, 32f), ButtonColor, () => ChangeHostMaxPlayers(-1), 16);
        _hostPlayersText = CreateText("Host Players", settingsPanel, "", new Vector2(105f, 60f), new Vector2(90f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("+", settingsPanel, new Vector2(190f, 60f), new Vector2(48f, 32f), ButtonColor, () => ChangeHostMaxPlayers(1), 16);

        CreateText("Visibility Label", settingsPanel, "LOBBY TYPE", new Vector2(-250f, 5f), new Vector2(180f, 28f), 13, TextAnchor.MiddleLeft, Color.white);
        CreateMenuButton("<", settingsPanel, new Vector2(20f, 5f), new Vector2(48f, 32f), ButtonColor, () => CycleHostVisibility(-1), 16);
        _hostVisibilityText = CreateText("Host Visibility", settingsPanel, "", new Vector2(120f, 5f), new Vector2(150f, 32f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", settingsPanel, new Vector2(220f, 5f), new Vector2(48f, 32f), ButtonColor, () => CycleHostVisibility(1), 16);

        CreateText("Password Label", settingsPanel, "PASSWORD", new Vector2(-250f, -50f), new Vector2(180f, 28f), 13, TextAnchor.MiddleLeft, Color.white);
        var passwordToggle = CreateMenuButton("Password Toggle", settingsPanel, new Vector2(20f, -50f), new Vector2(120f, 32f), ButtonColor, ToggleHostPassword, 12);
        _hostPasswordToggleText = passwordToggle.GetComponentInChildren<Text>();
        _hostPasswordInput = CreateInputField("Host Password", settingsPanel, new Vector2(210f, -50f), new Vector2(250f, 36f), "Password", InputField.ContentType.Password);

        _hostStatusText = CreateText("Host Status", settingsPanel, "", new Vector2(0f, -200f), new Vector2(700f, 90f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));

        CreateMenuButton("CANCEL", _hostPanel, new Vector2(-500f, -420f), new Vector2(180f, 42f), BackButtonColor, HideHostSetup, 15);
        CreateMenuButton("HOST LOBBY", _hostPanel, new Vector2(500f, -420f), new Vector2(220f, 42f), JoinButtonColor, HostLobbyClicked, 15);
        _hostPanel.gameObject.SetActive(false);
    }

    private void CreateLobbyClicked()
    {
        ShowHostSetup();
    }

    private void ShowHostSetup()
    {
        _browserListPanel?.gameObject.SetActive(false);
        _detailsPanel?.gameObject.SetActive(false);
        _hostPanel?.gameObject.SetActive(true);
        _passwordPanel?.gameObject.SetActive(false);
        LoadHostMissions();

        RefreshHostControls();
    }

    private void HideHostSetup()
    {
        _browserListPanel?.gameObject.SetActive(true);
        _detailsPanel?.gameObject.SetActive(true);
        _hostPanel?.gameObject.SetActive(false);
        RefreshFromOriginalLobbyList();
    }

    private void LoadHostMissions()
    {
        _hostMissions.Clear();
        _hostMissionPage = 0;
        _hostSelectedMissionIndex = 0;

        try
        {
            MissionGroup.Init();
            var entries = NativeMissionHeaders.QuickLoadMany(MissionGroup.All.GetMissions());
            foreach (var entry in entries)
            {
                if (!HasTag(entry.mission, MissionTag.Multiplayer))
                    continue;

                _hostMissions.Add(new HostMissionEntry(entry.key, entry.mission));
            }
            Debug.Log($"[NOVR] Loaded {_hostMissions.Count} multiplayer missions for the VR host menu.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Failed to load native multiplayer host mission list: {exception}");
            if (_hostStatusText != null)
            {
                _hostStatusText.text = "Failed to load multiplayer missions.";
            }
        }

        SelectHostMission(_hostMissions.Count > 0 ? 0 : -1);
    }

    private static bool HasTag(MissionQuickLoad mission, MissionTag tag)
    {
        var tags = mission.missionSettings.Tags;
        if (tags == null)
            return false;

        foreach (var existing in tags)
        {
            if (existing.Equals(tag))
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshHostControls()
    {
        RefreshHostMissionList();
        if (_hostPlayersText != null)
        {
            _hostPlayersText.text = _hostMaxPlayers.ToString();
        }

        if (_hostVisibilityText != null)
        {
            _hostVisibilityText.text = GetHostVisibilityLabel();
        }

        if (_hostPasswordToggleText != null)
        {
            _hostPasswordToggleText.text = _hostPasswordEnabled ? "[X] Enabled" : "[ ] Disabled";
        }

        if (_hostPasswordInput != null)
        {
            _hostPasswordInput.interactable = _hostPasswordEnabled;
        }
    }

    private void RefreshHostMissionList()
    {
        for (var index = 0; index < HostMissionPageSize; index++)
        {
            var missionIndex = _hostMissionPage * HostMissionPageSize + index;
            var hasMission = missionIndex >= 0 && missionIndex < _hostMissions.Count;
            var button = _hostMissionButtons[index];
            var text = _hostMissionButtonTexts[index];
            button.gameObject.SetActive(hasMission);
            if (!hasMission)
                continue;

            var mission = _hostMissions[missionIndex];
            text.text = $"  {Shorten(mission.Key.Name, 72)}";
            SetButtonColor(button, missionIndex == _hostSelectedMissionIndex ? ButtonSelectedColor : ButtonColor);
        }

        if (_hostMissionPageText != null)
        {
            _hostMissionPageText.text = $"{_hostMissionPage + 1} / {GetLastHostMissionPage() + 1}";
        }
    }

    private void SelectHostMission(int index)
    {
        if (index < 0 || index >= _hostMissions.Count)
        {
            _hostSelectedMissionIndex = -1;
            if (_hostMissionTitleText != null)
                _hostMissionTitleText.text = "";
            if (_hostMissionDescriptionText != null)
                _hostMissionDescriptionText.text = "";
            RefreshHostMissionList();
            return;
        }

        _hostSelectedMissionIndex = index;
        var mission = _hostMissions[index];
        if (_hostMissionTitleText != null)
            _hostMissionTitleText.text = mission.Key.Name;
        if (_hostMissionDescriptionText != null)
            _hostMissionDescriptionText.text = mission.Mission.missionSettings.description ?? "";
        if (_hostNameInput != null && string.IsNullOrWhiteSpace(_hostNameInput.text))
        {
            _hostNameInput.text = $"{mission.Key.Name} [Hosted by {PlayerSettings.playerName_Unsanitized}]";
        }

        RefreshHostMissionList();
    }

    private void PreviousHostMissionPage()
    {
        if (_hostMissionPage <= 0)
            return;

        _hostMissionPage--;
        SelectHostMission(Mathf.Clamp(_hostSelectedMissionIndex, _hostMissionPage * HostMissionPageSize, Mathf.Min(_hostMissions.Count - 1, (_hostMissionPage + 1) * HostMissionPageSize - 1)));
    }

    private void NextHostMissionPage()
    {
        if ((_hostMissionPage + 1) * HostMissionPageSize >= _hostMissions.Count)
            return;

        _hostMissionPage++;
        SelectHostMission(_hostMissionPage * HostMissionPageSize);
    }

    private int GetLastHostMissionPage()
    {
        return Mathf.Max(0, Mathf.CeilToInt(_hostMissions.Count / (float)HostMissionPageSize) - 1);
    }

    private void ChangeHostMaxPlayers(int delta)
    {
        var limit = _actions?.GetTooManyPlayerLimit() ?? 16;
        _hostMaxPlayers = Mathf.Clamp(_hostMaxPlayers + delta, 2, Mathf.Max(2, limit));
        RefreshHostControls();
    }

    private void CycleHostVisibility(int direction)
    {
        var values = new[] { HostLobbyVisibility.Public, HostLobbyVisibility.FriendsOnly, HostLobbyVisibility.Private };
        var currentIndex = Array.IndexOf(values, _hostVisibility);
        if (currentIndex < 0)
            currentIndex = 0;

        var nextIndex = (currentIndex + direction) % values.Length;
        if (nextIndex < 0)
            nextIndex += values.Length;

        _hostVisibility = values[nextIndex];
        RefreshHostControls();
    }

    private void ToggleHostPassword()
    {
        _hostPasswordEnabled = !_hostPasswordEnabled;
        RefreshHostControls();
    }

    private string GetHostVisibilityLabel()
    {
        return _hostVisibility switch
        {
            HostLobbyVisibility.FriendsOnly => "FRIENDS",
            HostLobbyVisibility.Private => "PRIVATE",
            _ => "PUBLIC"
        };
    }

    private enum HostLobbyVisibility
    {
        Public,
        FriendsOnly,
        Private
    }

    private readonly struct HostMissionEntry
    {
        public HostMissionEntry(MissionKey key, MissionQuickLoad mission)
        {
            Key = key;
            Mission = mission;
        }

        public MissionKey Key
        {
            get;
        }
        public MissionQuickLoad Mission
        {
            get;
        }
    }
}
