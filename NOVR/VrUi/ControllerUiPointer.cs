using NOVR.MotionControls;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NOVR.VrUi;

// Uses the controller ray itself, not a ray reconstructed from the centre eye.
// Standard UGUI pointer events keep buttons, dropdowns, scrolling and dragging working.
internal sealed class ControllerUiPointer
{
    private readonly PointerHandSelection _hands = new();
    private ControllerButtonGate Trigger => _hands.Trigger;
    private PointerEventData? _event;
    private EventSystem? _system;
    private readonly bool[] _hasPose = new bool[2];
    private readonly Vector3[] _positions = new Vector3[2];
    private readonly Quaternion[] _rotations = new Quaternion[2];
    internal Ray Ray { get; private set; }
    internal Vector3 HitPosition { get; private set; }
    internal bool Held => Trigger.Held;
    internal bool Pressed => Trigger.Pressed;
    internal Vector2 Scroll { get; private set; }
    internal bool OverInteractive { get; private set; }
    internal bool OverMap { get; private set; }

    internal bool Update(Camera camera)
    {
        var settings = ModConfiguration.Instance;
        var leftHand = TrackedControllers.Read(true); var rightHand = TrackedControllers.Read(false);
        var leftAvailable = leftHand.Valid && !HelmetNightVision.OwnsHand(true);
        var rightAvailable = rightHand.Valid && !HelmetNightVision.OwnsHand(false);
        if (!settings.ControllerBindings.PointerEnabled.Value || (!leftAvailable && !rightAvailable) || EventSystem.current == null)
        {
            Cancel();
            return false;
        }
        if (_system != EventSystem.current)
        {
            Cancel();
            _system = EventSystem.current;
            _event = new PointerEventData(_system) { pointerId = -101, button = PointerEventData.InputButton.Left };
        }
        if (!leftAvailable) _hasPose[0] = false;
        if (!rightAvailable) _hasPose[1] = false;
        var leftRay = leftAvailable ? HandRay(camera, leftHand, 0) : default;
        var rightRay = rightAvailable ? HandRay(camera, rightHand, 1) : default;
        var leftHit = leftAvailable ? Raycast(leftRay, camera) : default;
        var rightHit = rightAvailable ? Raycast(rightRay, camera) : default;
        var previousLeft = _hands.Left;
        _hands.Update(settings.MotionControls.LeftHanded.Value, leftAvailable, leftHand.Trigger, leftHit.gameObject != null,
            rightAvailable, rightHand.Trigger, rightHit.gameObject != null,
            TargetPriority(leftHit.gameObject), TargetPriority(rightHit.gameObject));
        if (previousLeft != _hands.Left) { Release(true); SetHover(null); }
        if (!(_hands.Left ? leftAvailable : rightAvailable)) { Cancel(); return false; }
        var hand = _hands.Left ? leftHand : rightHand;
        Ray = _hands.Left ? leftRay : rightRay;
        var hit = _hands.Left ? leftHit : rightHit;
        HitPosition = hit.gameObject != null ? hit.worldPosition : Ray.GetPoint(5);
        var screen = (Vector2)camera.WorldToScreenPoint(HitPosition, Camera.MonoOrStereoscopicEye.Mono);
        var data = _event!;
        data.delta = screen - data.position;
        data.position = screen;
        data.pointerCurrentRaycast = hit;
        OverInteractive = Interactive(hit.gameObject);
        OverMap = IsMap(hit.gameObject);
        SetHover(hit.gameObject);
        // UGUI scroll is in wheel steps. Use elapsed time, not frame count.
        Scroll = new Vector2(0, FlightControlMath.Axis(hand.Axis.y, 0.2f, 1) * 12f * Mathf.Min(Time.unscaledDeltaTime, 0.05f));
        data.scrollDelta = Scroll;
        if (Trigger.Pressed)
        {
            Press();
            var preview = data.pointerPress != null ? data.pointerPress.GetComponent<FeedbackPreviewButton>() : null;
            if (preview != null) preview.Arm(_hands.Left, hand.Device);
            else if (data.pointerPress != null || data.pointerDrag != null) InteractionHaptics.Pulse(hand.Device, .18f, .025f);
        }
        if (Trigger.Held) Drag();
        if (Trigger.Released) Release(false);
        if (Scroll.sqrMagnitude > 0 && !OverMap)
            ExecuteEvents.ExecuteHierarchy(hit.gameObject, data, ExecuteEvents.scrollHandler);
        return true;
    }

    private static bool Interactive(GameObject? target)
    {
        if (target == null) return false;
        var selectable = target.GetComponentInParent<Selectable>();
        if (selectable != null && (!selectable.isActiveAndEnabled || !selectable.IsInteractable())) return false;
        return ExecuteEvents.GetEventHandler<IPointerClickHandler>(target) != null ||
            ExecuteEvents.GetEventHandler<IDragHandler>(target) != null || ExecuteEvents.GetEventHandler<IScrollHandler>(target) != null;
    }
    private static bool IsMap(GameObject? target)
    {
        var map = SceneSingleton<DynamicMap>.i;
        return DynamicMap.mapMaximized && target != null && map != null &&
            (target.transform.IsChildOf(map.transform) || target == map.mapBackground?.gameObject);
    }
    private static int TargetPriority(GameObject? target) => target == null ? 0 : Interactive(target) || IsMap(target) ? 2 : 1;

    private Ray HandRay(Camera camera, ControllerSample hand, int index)
    {
        var root = camera.transform.parent;
        var position = hand.AimPosition; var rotation = hand.AimRotation;
        if (root != null) { position = root.TransformPoint(position); rotation = root.rotation * rotation; }
        var response = ModConfiguration.Instance.ControllerBindings.PointerResponse.Value;
        var blend = !_hasPose[index] || response <= 0 ? 1 : FlightControlMath.SmoothingWeight(Time.unscaledDeltaTime, response);
        _positions[index] = Vector3.Lerp(_positions[index], position, blend);
        _rotations[index] = Quaternion.Slerp(_rotations[index], rotation, blend);
        _hasPose[index] = true;
        return new Ray(_positions[index], _rotations[index] * Vector3.forward);
    }

    private void SetHover(GameObject? target)
    {
        var data = _event;
        if (data == null || data.pointerEnter == target) return;
        foreach (var hovered in data.hovered)
            if (hovered != null) ExecuteEvents.Execute(hovered, data, ExecuteEvents.pointerExitHandler);
        data.hovered.Clear();
        data.pointerEnter = target;
        for (var current = target != null ? target.transform : null; current != null; current = current.parent)
        {
            ExecuteEvents.Execute(current.gameObject, data, ExecuteEvents.pointerEnterHandler);
            data.hovered.Add(current.gameObject);
        }
    }

    private void Press()
    {
        var data = _event!;
        var target = data.pointerCurrentRaycast.gameObject;
        Debug.Log($"[NOVR] Controller UI press: {(target != null ? target.name : "no target")}.");
        data.eligibleForClick = true;
        data.dragging = false;
        data.useDragThreshold = true;
        data.pressPosition = data.position;
        data.pointerPressRaycast = data.pointerCurrentRaycast;
        var selected = ExecuteEvents.GetEventHandler<ISelectHandler>(target);
        if (_system!.currentSelectedGameObject != selected) _system.SetSelectedGameObject(null, data);
        data.pointerPress = ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.pointerDownHandler) ??
                            ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
        data.rawPointerPress = target;
        data.clickCount = data.lastPress == data.pointerPress && Time.unscaledTime - data.clickTime < 0.3f ? data.clickCount + 1 : 1;
        data.clickTime = Time.unscaledTime;
        data.pointerDrag = ExecuteEvents.GetEventHandler<IDragHandler>(target);
        if (data.pointerDrag != null) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.initializePotentialDrag);
    }

    private void Drag()
    {
        var data = _event!;
        if (data.pointerDrag == null) return;
        var threshold = _system!.pixelDragThreshold;
        if (!data.dragging && (!data.useDragThreshold || (data.position - data.pressPosition).sqrMagnitude >= threshold * threshold))
        {
            ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.beginDragHandler);
            data.dragging = true;
        }
        if (!data.dragging) return;
        if (data.pointerPress != data.pointerDrag)
        {
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            if (data.pointerPress != null) data.pointerPress.GetComponent<FeedbackPreviewButton>()?.Cancel();
            data.eligibleForClick = false;
            data.pointerPress = null;
            data.rawPointerPress = null;
        }
        ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.dragHandler);
    }

    private void Release(bool cancel)
    {
        var data = _event;
        if (data == null) return;
        ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
        if (!cancel)
        {
            var target = data.pointerCurrentRaycast.gameObject;
            var click = ExecuteEvents.GetEventHandler<IPointerClickHandler>(target);
            if (data.pointerPress == click && data.eligibleForClick)
                ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerClickHandler);
            else if (data.dragging) ExecuteEvents.ExecuteHierarchy(target, data, ExecuteEvents.dropHandler);
        }
        if (data.dragging) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.endDragHandler);
        if (data.pointerPress != null) data.pointerPress.GetComponent<FeedbackPreviewButton>()?.Cancel();
        data.eligibleForClick = data.dragging = false;
        data.pointerPress = data.rawPointerPress = data.pointerDrag = null;
    }

    internal void Cancel()
    {
        Release(true);
        SetHover(null);
        _hands.Reset();
        _hasPose[0] = _hasPose[1] = false;
        OverMap = OverInteractive = false;
        Scroll = Vector2.zero;
    }

    private static RaycastResult Raycast(Ray ray, Camera camera)
    {
        var best = default(RaycastResult);
        foreach (var caster in RaycasterManager.GetRaycasters())
        {
            if (caster is not GraphicRaycaster graphics || !graphics.isActiveAndEnabled) continue;
            var canvas = graphics.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode != RenderMode.WorldSpace || canvas.gameObject.layer != (int)LayerHelper.GetVrUiLayer()) continue;
            var eventCamera = graphics.eventCamera ?? camera;
            // Unity's IndexedSet implements IList but deliberately throws from
            // GetEnumerator. Match GraphicRaycaster's indexed traversal.
            var registeredGraphics = GraphicRegistry.GetGraphicsForCanvas(canvas);
            for (var i = 0; i < registeredGraphics.Count; i++)
            {
                var graphic = registeredGraphics[i];
                if (graphic == null || !graphic.isActiveAndEnabled || !graphic.raycastTarget || graphic.depth < 0 || graphic.canvasRenderer.cull) continue;
                if (graphics.ignoreReversedGraphics && Vector3.Dot(ray.direction, graphic.transform.forward) <= 0) continue;
                if (!HitRect(ray, graphic.rectTransform, out var distance, out var point)) continue;
                var screen = (Vector2)eventCamera.WorldToScreenPoint(point, Camera.MonoOrStereoscopicEye.Mono);
                if (!graphic.Raycast(screen, eventCamera)) continue;
                var candidate = new RaycastResult
                {
                    gameObject = graphic.gameObject, module = graphics, distance = distance,
                    worldPosition = point, worldNormal = -graphic.transform.forward, screenPosition = screen,
                    depth = graphic.depth, sortingLayer = canvas.sortingLayerID, sortingOrder = canvas.sortingOrder
                };
                if (InFront(candidate, best)) best = candidate;
            }
        }
        var map = SceneSingleton<DynamicMap>.i;
        if (DynamicMap.mapMaximized && map?.mapBackground != null && map.mapBackground.gameObject.activeInHierarchy &&
            HitRect(ray, map.mapBackground.rectTransform, out var mapDistance, out var mapPoint) &&
            (best.gameObject == null || mapDistance < best.distance - 0.005f))
            best = new RaycastResult { gameObject = map.mapBackground.gameObject, distance = mapDistance, worldPosition = mapPoint };
        return best;
    }

    private static bool InFront(RaycastResult candidate, RaycastResult current)
    {
        if (current.gameObject == null || candidate.distance < current.distance - 0.005f) return true;
        if (candidate.distance > current.distance + 0.005f) return false;
        var candidateLayer = SortingLayer.GetLayerValueFromID(candidate.sortingLayer);
        var currentLayer = SortingLayer.GetLayerValueFromID(current.sortingLayer);
        if (candidateLayer != currentLayer) return candidateLayer > currentLayer;
        if (candidate.sortingOrder != current.sortingOrder) return candidate.sortingOrder > current.sortingOrder;
        return candidate.depth > current.depth;
    }

    internal static bool HitRect(Ray ray, RectTransform rect, out float distance, out Vector3 point)
    {
        var origin = rect.InverseTransformPoint(ray.origin);
        var direction = rect.InverseTransformVector(ray.direction);
        var bounds = rect.rect;
        var hit = PointerGeometry.IntersectRect(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z,
            bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax, out distance);
        point = hit ? ray.GetPoint(distance) : default;
        return hit;
    }
}
