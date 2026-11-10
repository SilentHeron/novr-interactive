using UnityEngine;
using UnityEngine.XR;

namespace NOVR.Rendering;

// Keeps the image options that are not tied to one object in step with VR and
// their settings: the resting vignette every frame (after the game's physics
// step may have rewritten it) and the cockpit screen textures once a second.
internal sealed class VrImageQuality : MonoBehaviour
{
    private readonly RestingVignette _vignette = new();
    private float _nextSync;

    private void LateUpdate()
    {
        var settings = ModConfiguration.Instance.ImageQuality;
        var vr = XRSettings.enabled;
        _vignette.Tick(vr && settings.RemoveRestingVignette.Value);
        if (Time.unscaledTime < _nextSync)
            return;
        _nextSync = Time.unscaledTime + 1f;
        CockpitScreenFilter.Sync(vr && settings.FilterCockpitScreens.Value);
    }

    private void OnDisable()
    {
        _vignette.Tick(false);
        CockpitScreenFilter.Sync(false);
        _nextSync = 0;
    }
}
