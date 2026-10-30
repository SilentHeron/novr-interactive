using NOVR.MotionControls;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

[DefaultExecutionOrder(150)]
public class NOVRStatusDisplayBehavior : UIRenderedCanvasBehavior
{
    private Vector3 _originalScale;
    private Quaternion _originalRotation;
    private bool _docked;
    private void LateUpdate()
    {
        var cockpit = Fs20NativeCockpit.Cockpit;
        var game = APIBus.MainCamera?.transform.parent;
        var ui = APIBus.CockpitHudCamera?.transform.parent;
        if (cockpit != null && game != null && ui != null)
        {
            if (!_docked) { _originalScale = transform.localScale; _originalRotation = transform.rotation; _docked = true; }
            transform.SetPositionAndRotation(ui.TransformPoint(game.InverseTransformPoint(cockpit.TransformPoint(new Vector3(.39f,.40f,.43f)))),
                ui.rotation * Quaternion.Inverse(game.rotation) * cockpit.rotation * Quaternion.Euler(12,-12,0));
            transform.localScale = _originalScale * .00045f;
        }
        else
        {
            Restore(); transform.position = new Vector3(630f,145f,1000f);
        }
    }
    private void Restore() { if (!_docked) return; transform.localScale = _originalScale; transform.rotation = _originalRotation; _docked = false; }
    private void OnDisable() => Restore();
}
