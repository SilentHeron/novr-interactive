using System.Linq;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NOVR.VrUi.Native;

public partial class NativeSinglePlayerMissionPanel
{
    private void LoadMissions(MissionKey? preservedSelection = null)
    {
        _missions.Clear();
        _filteredMissions.Clear();
        _page = 0;
        _selectedIndex = 0;

        try
        {
            MissionGroup.Init();
            var entries = NativeMissionHeaders
                .QuickLoadMany(MissionGroup.All.GetMissions())
                .Where(entry => HasTag(entry.mission, MissionTag.SinglePlayer))
                .Select(entry => new MissionEntry(entry.key, entry.mission));
            _missions.AddRange(entries);
            Debug.Log($"[NOVR] Loaded {_missions.Count} single-player missions for the VR menu.");
        }
        catch (System.Exception exception)
        {
            Debug.LogError($"[NOVR] Failed to load native single player mission list: {exception}");
        }

        ApplyFilters(preservedSelection);
    }

    private static bool HasTag(MissionQuickLoad mission, MissionTag tag)
    {
        var tags = mission.missionSettings.Tags;
        return tags != null && tags.Any(existing => existing.Equals(tag));
    }

    private void ApplyFilters(MissionKey? preservedSelection = null)
    {
        _filteredMissions.Clear();

        for (var index = 0; index < _missions.Count; index++)
        {
            var mission = _missions[index];
            if (!MatchesGroupFilter(mission))
                continue;
            if (!MatchesTagFilter(mission))
                continue;

            _filteredMissions.Add(mission);
        }

        // Resolve the final row before synchronizing the native picker. Selecting
        // row zero temporarily would reload and discard a customized mission.
        if (preservedSelection.HasValue)
        {
            var index = _filteredMissions.FindIndex(entry => entry.Key.Equals(preservedSelection.Value));
            if (index >= 0)
            {
                _selectedIndex = index;
                _page = index / PageSize;
            }
        }
        _page = ClampInt(_page, 0, GetLastPage());
        SelectMission(_filteredMissions.Count > 0 ? Mathf.Clamp(_selectedIndex, 0, _filteredMissions.Count - 1) : -1);
        RefreshList();
        RefreshFilterButtons();
    }

    private void SelectMission(int index)
    {
        if (_launch.IsBusy)
            return;
        SetIdleLaunchStatus();
        if (index < 0 || index >= _filteredMissions.Count)
        {
            _selectedIndex = -1;
            if (_titleText != null)
                _titleText.text = "";
            if (_tagsText != null)
                _tagsText.text = "";
            if (_descriptionText != null)
                _descriptionText.text = "";
            if (_startButton != null)
                _startButton.interactable = false;
            if (_customizeButton != null)
                _customizeButton.interactable = false;
            RefreshList();
            return;
        }

        _selectedIndex = index;
        var mission = _filteredMissions[_selectedIndex];
        if (_titleText != null)
            _titleText.text = mission.Key.Name;
        if (_tagsText != null)
            _tagsText.text = string.Join("   ", mission.Mission.missionSettings.Tags.Select(tag => tag.Tag));
        if (_descriptionText != null)
            _descriptionText.text = mission.Mission.missionSettings.description ?? "";
        RefreshLaunchButtons();
        _actions?.TrySelectOriginalMission(mission.Key);
        RefreshList();
    }

    private void RefreshContentMissions()
    {
        // Content loaders can add missions at the end of startup. Refresh once
        // on that observed transition, preserving the pilot's choice and filters.
        MissionKey? selected = _selectedIndex >= 0 && _selectedIndex < _filteredMissions.Count
            ? _filteredMissions[_selectedIndex].Key : null;
        // If the row disappeared, LoadMissions retains the existing fallback to
        // the first filtered mission, without selecting an intermediate row.
        LoadMissions(selected);
    }

}
