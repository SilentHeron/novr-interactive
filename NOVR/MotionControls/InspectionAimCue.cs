using System.Collections.Generic;
using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.MotionControls;

// After the native designator/EOTS updates, before the ordinary gaze cue.
// Reuse the inspection canvas and mask, without another camera or HUD render.
[DefaultExecutionOrder(105)]
internal sealed class InspectionAimCue : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<CombatHUD, List<HUDUnitMarker>> Markers =
        AccessTools.FieldRefAccess<CombatHUD, List<HUDUnitMarker>>("markers");
    private static InspectionAimCue? _instance;
    private RectTransform? _anchor, _ring;
    private CanvasGroup? _group;
    private float _nextCandidateCheck, _opacity;
    private bool _candidate;
    internal float Magnification = 1;
    internal static bool Visible => _instance != null && _instance.isActiveAndEnabled &&
        _instance._ring != null && _instance._ring.gameObject.activeInHierarchy && _instance._opacity > .001f;

    private void Awake()
    {
        _instance = this;
        _anchor = (RectTransform)new GameObject("Magnified aim position", typeof(RectTransform), typeof(CanvasGroup)).transform;
        _anchor.SetParent(transform, false);
        _group = _anchor.GetComponent<CanvasGroup>(); _group.blocksRaycasts = false;
        _ring = (RectTransform)GazeRingGraphic.Create(_anchor).transform;
        LayerHelper.SetLayerRecursive(_anchor, LayerHelper.GetVrUiLayer());
        Hide();
    }

    private void LateUpdate()
    {
        var hud = SceneSingleton<CombatHUD>.i;
        var native = hud?.targetDesignator;
        if (!CockpitViewZoom.IsActive || hud == null || hud.aircraft == null || native == null ||
            !native.gameObject.activeInHierarchy || ModConfiguration.Instance.TargetCue.Value == TargetCueMode.Off)
        { Hide(); return; }
        var camera = APIBus.CockpitHudCamera;
        if (camera == null) { Hide(); return; }
        var aim = camera.transform.InverseTransformPoint(native.transform.position);
        if (!TrackedControllers.Finite(aim) || aim.z <= .1f || !FlightControlMath.IsFinite(Magnification) || Magnification < 1)
        { Hide(); return; }
        // Use exactly the marker projection. The small visual drop and ring
        // diameter remain constant; only its aim position is magnified.
        var projected = new Vector2(HudPresentationMath.Project(aim.x, aim.z, 3, Magnification),
            HudPresentationMath.Project(aim.y, aim.z, 3, Magnification));
        var ringBounds = _ring!.rect;
        ringBounds.position += projected + _ring.anchoredPosition;
        if (!((RectTransform)transform).rect.Overlaps(ringBounds))
        { Hide(); return; } // Do not clamp an offscreen direction to a false aim point.

        if (Time.unscaledTime >= _nextCandidateCheck)
        {
            _nextCandidateCheck = Time.unscaledTime + .08f; _candidate = false;
            var exclusions = SceneSingleton<TargetListSelector>.i;
            foreach (var marker in Markers(hud))
                if (marker.unit != null && !marker.selected && !marker.unit.disabled && marker.image != null &&
                    marker.image.isActiveAndEnabled && marker.image.canvasRenderer.GetInheritedAlpha() > .01f &&
                    exclusions != null && !exclusions.CheckExclusions(marker.unit) &&
                    Vector3.SqrMagnitude(marker.image.transform.position - native.transform.position) <= 10000)
                { _candidate = true; break; }
        }
        var selecting = ControllerActions.Instance?.TargetSelecting == true || GameManager.playerInput.GetButton("Select");
        var show = TargetCueVisibility.Show(ModConfiguration.Instance.TargetCue.Value, true, _candidate, selecting);
        var desired = show ? _candidate || selecting ? 1f : .8f : 0;
        _opacity = Mathf.MoveTowards(_opacity, desired, Time.unscaledDeltaTime * 6);
        _anchor!.anchoredPosition = projected;
        _group!.alpha = _opacity;
        _ring.gameObject.SetActive(_opacity > .001f);
        // Native HUD copies are added lazily; the hollow cue stays above them.
        if (_anchor.GetSiblingIndex() != transform.childCount - 1) _anchor.SetAsLastSibling();
    }

    private void Hide()
    {
        _opacity = 0; _candidate = false; _nextCandidateCheck = 0;
        if (_group != null) _group.alpha = 0;
        if (_ring != null) _ring.gameObject.SetActive(false);
    }
    private void OnDisable() => Hide();
    private void OnDestroy()
    {
        Hide(); if (_anchor != null) Destroy(_anchor.gameObject);
        if (_instance == this) _instance = null;
    }
}
