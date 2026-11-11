using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

public class NOVRGameplayUIBehaviour : UIRenderedCanvasBehavior
{
    // The game's menus hang three metres in front of the recentred head. The
    // aircraft walkaround can call them to wherever the player is standing;
    // until it does, nothing about their placement changes.
    private static bool _placed;
    private static Vector3 _origin;
    private static Quaternion _facing = Quaternion.identity;
    private bool _turned;

    internal static bool Placed => _placed;

    internal static void Place(Vector3 origin, Quaternion facing)
    {
        _placed = true;
        _origin = origin;
        _facing = facing;
    }

    internal static void ResetPlacement() => _placed = false;

    protected override void Update()
    {
        base.Update();
        transform.localScale = new Vector3(0.003f, 0.003f, 0.003f);
        if (_placed)
        {
            transform.position = _origin + _facing * new Vector3(0f, 0f, 3f);
            transform.rotation = _facing;
            _turned = true;
            return;
        }
        transform.position = new Vector3(0f, 0f, 3f);
        if (_turned)
        {
            transform.rotation = Quaternion.identity;
            _turned = false;
        }
    }
}
