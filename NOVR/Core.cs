using System;
using NOVR.VrCamera;
using NOVR.VrTogglers;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace NOVR;

public class Core : MonoBehaviour
{

    private bool _quitting;

    private NOVRHeadsetData? _headsetData;
    private NOUIManager? _vrUi;
    private VrTogglerManager? _vrTogglerManager;
    private readonly VrStartRetry _vrRetry = new();
    private VrStartNotice? _vrNotice;

    private Aircraft? _aircraft;

    public static void Create()
    {
        new GameObject("NOVR").AddComponent<Core>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<VrCameraManager>();
        gameObject.AddComponent<APIBus>();
        gameObject.AddComponent<MotionControls.CockpitProfiles>();
        gameObject.AddComponent<VrUi.SpecialBehavior.HudElementStyleDriver>();
        gameObject.AddComponent<VrUi.SpecialBehavior.CockpitHudWidgets>();
        gameObject.AddComponent<VrUi.SpecialBehavior.CockpitStatusSources>();
        gameObject.AddComponent<VrUi.SpecialBehavior.VrDamageOverlay>();
        gameObject.AddComponent<VrUi.SpecialBehavior.CockpitMapWidget>();
        gameObject.AddComponent<VrUi.SpecialBehavior.CockpitMapInteraction>();
        gameObject.AddComponent<MotionControls.CockpitLayout>();
        gameObject.AddComponent<MotionControls.MotionFlightController>();
        gameObject.AddComponent<MotionControls.HelmetNightVision>();
        gameObject.AddComponent<MotionControls.PilotHandVisuals>();
        gameObject.AddComponent<MotionControls.PilotHandUiDepth>();
        gameObject.AddComponent<Comfort.SeatClearance>();
        gameObject.AddComponent<VrUi.SpecialBehavior.CockpitWidgetDepthMaterials>();
        gameObject.AddComponent<MotionControls.InteractionAudio>();
        gameObject.AddComponent<MotionControls.ControllerActions>();
        gameObject.AddComponent<MotionControls.NativeHandOptionWheel>();
        gameObject.AddComponent<MotionControls.TargetAcquisitionCue>();
        gameObject.AddComponent<MotionControls.PcVrCapabilities>();
        gameObject.AddComponent<Rendering.FixedFoveationRuntime>();
        gameObject.AddComponent<Rendering.VrImageQuality>();
        gameObject.AddComponent<VrUi.WristMenu>();
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
        NOVRPlugin.ConfigSaves?.Flush(always: true);
    }

    private void OnDestroy()
    {
        if (_quitting)
            return;
        NOVRPlugin.ConfigSaves?.Flush();
        Debug.Log("NOVR has been destroyed. This shouldn't have happened. Recreating...");

        Create();
    }

    private void Start()
    {
        _headsetData = NOVRBehaviour.Create<NOVRHeadsetData>(transform);
        _vrUi = NOVRBehaviour.Create<NOUIManager>(transform);
        _vrNotice = gameObject.AddComponent<VrStartNotice>();
        _vrTogglerManager = new VrTogglerManager();
        ReportVrStart();
    }

    private void Update()
    {
        NOVRPlugin.ConfigSaves?.Tick(Time.unscaledTime);
        if (_vrTogglerManager == null || _vrTogglerManager.IsVrEnabled)
            return;
        var now = Time.unscaledTime;
        // A headset that was still waking up gets a few automatic attempts;
        // after that the key leaves the timing to the player.
        if (_vrRetry.Due(now) || Keyboard.current?.f11Key.wasPressedThisFrame == true)
        {
            _vrTogglerManager.TryEnable();
            ReportVrStart();
        }
        else if (_vrNotice != null)
            _vrNotice.SecondsUntilRetry = _vrRetry.SecondsUntilRetry(now);
    }

    private void ReportVrStart()
    {
        if (_vrTogglerManager == null)
            return;
        if (_vrTogglerManager.IsVrEnabled)
        {
            if (_vrNotice != null && _vrNotice.enabled)
                Debug.Log("[NOVR] VR started.");
            _vrRetry.Succeeded();
            if (_vrNotice != null)
                _vrNotice.Hide();
            return;
        }
        var now = Time.unscaledTime;
        _vrRetry.Failed(now);
        var reason = _vrTogglerManager.LastFailure ?? "The VR runtime did not start.";
        Debug.LogWarning("[NOVR] VR did not start: " + reason + (_vrRetry.WaitingToRetry
            ? " Trying again in " + _vrRetry.SecondsUntilRetry(now).ToString("0") + " s."
            : " Press F11 to try again, or start the game with -novr-flat to play without VR."));
        if (_vrNotice != null)
            _vrNotice.Show(reason, _vrRetry.SecondsUntilRetry(now));
    }

    private void FixedUpdate()
    {
        var previousAircraft = _aircraft;
        GameManager.GetLocalAircraft(out _aircraft);
        if (_aircraft != previousAircraft)
            NOVRHeadsetData.CalibrateTranslation();
    }

}
