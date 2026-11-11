using System.Reflection;
using HarmonyLib;
using NOVR.MotionControls;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.XR;

namespace NOVR.VrCamera;

// Walking around the aircraft at aircraft selection. The game's camera (the
// tracking origin) stays where it is; only the player's own offset and heading
// inside that tracking space change, so the aircraft and the menu stay put.
// The hand that is not holding the menu pointer walks: push its thumbstick
// forward for an arc and let go to teleport there, pull back and let go to
// call the menu to where you now stand. The left thumbstick snap-turns. A
// circle around the aircraft is as far as it goes, and leaving the selection
// screen puts the player, their heading and the menu back.
[DefaultExecutionOrder(-1100)]
internal sealed class AircraftWalkaround : MonoBehaviour
{
    private static readonly FieldInfo? Target = AccessTools.Field(typeof(CameraSelectionState), "target");
    private static readonly FieldInfo? FixedTarget = AccessTools.Field(typeof(CameraSelectionState), "fixedTarget");
    private readonly TeleportGesture _gesture = new();
    private readonly SnapTurnGesture _turn = new();
    // What the tracking space looked like before the first step, and what this
    // class last set. A recentre in between is the player's and is kept.
    private Vector3 _homeAnchor, _setAnchor;
    private Quaternion _homeRotation, _setRotation;
    private readonly Vector3[] _points = new Vector3[TeleportArc.Steps + 1];
    private LineRenderer? _arc, _ring;
    private Material? _valid, _blocked;
    private bool _walked, _hasSpot;
    private Vector3 _spot;
    // The selection camera clips everything within a metre, which is fine from
    // its stand-off and useless next to a wing.
    private const float WalkNearClip = .08f;
    private Camera? _nearCamera;
    private float _nearBefore;

    // True while the selection screen is up and walking around is possible.
    // The controller labels use it to call the teleport out.
    internal static bool Offered
    {
        get; private set;
    }

    private void Update()
    {
        var camera = APIBus.MainCamera;
        var root = camera != null ? camera.transform.parent : null;
        var manager = SceneSingleton<CameraStateManager>.i;
        Transform? target = null;
        if (manager != null && CameraStateManager.cameraMode == CameraMode.selection && manager.currentState == manager.selectionState &&
            FixedTarget?.GetValue(manager.selectionState) is false)
            target = Target?.GetValue(manager.selectionState) as Transform;
        Offered = ModConfiguration.Instance.SelectionTeleport.Value && XRSettings.enabled && TrackedControllers.Available &&
            (GameManager.gameState == GameState.SinglePlayer || GameManager.gameState == GameState.Multiplayer) &&
            root != null && target != null && Target != null && FixedTarget != null;
        if (!Offered)
        {
            Leave();
            return;
        }
        var busy = WristMenu.IsOpen || NativeHandOptionWheel.IsOpen || CockpitHudWidgets.Editing || DynamicMap.mapMaximized;
        // The hand that is not pointing at the menu walks.
        var left = VrUiCursor.I == null || !VrUiCursor.I.PointerHandLeft;
        var hand = TrackedControllers.Read(left);
        var change = _gesture.Update(!busy && hand.Valid && hand.HasAxis && hand.HasAimPose && !NativeHandOptionWheel.OwnsHand(left),
            hand.Axis.x, hand.Axis.y);
        if (change == TeleportEvent.Summon)
        {
            BringMenu();
            InteractionHaptics.PulseOnly(hand.Device, .2f, .03f);
        }
        else if (change == TeleportEvent.Teleport && _hasSpot)
        {
            MoveTo(_spot, camera!, root!);
            InteractionHaptics.PulseOnly(hand.Device, .3f, .04f);
        }
        // Snap turn is always the left thumbstick. When that is also the
        // walking hand, a turn waits until it is neither aiming nor calling the menu.
        var turnHand = left ? hand : TrackedControllers.Read(true);
        var turned = _turn.Update(!busy && turnHand.Valid && turnHand.HasAxis && !NativeHandOptionWheel.OwnsHand(true) && !(left && !_gesture.Idle),
            turnHand.Axis.x, turnHand.Axis.y);
        if (turned != 0)
        {
            Begin();
            NOVRHeadsetData.TurnAbout(NOVRHeadsetData.Translation, turned * ModConfiguration.Instance.SnapTurnDegrees.Value);
            Remember();
            InteractionHaptics.PulseOnly(turnHand.Device, .15f, .02f);
        }
        if (!_gesture.Aiming)
        {
            Show(false);
            _hasSpot = false;
            return;
        }
        _hasSpot = Aim(root!.TransformPoint(hand.AimPosition), root.rotation * hand.AimRotation * Vector3.forward, target!.position, root.position, out _spot, out var count);
        Draw(count, _hasSpot);
    }

    // Follows the arc until it lands on something walkable inside the boundary.
    private bool Aim(Vector3 origin, Vector3 direction, Vector3 centre, Vector3 camera, out Vector3 spot, out int count)
    {
        spot = default;
        _points[0] = origin;
        count = 1;
        for (var step = 1; step <= TeleportArc.Steps; step++)
        {
            TeleportArc.Point(origin.x, origin.y, origin.z, direction.x, direction.y, direction.z, step, out var x, out var y, out var z);
            var next = new Vector3(x, y, z);
            if (Physics.Linecast(_points[count - 1], next, out var hit, ~0, QueryTriggerInteraction.Ignore))
            {
                _points[count++] = hit.point;
                // A wall or the side of the aircraft is not somewhere to stand.
                if (hit.normal.y < .6f)
                    return false;
                spot = hit.point;
                var radius = TeleportArc.BoundaryRadius(Vector2.Distance(new Vector2(camera.x, camera.z), new Vector2(centre.x, centre.z)));
                if (!TeleportArc.ClampToBoundary(ref spot.x, ref spot.z, centre.x, centre.z, radius))
                    return true;
                // Pulled back to the boundary: find the ground under that point.
                if (!Physics.Raycast(new Vector3(spot.x, spot.y + 30f, spot.z), Vector3.down, out var ground, 90f, ~0, QueryTriggerInteraction.Ignore) ||
                    ground.normal.y < .6f)
                    return false;
                spot = ground.point;
                _points[count - 1] = spot;
                return true;
            }
            _points[count++] = next;
        }
        return false;
    }

    private void MoveTo(Vector3 spot, Camera camera, Transform root)
    {
        // Where the head would be if the player stood exactly on the
        // recentred spot, and how far they are leaning from it right now.
        var anchor = NOVRHeadsetData.TranslationAnchor;
        var origin = root.TransformPoint(anchor + CockpitProfiles.SeatOffset);
        var lean = camera.transform.position - origin;
        var wanted = new Vector3(spot.x - lean.x, spot.y + TeleportArc.EyeHeight, spot.z - lean.z);
        Begin();
        NOVRHeadsetData.SetAnchor(anchor + root.InverseTransformVector(wanted - origin));
        Remember();
        if (_nearCamera == null && camera.nearClipPlane > WalkNearClip)
        {
            _nearCamera = camera;
            _nearBefore = camera.nearClipPlane;
            camera.nearClipPlane = WalkNearClip;
        }
    }

    // The first step or turn notes where the player started.
    private void Begin()
    {
        if (_walked)
            return;
        _walked = true;
        _homeAnchor = NOVRHeadsetData.TranslationAnchor;
        _homeRotation = NOVRHeadsetData.RotationCalibrationOffset;
    }

    private void Remember()
    {
        _setAnchor = NOVRHeadsetData.TranslationAnchor;
        _setRotation = NOVRHeadsetData.RotationCalibrationOffset;
    }

    // Calls the menu to the player: three metres ahead of where they are
    // looking, upright and level with their eyes. It stays there until called
    // again, so it is never in the way while walking.
    private static void BringMenu()
    {
        var eyes = VrUiCursor.I != null ? VrUiCursor.I.UiCamera : null;
        if (eyes == null)
            return;
        var ahead = eyes.transform.forward;
        ahead.y = 0f;
        // Looking straight up or down: use the top of the head instead.
        if (ahead.sqrMagnitude < .01f)
        {
            ahead = eyes.transform.up;
            ahead.y = 0f;
        }
        if (ahead.sqrMagnitude < .0001f)
            return;
        NOVRGameplayUIBehaviour.Place(eyes.transform.position, Quaternion.LookRotation(ahead.normalized, Vector3.up));
    }

    // Puts back the player's position, heading and the menu. A value the
    // player changed in the meantime (a recentre) is theirs and is left alone.
    private void GoHome()
    {
        if (_walked)
        {
            if (NOVRHeadsetData.TranslationAnchor == _setAnchor)
                NOVRHeadsetData.SetAnchor(_homeAnchor);
            if (NOVRHeadsetData.RotationCalibrationOffset == _setRotation)
                NOVRHeadsetData.RestoreRotation(_homeRotation);
        }
        // Hand the near plane back only if it is still the value set here.
        if (_nearCamera != null && Mathf.Approximately(_nearCamera.nearClipPlane, WalkNearClip))
            _nearCamera.nearClipPlane = _nearBefore;
        _nearCamera = null;
        _walked = false;
        if (NOVRGameplayUIBehaviour.Placed)
            NOVRGameplayUIBehaviour.ResetPlacement();
    }

    private void Leave()
    {
        GoHome();
        _gesture.Reset();
        _turn.Reset();
        _hasSpot = false;
        Show(false);
    }

    private void Draw(int count, bool valid)
    {
        if (_arc == null)
        {
            _valid = CockpitMaterials.Opaque("NOVR teleport", new Color(.25f, .85f, 1f, 1f));
            _blocked = CockpitMaterials.Opaque("NOVR teleport blocked", new Color(1f, .3f, .25f, 1f));
            _arc = Line("NOVR Teleport Arc", .014f);
            _ring = Line("NOVR Teleport Spot", .02f);
            _ring.positionCount = 33;
        }
        _arc.sharedMaterial = valid ? _valid : _blocked;
        _arc.positionCount = count;
        for (var i = 0; i < count; i++)
            _arc.SetPosition(i, _points[i]);
        _arc.enabled = true;
        _ring!.enabled = valid;
        if (!valid)
            return;
        _ring.sharedMaterial = _valid;
        for (var i = 0; i < 33; i++)
        {
            var angle = i * Mathf.PI * 2f / 32f;
            _ring.SetPosition(i, _spot + new Vector3(Mathf.Cos(angle) * .35f, .03f, Mathf.Sin(angle) * .35f));
        }
    }

    private LineRenderer Line(string name, float width)
    {
        var line = new GameObject(name).AddComponent<LineRenderer>();
        DontDestroyOnLoad(line.gameObject);
        line.useWorldSpace = true;
        line.loop = false;
        line.startWidth = line.endWidth = width;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        return line;
    }

    private void Show(bool visible)
    {
        if (_arc != null)
            _arc.enabled = visible;
        if (_ring != null)
            _ring.enabled = visible;
    }

    private void OnDisable() => Leave();

    private void OnDestroy()
    {
        Leave();
        Offered = false;
        if (_arc != null)
            Destroy(_arc.gameObject);
        if (_ring != null)
            Destroy(_ring.gameObject);
        if (_valid != null)
            Destroy(_valid);
        if (_blocked != null)
            Destroy(_blocked);
    }
}
