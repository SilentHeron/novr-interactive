using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using NOVR.VrCamera;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

#if CPP
using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Injection;
#endif

namespace NOVR;

[BepInPlugin(
    "deltawing.novr",
    "NOVR Interactive",
    "0.4.7.10")]
public class NOVRPlugin : BaseUnityPlugin
{
    
    private static NOVRPlugin _instance;
    public static string ModFolderPath { get; private set; }

    public NOVRPlugin()
    {
        
        InputTracking.trackingAcquired += TrackingAcquired;
        _instance = this;
        ModFolderPath = Path.GetDirectoryName(Assembly.GetAssembly(typeof(NOVRPlugin)).Location);
        
        new ModConfiguration(Config);
        Compatibility.ProtonControllerCompatibility.Initialize();
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        Core.Create();
    }

    private void TrackingAcquired(XRNodeState obj)
    {
        // Controller tracking can return while the pilot is reaching for a control.
        // Only headset tracking acquisition should recenter the cockpit.
        if (obj.nodeType is XRNode.Head or XRNode.CenterEye)
            NOVRHeadsetData.CalibrateTranslation();
    }
     
    private void Awake()
    {

    }
    
    
}
