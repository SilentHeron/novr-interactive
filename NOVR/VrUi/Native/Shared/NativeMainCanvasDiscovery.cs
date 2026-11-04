using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOVR.VrUi.Native;

// The native menu can be inactive during flight or created after a scene loads.
// Keep that live canvas without repeatedly enumerating every loaded GameObject.
internal sealed class NativeMainCanvasDiscovery
{
    private const float InitialRetrySeconds = .5f;
    private const float MaximumRetrySeconds = 2f;
    private Canvas? _canvas;
    private bool _enabled, _dirty = true;
    private float _nextSearchTime, _retrySeconds = InitialRetrySeconds;

    internal void Enable()
    {
        if (_enabled) return;
        _enabled = true;
        SceneManager.sceneLoaded += SceneLoaded;
        SceneManager.sceneUnloaded += SceneUnloaded;
        SceneManager.activeSceneChanged += ActiveSceneChanged;
        Invalidate();
    }

    internal void Disable()
    {
        if (!_enabled) return;
        _enabled = false;
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneUnloaded -= SceneUnloaded;
        SceneManager.activeSceneChanged -= ActiveSceneChanged;
        _canvas = null;
        Invalidate();
    }

    internal void Invalidate()
    {
        _dirty = true;
        _retrySeconds = InitialRetrySeconds;
    }

    internal GameObject? Find(float now)
    {
        if (!_enabled) return null;
        if (!_dirty && IsLoaded(_canvas)) return _canvas!.gameObject;
        // A cached object becoming Unity-null/unloaded is a lifecycle change,
        // not another failed search: recover immediately, even during backoff.
        if (!_dirty && !ReferenceEquals(_canvas, null)) Invalidate();
        if (!_dirty && now < _nextSearchTime) return null;

        _dirty = false;
        _canvas = null;
        var activeScene = SceneManager.GetActiveScene();
        var bestScore = -1;
        foreach (var canvas in Resources.FindObjectsOfTypeAll<Canvas>())
        {
            if (!IsLoaded(canvas)) continue;
            var candidate = canvas.gameObject;
            if (candidate.name != "MainCanvas") continue;
            // A newly active scene may overlap the previous scene during an
            // additive transition. Its menu wins even while still inactive.
            var score = (candidate.scene == activeScene ? 2 : 0) + (candidate.activeInHierarchy ? 1 : 0);
            if (score <= bestScore) continue;
            _canvas = canvas;
            bestScore = score;
        }

        _nextSearchTime = now + _retrySeconds;
        _retrySeconds = _canvas != null ? InitialRetrySeconds : Mathf.Min(MaximumRetrySeconds, _retrySeconds * 2f);
        return _canvas != null ? _canvas.gameObject : null;
    }

    private static bool IsLoaded(Canvas? canvas) => canvas != null && canvas.gameObject != null &&
        canvas.gameObject.scene.IsValid() && canvas.gameObject.scene.isLoaded;

    private void SceneLoaded(Scene scene, LoadSceneMode mode) => Invalidate();
    private void SceneUnloaded(Scene scene) => Invalidate();
    private void ActiveSceneChanged(Scene before, Scene after) => Invalidate();
}
