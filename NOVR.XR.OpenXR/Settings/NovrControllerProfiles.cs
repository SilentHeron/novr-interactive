using System.Collections.Generic;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace UnityEngine.XR.OpenXR;

public partial class OpenXRSettings
{
    // NOVR creates its loader at runtime, without Unity's serialized XR settings asset.
    // Register controller profiles before loader initialization, or only the HMD is exposed.
    public void EnableNovrControllerProfiles()
    {
        var profiles = new List<OpenXRFeature>(features);
        AddProfile<OculusTouchControllerProfile>(profiles, OculusTouchControllerProfile.featureId);
        AddProfile<ValveIndexControllerProfile>(profiles, ValveIndexControllerProfile.featureId);
        AddProfile<HTCViveControllerProfile>(profiles, HTCViveControllerProfile.featureId);
        AddProfile<MicrosoftMotionControllerProfile>(profiles, MicrosoftMotionControllerProfile.featureId);
        AddProfile<Valve.OpenXR.Utils.SteamFrameControllerProfile>(profiles, Valve.OpenXR.Utils.SteamFrameControllerProfile.featureId);
        var frame = profiles.Find(item => item is Valve.OpenXR.Utils.SteamFrameControllerProfile);
        frame.company = "Valve";
        frame.openxrExtensionStrings = "XR_VALVE_frame_controller_interaction";
        // Request the standard gaze action before session creation. Runtime
        // permissions still govern availability; no raw gaze data is logged.
        AddProfile<EyeGazeInteraction>(profiles, EyeGazeInteraction.featureId);
        profiles.Find(item => item is EyeGazeInteraction)!.openxrExtensionStrings = EyeGazeInteraction.extensionString;
        features = profiles.ToArray();
    }

    private static void AddProfile<T>(List<OpenXRFeature> profiles, string id) where T : OpenXRInteractionFeature
    {
        var feature = profiles.Find(item => item is T);
        if (feature == null)
        {
            feature = CreateInstance<T>();
            feature.nameUi = typeof(T).Name;
            feature.featureIdInternal = id;
            feature.company = "Unity";
            profiles.Add(feature);
        }
        feature.enabled = true;
    }
}
