
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

namespace NOVR.VrTogglers;

public class XrPluginOpenXrToggler : XrPluginToggler
{
    protected override XRLoader CreateLoader()
    {
        // On the first install this assembly may be copied after the preloader has
        // enumerated patch targets. Configure stereo before loader initialization too.
        OpenXRSettings.Instance.renderMode = OpenXRSettings.RenderMode.MultiPass;
        OpenXRSettings.Instance.EnableNovrControllerProfiles();
        var xrLoader = ScriptableObject.CreateInstance<OpenXRLoader>();
        return xrLoader;
    }
}
