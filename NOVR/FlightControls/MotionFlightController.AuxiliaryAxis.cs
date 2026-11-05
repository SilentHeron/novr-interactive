using System.Reflection;
using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

namespace NOVR.MotionControls;

public sealed partial class MotionFlightController
{
    private static readonly FieldInfo? TiltWingAutoTilt = AccessTools.Field(typeof(TiltWingController), "autoTilt");
    private TiltWingController? _tiltWing;
    private bool _auxiliaryUsesNozzles;
    private bool _auxiliaryUsesWingSweep;

    private void ConfigureAuxiliaryControls(Aircraft aircraft)
    {
        _tiltWing = aircraft.GetComponentInChildren<TiltWingController>(true);
        _auxiliaryUsesNozzles = _tiltWing != null || aircraft.GetComponentInChildren<DuctedThrustSystem>(true) != null ||
            aircraft.GetComponentInChildren<SwivelDuctSystem>(true) != null;
        _auxiliaryUsesWingSweep = aircraft.GetComponentInChildren<SwingWingController>(true) != null;
    }

    internal float AuxiliaryAxis
    {
        get
        {
            if (!FlightActive || !ThrottleHeld || !_throttleHand.Valid || !_throttleHand.HasAxis ||
                CockpitMapInteraction.ReservesThumbstick(!Settings.LeftHanded.Value) ||
                _auxiliaryUsesNozzles && _aircraft?.IsAutoHoverEnabled() == true ||
                !FlightControlMath.IsFinite(_throttleHand.Axis.y) || Mathf.Abs(_throttleHand.Axis.y) < .35f)
                return 0;

            // Custom Axis 1 also drives wing sweep, pusher fans and modded
            // systems. Native input integrates these +/-1 commands and retains
            // the selected position on release; no aircraft whitelist is needed.
            // Keep nozzle hover protection and the existing tiltwing takeover.
            if (_tiltWing != null)
                TiltWingAutoTilt?.SetValue(_tiltWing, false);
            // Forward extends swept wings; backward sweeps them aft. Tiltwing,
            // nozzle and pusher-fan controls keep their existing direction.
            return Mathf.Sign(_throttleHand.Axis.y) * (_auxiliaryUsesWingSweep ? -1f : 1f);
        }
    }
}
