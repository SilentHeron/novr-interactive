using System.Collections.Generic;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

namespace NOVR.VrUi;

[DefaultExecutionOrder(-1000)]
public class VrUiCursor: NOVRBehaviour
{
    public static VrUiCursor? Instance { get; private set; }
    public static VrUiCursor? I => Instance;

    public bool IsActive => _cursor != null && _cursor.activeSelf;
    public bool IsControllerPointer { get; private set; }
    public bool PrimaryHeld { get; private set; }
    public bool PrimaryPressed { get; private set; }
    public Vector2 Scroll { get; private set; }
    internal bool ControllerOverMap => IsControllerPointer && _controllerPointer.OverMap;
    private readonly ControllerUiPointer _controllerPointer = new();
    private readonly ControllerButtonGate _mouseClick = new();
    private LineRenderer? _laser;
    private Material? _laserMaterial;
    private UnityEngine.InputSystem.UI.InputSystemUIInputModule? _boundUiModule;
    private Native.NativeVrUiRoot? _nativeMenu;
    private string? _reportedPointerState;
    private float _nextPointerReport;
    public Vector3 CursorPosition => _cursor != null ? _cursor.transform.position : Vector3.zero;

    protected override void Awake()
    {
        base.Awake();
        Instance = this;
    }

    private void OnDestroy()
    {
        _controllerPointer.Cancel();
        if (_cursor != null) Destroy(_cursor);
        if (_laser != null) Destroy(_laser.gameObject);
        if (_laserMaterial != null) Destroy(_laserMaterial);
        if (_texture != null) Destroy(_texture);
        if (Instance == this)
        {
            Instance = null;
        }
        if (_virtualMouse != null)
        {
            try
            {
                InputSystem.RemoveDevice(_virtualMouse);
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[{nameof(VrUiCursor)}] Failed to remove VirtualMouse during OnDestroy: {ex}");
            }
            _virtualMouse = null;
        }
    }

    private Texture2D? _texture;
    private const float MaxYawDegrees = 65f;
    private const float MaxPitchDegrees = 45f;
    private const float DefaultProjectionDistance = 5;
    private const float CursorCanvasScale = 0.001f;
    private const int CursorTextureSize = 64;
    private const float CursorRingRadius = 12f;
    private const float CursorRingThickness = 4f;
    private const float CursorIdlePulseScale = 0.035f;
    private const float CursorIdlePulseSpeed = 5.5f;
    private const float CursorHoverScale = 1.18f;
    private const float CursorPressedScale = 0.84f;
    private const float CursorClickPulseScale = 0.22f;
    private const float CursorClickPulseDuration = 0.18f;
    private const float CursorAnimationLerpSpeed = 24f;
    private static readonly Color CursorNormalColor = new Color32(100, 200, 100, 255);
    private static readonly Color CursorHoverColor = new Color32(155, 255, 175, 255);
    private static readonly Color CursorPressedColor = new Color32(255, 224, 92, 255);
    private GameObject? _cursor;
    private RectTransform? _cursorRectTransform;
    private Canvas? _cursorCanvas;
    private RawImage? _cursorImage;
    private bool _cursorOverInteractive;
    private float _lastCursorClickTime = -100f;
    private bool _hasProjectionReferenceOverride;
    private Quaternion _projectionReferenceRotation = Quaternion.identity;

    private bool _hasInitializedEventSystem = false;
    private Mouse? _virtualMouse;
    private Mouse? _realMouse;
    
    
    private int ScreenWidth => Screen.width;
    private int ScreenHeight => Screen.height;
    public Camera? UiCamera
    {
        get
        {
            return APIBus.CockpitHudCamera;
        }
    }
    
    
    public Vector2 GetScreenPoint()
    {
        var camera = UiCamera;
        if (_cursor != null && camera != null)
        {
            Vector3 viewportPoint = camera.WorldToViewportPoint(_cursor.transform.position, Camera.MonoOrStereoscopicEye.Mono);
            float screenX = Mathf.Clamp(viewportPoint.x * Screen.width, 0f, Screen.width);
            float screenY = Mathf.Clamp(viewportPoint.y * Screen.height, 0f, Screen.height);
            return new Vector2(screenX, screenY);
        }
        return Vector2.zero;
    }

    public void SetProjectionReferenceRotation(Quaternion referenceRotation)
    {
        _projectionReferenceRotation = referenceRotation;
        _hasProjectionReferenceOverride = true;
    }

    public void ClearProjectionReferenceRotation()
    {
        _hasProjectionReferenceOverride = false;
    }
    
    
    private void Start()
    {
        _texture = CreateCursorTexture();
    }

    private void Update()
    {
        var camera = UiCamera;
        if (XRSettings.enabled ? !OpenXRUtility.IsSessionFocused : !Application.isFocused)
        {
            ReportPointerState("hidden: input focus");
            HidePointer();
            return;
        }
        if (_nativeMenu == null && transform.parent != null)
            _nativeMenu = transform.parent.GetComponentInChildren<Native.NativeVrUiRoot>(true);
        // The native menu owns its own visibility. A stale desktop cursor flag
        // after a loading-scene transition must not disable its tracked pointer.
        if (camera == null || (!IsRealCursorVisible() && _nativeMenu?.IsVisible != true && !WristMenu.IsOpen))
        {
            ReportPointerState(camera == null ? "hidden: no UI camera" : "hidden: no interactive UI");
            HidePointer();
            return;
        }
        if (_virtualMouse == null)
        {
            foreach (var device in InputSystem.devices)
                if (device is Mouse mouse) { _realMouse = mouse; break; }
            _virtualMouse = InputSystem.AddDevice<Mouse>("VirtualMouse");
        }
        // Menus may recreate their EventSystem after a scene transition.
        var currentModule = EventSystem.current?.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        if (currentModule != _boundUiModule) _hasInitializedEventSystem = false;
        if (!_hasInitializedEventSystem) _hasInitializedEventSystem = RestrictUIModuleToVirtualMouse();
        if (_texture == null) return;
        EnsureCursorCanvas(camera);
        var wasController = IsControllerPointer;
        IsControllerPointer = _controllerPointer.Update(camera);
        if (IsControllerPointer)
        {
            ReportPointerState("controller pointer active");
            _mouseClick.Reset();
            _cursor!.SetActive(true);
            _cursor.transform.position = _controllerPointer.HitPosition;
            _cursor.transform.rotation = Quaternion.LookRotation(_controllerPointer.Ray.direction, camera.transform.up);
            _cursorOverInteractive = _controllerPointer.OverInteractive;
            PrimaryHeld = _controllerPointer.Held;
            PrimaryPressed = _controllerPointer.Pressed;
            Scroll = _controllerPointer.Scroll;
            // The tracked pointer sends UGUI events at its exact surface hit. Keep the
            // separate mouse pointer off-screen to avoid duplicate hover/click events.
            InputState.Change(_virtualMouse, new MouseState { position = new Vector2(-10000, -10000) });
            UpdateLaser(_controllerPointer.Ray.origin, _controllerPointer.HitPosition);
        }
        else
        {
            if (_laser != null) _laser.enabled = false;
            // A background desktop must not feed mouse clicks into the headset.
            if (!Application.isFocused) { ReportPointerState("hidden: no tracked pointer and desktop unfocused"); HidePointer(); return; }
            if (_realMouse == null || !_realMouse.added)
            {
                _realMouse = null;
                foreach (var device in InputSystem.devices)
                    if (device is Mouse mouse && mouse != _virtualMouse) { _realMouse = mouse; break; }
                if (_realMouse == null) { ReportPointerState("hidden: no tracked pointer or mouse"); HidePointer(); return; }
            }
            ReportPointerState("mouse fallback active");
            if (wasController) _mouseClick.Reset();
            UpdateCursorAngles();
            _mouseClick.Update(true, _realMouse.leftButton.isPressed ? 1f : 0f);
            PrimaryHeld = _mouseClick.Held;
            PrimaryPressed = _mouseClick.Pressed;
            Scroll = _realMouse.scroll.ReadValue();
            ushort buttons = PrimaryHeld ? (ushort)1 : (ushort)0;
            if (_realMouse.rightButton.isPressed) buttons |= 2;
            if (_realMouse.middleButton.isPressed) buttons |= 4;
            InputState.Change(_virtualMouse, new MouseState
            {
                position = GetScreenPoint(), delta = _realMouse.delta.ReadValue(), scroll = Scroll, buttons = buttons
            });
        }
        UpdateCursorAnimation();
    }

    private void ReportPointerState(string state)
    {
        if (state == _reportedPointerState || Time.unscaledTime < _nextPointerReport) return;
        _reportedPointerState = state;
        _nextPointerReport = Time.unscaledTime + 2f;
        var left = ModConfiguration.Instance.MotionControls.LeftHanded.Value;
        var hand = InputDevices.GetDeviceAtXRNode(left ? XRNode.LeftHand : XRNode.RightHand);
        hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked);
        hand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trackingState, out var tracking);
        var head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var headTracked);
        var hasPresence = head.TryGetFeatureValue(UnityEngine.XR.CommonUsages.userPresence, out var present);
        Debug.Log($"[NOVR] UI input: {state}; desktopFocused={Application.isFocused}, xrFocused={OpenXRUtility.IsSessionFocused}, cursor={Cursor.visible}/{Cursor.lockState}, nativeMenu={_nativeMenu?.IsVisible}, pointerEnabled={ModConfiguration.Instance.ControllerBindings.PointerEnabled.Value}, handValid={hand.isValid}, handTracked={tracked}/{tracking}, headTracked={headTracked}, presence={(hasPresence ? present.ToString() : "unreported")}, eventSystem={EventSystem.current != null}.");
        if (IsControllerPointer)
            Debug.Log($"[NOVR] UI pointer geometry: origin={_controllerPointer.Ray.origin}, target={_controllerPointer.HitPosition}, camera={UiCamera?.transform.position}, interactive={_controllerPointer.OverInteractive}.");
    }

    private void HidePointer()
    {
        _controllerPointer.Cancel();
        _mouseClick.Reset();
        IsControllerPointer = PrimaryHeld = PrimaryPressed = false;
        Scroll = Vector2.zero;
        if (_cursor != null) _cursor.SetActive(false);
        if (_laser != null) _laser.enabled = false;
        if (_virtualMouse != null)
            InputState.Change(_virtualMouse, new MouseState { position = new Vector2(-10000, -10000) });
    }

    private void UpdateLaser(Vector3 start, Vector3 end)
    {
        if (_laser == null)
        {
            var go = new GameObject("NOVR Controller Laser");
            go.transform.SetParent(transform, false);
            go.layer = (int)LayerHelper.GetVrUiLayer();
            _laser = go.AddComponent<LineRenderer>();
            _laser.useWorldSpace = true;
            _laser.positionCount = 2;
            _laser.startWidth = 0.0015f;
            _laser.endWidth = 0.0007f;
            _laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _laser.receiveShadows = false;
            _laserMaterial = CockpitMaterials.Opaque("NOVR menu pointer", CursorHoverColor);
            if (_laserMaterial.HasProperty("_Cull")) _laserMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            _laser.sharedMaterial = _laserMaterial;
        }
        _laser.enabled = true;
        _laser.SetPosition(0, start);
        _laser.SetPosition(1, end);
    }

    protected override void OnDisable() { HidePointer(); base.OnDisable(); }

    private void UpdateCursorAngles()
    {
        var camera = UiCamera;
        if (camera == null) return;

        EnsureCursorCanvas(camera);

        if (_cursor == null || _cursorRectTransform == null)
        {
            return;
        }

        if (!_cursor.activeSelf)
        {
            _cursor.SetActive(true);
        }
        
        var mouse = _realMouse;
        if (mouse == null) return;

        var mousePos = mouse.position.ReadValue();
        float cursorPitch = ProjectPitchAngle(mousePos.y);
        float cursorYaw = ProjectYawAngle(mousePos.x);        
        
        Vector3 localDirection = Quaternion.Euler(-cursorPitch, cursorYaw, 0f) * Vector3.forward;
        Quaternion referenceRotation = GetProjectionReferenceRotation();
        Vector3 worldDirection = referenceRotation * localDirection;
        Vector3 viewportSpace = camera.WorldToViewportPoint(camera.transform.position + worldDirection * DefaultProjectionDistance, Camera.MonoOrStereoscopicEye.Mono);
        Vector2 inScreenSpace = new Vector2(viewportSpace.x * Screen.width, viewportSpace.y * Screen.height);
        float cursorDistance = GetDistanceUnderCursor(inScreenSpace, new Ray(camera.transform.position, worldDirection), camera);
        Vector3 pos = camera.transform.position + worldDirection * cursorDistance;
        _cursor.transform.position = pos;
        _cursor.transform.rotation = Quaternion.LookRotation(worldDirection, camera.transform.up);
    }

    private Quaternion GetProjectionReferenceRotation()
    {
        if (_hasProjectionReferenceOverride)
        {
            return _projectionReferenceRotation;
        }

        return transform.parent != null ? transform.parent.rotation : Quaternion.identity;
    }
    
    private void EnsureCursorCanvas(Camera uiCaptureCamera)
    {
        if (_cursor != null)
        {
            if (_cursorImage != null)
            {
                _cursorImage.texture = _texture;
            }
            return;
        }

        _cursor = new GameObject("VrUiCursorCanvas");
        _cursor.transform.localScale = Vector3.one * CursorCanvasScale;
        _cursorCanvas = _cursor.AddComponent<Canvas>();
        _cursorCanvas.renderMode = RenderMode.WorldSpace;
        _cursorCanvas.planeDistance = Mathf.Max(uiCaptureCamera.nearClipPlane + 0.01f, 0.11f);
        _cursorCanvas.overrideSorting = true;
        _cursorCanvas.sortingOrder = short.MaxValue;
        _cursorCanvas.pixelPerfect = true;

        _cursorRectTransform = _cursor.GetComponent<RectTransform>();
        _cursorRectTransform.sizeDelta = new Vector2(CursorTextureSize, CursorTextureSize);
        _cursorImage = _cursor.AddComponent<RawImage>();
        _cursorImage.raycastTarget = false;
        _cursorImage.texture = _texture;
        _cursorImage.color = CursorNormalColor;
        LayerHelper.SetLayerRecursive(_cursor.transform, LayerHelper.GetVrUiLayer());
        
        
        
        
    }


    // The cursor must sit on the surface it points at; at any other depth each eye sees it shifted
    // relative to that surface, even though clicks (resolved along the centre ray) are correct.
    private float GetDistanceUnderCursor(Vector2 screenPos, Ray cursorRay, Camera camera)
    {
        _cursorOverInteractive = false;
        if (TryGetUiDistanceUnderCursor(screenPos, cursorRay, out var uiDistance, out var overInteractive))
        {
            _cursorOverInteractive = overInteractive;
            return uiDistance;
        }

        // The map image is not a raycast target, so empty map areas return no hits.
        if (TryGetMapDistanceUnderCursor(screenPos, cursorRay, camera, out var mapDistance))
        {
            return mapDistance;
        }

        return DefaultProjectionDistance;
    }

    private bool TryGetUiDistanceUnderCursor(Vector2 screenPos, Ray cursorRay, out float distance, out bool overInteractive)
    {
        distance = default;
        overInteractive = false;

        if (EventSystem.current == null)
        {
            return false;
        }

        var pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);

        foreach (var result in results)
        {
            if (result.gameObject == _cursor || result.distance < 0f)
            {
                continue;
            }

            if (!TryGetPlaneDistance(cursorRay, result.gameObject.transform, out distance))
            {
                continue;
            }

            overInteractive = IsInteractiveRaycastTarget(result.gameObject);
            return true;
        }

        return false;
    }

    private static bool TryGetMapDistanceUnderCursor(Vector2 screenPos, Ray cursorRay, Camera camera, out float distance)
    {
        distance = default;
        var map = SceneSingleton<global::DynamicMap>.i;
        if (map == null || map.mapBackground == null || !map.mapBackground.gameObject.activeInHierarchy)
        {
            return false;
        }

        var background = map.mapBackground.rectTransform;
        return RectTransformUtility.RectangleContainsScreenPoint(background, screenPos, camera) &&
               TryGetPlaneDistance(cursorRay, background, out distance);
    }

    private static bool TryGetPlaneDistance(Ray ray, Transform surface, out float distance)
    {
        return new Plane(surface.forward, surface.position).Raycast(ray, out distance) && distance > 0f;
    }

    private static bool IsInteractiveRaycastTarget(GameObject gameObject)
    {
        var selectable = gameObject.GetComponentInParent<Selectable>();
        if (selectable != null)
        {
            return selectable.IsInteractable();
        }

        return ExecuteEvents.GetEventHandler<IPointerClickHandler>(gameObject) != null ||
               ExecuteEvents.GetEventHandler<IPointerDownHandler>(gameObject) != null ||
               ExecuteEvents.GetEventHandler<ISubmitHandler>(gameObject) != null ||
               ExecuteEvents.GetEventHandler<IDragHandler>(gameObject) != null;
    }

    private void UpdateCursorAnimation()
    {
        if (_cursor == null || _cursorImage == null) return;

        if (PrimaryPressed)
        {
            _lastCursorClickTime = Time.unscaledTime;
        }

        var isPressed = PrimaryHeld;
        var idlePulse = Mathf.Sin(Time.unscaledTime * CursorIdlePulseSpeed) * CursorIdlePulseScale;
        var clickProgress = Mathf.Clamp01((Time.unscaledTime - _lastCursorClickTime) / CursorClickPulseDuration);
        var clickPulse = clickProgress < 1f
            ? Mathf.Sin((1f - clickProgress) * Mathf.PI) * CursorClickPulseScale
            : 0f;

        var targetVisualScale = 1f + idlePulse + clickPulse;
        if (_cursorOverInteractive)
        {
            targetVisualScale *= CursorHoverScale;
        }
        if (isPressed)
        {
            targetVisualScale *= CursorPressedScale;
        }

        var targetScale = Vector3.one * (CursorCanvasScale * targetVisualScale);
        _cursor.transform.localScale = Vector3.Lerp(_cursor.transform.localScale, targetScale, Time.unscaledDeltaTime * CursorAnimationLerpSpeed);

        var targetColor = CursorNormalColor;
        if (_cursorOverInteractive)
        {
            targetColor = CursorHoverColor;
        }
        if (isPressed)
        {
            targetColor = CursorPressedColor;
        }

        _cursorImage.color = Color.Lerp(_cursorImage.color, targetColor, Time.unscaledDeltaTime * CursorAnimationLerpSpeed);
    }


    private float ProjectPitchAngle(float y)
    {
        int height = ScreenHeight;
        if (height <= 0) throw new System.InvalidOperationException($"[{nameof(VrUiCursor)}] Screen height is invalid ({height}).");
        return Mathf.Lerp(-MaxPitchDegrees, MaxPitchDegrees, y / height);
    }
    private float ProjectYawAngle(float x)
    {
        int width = ScreenWidth;
        if (width <= 0) throw new System.InvalidOperationException($"[{nameof(VrUiCursor)}] Screen width is invalid ({width}).");
        return Mathf.Lerp(-MaxYawDegrees, MaxYawDegrees, x / width);
    }
    private static bool IsRealCursorVisible() => Cursor.visible && Cursor.lockState != CursorLockMode.Locked;

    private static Texture2D CreateCursorTexture()
    {
        var texture = new Texture2D(CursorTextureSize, CursorTextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var colors = new Color32[CursorTextureSize * CursorTextureSize];
        var center = new Vector2((CursorTextureSize - 1) * 0.5f, (CursorTextureSize - 1) * 0.5f);
        var innerRadius = CursorRingRadius - CursorRingThickness * 0.5f;
        var outerRadius = CursorRingRadius + CursorRingThickness * 0.5f;
        var transparent = new Color32(0, 0, 0, 0);

        for (var y = 0; y < CursorTextureSize; y++)
        {
            for (var x = 0; x < CursorTextureSize; x++)
            {
                var distanceFromCenter = Vector2.Distance(new Vector2(x, y), center);
                var isRing = distanceFromCenter >= innerRadius && distanceFromCenter <= outerRadius;
                colors[y * CursorTextureSize + x] = isRing ? Color.white : transparent;
            }
        }

        texture.SetPixels32(colors);
        texture.Apply();
        return texture;
    }
    
    private void LogRaycastAtCursor()
    {
        if (EventSystem.current == null) return;
        
        var screenPos = GetScreenPoint();
        var pointerEventData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);
        
        Debug.Log($"[VrUiCursor] Click Raycast at screenPos={screenPos}: found {results.Count} results");
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            if (result.gameObject == _cursor) continue;
            
            var canvas = result.gameObject.GetComponentInParent<Canvas>();
            var cg = result.gameObject.GetComponentInParent<CanvasGroup>();
            string cgInfo = cg != null ? $", CanvasGroup(alpha={cg.alpha}, interactable={cg.interactable}, blocksRaycasts={cg.blocksRaycasts})" : "";
            string rectInfo = "";
            var rt = result.gameObject.GetComponent<RectTransform>();
            if (rt != null)
            {
                rectInfo = $", localPos={rt.localPosition}, size={rt.sizeDelta}";
            }
            Debug.Log($"[VrUiCursor]   Hit[{i}]: name='{result.gameObject.name}', path='{GetGameObjectPath(result.gameObject)}', canvas='{(canvas != null ? canvas.name : "None")}'{rectInfo}{cgInfo}");
        }
    }
    
    private static string GetGameObjectPath(GameObject go)
    {
        string path = go.name;
        Transform p = go.transform.parent;
        while (p != null)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }
        return path;
    }

    private bool RestrictUIModuleToVirtualMouse()
    {
        try
        {
            if (_virtualMouse == null) return false;
            var uiModule = EventSystem.current?.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (uiModule != null)
            {
                Debug.Log($"[NOVR] Restricting InputSystemUIInputModule actions to VirtualMouse (path: {_virtualMouse.path})");
                RestrictActionToVirtualMouse(uiModule.point?.action, _virtualMouse.path);
                RestrictActionToVirtualMouse(uiModule.leftClick?.action, _virtualMouse.path);
                RestrictActionToVirtualMouse(uiModule.middleClick?.action, _virtualMouse.path);
                RestrictActionToVirtualMouse(uiModule.rightClick?.action, _virtualMouse.path);
                RestrictActionToVirtualMouse(uiModule.scrollWheel?.action, _virtualMouse.path);
                RestrictNavigationToKeyboardAndGamepad(uiModule.move?.action);
                _boundUiModule = uiModule;
                return true;
            }
            else
            {
                Debug.LogWarning("[NOVR] InputSystemUIInputModule not found in scene yet, retrying next frame...");
                return false;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[NOVR] Exception while restricting UI actions to VirtualMouse: {ex}");
            return false;
        }
    }

    // Generic joystick / XR axes (HMD, controllers, HOTAS at rest) otherwise feed UI navigation like a held arrow key.
    private static void RestrictNavigationToKeyboardAndGamepad(InputAction? action)
    {
        if (action == null) return;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.isComposite) continue;
            var path = binding.path ?? string.Empty;
            if (path.StartsWith("<Keyboard>") || path.StartsWith("<Gamepad>")) continue;
            action.ApplyBindingOverride(i, string.Empty);
            Debug.Log($"[NOVR] Disabled UI navigation binding '{path}' on action '{action.name}'");
        }
    }

    private static void RestrictActionToVirtualMouse(InputAction? action, string devicePath)
    {
        if (action == null) return;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (binding.path.Contains("<Mouse>"))
            {
                var newPath = binding.path.Replace("<Mouse>", devicePath);
                action.ApplyBindingOverride(i, newPath);
                Debug.Log($"[NOVR] Overriding UI binding path: '{binding.path}' -> '{newPath}' for action '{action.name}'");
            }
        }
    }
}
