using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;

namespace NOVR.VrUi.SpecialBehavior;

// Native GameplayUI owns damage intensity, expiry and active state. Only its
// fullscreen hurt image is mirrored; menus and blackout/GLOC remain untouched.
[DefaultExecutionOrder(12000)]
internal sealed class VrDamageOverlay : MonoBehaviour
{
    private Image? _source, _copy;
    private Canvas? _canvas;
    private HudGraphicVisibilityLease? _hide;
    private RectTransform? _rect;
    private Material? _sourceMaterial, _material;

    private void OnEnable() => RenderPipelineManager.beginCameraRendering += BeforeCamera;
    private void LateUpdate() => Refresh();

    private void BeforeCamera(ScriptableRenderContext context, Camera camera)
    {
        // URP has populated the active eye's base-camera matrices by this point.
        // Refresh again here so head motion after LateUpdate cannot expose an edge.
        if (camera == APIBus.CockpitHudCamera)
            Refresh();
    }

    private void Refresh()
    {
        var gameplay = SceneSingleton<GameplayUI>.i;
        var source = gameplay != null ? gameplay.hurt : null;
        if (_source != source)
        {
            Release();
            _source = source;
        }
        if (_source == null || !XRSettings.enabled || !MotionControls.TrackedControllers.Available ||
            NOUIManager.I == null || APIBus.MainCamera == null)
        {
            Hide();
            return;
        }
        var main = APIBus.MainCamera;
        var camera = APIBus.CockpitHudCamera;
        var nativeCanvas = _source.canvas;
        if (camera == null || !camera.isActiveAndEnabled || !main.isActiveAndEnabled || !main.stereoEnabled ||
            !_source.isActiveAndEnabled || _source.color.a <= 0 || nativeCanvas != null && !nativeCanvas.isActiveAndEnabled)
        {
            Hide();
            return;
        }
        // URP copies base-camera XR matrices onto overlay cameras. Derive the
        // head-local envelope from the actual game camera, then place it in the
        // HUD camera's separate tracking space; their world origins differ.
        var depth = Mathf.Max(1f, camera.nearClipPlane + .02f);
        if (!StereoOverlayGeometry.TryFit(main, depth, out var center, out var size))
        {
            Hide();
            return;
        }
        EnsureCopy(camera);
        if (_rect!.parent != camera.transform)
            _rect.SetParent(camera.transform, false);
        _rect.localPosition = new Vector3(center.x, center.y, depth);
        _rect.localRotation = Quaternion.identity;
        _rect.localScale = Vector3.one;
        _rect.sizeDelta = size;
        _canvas!.worldCamera = camera;
        // Preserve the native artwork, material and exact color/alpha. Use its
        // effective sprite (including any game/mod override), never a new tint.
        _copy!.sprite = _source.overrideSprite;
        RefreshMaterial();
        _copy.color = _source.color;
        _copy.type = _source.type;
        _copy.fillCenter = _source.fillCenter;
        _copy.fillMethod = _source.fillMethod;
        _copy.fillAmount = _source.fillAmount;
        _copy.fillClockwise = _source.fillClockwise;
        _copy.fillOrigin = _source.fillOrigin;
        _rect.gameObject.SetActive(true);
        _hide!.Apply();
    }

    private void RefreshMaterial()
    {
        var source = _source!.material;
        if (_sourceMaterial != source || _material == null || _material.shader != source.shader)
        {
            if (_material != null)
                Destroy(_material);
            _sourceMaterial = source;
            _material = new Material(source) { name = "NOVR full-view damage material" };
        }
        // Preserve the native UI shader, blend, textures and any live properties.
        // Request foreground depth only on our copy; changing the global UI depth
        // mode would also make unrelated cockpit widgets draw through the hands.
        _material.CopyPropertiesFromMaterial(source);
        _material.SetInt("unity_GUIZTestMode", (int)CompareFunction.Always);
        _copy!.material = _material;
    }

    private void EnsureCopy(Camera camera)
    {
        if (_copy != null)
            return;
        // A replaced/destroyed UI camera also destroys its child canvas.
        // Release the old source lease before creating its replacement.
        _hide?.Release();
        var go = new GameObject("NOVR full-view damage", typeof(RectTransform), typeof(Canvas), typeof(CanvasRenderer), typeof(Image));
        _rect = (RectTransform)go.transform;
        _rect.SetParent(camera.transform, false);
        _rect.pivot = new Vector2(.5f, .5f);
        _canvas = go.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = camera;
        _canvas.overrideSorting = true;
        _canvas.sortingOrder = 32700;
        _copy = go.GetComponent<Image>();
        _copy.raycastTarget = false;
        _copy.maskable = false;
        _copy.preserveAspect = false;
        LayerHelper.SetLayerRecursive(go.transform, LayerHelper.GetVrUiLayer());
        _hide = new HudGraphicVisibilityLease(_source!, true);
    }

    private void Hide()
    {
        if (_rect != null)
            _rect.gameObject.SetActive(false);
        _hide?.Restore();
    }

    private void Release()
    {
        _hide?.Release();
        _hide = null;
        if (_rect != null)
        {
            _rect.gameObject.SetActive(false);
            Destroy(_rect.gameObject);
        }
        _rect = null;
        _copy = null;
        _canvas = null;
        if (_material != null)
            Destroy(_material);
        _material = null;
        _sourceMaterial = null;
        _source = null;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= BeforeCamera;
        Release();
    }
    private void OnDestroy() => OnDisable();
}
