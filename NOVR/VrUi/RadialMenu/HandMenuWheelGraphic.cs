using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// A self-owned wheel for gameplay views without usable flight actions. It
// needs no native wheel prefab, action assets, aircraft or weapons page. A
// fourth EJECT sector appears while the local pilot can still eject from a
// wreck whose flight wheel is unavailable (detached cockpit, destroyed airframe).
internal sealed class HandMenuWheelGraphic : MaskableGraphic
{
    internal const int BaseCount = 3, EjectIndex = 3;
    private readonly Text?[] _labels = new Text?[BaseCount + 1];
    private int _hovered = -1;
    private bool _mapAvailable = true, _ejectAvailable;
    internal int Count => _ejectAvailable ? BaseCount + 1 : BaseCount;
    internal bool EjectAvailable => _ejectAvailable;

    internal void Initialize()
    {
        raycastTarget = false;
        rectTransform.sizeDelta = new Vector2(300, 300);
        for (var index = 0; index < BaseCount; index++)
            _labels[index] = CreateLabel(index == 0 ? "GAME MENU" : index == 1 ? "VR MENU" : "OPEN MAP");
        Layout();
    }

    private Text CreateLabel(string name)
    {
        var label = new GameObject(name, typeof(RectTransform), typeof(Text));
        label.transform.SetParent(transform, false);
        label.layer = gameObject.layer;
        var text = label.GetComponent<Text>();
        text.text = label.name;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.rectTransform.sizeDelta = new Vector2(110, 48);
        return text;
    }

    private void Layout()
    {
        for (var index = 0; index < _labels.Length; index++)
        {
            var text = _labels[index];
            if (text == null)
                continue;
            var shown = index < Count;
            if (text.gameObject.activeSelf != shown)
                text.gameObject.SetActive(shown);
            var angle = index * 360f / Count * Mathf.Deg2Rad;
            text.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 90f;
        }
    }

    internal NativeWheelMenuAction MenuAt(int index) => index == 0 ? NativeWheelMenuAction.GameMenu :
        index == 1 ? NativeWheelMenuAction.VrMenu : index == 2 ? NativeWheelMenuAction.Map :
        index == EjectIndex && _ejectAvailable ? NativeWheelMenuAction.Eject : NativeWheelMenuAction.None;

    internal void SetEjectAvailable(bool available)
    {
        if (_ejectAvailable == available)
            return;
        _ejectAvailable = available;
        if (available && _labels[EjectIndex] == null)
        {
            var label = CreateLabel("EJECT");
            label.color = new Color(1f, .62f, .55f, 1f);
            _labels[EjectIndex] = label;
        }
        if (_hovered >= Count)
            _hovered = -1;
        Layout();
        SetVerticesDirty();
    }

    internal void RefreshMapState(bool open, bool available)
    {
        var map = _labels[2];
        if (map != null)
        {
            map.text = (open ? "CLOSE MAP" : "OPEN MAP") + (available ? "" : "\nUnavailable");
            map.color = available ? Color.white : new Color(.5f, .55f, .58f, 1);
        }
        if (_mapAvailable == available) return;
        _mapAvailable = available;
        if (!available && _hovered == 2) _hovered = -1;
        SetVerticesDirty();
    }

    internal bool Hover(int index)
    {
        var next = index >= 0 && index < Count && (index != 2 || _mapAvailable) ? index : -1;
        if (next == _hovered) return false;
        _hovered = next;
        SetVerticesDirty();
        return next >= 0;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var centre = rectTransform.rect.center;
        var radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .48f;
        if (radius <= 0) return;
        const int segments = 32;
        var count = Count;
        for (var sector = 0; sector < count; sector++)
        {
            var eject = sector == EjectIndex;
            var tint = sector == 2 && !_mapAvailable ? new Color(.04f, .06f, .08f, .94f) :
                sector == _hovered ? eject ? new Color(.55f, .16f, .12f, .97f) : new Color(.10f, .48f, .52f, .97f) :
                eject ? new Color(.20f, .07f, .06f, .94f) : new Color(.06f, .10f, .13f, .94f);
            var first = mesh.currentVertCount;
            var sectorAngle = 360f / count;
            for (var point = 0; point <= segments; point++)
            {
                // Match the clockwise sector calculation used by the flight
                // wheel, leaving a small gap between adjacent choices.
                var angle = (sector * sectorAngle - sectorAngle * .5f + 2f + point * (sectorAngle - 4f) / segments) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                mesh.AddVert(centre + direction * (radius * .25f), tint, Vector2.zero);
                mesh.AddVert(centre + direction * radius, tint, Vector2.zero);
                if (point == segments) continue;
                var current = first + point * 2;
                mesh.AddTriangle(current, current + 1, current + 2);
                mesh.AddTriangle(current + 1, current + 3, current + 2);
            }
        }
    }
}
