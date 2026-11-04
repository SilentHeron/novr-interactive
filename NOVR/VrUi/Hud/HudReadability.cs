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
    private HudCanvasGroupLease? _group;
    private bool _secondary;
    private bool _sourcesMoved;
    private float _readabilityAlpha = 1f;
    private ThreatList? _threats;

    internal static void Add(Transform panel, bool secondary)
    {
        var behavior = panel.GetComponent<HudReadability>();
        if (behavior == null)
            behavior = panel.gameObject.AddComponent<HudReadability>();
        behavior._secondary = secondary;
    }

    private void Awake()
    {
        _threats = GetComponentInChildren<ThreatList>(true);
        _baseScale = transform.localScale;
        _basePosition = transform.localPosition;
        _group = new HudCanvasGroupLease(gameObject);
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
        if (_sourcesMoved)
        {
            if (_backplate != null)
                _backplate.enabled = false;
            _group?.Apply(0);
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
        // The lower-left source container also held the minimized map. Once
        // that map moves, retain native threat/text fallbacks without its empty
        // 400px square backing plate. Active native missile warnings may still
        // use contrast when the compact warning view cannot replace them.
        var mapMoved = _secondary && CockpitHudWidgets.Owns("map") && GetComponentInChildren<DynamicMap>(true) == null;
        var nativeThreats = incomingMissile && !CockpitAlerts.Active;
        if (_backplate != null)
            _backplate.enabled = config.HudContrast.Value &&
            !(_secondary && CockpitAlerts.Active) &&
            (!mapMoved || nativeThreats);
        _readabilityAlpha = _secondary && config.HudDeclutter.Value && !incomingMissile ? 0.15f : 1f;
        _group?.Apply(_readabilityAlpha);
    }

    internal void SetWidgetSourcesMoved(bool moved)
    {
        _sourcesMoved = moved;
        if (moved && _backplate != null)
            _backplate.enabled = false;
        if (isActiveAndEnabled)
            _group?.Apply(moved ? 0 : _readabilityAlpha);
        else
            _group?.Restore();
    }

    private void OnDisable()
    {
        _sourcesMoved = false;
        _group?.Restore();
        if (_backplate != null)
            _backplate.enabled = false;
    }

    private void OnDestroy()
    {
        _group?.Release();
        if (_backplate != null)
            Destroy(_backplate.gameObject);
    }
}
