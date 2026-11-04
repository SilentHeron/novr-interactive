using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// The native weaponDisabled image is a full-panel grey safety overlay, not a
// weapon icon. Keep WeaponStatus's enabled state as the authority, but present
// it as a compact label so the selected weapon remains legible in VR.
internal sealed class NativeWeaponSafetyIndicator
{
    internal Image Source { get; }
    private readonly HudCanvasGroupLease _hide;
    private readonly Text _label;

    internal NativeWeaponSafetyIndicator(Image source, RectTransform parent)
    {
        Source = source; _hide = new HudCanvasGroupLease(source.gameObject);
        var go = new GameObject("NOVR weapon safety", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false); go.layer = parent.gameObject.layer;
        _label = go.GetComponent<Text>(); _label.text = "SAFE";
        _label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _label.fontSize = 18; _label.alignment = TextAnchor.MiddleRight;
        _label.color = new Color(1f, .78f, .28f, 1f); _label.raycastTarget = false;
        var rect = _label.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = new Vector2(68, 24);
        rect.localPosition = new Vector3(parent.rect.xMax - 38, parent.rect.yMax - 14, 0);
        Refresh();
    }

    internal void Refresh()
    {
        _hide.Apply(0);
        _label.enabled = Source != null && Source.enabled && Source.gameObject.activeSelf;
    }

    internal void Release()
    {
        _hide.Release();
        if (_label != null) UnityEngine.Object.Destroy(_label.gameObject);
    }
}
