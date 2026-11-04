using System;
using System.Collections.Generic;
using Rewired;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

// Native keyboard/HOTAS binding rows, filters and live input display. The VR
// controller editor is a separate shared panel, hosted from the Controls tab.
public partial class NativeSettingsPanel
{
    private const float BindingScrollViewportHeight = 430f;
    private const float BindingScrollRowHeight = 40f;
    private const float BindingScrollIndicatorHeight = 430f;
    private const float BindingScrollIndicatorMinThumbHeight = 42f;
    private const float BindingActivePollSeconds = 0.08f;
    private const float BindingAxisValueRefreshSeconds = 0.08f;
    private readonly Dictionary<BindingEntry, Text> _bindingAxisValueTexts = new();
    private int _bindingDeviceFilterIndex;
    private BindingVisibilityFilter _bindingVisibilityFilter = BindingVisibilityFilter.All;
    private float _bindingScrollOffset;
    private float _nextBindingActivePollTime;
    private float _nextBindingAxisValueUpdateTime;
    private string? _focusedBindingKey;
    private string? _lastActiveBindingKey;


    private void RenderBindingsTab()
    {
        if (_contentRoot == null)
            return;

        EnsureBindingEntriesLoaded();

        CreateText(
            "Bindings Hint",
            _contentRoot,
            "Bindings for keyboard, mouse, and assigned controllers. REMAP or ASSIGN listens for the next button, key, or axis.",
            new Vector2(0f, 292f),
            new Vector2(900f, 28f),
            13,
            TextAnchor.MiddleCenter,
            new Color(0.80f, 0.86f, 0.88f, 1f));

        if (!ReInput.isReady)
        {
            AddReadOnlyRow("Rewired is not ready yet.");
            return;
        }

        if (_bindingEntries.Count == 0)
        {
            AddReadOnlyRow("No keyboard, mouse, or controller bindings were found.");
            return;
        }

        RenderBindingDeviceFilter();
        RenderBindingVisibilityFilter();

        var visibleEntries = GetVisibleBindingEntries();
        if (visibleEntries.Count == 0)
        {
            _nextRowY = 172f;
            AddReadOnlyRow($"No {GetBindingVisibilityFilterLabel().ToLowerInvariant()} bindings were found for {GetCurrentBindingDeviceFilter().Label}.");
            return;
        }

        RenderBindingScrollList(visibleEntries);

        if (_bindingCapture.Pending)
        {
            CreateMenuButton("CANCEL", _contentRoot, new Vector2(520f, -300f), new Vector2(120f, 32f), BackButtonColor, () =>
            {
                CancelBindingCapture("Binding capture canceled.");
                RenderCurrentTab();
            }, 13);
        }

        CreateText("Bindings Status", _contentRoot, _bindingStatus, new Vector2(0f, -345f), new Vector2(880f, 30f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));
    }

    private void RenderBindingScrollList(IList<BindingEntry> visibleEntries)
    {
        if (_contentRoot == null)
            return;

        CreateBindingColumnHeader();

        var viewportSize = new Vector2(1250f, BindingScrollViewportHeight);
        var viewport = CreateImage("Bindings Scroll Viewport", _contentRoot, new Color(0.03f, 0.04f, 0.045f, 0.72f), new Vector2(0f, -44f), viewportSize);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        var scrollThumb = CreateBindingScrollIndicator();

        var scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 34f;
        scrollRect.viewport = viewport;

        var contentHeight = Mathf.Max(viewportSize.y, visibleEntries.Count * BindingScrollRowHeight);
        var content = CreateContainer("Bindings Scroll Content", viewport, new Vector2(viewportSize.x, contentHeight));
        content.anchorMin = new Vector2(0.5f, 1f);
        content.anchorMax = new Vector2(0.5f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        scrollRect.content = content;

        var focusIndex = GetFocusedBindingIndex(visibleEntries);
        if (focusIndex >= 0)
        {
            _bindingScrollOffset = GetScrollOffsetForFocusedRow(focusIndex, contentHeight, viewportSize.y);
        }

        _bindingScrollOffset = Mathf.Clamp(_bindingScrollOffset, 0f, Mathf.Max(0f, contentHeight - viewportSize.y));
        content.anchoredPosition = new Vector2(0f, _bindingScrollOffset);
        UpdateBindingScrollIndicator(scrollThumb, contentHeight, viewportSize.y);

        scrollRect.onValueChanged.AddListener(_ =>
        {
            _bindingScrollOffset = Mathf.Clamp(content.anchoredPosition.y, 0f, Mathf.Max(0f, contentHeight - viewportSize.y));
            UpdateBindingScrollIndicator(scrollThumb, contentHeight, viewportSize.y);
        });

        for (var index = 0; index < visibleEntries.Count; index++)
        {
            AddBindingRow(visibleEntries[index], content, index);
        }
    }

    private RectTransform? CreateBindingScrollIndicator()
    {
        if (_contentRoot == null)
            return null;

        var track = CreateImage("Bindings Scroll Indicator Track", _contentRoot, new Color(0.16f, 0.18f, 0.19f, 0.70f), new Vector2(642f, -44f), new Vector2(10f, BindingScrollIndicatorHeight));
        var thumb = CreateImage("Bindings Scroll Indicator Thumb", track, new Color(0.54f, 0.62f, 0.64f, 0.94f), Vector2.zero, new Vector2(8f, BindingScrollIndicatorHeight));
        thumb.anchorMin = new Vector2(0.5f, 0.5f);
        thumb.anchorMax = new Vector2(0.5f, 0.5f);
        thumb.pivot = new Vector2(0.5f, 0.5f);
        return thumb;
    }

    private void UpdateBindingScrollIndicator(RectTransform? thumb, float contentHeight, float viewportHeight)
    {
        if (thumb == null)
            return;

        var maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);
        var trackHeight = BindingScrollIndicatorHeight;
        var thumbHeight = maxOffset <= 0f
            ? trackHeight
            : Mathf.Clamp(trackHeight * viewportHeight / Mathf.Max(viewportHeight, contentHeight), BindingScrollIndicatorMinThumbHeight, trackHeight);
        var travel = Mathf.Max(0f, trackHeight - thumbHeight);
        var scrollRatio = maxOffset <= 0f ? 0f : Mathf.Clamp01(_bindingScrollOffset / maxOffset);

        thumb.sizeDelta = new Vector2(8f, thumbHeight);
        thumb.anchoredPosition = new Vector2(0f, travel * 0.5f - scrollRatio * travel);

        var image = thumb.GetComponent<Image>();
        if (image != null)
        {
            image.color = maxOffset <= 0f
                ? new Color(0.34f, 0.40f, 0.42f, 0.42f)
                : new Color(0.54f, 0.62f, 0.64f, 0.94f);
        }
    }

    private void CreateBindingColumnHeader()
    {
        if (_contentRoot == null)
            return;

        const float y = 178f;
        CreateText("Bindings Header Action", _contentRoot, "ACTION", new Vector2(-470f, y), new Vector2(240f, 24f), 12, TextAnchor.MiddleLeft, new Color(0.68f, 0.76f, 0.78f, 1f));
        CreateText("Bindings Header Source", _contentRoot, "SOURCE", new Vector2(-205f, y), new Vector2(160f, 24f), 12, TextAnchor.MiddleCenter, new Color(0.68f, 0.76f, 0.78f, 1f));
        CreateText("Bindings Header Binding", _contentRoot, "BINDING", new Vector2(45f, y), new Vector2(220f, 24f), 12, TextAnchor.MiddleCenter, new Color(0.68f, 0.76f, 0.78f, 1f));
        CreateText("Bindings Header Value", _contentRoot, "VALUE", new Vector2(220f, y), new Vector2(90f, 24f), 12, TextAnchor.MiddleCenter, new Color(0.68f, 0.76f, 0.78f, 1f));
    }

    private int GetFocusedBindingIndex(IList<BindingEntry> visibleEntries)
    {
        if (string.IsNullOrWhiteSpace(_focusedBindingKey))
        {
            return -1;
        }

        for (var index = 0; index < visibleEntries.Count; index++)
        {
            if (visibleEntries[index].Key == _focusedBindingKey)
            {
                return index;
            }
        }

        return -1;
    }

    private static float GetScrollOffsetForFocusedRow(int rowIndex, float contentHeight, float viewportHeight)
    {
        var centeredOffset = rowIndex * BindingScrollRowHeight - viewportHeight * 0.45f + BindingScrollRowHeight;
        return Mathf.Clamp(centeredOffset, 0f, Mathf.Max(0f, contentHeight - viewportHeight));
    }

    private void RenderBindingDeviceFilter()
    {
        if (_contentRoot == null)
            return;

        var filter = GetCurrentBindingDeviceFilter();
        var label = ShortenLabel(filter.Label, 42);
        const float y = 252f;

        CreateText("Bindings Device Filter Label", _contentRoot, "DEVICE", new Vector2(-385f, y), new Vector2(120f, 30f), 13, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.86f, 1f));
        CreateMenuButton("<", _contentRoot, new Vector2(-215f, y), new Vector2(50f, 30f), ButtonColor, () => CycleBindingDeviceFilter(-1), 15);
        CreateText("Bindings Device Filter Value", _contentRoot, label, new Vector2(0f, y), new Vector2(360f, 30f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", _contentRoot, new Vector2(215f, y), new Vector2(50f, 30f), ButtonColor, () => CycleBindingDeviceFilter(1), 15);
    }

    private void RenderBindingVisibilityFilter()
    {
        if (_contentRoot == null)
            return;

        const float y = 216f;
        CreateText("Bindings Visibility Filter Label", _contentRoot, "SHOW", new Vector2(-385f, y), new Vector2(120f, 30f), 13, TextAnchor.MiddleLeft, new Color(0.78f, 0.84f, 0.86f, 1f));
        CreateMenuButton("<", _contentRoot, new Vector2(-215f, y), new Vector2(50f, 30f), ButtonColor, () => CycleBindingVisibilityFilter(-1), 15);
        CreateText("Bindings Visibility Filter Value", _contentRoot, GetBindingVisibilityFilterLabel(), new Vector2(0f, y), new Vector2(360f, 30f), 14, TextAnchor.MiddleCenter, Color.white);
        CreateMenuButton(">", _contentRoot, new Vector2(215f, y), new Vector2(50f, 30f), ButtonColor, () => CycleBindingVisibilityFilter(1), 15);
    }

    private void CycleBindingDeviceFilter(int delta)
    {
        EnsureBindingEntriesLoaded();
        if (_bindingDeviceFilters.Count == 0)
            return;

        _bindingDeviceFilterIndex += delta;
        if (_bindingDeviceFilterIndex < 0)
        {
            _bindingDeviceFilterIndex = _bindingDeviceFilters.Count - 1;
        }
        else if (_bindingDeviceFilterIndex >= _bindingDeviceFilters.Count)
        {
            _bindingDeviceFilterIndex = 0;
        }

        ResetBindingScroll();
        RenderCurrentTab();
    }

    private void CycleBindingVisibilityFilter(int delta)
    {
        var next = (int)_bindingVisibilityFilter + delta;
        var count = Enum.GetValues(typeof(BindingVisibilityFilter)).Length;

        if (next < 0)
        {
            next = count - 1;
        }
        else if (next >= count)
        {
            next = 0;
        }

        _bindingVisibilityFilter = (BindingVisibilityFilter)next;
        ResetBindingScroll();
        RenderCurrentTab();
    }

    private string GetBindingVisibilityFilterLabel()
    {
        return _bindingVisibilityFilter switch
        {
            BindingVisibilityFilter.Assigned => "ASSIGNED",
            BindingVisibilityFilter.Unassigned => "UNASSIGNED",
            _ => "ALL"
        };
    }

    private BindingDeviceFilter GetCurrentBindingDeviceFilter()
    {
        if (_bindingDeviceFilters.Count == 0)
        {
            return new BindingDeviceFilter(string.Empty, "NO DEVICE");
        }

        _bindingDeviceFilterIndex = ClampInt(_bindingDeviceFilterIndex, 0, _bindingDeviceFilters.Count - 1);
        return _bindingDeviceFilters[_bindingDeviceFilterIndex];
    }

    private IList<BindingEntry> GetVisibleBindingEntries()
    {
        var filter = GetCurrentBindingDeviceFilter();
        var visibleEntries = new List<BindingEntry>();
        for (var index = 0; index < _bindingEntries.Count; index++)
        {
            var entry = _bindingEntries[index];
            if (entry.DeviceKey == filter.Key && IsBindingVisible(entry))
            {
                visibleEntries.Add(entry);
            }
        }

        return visibleEntries;
    }

    private bool IsBindingVisible(BindingEntry entry)
    {
        return _bindingVisibilityFilter switch
        {
            BindingVisibilityFilter.Assigned => entry.IsAssigned,
            BindingVisibilityFilter.Unassigned => !entry.IsAssigned,
            _ => true
        };
    }

    private void ResetBindingScroll()
    {
        _bindingScrollOffset = 0f;
        _focusedBindingKey = null;
        _lastActiveBindingKey = null;
    }

    private void AddBindingRow(BindingEntry entry, RectTransform parent, int rowIndex)
    {
        var y = -BindingScrollRowHeight * rowIndex - BindingScrollRowHeight * 0.5f;
        var active = IsBindingButtonInputActive(entry);
        var pending = ReferenceEquals(_bindingCapture.Queued, entry) || ReferenceEquals(_bindingCapture.Active, entry);
        var rowColor = active
            ? new Color(0.18f, 0.35f, 0.22f, 0.88f)
            : pending
                ? new Color(0.22f, 0.25f, 0.28f, 0.88f)
                : rowIndex % 2 == 0
                    ? new Color(0.075f, 0.085f, 0.09f, 0.58f)
                    : new Color(0.055f, 0.065f, 0.07f, 0.52f);

        AnchorBindingRowElement(CreateImage($"{entry.Key} Row", parent, rowColor, new Vector2(0f, y), new Vector2(1210f, 36f)));

        var remapLabel = pending ? "..." : entry.IsAssigned ? "REMAP" : "ASSIGN";
        var bindingColor = entry.IsAssigned ? Color.white : new Color(0.96f, 0.82f, 0.42f, 1f);
        if (active)
        {
            bindingColor = new Color(0.80f, 1f, 0.72f, 1f);
        }

        AnchorBindingRowElement(CreateText($"{entry.Key} Action", parent, entry.DisplayName, new Vector2(-470f, y), new Vector2(270f, 32f), 13, TextAnchor.MiddleLeft, Color.white).rectTransform);
        AnchorBindingRowElement(CreateText($"{entry.Key} Source", parent, $"{entry.ControllerLabel} {entry.BindingKind}", new Vector2(-205f, y), new Vector2(190f, 32f), 11, TextAnchor.MiddleCenter, new Color(0.78f, 0.84f, 0.86f, 1f)).rectTransform);
        AnchorBindingRowElement(CreateText($"{entry.Key} Binding", parent, entry.BindingName, new Vector2(45f, y), new Vector2(220f, 32f), 13, TextAnchor.MiddleCenter, bindingColor).rectTransform);
        if (entry.CanShowAxisValue)
        {
            var axisText = CreateText($"{entry.Key} Axis Value", parent, GetBindingAxisValueText(entry), new Vector2(220f, y), new Vector2(90f, 32f), 12, TextAnchor.MiddleCenter, new Color(0.82f, 0.92f, 1f, 1f));
            AnchorBindingRowElement(axisText.rectTransform);
            _bindingAxisValueTexts[entry] = axisText;
        }

        if (entry.CanInvert)
        {
            AnchorBindingRowElement((RectTransform)CreateMenuButton(entry.IsInverted ? "INV ON" : "INV", parent, new Vector2(310f, y), new Vector2(76f, 30f), entry.IsInverted ? ApplyButtonColor : ButtonColor, () => ToggleBindingInvert(entry), 11).transform);
        }

        if (entry.IsAssigned)
        {
            AnchorBindingRowElement((RectTransform)CreateMenuButton("CLEAR", parent, new Vector2(405f, y), new Vector2(82f, 30f), BackButtonColor, () => ClearBinding(entry), 11).transform);
        }

        AnchorBindingRowElement((RectTransform)CreateMenuButton(remapLabel, parent, new Vector2(520f, y), new Vector2(105f, 30f), pending ? ButtonSelectedColor : ButtonColor, () => QueueBindingCapture(entry), 12).transform);
    }

    private static void AnchorBindingRowElement(RectTransform rectTransform)
    {
        rectTransform.anchorMin = new Vector2(0.5f, 1f);
        rectTransform.anchorMax = new Vector2(0.5f, 1f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
    }

    private void UpdateActiveBindingFocus()
    {
        if (Time.unscaledTime < _nextBindingActivePollTime)
            return;
        _nextBindingActivePollTime = Time.unscaledTime + BindingActivePollSeconds;

        if (!ReInput.isReady || GameManager.playerInput == null)
        {
            return;
        }

        EnsureBindingEntriesLoaded();
        var visibleEntries = GetVisibleBindingEntries();
        BindingEntry? activeEntry = null;
        for (var index = 0; index < visibleEntries.Count; index++)
        {
            if (!IsBindingButtonInputActive(visibleEntries[index]))
                continue;

            activeEntry = visibleEntries[index];
            break;
        }

        var activeKey = activeEntry?.Key;
        if (activeKey == _lastActiveBindingKey)
        {
            return;
        }

        _lastActiveBindingKey = activeKey;
        _focusedBindingKey = activeKey;
        if (activeEntry != null)
        {
            _bindingStatus = $"Pressed input is bound to {activeEntry.DisplayName}.";
        }

        RenderCurrentTab();
    }

    private void UpdateVisibleBindingAxisValues()
    {
        if (Time.unscaledTime < _nextBindingAxisValueUpdateTime)
            return;
        _nextBindingAxisValueUpdateTime = Time.unscaledTime + BindingAxisValueRefreshSeconds;

        foreach (var pair in _bindingAxisValueTexts)
        {
            if (pair.Value == null)
                continue;

            pair.Value.text = GetBindingAxisValueText(pair.Key);
        }
    }

    private static bool IsBindingButtonInputActive(BindingEntry entry)
    {
        if (!entry.IsAssigned || GameManager.playerInput == null || !ReInput.isReady)
        {
            return false;
        }

        if (entry.ActionType != InputActionType.Button || entry.ActionElementMap?.elementType == ControllerElementType.Axis)
        {
            return false;
        }

        try
        {
            return GameManager.playerInput.GetButton(entry.ActionId);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string GetBindingAxisValueText(BindingEntry entry)
    {
        if (!entry.CanShowAxisValue || GameManager.playerInput == null || !ReInput.isReady)
        {
            return string.Empty;
        }

        try
        {
            var value = GameManager.playerInput.GetAxis(entry.ActionId);
            if (Mathf.Abs(value) < 0.005f)
            {
                value = 0f;
            }

            return $"{value:+0.00;-0.00;0.00}";
        }
        catch (Exception)
        {
            return "--";
        }
    }

    private enum BindingVisibilityFilter
    {
        All,
        Assigned,
        Unassigned
    }
}
