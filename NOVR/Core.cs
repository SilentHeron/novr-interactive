using System;
using NOVR.VrCamera;
using NOVR.VrTogglers;
using NOVR.VrUi;
using UnityEngine;
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
    
    private Aircraft _aircraft;
    private Aircraft _oldAircraft;

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
        gameObject.AddComponent<VrUi.SpecialBehavior.CockpitHudWidgets>();
        gameObject.AddComponent<VrUi.SpecialBehavior.CockpitStatusSources>();
        gameObject.AddComponent<VrUi.SpecialBehavior.CockpitMapWidget>();
        gameObject.AddComponent<VrUi.SpecialBehavior.CockpitMapInteraction>();
        gameObject.AddComponent<MotionControls.CockpitLayout>();
        gameObject.AddComponent<MotionControls.MotionFlightController>();
        gameObject.AddComponent<MotionControls.HelmetNightVision>();
        gameObject.AddComponent<MotionControls.PilotHandVisuals>();
        gameObject.AddComponent<MotionControls.InteractionAudio>();
        gameObject.AddComponent<MotionControls.ControllerActions>();
        gameObject.AddComponent<MotionControls.NativeHandOptionWheel>();
        gameObject.AddComponent<MotionControls.TargetAcquisitionCue>();
        gameObject.AddComponent<MotionControls.PcVrCapabilities>();
        gameObject.AddComponent<VrUi.WristMenu>();
    }

    private void OnApplicationQuit() => _quitting = true;

    private void OnDestroy()
    {
        if (_quitting) return;
        Debug.Log("NOVR has been destroyed. This shouldn't have happened. Recreating...");
        
        Create();
    }

    private void Start()
    {
        
        
        _headsetData = NOVRBehaviour.Create<NOVRHeadsetData>(transform);
        _vrUi = NOVRBehaviour.Create<NOUIManager>(transform);
        
        _vrTogglerManager = new VrTogglerManager();
        
    }



    private void FixedUpdate()
    {
        _oldAircraft = _aircraft;
        GameManager.GetLocalAircraft(out _aircraft);
        if (_aircraft != _oldAircraft) NOVRHeadsetData.CalibrateTranslation();
        CameraStateManager.enableMouseLook = false;
    }

}
