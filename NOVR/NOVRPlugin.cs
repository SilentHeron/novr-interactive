using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using NOVR.VrCamera;
using UnityEngine.XR;

namespace NOVR;

[BepInPlugin(
    "deltawing.novr",
    "NOVR Interactive",
    "0.4.8.10")]
public class NOVRPlugin : BaseUnityPlugin
{
    public static string ModFolderPath { get; private set; } = string.Empty;

    public NOVRPlugin()
    {
        ModFolderPath = Path.GetDirectoryName(typeof(NOVRPlugin).Assembly.Location) ?? string.Empty;
        new ModConfiguration(Config);
        Compatibility.ProtonControllerCompatibility.Initialize();
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Core.Create();
        InputTracking.trackingAcquired += TrackingAcquired;
    }

    private void TrackingAcquired(XRNodeState obj)
    {
        // Controller tracking can return while the pilot is reaching for a control.
        // Only headset tracking acquisition should recenter the cockpit.
        if (obj.nodeType is XRNode.Head or XRNode.CenterEye)
            NOVRHeadsetData.CalibrateTranslation();
    }
    private void OnDestroy() => InputTracking.trackingAcquired -= TrackingAcquired;
}
