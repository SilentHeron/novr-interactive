using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public partial class NativeMultiplayerPanel
{
    private const int PageSize = 10;

    private readonly List<NativeLobbyEntry> _lobbies = new();

    private readonly List<NativeLobbyEntry> _visibleLobbies = new();

    private readonly List<NativeLobbyRow> _lobbyRows = new();

    private readonly List<SortHeaderBinding> _sortHeaders = new();

    private readonly List<FilterToggleBinding> _filterToggles = new();

    private RectTransform? _browserListPanel;

    private RectTransform? _detailsPanel;

    private Text? _titleText;

    private Text? _summaryText;

    private Text? _descriptionText;

    private Text? _statusText;

    private Text? _pageText;

    private Text? _maxPingFilterText;

    private Text? _distanceFilterText;

    private InputField? _searchInput;

    private Button? _joinButton;

    private int _selectedIndex = -1;

    private int _page;

    private readonly LobbyBrowserQuery _browserQuery = new();

    private void BuildBrowserView(RectTransform parent)
    {
        var listPanel = CreatePanel("Lobby List Panel", parent, PanelColor, new Vector2(-330f, -15f), new Vector2(1220f, 950f));
        _browserListPanel = listPanel;
        CreateText("List Header", listPanel, "SERVER BROWSER", new Vector2(0f, 430f), new Vector2(1160f, 30f), 18, TextAnchor.MiddleCenter, Color.white);
        BuildFilterControls(listPanel);
        BuildColumnHeaders(listPanel);

        for (var index = 0; index < PageSize; index++)
        {
            var rowIndex = index;
            _lobbyRows.Add(CreateLobbyRow(listPanel, index, new Vector2(0f, 212f - index * 45f), () => SelectLobby(_page * PageSize + rowIndex)));
        }

        CreateMenuButton("REFRESH", listPanel, new Vector2(-430f, -420f), new Vector2(170f, 34f), ButtonColor, RefreshClicked, 13);
        CreateMenuButton("<", listPanel, new Vector2(-105f, -420f), new Vector2(62f, 34f), ButtonColor, PreviousPage, 16);
        _pageText = CreateText("Page", listPanel, "", new Vector2(0f, -420f), new Vector2(140f, 34f), 13, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", listPanel, new Vector2(105f, -420f), new Vector2(62f, 34f), ButtonColor, NextPage, 16);
        CreateMenuButton("CREATE LOBBY", listPanel, new Vector2(430f, -420f), new Vector2(190f, 34f), ButtonColor, CreateLobbyClicked, 13);

        var detailsPanel = CreatePanel("Lobby Details Panel", parent, PanelColor, new Vector2(630f, -15f), new Vector2(690f, 950f));
        _detailsPanel = detailsPanel;
        _titleText = CreateText("Lobby Title", detailsPanel, "", new Vector2(0f, 395f), new Vector2(630f, 46f), 21, TextAnchor.MiddleCenter, Color.white);
        _summaryText = CreateText("Lobby Summary", detailsPanel, "", new Vector2(0f, 330f), new Vector2(630f, 62f), 14, TextAnchor.MiddleCenter, new Color(0.82f, 0.86f, 0.72f, 1f));
        _descriptionText = CreateText("Lobby Description", detailsPanel, "", new Vector2(0f, 75f), new Vector2(630f, 450f), 14, TextAnchor.UpperLeft, new Color(0.84f, 0.88f, 0.90f, 1f));
        _statusText = CreateText("Lobby Status", detailsPanel, "", new Vector2(0f, -345f), new Vector2(630f, 76f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));
    }

    private void BuildFilterControls(RectTransform listPanel)
    {
        var labelColor = new Color(0.78f, 0.84f, 0.86f, 1f);
        CreateText("Search Filter Label", listPanel, "SEARCH", new Vector2(-545f, 392f), new Vector2(92f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        _searchInput = CreateInputField("Lobby Search", listPanel, new Vector2(-330f, 392f), new Vector2(330f, 30f), "Lobby, mission, map");
        _searchInput.onValueChanged.AddListener(_ => ReapplyFiltersFromFirstPage());
        CreateMenuButton("CLEAR", listPanel, new Vector2(-120f, 392f), new Vector2(82f, 30f), ButtonColor, ClearSearch, 11);

        CreateText("Max Ping Filter Label", listPanel, "MAX PING", new Vector2(25f, 392f), new Vector2(104f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateMenuButton("<", listPanel, new Vector2(105f, 392f), new Vector2(42f, 30f), ButtonColor, () => CycleMaxPingFilter(-1), 14);
        _maxPingFilterText = CreateText("Max Ping Filter", listPanel, "", new Vector2(178f, 392f), new Vector2(96f, 30f), 11, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", listPanel, new Vector2(250f, 392f), new Vector2(42f, 30f), ButtonColor, () => CycleMaxPingFilter(1), 14);

        CreateText("Distance Filter Label", listPanel, "DISTANCE", new Vector2(355f, 392f), new Vector2(104f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateMenuButton("<", listPanel, new Vector2(440f, 392f), new Vector2(42f, 30f), ButtonColor, () => CycleDistanceFilter(-1), 14);
        _distanceFilterText = CreateText("Distance Filter", listPanel, "", new Vector2(520f, 392f), new Vector2(112f, 30f), 11, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", listPanel, new Vector2(595f, 392f), new Vector2(42f, 30f), ButtonColor, () => CycleDistanceFilter(1), 14);

        CreateText("Mission Filter Label", listPanel, "MISSION", new Vector2(-545f, 350f), new Vector2(92f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateFilterToggle(listPanel, "PvE", new Vector2(-460f, 350f), new Vector2(82f, 28f), () => _browserQuery.ShowPve, value => _browserQuery.ShowPve = value);
        CreateFilterToggle(listPanel, "PvP", new Vector2(-370f, 350f), new Vector2(82f, 28f), () => _browserQuery.ShowPvp, value => _browserQuery.ShowPvp = value);

        CreateText("Access Filter Label", listPanel, "ACCESS", new Vector2(-160f, 350f), new Vector2(92f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateFilterToggle(listPanel, "Open", new Vector2(-68f, 350f), new Vector2(94f, 28f), () => _browserQuery.ShowOpenLobbies, value => _browserQuery.ShowOpenLobbies = value);
        CreateFilterToggle(listPanel, "Pwd", new Vector2(35f, 350f), new Vector2(90f, 28f), () => _browserQuery.ShowPasswordedLobbies, value => _browserQuery.ShowPasswordedLobbies = value);

        CreateText("Mods Filter Label", listPanel, "MODS", new Vector2(160f, 350f), new Vector2(76f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateFilterToggle(listPanel, "Vanilla", new Vector2(270f, 350f), new Vector2(118f, 28f), () => _browserQuery.ShowVanillaLobbies, value => _browserQuery.ShowVanillaLobbies = value);
        CreateFilterToggle(listPanel, "Modded", new Vector2(405f, 350f), new Vector2(118f, 28f), () => _browserQuery.ShowModdedLobbies, value => _browserQuery.ShowModdedLobbies = value);

        CreateText("Server Filter Label", listPanel, "SERVER", new Vector2(-545f, 310f), new Vector2(92f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateFilterToggle(listPanel, "Dedicated", new Vector2(-430f, 310f), new Vector2(132f, 28f), () => _browserQuery.ShowDedicatedServers, value => _browserQuery.ShowDedicatedServers = value);
        CreateFilterToggle(listPanel, "Player Host", new Vector2(-275f, 310f), new Vector2(142f, 28f), () => _browserQuery.ShowPlayerHostedServers, value => _browserQuery.ShowPlayerHostedServers = value);

        CreateText("Availability Filter Label", listPanel, "AVAIL", new Vector2(-105f, 310f), new Vector2(76f, 24f), 10, TextAnchor.MiddleCenter, labelColor);
        CreateFilterToggle(listPanel, "Hide Full", new Vector2(20f, 310f), new Vector2(122f, 28f), () => _browserQuery.HideFullLobbies, value => _browserQuery.HideFullLobbies = value);
        CreateFilterToggle(listPanel, "Hide Empty", new Vector2(160f, 310f), new Vector2(134f, 28f), () => _browserQuery.HideEmptyLobbies, value => _browserQuery.HideEmptyLobbies = value);
    }

    private void BuildColumnHeaders(RectTransform listPanel)
    {
        CreateSortHeaderButton(listPanel, LobbySortColumn.Type, "TYPE", new Vector2(-548f, 260f), new Vector2(64f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Password, "LOCK", new Vector2(-475f, 260f), new Vector2(66f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Mods, "MODS", new Vector2(-395f, 260f), new Vector2(78f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Server, "HOST", new Vector2(-298f, 260f), new Vector2(100f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Lobby, "LOBBY", new Vector2(-69f, 260f), new Vector2(342f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Mission, "MISSION", new Vector2(263f, 260f), new Vector2(306f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Players, "PLAYERS", new Vector2(465f, 260f), new Vector2(82f, 30f));
        CreateSortHeaderButton(listPanel, LobbySortColumn.Ping, "PING", new Vector2(547f, 260f), new Vector2(66f, 30f));
    }

    private void CreateSortHeaderButton(RectTransform parent, LobbySortColumn column, string label, Vector2 anchoredPosition, Vector2 size)
    {
        var button = CreateMenuButton($"Sort {label}", parent, anchoredPosition, size, ButtonColor, () => SetSortColumn(column), 11);
        var text = button.GetComponentInChildren<Text>();
        _sortHeaders.Add(new SortHeaderBinding(column, label, button, text));
    }

    private void CreateFilterToggle(RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size, Func<bool> getValue, Action<bool> setValue)
    {
        var button = CreateMenuButton($"Filter {label}", parent, anchoredPosition, size, ButtonColor, () =>
        {
            setValue(!getValue());
            ReapplyFiltersFromFirstPage();
        }, 11, TextAnchor.MiddleCenter);
        var text = button.GetComponentInChildren<Text>();
        _filterToggles.Add(new FilterToggleBinding(label, button, text, getValue));
    }

    private NativeLobbyRow CreateLobbyRow(RectTransform parent, int index, Vector2 anchoredPosition, UnityEngine.Events.UnityAction onClick)
    {
        var button = CreateButton($"Lobby Row {index}", parent, anchoredPosition, new Vector2(1180f, 38f), ButtonColor, onClick);
        var rowTransform = (RectTransform)button.transform;
        return new NativeLobbyRow(
            button,
            CreateColumnText("Type", rowTransform, new Vector2(-548f, 0f), new Vector2(64f, 30f), TextAnchor.MiddleCenter),
            CreateColumnText("Lock", rowTransform, new Vector2(-475f, 0f), new Vector2(66f, 30f), TextAnchor.MiddleCenter),
            CreateColumnText("Mods", rowTransform, new Vector2(-395f, 0f), new Vector2(78f, 30f), TextAnchor.MiddleCenter),
            CreateColumnText("Host", rowTransform, new Vector2(-298f, 0f), new Vector2(100f, 30f), TextAnchor.MiddleCenter),
            CreateColumnText("Lobby", rowTransform, new Vector2(-69f, 0f), new Vector2(342f, 30f), TextAnchor.MiddleLeft),
            CreateColumnText("Mission", rowTransform, new Vector2(263f, 0f), new Vector2(306f, 30f), TextAnchor.MiddleLeft),
            CreateColumnText("Players", rowTransform, new Vector2(465f, 0f), new Vector2(82f, 30f), TextAnchor.MiddleCenter),
            CreateColumnText("Ping", rowTransform, new Vector2(547f, 0f), new Vector2(66f, 30f), TextAnchor.MiddleCenter));
    }

    private Text CreateColumnText(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 size, TextAnchor alignment)
    {
        var text = CreateText(name, parent, "", anchoredPosition, size, 12, alignment, Color.white);
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        return text;
    }

    private void UpdateFilterLabels()
    {
        for (var index = 0; index < _filterToggles.Count; index++)
        {
            var toggle = _filterToggles[index];
            var enabled = toggle.IsEnabled();
            toggle.Text.text = $"{(enabled ? "[X]" : "[ ]")} {toggle.Label}";
            SetButtonColor(toggle.Button, enabled ? ButtonSelectedColor : FilterDisabledColor);
        }

        for (var index = 0; index < _sortHeaders.Count; index++)
        {
            var header = _sortHeaders[index];
            var selected = header.Column == _browserQuery.SortColumn;
            var direction = selected ? (_browserQuery.SortAscending ? " ^" : " v") : "";
            header.Text.text = header.Label + direction;
            SetButtonColor(header.Button, selected ? ButtonSelectedColor : ButtonColor);
        }

        if (_maxPingFilterText != null)
        {
            _maxPingFilterText.text = _browserQuery.GetMaxPingFilterLabel();
        }

        if (_distanceFilterText != null)
        {
            _distanceFilterText.text = _browserQuery.GetDistanceFilterLabel();
        }
    }

    private void ClearSearch()
    {
        if (_searchInput == null)
            return;

        if (string.IsNullOrEmpty(_searchInput.text))
        {
            ReapplyFiltersFromFirstPage();
            return;
        }

        _searchInput.text = "";
    }

    private void RefreshList()
    {
        for (var index = 0; index < PageSize; index++)
        {
            var lobbyIndex = _page * PageSize + index;
            var hasLobby = lobbyIndex >= 0 && lobbyIndex < _visibleLobbies.Count;
            var row = _lobbyRows[index];
            row.Button.gameObject.SetActive(hasLobby);
            if (!hasLobby)
                continue;

            var lobby = _visibleLobbies[lobbyIndex];
            row.TypeText.text = lobby.MissionTypeText;
            row.LockText.text = lobby.AccessText;
            row.ModsText.text = lobby.ModsText;
            row.HostText.text = lobby.ServerColumnText;
            row.LobbyText.text = Shorten(lobby.LobbyName, 44);
            row.MissionText.text = Shorten(lobby.MissionName, 40);
            row.PlayersText.text = lobby.PlayerCountText;
            row.PingText.text = lobby.PingText;
            SetButtonColor(row.Button, lobbyIndex == _selectedIndex ? ButtonSelectedColor : ButtonColor);
        }

        if (_pageText != null)
        {
            _pageText.text = $"{_page + 1} / {GetLastPage() + 1}";
        }
    }

    private void PreviousPage()
    {
        if (_page <= 0)
            return;

        _page--;
        SelectLobby(Mathf.Clamp(_selectedIndex, _page * PageSize, Mathf.Min(_visibleLobbies.Count - 1, (_page + 1) * PageSize - 1)));
    }

    private void NextPage()
    {
        if ((_page + 1) * PageSize >= _visibleLobbies.Count)
            return;

        _page++;
        SelectLobby(_page * PageSize);
    }

    private void SetStatus(string status)
    {
        if (_statusText != null)
        {
            _statusText.text = status;
        }
    }

    private int GetLastPage()
    {
        return Mathf.Max(0, Mathf.CeilToInt(_visibleLobbies.Count / (float)PageSize) - 1);
    }

    private sealed class NativeLobbyRow
    {
        public NativeLobbyRow(Button button, Text typeText, Text lockText, Text modsText, Text hostText, Text lobbyText, Text missionText, Text playersText, Text pingText)
        {
            Button = button;
            TypeText = typeText;
            LockText = lockText;
            ModsText = modsText;
            HostText = hostText;
            LobbyText = lobbyText;
            MissionText = missionText;
            PlayersText = playersText;
            PingText = pingText;
        }

        public Button Button
        {
            get;
        }
        public Text TypeText
        {
            get;
        }
        public Text LockText
        {
            get;
        }
        public Text ModsText
        {
            get;
        }
        public Text HostText
        {
            get;
        }
        public Text LobbyText
        {
            get;
        }
        public Text MissionText
        {
            get;
        }
        public Text PlayersText
        {
            get;
        }
        public Text PingText
        {
            get;
        }
    }

    private readonly struct SortHeaderBinding
    {
        public SortHeaderBinding(LobbySortColumn column, string label, Button button, Text text)
        {
            Column = column;
            Label = label;
            Button = button;
            Text = text;
        }

        public LobbySortColumn Column
        {
            get;
        }
        public string Label
        {
            get;
        }
        public Button Button
        {
            get;
        }
        public Text Text
        {
            get;
        }
    }

    private readonly struct FilterToggleBinding
    {
        public FilterToggleBinding(string label, Button button, Text text, Func<bool> isEnabled)
        {
            Label = label;
            Button = button;
            Text = text;
            IsEnabled = isEnabled;
        }

        public string Label
        {
            get;
        }
        public Button Button
        {
            get;
        }
        public Text Text
        {
            get;
        }
        public Func<bool> IsEnabled
        {
            get;
        }
    }

    private bool MatchesFilters(NativeLobbyEntry lobby) => _browserQuery.Matches(lobby, _searchInput?.text ?? "");
    private int CompareLobbies(NativeLobbyEntry left, NativeLobbyEntry right) => _browserQuery.Compare(left, right);

    private void SetSortColumn(LobbySortColumn column)
    {
        _browserQuery.SetSortColumn(column);
        ReapplyFiltersFromFirstPage();
    }

    private void CycleMaxPingFilter(int direction)
    {
        _browserQuery.CycleMaxPingFilter(direction);
        ReapplyFiltersFromFirstPage();
    }

    private void CycleDistanceFilter(int direction)
    {
        _browserQuery.CycleDistanceFilter(direction);
        ReapplyFiltersFromFirstPage();
    }
}
