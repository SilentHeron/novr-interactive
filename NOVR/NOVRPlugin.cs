using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using NOVR.Compatibility;
using NOVR.VrCamera;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR;

[BepInPlugin(
    "deltawing.novr",
    "NOVR Interactive",
    "0.4.8.34")]
public class NOVRPlugin : BaseUnityPlugin
{
    public static string ModFolderPath { get; private set; } = string.Empty;
    internal static DeferredConfigSave? ConfigSaves { get; private set; }

    public NOVRPlugin()
    {
        ModFolderPath = Path.GetDirectoryName(typeof(NOVRPlugin).Assembly.Location) ?? string.Empty;
        if (!Compatibility.D3D11Startup.Prepare())
            return;
        StartupDiagnostics.Log = Debug.LogWarning;
        // By default BepInEx rewrites the whole file on every Bind and every
        // changed value. Bind everything first, write once, then save shortly
        // after changes instead (see DeferredConfigSave).
        Config.SaveOnConfigSet = false;
        new ModConfiguration(Config);
        var saves = new DeferredConfigSave(Config.Save, Debug.LogWarning);
        saves.Changed(0);
        saves.Flush();
        Config.SettingChanged += (_, _) => saves.Changed(Time.unscaledTime);
        ConfigSaves = saves;
        StartupDiagnostics.CheckGame(Application.version);
        StartupDiagnostics.CheckLayers(LayerHelper.OwnLayers, LayerMask.LayerToName);
        Compatibility.ProtonControllerCompatibility.Initialize();
        InstallPatches();
        Core.Create();
        InputTracking.trackingAcquired += TrackingAcquired;
    }

    // One patch class at a time: a target or private member that a game update
    // renamed disables that adjustment and is reported, instead of leaving the
    // game half patched and the rest of the mod unstarted.
    private static void InstallPatches()
    {
        var harmony = new Harmony("deltawing.novr");
        var report = PatchInstaller.Apply(AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()),
            type => type.GetCustomAttributes(typeof(HarmonyAttribute), true).Length > 0,
            GameHandles.FirstUnresolved,
            type => harmony.CreateClassProcessor(type).Patch());
        StartupDiagnostics.ReportPatches(report);
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
