using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.SpecialBehavior;

// Attach only to status panels, never to world target or flight-path symbology.
[DefaultExecutionOrder(70)]
internal sealed class HudReadability : MonoBehaviour
{
    private Vector3 _baseScale;
    private Vector3 _basePosition;
    private Image? _backplate;
    private CanvasGroup? _group;
    private bool _secondary;
    private ThreatList? _threats;

    internal static void Add(Transform panel, bool secondary)
    {
        var behavior = panel.gameObject.AddComponent<HudReadability>();
        behavior._secondary = secondary;
    }

    private void Awake()
    {
        _threats = GetComponentInChildren<ThreatList>(true);
        _baseScale = transform.localScale;
        _basePosition = transform.localPosition;
        _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        var go = new GameObject("NOVR status backplate", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-12, -8);
        rect.offsetMax = new Vector2(12, 8);
        _backplate = go.GetComponent<Image>();
        _backplate.color = new Color(0.01f, 0.02f, 0.025f, 0.78f);
        _backplate.raycastTarget = false;
        go.layer = gameObject.layer;
    }

    private void LateUpdate()
    {
        if (MotionControls.UniversalCockpitPanel.IsDocked(transform))
        {
            if (_backplate != null) _backplate.enabled = false;
            if (_group != null) _group.alpha = 1f;
            return;
        }
        var config = ModConfiguration.Instance;
        var camera = APIBus.CockpitHudCamera;
        var originalPosition = transform.parent != null ? transform.parent.TransformPoint(_basePosition) : _basePosition;
        var direction = originalPosition - camera.transform.position;
        var ratio = config.HudPanelDistance.Value / Mathf.Max(0.01f, direction.magnitude);
        transform.position = camera.transform.position + direction * ratio;
        transform.localScale = _baseScale * (config.HudPanelScale.Value * ratio);
        var incomingMissile = _secondary && _threats != null && _threats.transform.childCount > 0;
        if (_backplate != null) _backplate.enabled = config.HudContrast.Value &&
            !(_secondary && CockpitAlerts.Active) &&
            (!MotionControls.UniversalCockpitPanel.HasDisplays || incomingMissile);
        if (_group != null) _group.alpha = _secondary && config.HudDeclutter.Value && !incomingMissile ? 0.15f : 1f;
    }
}
