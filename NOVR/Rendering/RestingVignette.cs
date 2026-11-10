using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NOVR.Rendering;

// The game's flight post-processing darkens the edges of the view at all
// times: its vignette rests at 0.4, and the G-force effect raises it from
// there to 1 as the pilot blacks out (GLOC.SimulateGLOC: Lerp(1, 0.4, t)).
// On a monitor that frames the picture. In a headset it darkens the outer
// part of each eye's view wherever the head points.
//
// This rescales whatever the game writes so the resting value becomes zero
// and full blackout stays full. It reads the value back each frame rather than
// patching the G-force code, so it also covers the value the profile ships
// with and any other writer. Night vision has its own profile and keeps its
// goggle edge.
internal sealed class RestingVignette
{
    internal const float NativeResting = .4f;

    internal static float Remap(float native) => Mathf.Clamp01((native - NativeResting) / (1f - NativeResting));

    private VolumeProfile? _profile;
    private Vignette? _found, _vignette;
    private float _native = float.NaN, _written = float.NaN;

    internal void Tick(bool wanted)
    {
        if (!wanted && _vignette is null)
            return;
        var vignette = wanted ? Find() : null;
        if (!ReferenceEquals(vignette, _vignette))
        {
            Restore();
            _vignette = vignette;
        }
        if (vignette == null)
            return;
        // The game writes its value statelessly (on every physics step in the
        // cockpit, and on camera switches). Anything other than what was
        // written here last is therefore a fresh native value.
        var value = vignette.intensity.value;
        if (value == _written)
            return;
        _native = value;
        _written = Remap(value);
        vignette.intensity.value = _written;
    }

    // The profile the volume is using right now. Asking Volume.profile for an
    // instance the game has not made yet would detach the volume from its
    // shared profile early, so only follow what already exists.
    private Vignette? Find()
    {
        var manager = SceneSingleton<CameraStateManager>.i;
        var volume = manager != null ? manager.GetPostProcessVolume() : null;
        var profile = volume == null ? null : volume.HasInstantiatedProfile() ? volume.profile : volume.sharedProfile;
        if (profile == null)
        {
            _profile = null;
            _found = null;
            return null;
        }
        if (!ReferenceEquals(profile, _profile) || _found == null)
        {
            _profile = profile;
            _found = profile.TryGet<Vignette>(out var found) ? found : null;
        }
        return _found != null ? _found : null;
    }

    private void Restore()
    {
        // Only undo a value that is still the one written here.
        if (_vignette != null && !float.IsNaN(_native) && _vignette.intensity.value == _written)
            _vignette.intensity.value = _native;
        _vignette = null;
        _native = _written = float.NaN;
    }
}
