using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// CanvasGroup is a single-instance Unity component. Borrow a native group when
// present, and restore its opacity/input state instead of adding a second one.
internal sealed class HudCanvasGroupLease
{
    private readonly CanvasGroup _group;
    private readonly bool _owned, _blocksRaycasts, _interactable;
    private readonly float _alpha;

    internal HudCanvasGroupLease(GameObject owner)
    {
        var existing = owner.GetComponent<CanvasGroup>();
        _owned = existing == null;
        _group = existing != null ? existing : owner.AddComponent<CanvasGroup>();
        _alpha = _group.alpha; _blocksRaycasts = _group.blocksRaycasts; _interactable = _group.interactable;
    }

    internal void Apply(float multiplier)
    {
        if (_group == null) return;
        _group.alpha = _alpha * multiplier;
        _group.blocksRaycasts = _group.interactable = false;
    }

    internal void Restore()
    {
        if (_group == null) return;
        _group.alpha = _alpha; _group.blocksRaycasts = _blocksRaycasts; _group.interactable = _interactable;
    }

    internal void Release()
    {
        Restore();
        if (_owned && _group != null) UnityEngine.Object.Destroy(_group);
    }
}
