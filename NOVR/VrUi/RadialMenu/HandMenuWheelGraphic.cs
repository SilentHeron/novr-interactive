using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// A self-owned three-item wheel for gameplay views without usable flight actions.
// It needs no native wheel prefab, action assets, aircraft or weapons page.
internal sealed class HandMenuWheelGraphic : MaskableGraphic
{
    internal const int Count = 3;
    private int _hovered = -1;
    private Text? _mapLabel;
    private bool _mapAvailable = true;

    internal void Initialize()
    {
        raycastTarget = false;
        rectTransform.sizeDelta = new Vector2(300, 300);
        for (var index = 0; index < Count; index++)
        {
            var label = new GameObject(index == 0 ? "GAME MENU" : index == 1 ? "VR MENU" : "OPEN MAP", typeof(RectTransform), typeof(Text));
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
            var angle = index * 360f / Count * Mathf.Deg2Rad;
            text.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 90f;
            if (index == 2) _mapLabel = text;
        }
    }

    internal static NativeWheelMenuAction MenuAt(int index) => index == 0 ? NativeWheelMenuAction.GameMenu :
        index == 1 ? NativeWheelMenuAction.VrMenu : index == 2 ? NativeWheelMenuAction.Map : NativeWheelMenuAction.None;

    internal void RefreshMapState(bool open, bool available)
    {
        if (_mapLabel != null)
        {
            _mapLabel.text = (open ? "CLOSE MAP" : "OPEN MAP") + (available ? "" : "\nUnavailable");
            _mapLabel.color = available ? Color.white : new Color(.5f, .55f, .58f, 1);
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
        for (var sector = 0; sector < Count; sector++)
        {
            var tint = sector == 2 && !_mapAvailable ? new Color(.04f, .06f, .08f, .94f) :
                sector == _hovered ? new Color(.10f, .48f, .52f, .97f) : new Color(.06f, .10f, .13f, .94f);
            var first = mesh.currentVertCount;
            for (var point = 0; point <= segments; point++)
            {
                // Match the clockwise sector calculation used by the flight
                // wheel, leaving a small gap between adjacent choices.
                const float sectorAngle = 360f / Count;
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
