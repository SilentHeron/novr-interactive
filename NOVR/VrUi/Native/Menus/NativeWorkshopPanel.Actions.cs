using System;
using Cysharp.Threading.Tasks;
using NuclearOption.Workshop;
using UnityEngine;

namespace NOVR.VrUi.Native;

public partial class NativeWorkshopPanel
{
    private void ToggleSubscribe()
    {
        if (!_wasVisible || _selectedItem == null || _steamWorkshop == null || _itemActionInProgress)
            return;

        ToggleSubscribeAsync(_selectedItem).Forget();
    }

    private async UniTaskVoid ToggleSubscribeAsync(SteamWorkshopItem item)
    {
        var workshop = _steamWorkshop;
        if (workshop == null)
            return;

        var version = _queryVersion;
        var unsubscribe = item.Subscribed;
        _itemActionInProgress = true;
        try
        {
            RenderActionFeedback(RefreshButtons);
            SetItemActionStatus(version, item, unsubscribe ? "Unsubscribing." : "Subscribing and downloading.");
            // An accepted Steam operation outlives this presentation. Changing
            // selection or closing the menu suppresses feedback, not the work.
            if (unsubscribe)
                await workshop.Unsubscribe(item);
            else
                await workshop.DownloadItem(item);
            SetItemActionStatus(version, item, unsubscribe ? "Unsubscribed." : "Installed locally.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Native Workshop subscribe action failed: {exception}");
            SetItemActionStatus(version, item, "Workshop item action failed. Check the BepInEx log.");
        }
        finally
        {
            _itemActionInProgress = false;
            // Reopening may have created a different selection while the same
            // operation held the busy gate. Refresh that live view's controls,
            // without presenting the old item's completion message there.
            RenderActionFeedback(RefreshButtons);
            RenderActionFeedback(RefreshRows);
        }
    }

    private void UpdateAllSubscribed()
    {
        if (!_wasVisible || _itemActionInProgress)
            return;

        UpdateAllSubscribedAsync().Forget();
    }

    private async UniTaskVoid UpdateAllSubscribedAsync()
    {
        var version = _queryVersion;
        _itemActionInProgress = true;
        try
        {
            RenderActionFeedback(RefreshButtons);
            SetWorkshopActionStatus(version, "Updating subscribed Workshop items.");
            await SteamWorkshop.UpdateAllSubscribedItems();
            SetWorkshopActionStatus(version, "Subscribed Workshop items are up to date.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Native Workshop update-all failed: {exception}");
            SetWorkshopActionStatus(version, "Workshop update-all failed. Check the BepInEx log.");
        }
        finally
        {
            _itemActionInProgress = false;
            RenderActionFeedback(RefreshButtons);
        }
    }

    private void SetItemActionStatus(int version, SteamWorkshopItem item, string status)
    {
        if (IsCurrentQuery(version) && ReferenceEquals(_selectedItem, item))
            RenderActionFeedback(() => SetDetailsStatus(status));
    }

    private void SetWorkshopActionStatus(int version, string status)
    {
        if (IsCurrentQuery(version))
            RenderActionFeedback(() => SetStatus(status));
    }

    private void RenderActionFeedback(Action render)
    {
        if (this == null || !_wasVisible)
            return;
        try
        {
            render();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Native Workshop action feedback failed: {exception}");
        }
    }
}
