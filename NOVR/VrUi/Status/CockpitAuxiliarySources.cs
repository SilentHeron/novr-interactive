using System;
using System.Collections.Generic;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Move the complete authored gauge, including its animated diagram. The native
// HUDAppManager keeps refreshing its serialized app references after reparenting;
// no values, animation rules or unit labels are reconstructed here.
internal sealed class CockpitAuxiliarySources
{
    private sealed class Borrowed
    {
        internal readonly HUDApp App;
        internal readonly RectTransform Slot;
        internal readonly CockpitStatusSources.Lease Lease;
        internal readonly WingGaugeEditPreview? Preview;
        internal Borrowed(HUDApp app, RectTransform slot, CockpitStatusSources.Lease lease)
        {
            App = app;
            Slot = slot;
            Lease = lease;
            Preview = app is WingAngleGauge wing ? new WingGaugeEditPreview(wing) : null;
        }
    }

    private readonly List<Borrowed> _sources = new();
    private readonly List<Borrowed> _pending = new();
    private readonly List<HUDApp> _apps = new(), _scratch = new();
    private readonly HashSet<HUDApp> _failed = new();
    private RectTransform? _content;

    internal bool Owns(Transform source)
    {
        foreach (var item in _sources)
            if (item.Lease.Valid && (source == item.Lease.Widget || source.IsChildOf(item.Lease.Widget)))
                return true;
        return false;
    }

    internal void Update(FlightHud hud, bool discover)
    {
        if (!CockpitHudWidgets.TryGetContent("auxiliary", out var content))
        {
            Release();
            return;
        }
        if (_content != content)
        {
            Release();
            _content = content;
            discover = true;
        }
        for (var i = _sources.Count - 1; i >= 0; i--)
            if (!_sources[i].Lease.Valid)
            {
                Restore(_sources[i]);
                _sources.RemoveAt(i);
                discover = true;
            }
        if (discover)
        {
            HudHierarchy.Collect(hud.transform, HudHierarchy.MarkerLayer, _apps, _scratch);
            foreach (var app in _apps)
            {
                if (app == null || _failed.Contains(app) ||
                    !(app is NozzleGauge || app is WingAngleGauge || app is CustomAxis1Gauge) ||
                    app.GetComponentInParent<HUDAppManager>() == null ||
                    !(app.transform is RectTransform source) || Owns(source))
                    continue;
                var go = new GameObject("NOVR auxiliary gauge slot", typeof(RectTransform));
                go.layer = content.gameObject.layer;
                var slot = (RectTransform)go.transform;
                slot.SetParent(content, false);
                _pending.Add(new Borrowed(app, slot, new CockpitStatusSources.Lease(source, slot, false, preserveRootImage: true, auxiliary: app)));
            }
            // Capture every original sibling index before moving any gauge.
            foreach (var item in _pending)
            {
                try
                {
                    item.Lease.Attach();
                    _sources.Add(item);
                }
                catch (Exception error)
                {
                    Restore(item);
                    _failed.Add(item.App);
                    Debug.LogWarning("[NOVR] Keeping native auxiliary gauge after attachment failed: " + error.Message);
                }
            }
            _pending.Clear();
            _apps.Clear();
        }
        var hasContent = false;
        var activeCount = 0;
        foreach (var item in _sources)
            if (item.Lease.Widget.gameObject.activeSelf)
                activeCount++;
        var row = 0;
        for (var i = 0; i < _sources.Count; i++)
        {
            var item = _sources[i];
            // Ordinarily an aircraft has one gauge. Preserve every authored
            // auxiliary gauge if a custom aircraft supplies more than one.
            item.Slot.anchorMin = item.Slot.anchorMax = new Vector2(.5f, .5f);
            var height = content.rect.height / Math.Max(1, activeCount);
            item.Slot.sizeDelta = new Vector2(content.rect.width, height);
            item.Slot.anchoredPosition = new Vector2(0, content.rect.height * .5f - height * (row + .5f));
            if (item.Lease.Widget.gameObject.activeSelf)
                row++;
            if (discover)
                item.Lease.Refresh();
            item.Preview?.Update(CockpitHudWidgets.Editing);
            item.Lease.Fit();
            hasContent |= item.Lease.HasContent;
        }
        CockpitHudWidgets.SetHasContent("auxiliary", hasContent);
    }

    internal void Release()
    {
        // Capture-order restoration preserves authored sibling order when two
        // gauges originally shared a parent.
        _sources.Sort((a, b) => a.Lease.Index.CompareTo(b.Lease.Index));
        foreach (var item in _sources)
            Restore(item);
        _sources.Clear();
        _pending.Clear();
        _apps.Clear();
        _scratch.Clear();
        _failed.Clear();
        _content = null;
        CockpitHudWidgets.SetHasContent("auxiliary", false);
    }

    private static void Restore(Borrowed item)
    {
        item.Preview?.Restore();
        item.Lease.Restore();
        if (item.Slot != null)
            UnityEngine.Object.Destroy(item.Slot.gameObject);
    }
}
