#if MODERN
using System;
using UnityEngine;
using UnityEngine.XR.Management;

namespace NOVR.VrTogglers;

public abstract class XrPluginToggler: VrToggler
{
    protected XRGeneralSettings _generalSettings = null!;
    protected XRManagerSettings _managerSetings = null!;
    private bool _previousRunInBackground;
    private bool _ownsBackgroundSetting;
    
    protected override bool SetUp()
    {
        _generalSettings = ScriptableObject.CreateInstance<XRGeneralSettings>();
        _managerSetings = ScriptableObject.CreateInstance<XRManagerSettings>();
        _generalSettings.Manager = _managerSetings;
        
#pragma warning disable CS0618
        /*
         * ManagerSettings.loaders is deprecated but very useful, allows me to add the xr loader without reflection.
         * Should be fine unless the game's Unity version gets majorly updated, in which case the whole mod will be
         * broken, so I'll have to update it anyway.
         */
        _managerSetings.loaders.Add(CreateLoader());
#pragma warning restore CS0618
        
        return true;
    }

    protected override bool EnableVr()
    {
        // Setup configures the loader list once. DeinitializeLoader clears the
        // active loader, so every later enable must initialize it again.
        if (_managerSetings.activeLoader == null)
            _managerSetings.InitializeLoaderSync();
        if (_managerSetings.activeLoader == null)
            throw new Exception("Cannot initialize OpenXR Loader. Maybe The VR headset wasn't ready?");

        _previousRunInBackground = Application.runInBackground;
        _ownsBackgroundSetting = true;
        Application.runInBackground = true;
        try
        {
            _managerSetings.StartSubsystems();
            if (_managerSetings.activeLoader != null) return true;
            RestoreBackgroundSetting();
            return false;
        }
        catch
        {
            try { _managerSetings.DeinitializeLoader(); }
            finally { RestoreBackgroundSetting(); }
            throw;
        }
    }

    protected override bool DisableVr()
    {
        try
        {
            // The manager stops subsystems as part of deinitialization.
            if (_managerSetings.activeLoader != null) _managerSetings.DeinitializeLoader();
            return _managerSetings.activeLoader == null;
        }
        finally { RestoreBackgroundSetting(); }
    }

    private void RestoreBackgroundSetting()
    {
        if (!_ownsBackgroundSetting) return;
        Application.runInBackground = _previousRunInBackground;
        _ownsBackgroundSetting = false;
    }

    protected abstract XRLoader CreateLoader();
}
#endif
