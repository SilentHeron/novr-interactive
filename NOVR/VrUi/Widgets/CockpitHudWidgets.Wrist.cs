using NOVR.MotionControls;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

internal sealed partial class CockpitHudWidgets
{
    // Cockpit coordinates are never overwritten by a wrist attachment. An edit
    // uses the active anchor's local space; swapping back restores the cockpit.
    private HudWidgetPose ActivePose(string id)
    {
        var pose = _poses[id];
        if (pose.Anchor != HudWidgetAnchor.Cockpit)
        {
            pose.Position = pose.WristPosition;
            pose.Euler = pose.WristEuler;
            pose.Scale = pose.WristScale;
        }
        return pose;
    }
    private void StoreActivePose(string id, HudWidgetPose active)
    {
        var stored = _poses[id];
        if (stored.Anchor != HudWidgetAnchor.Cockpit)
        {
            active.WristPosition = active.Position;
            active.WristEuler = active.Euler;
            active.WristScale = active.Scale;
            active.Position = stored.Position;
            active.Euler = stored.Euler;
            active.Scale = stored.Scale;
        }
        active.Anchor = stored.Anchor;
        _poses[id] = HudWidgetProfiles.Sanitize(id, active);
    }
    internal void SetAnchor(string id, HudWidgetAnchor anchor)
    {
        if (!_editing || !_poses.TryGetValue(id, out var pose) || pose.Anchor == anchor)
            return;
        ReleaseHands();
        pose = _poses[id]; // Releasing a frozen edit frame may have rebased its wrist offset.
        pose.Anchor = anchor;
        _poses[id] = HudWidgetProfiles.Sanitize(id, pose);
        if (_widgets.TryGetValue(id, out var widget))
        {
            widget.WristVisibility.Reset();
            widget.WristFrozen = false;
            widget.Appearance.ReleaseHands();
        }
        Select(id);
        Place();
    }
    private bool TryLiveWristFrame(string id, out Vector3 origin, out Quaternion rotation)
    {
        origin = Vector3.zero;
        rotation = Quaternion.identity;
        return _poses.TryGetValue(id, out var pose) && pose.Anchor != HudWidgetAnchor.Cockpit &&
            TryWristFrame(pose.Anchor == HudWidgetAnchor.LeftWrist, out origin, out rotation);
    }
    private bool TryWristFrame(bool left, out Vector3 origin, out Quaternion rotation)
    {
        origin = Vector3.zero;
        rotation = Quaternion.identity;
        if (!Space(out var gameRoot, out _, out var eye))
            return false;
        var hand = TrackedControllers.Read(left);
        if (!hand.Valid)
            return false;
        var handRotation = Quaternion.Inverse(eye.rotation) * PilotHandVisuals.FreeHandRotation(left, hand, gameRoot);
        var forward = handRotation * Vector3.forward;
        var dorsal = handRotation * (left ? Vector3.left : Vector3.right);
        origin = CockpitWidgetSpace.ToLocal(eye.position, eye.rotation, gameRoot.TransformPoint(hand.Position)) - forward * .09f + dorsal * .045f;
        rotation = Quaternion.LookRotation(-dorsal, forward);
        return true;
    }
    private bool TryWidgetFrame(string id, out Vector3 origin, out Quaternion rotation)
    {
        origin = Vector3.zero;
        rotation = Quaternion.identity;
        if (_poses[id].Anchor == HudWidgetAnchor.Cockpit)
            return true;
        if (!TrackedControllers.Read(_poses[id].Anchor == HudWidgetAnchor.LeftWrist).Valid)
            return false;
        if (_widgets.TryGetValue(id, out var widget) && widget.WristFrozen)
        {
            origin = widget.WristOrigin;
            rotation = widget.WristRotation;
            return true;
        }
        return TryLiveWristFrame(id, out origin, out rotation);
    }
    private Vector3 WidgetPoint(string id, Vector3 cockpitPoint) => TryWidgetFrame(id, out var origin, out var rotation)
        ? Quaternion.Inverse(rotation) * (cockpitPoint - origin) : cockpitPoint;
    private Quaternion WidgetRotation(string id, Quaternion cockpitRotation) => TryWidgetFrame(id, out _, out var rotation)
        ? Quaternion.Inverse(rotation) * cockpitRotation : cockpitRotation;
    private void CancelUntrackedWristEdits()
    {
        foreach (var pair in _widgets)
            if (_poses[pair.Key].Anchor != HudWidgetAnchor.Cockpit &&
                !TrackedControllers.Read(_poses[pair.Key].Anchor == HudWidgetAnchor.LeftWrist).Valid &&
                (pair.Value.WristFrozen || pair.Value.Appearance.IsHandBusy(0) || pair.Value.Appearance.IsHandBusy(1)))
            {
                // The other hand may still be tracked and holding this card.
                // Tracking recovery must require a fresh deliberate contact.
                ReleaseHands();
                break;
            }
    }
    private void FreezeWrist(string id)
    {
        if (_widgets.TryGetValue(id, out var widget) && !widget.WristFrozen && TryLiveWristFrame(id, out var origin, out var rotation))
        {
            widget.WristOrigin = origin;
            widget.WristRotation = rotation;
            widget.WristFrozen = true;
        }
    }
    private void ReleaseWristFrames()
    {
        foreach (var pair in _widgets)
        {
            var widget = pair.Value;
            if (!widget.WristFrozen || _hands[0].Id == pair.Key || _hands[1].Id == pair.Key)
                continue;
            if (TryLiveWristFrame(pair.Key, out var origin, out var rotation))
            {
                var pose = ActivePose(pair.Key);
                pose.Position = Quaternion.Inverse(rotation) * (widget.WristOrigin + widget.WristRotation * pose.Position - origin);
                pose.Euler = (Quaternion.Inverse(rotation) * widget.WristRotation * Quaternion.Euler(pose.Euler)).eulerAngles;
                StoreActivePose(pair.Key, pose);
            }
            widget.WristFrozen = false;
        }
    }
    private float WristAlpha(string id, Widget widget, Vector3 cockpitPosition, Quaternion cockpitRotation)
    {
        if (_poses[id].Anchor == HudWidgetAnchor.Cockpit)
        {
            widget.WristVisibility.Reset();
            return 1;
        }
        var left = _poses[id].Anchor == HudWidgetAnchor.LeftWrist;
        var tracked = TrackedControllers.Read(left).Valid;
        var blocked = NativeHandOptionWheel.OwnsHand(left) || WristMenu.IsOpen && !_editing;
        var now = Time.unscaledTime;
        var dt = now - widget.WristLastTime;
        widget.WristLastTime = now;
        var camera = APIBus.MainCamera;
        if (camera == null || !Space(out _, out _, out var eye))
            return widget.WristVisibility.Update(false, true, false, 0, 0, 0, dt);
        var head = CockpitWidgetSpace.ToLocal(eye.position, eye.rotation, camera.transform.position);
        var towards = cockpitPosition - head;
        var direction = towards.normalized;
        var gaze = Quaternion.Inverse(eye.rotation) * camera.transform.forward;
        return widget.WristVisibility.Update(tracked, blocked, _editing, Vector3.Dot(gaze, direction),
            Vector3.Dot(cockpitRotation * Vector3.back, -direction), towards.magnitude, dt);
    }
}
