using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// LeaderboardMenu's root Image is a full-viewport additive veil. Its children
// contain the actual menu panels; keep those and the root's click blocker intact.
internal sealed class VrGameMenuBackdrop : MonoBehaviour
{
    private Image? _backdrop;
    private bool _hidden, _originalTransparentCull;
    private float _originalAlpha;

    private void OnEnable() => Refresh();
    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (_backdrop == null)
        {
            Restore();
            _backdrop = GetComponent<Image>();
        }
        if (!XRSettings.enabled || _backdrop == null)
        {
            Restore();
            return;
        }
        if (!_hidden)
        {
            _originalAlpha = _backdrop.color.a;
            _originalTransparentCull = _backdrop.canvasRenderer.cullTransparentMesh;
            _hidden = true;
        }
        // GraphicRaycaster skips culled renderers. Keep the transparent mesh
        // eligible for raycasts so clicking outside a menu cannot hit the map.
        _backdrop.canvasRenderer.cullTransparentMesh = false;
        var color = _backdrop.color;
        color.a = 0;
        _backdrop.color = color;
    }

    private void Restore()
    {
        if (!_hidden)
            return;
        if (_backdrop != null)
        {
            var color = _backdrop.color;
            color.a = _originalAlpha;
            _backdrop.color = color;
            _backdrop.canvasRenderer.cullTransparentMesh = _originalTransparentCull;
        }
        _hidden = false;
    }

    private void OnDisable() => Restore();
    private void OnDestroy() => Restore();
}
