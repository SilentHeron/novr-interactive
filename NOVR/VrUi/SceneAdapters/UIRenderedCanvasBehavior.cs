using System;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class UIRenderedCanvasBehavior : MonoBehaviour
{
    private bool _initialized;
    private Canvas? _canvas;

    protected virtual bool ShouldInitializeCanvas => true;

    public virtual void Awake() => Initialize();

    public virtual void OnEnable() => Initialize();

    protected virtual void Update() => RefreshCanvasCamera();

    private void Initialize()
    {
        if (!ShouldInitializeCanvas)
            return;
        if (!_initialized)
        {
            _initialized = true;
            ApplyVrUiLayerRecursive(transform);
            transform.localPosition = transform.localPosition with
            {
                z = 0
            };
        }
        _canvas = gameObject.GetComponent<Canvas>();
        if (_canvas == null)
            return;
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.planeDistance = 1f;
        RefreshCanvasCamera();
    }

    private void RefreshCanvasCamera()
    {
        if (_canvas == null)
            return;
        // The UI manager can arrive after Awake or recreate its camera during
        // a scene change. Rebind without replaying the canvas/layout setup.
        var camera = APIBus.CockpitHudCamera;
        if (!ReferenceEquals(_canvas.worldCamera, camera))
            _canvas.worldCamera = camera;
    }

    private static void ApplyVrUiLayerRecursive(Transform root)
    {
        LayerHelper.SetLayerRecursive(root, LayerHelper.GetVrUiLayer());
    }


    protected static Transform? FindChildStartingWith(Transform parent, string childNamePrefix)
    {
        for (var i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name.StartsWith(childNamePrefix, StringComparison.Ordinal))
            {
                return child;
            }

            var nestedChild = FindChildStartingWith(child, childNamePrefix);
            if (nestedChild != null)
            {
                return nestedChild;
            }
        }

        return null;
    }
}
