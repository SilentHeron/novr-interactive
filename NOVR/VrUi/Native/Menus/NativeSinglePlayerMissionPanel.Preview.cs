using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public partial class NativeSinglePlayerMissionPanel
{
    private Image? _previewImage;
    private Text? _previewStatusText;
    private bool _previewSelectionReady;
    private MissionPicker? _previewPicker;

    private void ClearMissionPreview()
    {
        _previewSelectionReady = false;
        _previewPicker = null;
        SetMissionPreview(null, "Select a mission.");
    }

    private void RefreshMissionPreview()
    {
        if (!_visible || _previewImage == null || _actions == null)
            return;
        if (_selectedIndex < 0 || _selectedIndex >= _filteredMissions.Count)
        {
            ClearMissionPreview();
            return;
        }

        var key = _filteredMissions[_selectedIndex].Key;
        var picker = _actions.GetOriginalMissionPreviewPicker();
        if (!ReferenceEquals(_previewPicker, picker))
        {
            _previewPicker = picker;
            _previewSelectionReady = false;
        }
        // The stock picker may arrive a few frames after the VR page opens.
        // Synchronize once it exists; never reload a customized selected mission.
        if (!_previewSelectionReady)
            _previewSelectionReady = _actions.TrySelectOriginalMission(key);
        if (!_previewSelectionReady || !_actions.TryGetOriginalMissionPreview(key, out var source, out var fallback))
        {
            SetMissionPreview(null, "Loading preview…");
            return;
        }

        // MissionPicker keeps the previous sprite tinted grey during its async
        // load and turns it white only after the current request completes.
        // Borrow that result instead of starting a second Workshop download.
        var ready = source != null && source.color == Color.white;
        var sprite = ready && source!.sprite != null ? source.sprite : fallback;
        SetMissionPreview(sprite, ready ? "Preview unavailable." : "Loading preview…");
    }

    private void SetMissionPreview(Sprite? sprite, string emptyText)
    {
        if (_previewImage != null)
        {
            _previewImage.sprite = sprite;
            _previewImage.color = Color.white;
            _previewImage.enabled = sprite != null;
        }
        if (_previewStatusText != null)
            _previewStatusText.text = sprite != null ? "" : emptyText;
    }

    // Stock Customize temporarily hides the root. Keep the selected mission,
    // but drop the borrowed image and refresh it when presentation resumes.
    private void OnDisable() => ClearMissionPreview();
    private void OnEnable() => RefreshMissionPreview();
    private void OnDestroy() => ClearMissionPreview();
}
