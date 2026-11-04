using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NuclearOption.Workshop;
using UnityEngine;

namespace NOVR.VrUi.Native;

public partial class NativeWorkshopPanel
{
    private int _queryVersion;
    private bool _queryRefreshPending;

    private void SelectTab(WorkshopTab tab)
    {
        if (_tab == tab)
            return;

        _tab = tab;
        RefreshFirstSteamPage();
    }

    private void CycleOrder(int direction)
    {
        var values = new[] { OrderBy.Trend30Days, OrderBy.TopAllTime, OrderBy.New };
        var index = Array.IndexOf(values, _orderBy);
        if (index < 0)
            index = 0;

        _orderBy = values[WrapIndex(index + direction, values.Length)];
        RefreshFirstSteamPage();
    }

    private void RefreshFirstSteamPage()
    {
        _steamPage = 1;
        _displayPage = 0;
        RequestSteamPage();
    }

    private void LoadNextSteamPage()
    {
        if (_queryInProgress || !_hasMoreSteamPages)
            return;

        _steamPage++;
        _displayPage = 0;
        RequestSteamPage();
    }

    private void RequestSteamPage()
    {
        _queryVersion = unchecked(_queryVersion + 1);
        _hasMoreSteamPages = false;
        _items.Clear();
        _visibleItems.Clear();
        // Coalesce tab/order/refresh changes while Steam is busy. Only the
        // latest requested page should be presented when the old query ends.
        _queryRefreshPending = true;
        RefreshQueryView($"Loading Workshop {_tab} page {_steamPage}.");
        if (!_queryInProgress)
            QuerySteamPageAsync().Forget();
    }

    private bool IsCurrentQuery(int version) =>
        this != null && _wasVisible && version == _queryVersion;

    private async UniTaskVoid QuerySteamPageAsync()
    {
        if (_steamWorkshop == null || _queryInProgress || !_wasVisible)
            return;

        _queryRefreshPending = false;
        _queryInProgress = true;
        var version = _queryVersion;
        var page = _steamPage;
        // The game's API mutates the supplied list after awaiting Steam. Never
        // give it the displayed list: a stale request must not overwrite it.
        var result = new List<SteamWorkshopItem>();
        try
        {
            var hasMore = await _steamWorkshop.RefreshItems(_orderBy, GetCurrentTag(), result, (uint)page);
            if (!IsCurrentQuery(version))
                return;
            _items.AddRange(result);
            _hasMoreSteamPages = hasMore;
            ApplySearchFromCurrentPage();
            SetStatus($"Loaded {_items.Count} Workshop items from page {page}.");
        }
        catch (Exception exception)
        {
            if (IsCurrentQuery(version))
            {
                Debug.LogError($"[NOVR] Native Workshop refresh failed: {exception}");
                _items.Clear();
                _visibleItems.Clear();
                _hasMoreSteamPages = false;
                RefreshQueryView("Workshop refresh failed. Check the BepInEx log.");
            }
        }
        finally
        {
            _queryInProgress = false;
            if (this != null && _wasVisible)
            {
                RefreshQueryView();
                if (_queryRefreshPending)
                    QuerySteamPageAsync().Forget();
            }
        }
    }

    private void RefreshQueryView(string? resetStatus = null)
    {
        // A disappearing Unity row must not strand the request or prevent a
        // queued tab change from starting. State ownership lives outside redraw.
        try
        {
            if (resetStatus != null)
            {
                SelectItem(-1);
                SetStatus(resetStatus);
            }
            RefreshButtons();
        }
        catch (Exception exception)
        {
            Debug.LogError($"[NOVR] Native Workshop view update failed: {exception}");
        }
    }

    private string GetCurrentTag()
    {
        return _tab == WorkshopTab.AircraftLiveries
            ? NuclearOption.ModScripts.Impl.AircraftLiveryMod.MOD_NAME
            : NuclearOption.ModScripts.Impl.MissionMod.MOD_NAME;
    }

}
