using System.Threading;
using Cysharp.Threading.Tasks;
using NuclearOption.Workshop;
using UnityEngine;

namespace NOVR.VrUi.Native;

public partial class NativeWorkshopPanel
{
    private CancellationTokenSource? _previewCancellation;

    public void SetVisible(bool visible)
    {
        if (_container == null)
            return;

        var opening = visible && !_wasVisible;
        _wasVisible = visible;
        if (opening)
            RefreshFirstSteamPage();
        else if (!visible)
            EndPresentation();

        NativePanelTransition.SetVisible(_container, visible);
    }

    private void OnDisable() => EndPresentation();
    private void OnDestroy() => EndPresentation();

    private void EndPresentation()
    {
        _wasVisible = false;
        // Steam owns the in-flight query. Invalidate its presentation and drop
        // any queued refresh; reopening requests the latest page explicitly.
        _queryVersion = unchecked(_queryVersion + 1);
        _queryRefreshPending = false;
        ReleaseSelectedItem();
    }

    private void ReleaseSelectedItem()
    {
        if (_selectedItem != null)
            _selectedItem.OwnerNameChanged -= OnSelectedOwnerNameChanged;
        _selectedItem = null;
        var cancellation = _previewCancellation;
        _previewCancellation = null;
        if (cancellation == null)
            return;
        try
        {
            cancellation.Cancel();
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private void SelectItem(int index)
    {
        ReleaseSelectedItem();

        if (index < 0 || index >= _visibleItems.Count)
        {
            _selectedIndex = -1;
            _selectedItem = null;
            if (_titleText != null)
                _titleText.text = "";
            if (_ownerText != null)
                _ownerText.text = "";
            if (_descriptionText != null)
                _descriptionText.text = "";
            if (_detailsStatusText != null)
                _detailsStatusText.text = "Select a Workshop item.";
            if (_previewImage != null)
            {
                _previewImage.sprite = null;
                _previewImage.color = new Color(0.12f, 0.14f, 0.15f, 1f);
            }

            RefreshButtons();
            RefreshRows();
            return;
        }

        _selectedIndex = index;
        _selectedItem = _visibleItems[index];
        _selectedItem.OwnerNameChanged += OnSelectedOwnerNameChanged;

        if (_titleText != null)
            _titleText.text = _selectedItem.Name;
        if (_ownerText != null)
            _ownerText.text = GetOwnerText(_selectedItem);
        if (_descriptionText != null)
            _descriptionText.text = _selectedItem.Description ?? "";
        if (_detailsStatusText != null)
            _detailsStatusText.text = _selectedItem.Subscribed ? "Installed locally." : "Not installed.";
        if (_previewImage != null)
        {
            _selectedItem.SetPreviewImageAsync(_previewImage, ref _previewCancellation).Forget();
        }

        RefreshButtons();
        RefreshRows();
    }

    private void OnSelectedOwnerNameChanged()
    {
        if (_selectedItem == null || _ownerText == null)
            return;

        _ownerText.text = GetOwnerText(_selectedItem);
        RefreshRows();
    }

    private static string GetOwnerText(SteamWorkshopItem item)
    {
        return string.IsNullOrWhiteSpace(item.OwnerName)
            ? "Author unknown"
            : $"By {item.OwnerName}";
    }

}
