using System.Collections.Generic;
using System.Linq;
using NuclearOption.SavedMission;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public partial class NativeSinglePlayerMissionPanel : MonoBehaviour
{
    private const int PageSize = 13;

    private static readonly Color BackgroundColor = new(0.025f, 0.035f, 0.045f, 0.92f);
    private static readonly Color PanelColor = new(0.05f, 0.06f, 0.065f, 0.94f);
    private static readonly Color ButtonColor = new(0.24f, 0.29f, 0.31f, 0.96f);
    private static readonly Color ButtonSelectedColor = new(0.44f, 0.49f, 0.50f, 1f);
    private static readonly Color ButtonHoverColor = new(0.34f, 0.40f, 0.42f, 1f);
    private static readonly Color ButtonPressedColor = new(0.16f, 0.20f, 0.22f, 1f);
    private static readonly Color BackButtonColor = new(0.62f, 0.12f, 0.14f, 0.96f);
    private static readonly Color StartButtonColor = new(0.12f, 0.34f, 0.20f, 0.96f);

    private readonly List<MissionEntry> _missions = new();
    private readonly List<MissionEntry> _filteredMissions = new();
    private readonly List<Button> _missionButtons = new();
    private readonly List<Text> _missionButtonTexts = new();
    private readonly List<Button> _groupFilterButtons = new();
    private readonly List<Text> _groupFilterTexts = new();
    private readonly List<Button> _tagFilterButtons = new();
    private readonly List<Text> _tagFilterTexts = new();
    private readonly List<TagFilterDefinition> _tagFilters = new();

    private NativeGameActionAdapter? _actions;
    private RectTransform? _container;
    private Font? _font;
    private Text? _titleText;
    private Text? _descriptionText;
    private ScrollRect? _descriptionScroll;
    private Text? _tagsText;
    private Text? _pageText;
    private Button? _startButton;
    private Button? _customizeButton;
    private Text? _startButtonText;
    private Text? _launchStatusText;
    private readonly MissionLaunchGate _launch = new();
    private readonly List<(Button button, bool interactable)> _launchButtonStates = new();
    private float _launchStartedAt, _nextLaunchStatusTime;
    private string _launchPhase = "";
    private int _contentRevision = -1;
    private float _nextContentCheck;
    private int _selectedIndex;
    private int _page;
    private MissionGroupFilter _activeGroupFilter = MissionGroupFilter.All;
    private int _activeTagFilterIndex;
    private bool _visible;

    public void Initialize(NativeGameActionAdapter actions, RectTransform root)
    {
        _actions = actions;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildLayout(root);
    }

    public void SetVisible(bool visible)
    {
        if (_container == null)
            return;

        if (visible && !_visible && !_launch.IsBusy)
        {
            LoadMissions();
            SetIdleLaunchStatus();
            _contentRevision = BlueprinterContentReadiness.Read().Revision;
            RefreshLaunchButtons();
        }

        if (!visible && _visible)
            ClearMissionPreview();
        _visible = visible;
        NativePanelTransition.SetVisible(_container, visible);
    }

    private void BuildLayout(RectTransform root)
    {
        _container = CreateContainer("Native Single Player Missions", root, root.sizeDelta);
        CreateImage("Background", _container, BackgroundColor, Vector2.zero, _container.sizeDelta);
        CreateText("Header", _container, "SINGLE PLAYER MISSIONS", new Vector2(0f, NativeUiLayout.HeaderY), NativeUiLayout.HeaderSize, 22, TextAnchor.MiddleCenter, Color.white);

        var listPanel = CreatePanel("Mission List Panel", _container, PanelColor, new Vector2(-480f, -15f), new Vector2(820f, 950f));
        CreateText("List Header", listPanel, "SELECT MISSION", new Vector2(0f, 430f), new Vector2(760f, 30f), 18, TextAnchor.MiddleCenter, Color.white);

        BuildGroupFilters(listPanel);
        BuildTagFilters(listPanel);

        for (var index = 0; index < PageSize; index++)
        {
            var rowIndex = index;
            var row = CreateMenuButton(
                $"Mission Row {index}",
                listPanel,
                new Vector2(110f, 280f - index * 42f),
                new Vector2(610f, 34f),
                ButtonColor,
                () => SelectMission(_page * PageSize + rowIndex),
                13,
                TextAnchor.MiddleLeft);
            _missionButtons.Add(row);
            _missionButtonTexts.Add(row.GetComponentInChildren<Text>());
        }

        CreateMenuButton("OPEN FOLDER", listPanel, new Vector2(-330f, -360f), new Vector2(150f, 32f), ButtonColor, OpenUserFolder, 11);
        CreateMenuButton("Prev Page", listPanel, new Vector2(-230f, -420f), new Vector2(160f, 34f), ButtonColor, PreviousPage, 14);
        _pageText = CreateText("Page", listPanel, "", new Vector2(0f, -420f), new Vector2(190f, 34f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton("Next Page", listPanel, new Vector2(230f, -420f), new Vector2(160f, 34f), ButtonColor, NextPage, 14);

        var detailsPanel = CreatePanel("Mission Details Panel", _container, PanelColor, new Vector2(455f, -15f), new Vector2(900f, 950f));
        _titleText = CreateText("Mission Title", detailsPanel, "", new Vector2(0f, 395f), new Vector2(820f, 44f), 21, TextAnchor.MiddleCenter, Color.white);
        _tagsText = CreateText("Mission Tags", detailsPanel, "", new Vector2(0f, 345f), new Vector2(820f, 28f), 13, TextAnchor.MiddleCenter, new Color(0.82f, 0.86f, 0.72f, 1f));
        var previewFrame = CreateImage("Mission Preview Frame", detailsPanel, new Color(.025f, .035f, .045f, 1), new Vector2(0, 140), new Vector2(820, 350));
        previewFrame.GetComponent<Image>().raycastTarget = false;
        _previewImage = CreateImage("Mission Preview", previewFrame, Color.white, Vector2.zero, previewFrame.sizeDelta).GetComponent<Image>();
        _previewImage.preserveAspect = true;
        _previewImage.raycastTarget = false;
        _previewStatusText = CreateText("Preview Status", previewFrame, "Select a mission.", Vector2.zero, new Vector2(780, 40), 16, TextAnchor.MiddleCenter, Color.white);
        _previewStatusText.raycastTarget = false;
        ClearMissionPreview();
        BuildMissionDescription(detailsPanel);
        _launchStatusText = CreateText("Launch Status", detailsPanel, "", new Vector2(0f, -375f), new Vector2(820f, 70f), 15, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));
        _launchStatusText.supportRichText = false;

        CreateMenuButton("BACK", _container, new Vector2(NativeUiLayout.FooterLeftX, NativeUiLayout.FooterY), NativeUiLayout.FooterButtonSize, BackButtonColor, BackToMainMenu, 15);
        _customizeButton = CreateMenuButton("CUSTOMIZE MISSION", _container, new Vector2(540f, NativeUiLayout.FooterY), new Vector2(260f, 44f), ButtonColor, CustomizeSelectedMission, 14);
        _startButton = CreateMenuButton("START MISSION", _container, new Vector2(830f, NativeUiLayout.FooterY), new Vector2(240f, 44f), StartButtonColor, StartSelectedMission, 15);
        _startButtonText = _startButton.GetComponentInChildren<Text>();

        NativePanelTransition.SetVisible(_container, false, instant: true);
    }

    private void BuildMissionDescription(RectTransform detailsPanel)
    {
        var viewport = CreateImage("Briefing Viewport", detailsPanel, Color.clear, new Vector2(-10, -187), new Vector2(796, 260));
        viewport.gameObject.AddComponent<RectMask2D>();
        _descriptionText = CreateText("Mission Description", viewport, "", Vector2.zero, viewport.sizeDelta, 16, TextAnchor.UpperLeft, new Color(.84f, .88f, .90f, 1));
        var content = _descriptionText.rectTransform;
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(.5f, 1);
        content.sizeDelta = new Vector2(0, 260);
        content.anchoredPosition = Vector2.zero;
        _descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _descriptionScroll = viewport.gameObject.AddComponent<ScrollRect>();
        _descriptionScroll.viewport = viewport;
        _descriptionScroll.content = content;
        _descriptionScroll.horizontal = false;
        _descriptionScroll.movementType = ScrollRect.MovementType.Clamped;
        _descriptionScroll.scrollSensitivity = 28;

        var track = CreateImage("Briefing Scrollbar", detailsPanel, new Color(.18f, .22f, .24f, 1), new Vector2(405, -187), new Vector2(12, 260));
        var handle = CreateImage("Handle", track, new Color(.58f, .65f, .68f, 1), Vector2.zero, Vector2.zero);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        _descriptionScroll.verticalScrollbar = scrollbar;
        _descriptionScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
    }

    private void ResetMissionDescriptionScroll()
    {
        if (_descriptionScroll != null)
            _descriptionScroll.verticalNormalizedPosition = 1;
    }

    private void BuildGroupFilters(RectTransform listPanel)
    {
        var filters = new[]
        {
            MissionGroupFilter.All,
            MissionGroupFilter.FreeFlight,
            MissionGroupFilter.Tutorials,
            MissionGroupFilter.Missions,
            MissionGroupFilter.User,
            MissionGroupFilter.Workshop
        };

        for (var index = 0; index < filters.Length; index++)
        {
            var filter = filters[index];
            var button = CreateMenuButton(
                $"Group Filter {filter}",
                listPanel,
                new Vector2(-330f, 280f - index * 48f),
                new Vector2(150f, 34f),
                ButtonColor,
                () => SetGroupFilter(filter),
                11);
            _groupFilterButtons.Add(button);
            _groupFilterTexts.Add(button.GetComponentInChildren<Text>());
        }
    }

    private void BuildTagFilters(RectTransform listPanel)
    {
        _tagFilters.Clear();
        _tagFilters.Add(new TagFilterDefinition("ALL TAGS", null));
        _tagFilters.Add(new TagFilterDefinition("PvE", MissionTag.PVE));
        _tagFilters.Add(new TagFilterDefinition("PvP", MissionTag.PVP));
        _tagFilters.Add(new TagFilterDefinition("DAWN", MissionTag.Dawn));
        _tagFilters.Add(new TagFilterDefinition("DAY", MissionTag.Day));
        _tagFilters.Add(new TagFilterDefinition("DUSK", MissionTag.Dusk));
        _tagFilters.Add(new TagFilterDefinition("NIGHT", MissionTag.Night));

        for (var index = 0; index < _tagFilters.Count; index++)
        {
            var filterIndex = index;
            var button = CreateMenuButton(
                $"Tag Filter {index}",
                listPanel,
                new Vector2(-285f + index * 95f, 375f),
                new Vector2(86f, 28f),
                ButtonColor,
                () => SetTagFilter(filterIndex),
                10);
            _tagFilterButtons.Add(button);
            _tagFilterTexts.Add(button.GetComponentInChildren<Text>());
        }
    }

    private void RefreshList()
    {
        for (var index = 0; index < PageSize; index++)
        {
            var missionIndex = _page * PageSize + index;
            var hasMission = missionIndex >= 0 && missionIndex < _filteredMissions.Count;
            var button = _missionButtons[index];
            var text = _missionButtonTexts[index];
            button.gameObject.SetActive(hasMission);
            if (!hasMission)
                continue;

            var mission = _filteredMissions[missionIndex];
            text.text = $"  {mission.Key.Name}";
            SetButtonColor(button, missionIndex == _selectedIndex ? ButtonSelectedColor : ButtonColor);
        }

        if (_pageText != null)
        {
            var totalPages = GetLastPage() + 1;
            _pageText.text = $"{_page + 1} / {totalPages}";
        }
    }

    private void RefreshFilterButtons()
    {
        for (var index = 0; index < _groupFilterButtons.Count; index++)
        {
            var filter = (MissionGroupFilter)index;
            _groupFilterTexts[index].text = $"{GetGroupFilterLabel(filter)} ({CountGroupMissions(filter)})";
            SetButtonColor(_groupFilterButtons[index], filter == _activeGroupFilter ? ButtonSelectedColor : ButtonColor);
        }

        for (var index = 0; index < _tagFilterButtons.Count; index++)
        {
            var filter = _tagFilters[index];
            var label = filter.HasTag ? $"{filter.Label} ({CountTagMissions(filter.Tag)})" : filter.Label;
            _tagFilterTexts[index].text = label;
            SetButtonColor(_tagFilterButtons[index], index == _activeTagFilterIndex ? ButtonSelectedColor : ButtonColor);
        }
    }

    private void PreviousPage()
    {
        if (_launch.IsBusy || _page <= 0)
            return;

        _page--;
        SelectMission(Mathf.Clamp(_selectedIndex, _page * PageSize, Mathf.Min(_filteredMissions.Count - 1, (_page + 1) * PageSize - 1)));
        RefreshList();
    }

    private void NextPage()
    {
        if (_launch.IsBusy || (_page + 1) * PageSize >= _filteredMissions.Count)
            return;

        _page++;
        SelectMission(_page * PageSize);
        RefreshList();
    }

    private void SetGroupFilter(MissionGroupFilter filter)
    {
        if (_launch.IsBusy || _activeGroupFilter == filter)
            return;

        _activeGroupFilter = filter;
        _page = 0;
        _selectedIndex = 0;
        ApplyFilters();
    }

    private void SetTagFilter(int filterIndex)
    {
        if (_launch.IsBusy || filterIndex < 0 || filterIndex >= _tagFilters.Count || _activeTagFilterIndex == filterIndex)
            return;

        _activeTagFilterIndex = filterIndex;
        _page = 0;
        _selectedIndex = 0;
        ApplyFilters();
    }

    private void BackToMainMenu()
    {
        if (_launch.IsBusy)
            return;
        _actions?.TryInvokeCurrentMenuButton("Back", "< BACK", "BACK", "MenuExit_Button");
    }

    private void OpenUserFolder()
    {
        if (_launch.IsBusy)
            return;
        _actions?.TryInvokeCurrentMenuButton("Open User Folder", "Open User Folder", "OPEN USER FOLDER");
    }

    private void CustomizeSelectedMission()
    {
        if (_launch.IsBusy || HasActiveSession() || _selectedIndex < 0 || _selectedIndex >= _filteredMissions.Count)
            return;
        if (!BlueprinterContentReadiness.Read().CanLaunch)
        {
            SetIdleLaunchStatus();
            return;
        }

        _actions?.TrySelectOriginalMission(_filteredMissions[_selectedIndex].Key);
        _actions?.TryInvokeCurrentMenuButton("Customize Mission", "Customize Mission", "CUSTOMIZE MISSION");
    }

    private void Update()
    {
        if (!_launch.IsBusy && _visible && Time.unscaledTime >= _nextContentCheck)
        {
            _nextContentCheck = Time.unscaledTime + .25f;
            var content = BlueprinterContentReadiness.Read();
            if (_contentRevision != content.Revision)
            {
                _contentRevision = content.Revision;
                if (content.Phase == MissionContentPhase.Ready)
                    RefreshContentMissions();
                SetIdleLaunchStatus();
            }
            RefreshLaunchButtons();
            RefreshMissionPreview();
        }
        if (!_launch.IsBusy || Time.unscaledTime < _nextLaunchStatusTime)
            return;
        _nextLaunchStatusTime = Time.unscaledTime + 1;
        var elapsed = Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime - _launchStartedAt));
        SetLaunchStatus(elapsed < 3 ? _launchPhase + "…" : $"{_launchPhase}… {elapsed}s\nPlease wait for loading to finish.");
    }

    private void SetLaunchStatus(string status)
    {
        if (_launchStatusText != null)
            _launchStatusText.text = status;
    }

    private void SetIdleLaunchStatus() => SetLaunchStatus(BlueprinterContentReadiness.Read().Status);

    private void LockLaunchButtons()
    {
        _launchButtonStates.Clear();
        if (_container != null)
            foreach (var button in _container.GetComponentsInChildren<Button>(true))
            {
                _launchButtonStates.Add((button, button.interactable));
                button.interactable = false;
            }
        if (_startButtonText != null)
            _startButtonText.text = "LOADING…";
    }

    private void RestoreLaunchButtons()
    {
        foreach (var state in _launchButtonStates)
            if (state.button != null)
                state.button.interactable = state.interactable;
        _launchButtonStates.Clear();
        RefreshLaunchButtons();
    }

    private void RefreshLaunchButtons()
    {
        var selectable = !_launch.IsBusy && _selectedIndex >= 0 && _selectedIndex < _filteredMissions.Count;
        var activeSession = HasActiveSession();
        var content = BlueprinterContentReadiness.Read();
        if (_startButton != null)
            _startButton.interactable = selectable && !activeSession && content.CanLaunch;
        if (_customizeButton != null)
            _customizeButton.interactable = selectable && !activeSession && content.CanLaunch;
        if (_startButtonText != null)
            _startButtonText.text = _launch.IsBusy ? "LOADING…"
            : content.Phase == MissionContentPhase.Preparing ? "PREPARING CONTENT"
            : content.Phase == MissionContentPhase.Failed ? "CONTENT NOT READY"
            : activeSession ? "SESSION OPEN" : "START MISSION";
    }

    private bool MatchesGroupFilter(MissionEntry mission)
    {
        return _activeGroupFilter switch
        {
            MissionGroupFilter.FreeFlight => SameGroup(mission.Key.Group, MissionGroup.Default),
            MissionGroupFilter.Tutorials => SameGroup(mission.Key.Group, MissionGroup.Tutorial),
            MissionGroupFilter.Missions => SameGroup(mission.Key.Group, MissionGroup.BuiltIn),
            MissionGroupFilter.User => SameGroup(mission.Key.Group, MissionGroup.User),
            MissionGroupFilter.Workshop => SameGroup(mission.Key.Group, MissionGroup.Workshop),
            _ => true
        };
    }

    private bool MatchesTagFilter(MissionEntry mission)
    {
        var filter = _tagFilters.Count > 0 ? _tagFilters[ClampInt(_activeTagFilterIndex, 0, _tagFilters.Count - 1)] : default;
        return !filter.HasTag || HasTag(mission.Mission, filter.Tag);
    }

    private int CountGroupMissions(MissionGroupFilter filter)
    {
        var previousFilter = _activeGroupFilter;
        _activeGroupFilter = filter;
        var count = _missions.Count(MatchesGroupFilter);
        _activeGroupFilter = previousFilter;
        return count;
    }

    private int CountTagMissions(MissionTag tag)
    {
        return _missions.Count(mission => MatchesGroupFilter(mission) && HasTag(mission.Mission, tag));
    }

    private int GetLastPage()
    {
        return Mathf.Max(0, Mathf.CeilToInt(_filteredMissions.Count / (float)PageSize) - 1);
    }

    private static bool SameGroup(MissionGroup left, MissionGroup right)
    {
        return ReferenceEquals(left, right) ||
               string.Equals(left?.Name, right?.Name, System.StringComparison.OrdinalIgnoreCase);
    }

    private static string GetGroupFilterLabel(MissionGroupFilter filter)
    {
        return filter switch
        {
            MissionGroupFilter.FreeFlight => "FREE FLIGHT",
            MissionGroupFilter.Tutorials => "TUTORIALS",
            MissionGroupFilter.Missions => "MISSIONS",
            MissionGroupFilter.User => "USER",
            MissionGroupFilter.Workshop => "WORKSHOP",
            _ => "ALL"
        };
    }

    private static int ClampInt(int value, int min, int max)
    {
        return Mathf.Clamp(value, min, max);
    }

    private RectTransform CreateContainer(string name, RectTransform parent, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = Vector2.zero;
        return rectTransform;
    }

    private RectTransform CreatePanel(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        return CreateImage(name, parent, color, anchoredPosition, size);
    }

    private RectTransform CreateImage(string name, RectTransform parent, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var image = gameObject.AddComponent<Image>();
        image.color = color;
        return rectTransform;
    }

    private Text CreateText(string name, RectTransform parent, string text, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment, Color color)
    {
        var gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(gameObject.transform, LayerHelper.GetVrUiLayer());

        var rectTransform = gameObject.AddComponent<RectTransform>();
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = anchoredPosition;

        var textComponent = gameObject.AddComponent<Text>();
        textComponent.text = text;
        textComponent.font = _font;
        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.color = color;
        textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
        textComponent.verticalOverflow = VerticalWrapMode.Truncate;
        return textComponent;
    }

    private Button CreateMenuButton(string label, RectTransform parent, Vector2 anchoredPosition, Vector2 size, Color color, UnityEngine.Events.UnityAction onClick, int fontSize = 15, TextAnchor alignment = TextAnchor.MiddleCenter)
    {
        var rectTransform = CreateImage(label, parent, color, anchoredPosition, size);
        var button = rectTransform.gameObject.AddComponent<Button>();
        button.targetGraphic = rectTransform.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        NativeButtonFeedback.Configure(button, color);

        CreateText($"{label} Text", rectTransform, label, Vector2.zero, size, fontSize, alignment, Color.white);
        return button;
    }

    private static void SetButtonColor(Button button, Color color)
    {
        var image = button.targetGraphic;
        if (image != null)
        {
            image.color = color;
        }

        NativeButtonFeedback.SetNormalColor(button, color);
    }

    private enum MissionGroupFilter
    {
        All,
        FreeFlight,
        Tutorials,
        Missions,
        User,
        Workshop
    }

    private readonly struct TagFilterDefinition
    {
        public TagFilterDefinition(string label, MissionTag? tag)
        {
            Label = label;
            Tag = tag.GetValueOrDefault();
            HasTag = tag.HasValue;
        }

        public string Label
        {
            get;
        }
        public MissionTag Tag
        {
            get;
        }
        public bool HasTag
        {
            get;
        }
    }

    private readonly struct MissionEntry
    {
        public MissionEntry(MissionKey key, MissionQuickLoad mission)
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
