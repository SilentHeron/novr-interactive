using NOVR.MotionControls;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Docking is an edit gesture, never a side effect of saving, losing tracking or
// opening a menu. The normal grip-release path is its only commit point.
internal sealed partial class CockpitHudWidgets
{
    private sealed class WristDockGesture
    {
        internal readonly HudWidgetWristDock Gate = new();
        internal HudWidgetPose Before;
        internal Vector3 StartPoint;
        internal HudWidgetAnchor Target;
        internal bool Near, Detach, Suppressed;
        internal void Reset()
        {
            Gate.Reset();
            Near = Detach = Suppressed = false;
            Target = HudWidgetAnchor.Cockpit;
        }
    }

    private void BeginWristDrag(int index)
    {
        var drag = _hands[index];
        var id = drag.Id!;
        drag.Dock.Reset();
        drag.Dock.Before = _poses[id];
        drag.Dock.StartPoint = _points[index];
        if (_hands[1 - index].Id == id)
        {
            // A resize remains a resize until both hands let go. Its final
            // release must not unexpectedly dock or detach the fitted card.
            foreach (var hand in _hands)
            {
                hand.Dock.Gate.Reset();
                hand.Dock.Near = hand.Dock.Detach = false;
                hand.Dock.Suppressed = true;
            }
        }
    }

    private HudWidgetPose CockpitPose(string id, HudWidgetPose active)
    {
        TryWidgetFrame(id, out var origin, out var frame);
        active.Position = origin + frame * active.Position;
        active.Euler = (frame * Quaternion.Euler(active.Euler)).eulerAngles;
        return active;
    }

    private HudWidgetPose RawDragPose(int index)
    {
        var drag = _hands[index];
        var pose = ActivePose(drag.Id!);
        pose.Euler = HudWidgetRotationMath.Drag(drag.StartEuler, drag.GripRotation,
            WidgetRotation(drag.Id!, _handRotations[index]), pose.Euler, pose.Anchor != HudWidgetAnchor.Cockpit);
        pose.Position = WidgetPoint(drag.Id!, _points[index]) - Quaternion.Euler(pose.Euler) * drag.LocalAnchor;
        return CockpitPose(drag.Id!, pose);
    }

    private bool NearWrist(string id, HudWidgetPose pose, Vector3 wrist)
    {
        var local = Quaternion.Inverse(Quaternion.Euler(pose.Euler)) * (wrist - pose.Position);
        var half = Size(id) * (PixelSize * pose.Scale * .5f);
        return HudWidgetWristDock.IsNear(local.x, local.y, local.z, half.x, half.y);
    }

    private bool DockTarget(int index, out HudWidgetAnchor target)
    {
        target = index == 0 ? HudWidgetAnchor.RightWrist : HudWidgetAnchor.LeftWrist;
        var drag = _hands[index];
        var other = 1 - index;
        if (!_editing || drag.Id == null || drag.Dock.Suppressed || !_samples[index].Valid || !_samples[other].Valid ||
            !(_samples[other].Grip < .65f) || _hands[other].Id != null || _appearanceBusy[other] ||
            CockpitLayout.HeldByHand(other == 0) != CockpitLayout.Element.None ||
            NativeHandOptionWheel.OwnsHand(other == 0) || _poses[drag.Id].Anchor == target ||
            !TryWristFrame(other == 0, out var wrist, out _))
            return false;
        // Test both visible (smoothed) and raw positions. A quick move away on
        // the release frame cannot commit the previous frame's visible proximity.
        return NearWrist(drag.Id, CockpitPose(drag.Id, ActivePose(drag.Id)), wrist) &&
            NearWrist(drag.Id, RawDragPose(index), wrist);
    }

    private bool DetachOnDrop(int index)
    {
        var drag = _hands[index];
        if (drag.Id == null || drag.Dock.Suppressed || !_samples[index].Valid || _hands[1 - index].Id != null ||
            (_points[index] - drag.Dock.StartPoint).magnitude < .08f)
            return false;
        var anchor = _poses[drag.Id].Anchor;
        if (anchor == HudWidgetAnchor.Cockpit || !TryWristFrame(anchor == HudWidgetAnchor.LeftWrist, out var wrist, out _))
            return false;
        var displayed = CockpitPose(drag.Id, ActivePose(drag.Id));
        return (displayed.Position - wrist).magnitude > .26f &&
            !NearWrist(drag.Id, displayed, wrist) && !NearWrist(drag.Id, RawDragPose(index), wrist);
    }

    private void RefreshWristDock(int index, bool feedback)
    {
        var dock = _hands[index].Dock;
        var near = DockTarget(index, out var target);
        if (dock.Target != target)
            dock.Gate.Reset();
        dock.Target = target;
        dock.Near = near;
        dock.Detach = !near && DetachOnDrop(index);
        dock.Gate.Update(near, Time.unscaledTime);
        if (feedback && dock.Gate.BecameReady)
        {
            InteractionHaptics.PulseOnly(_samples[index].Device, .24f, .045f);
            InteractionHaptics.PulseOnly(_samples[1 - index].Device, .18f, .035f);
        }
    }

    private void UpdateWristDocking()
    {
        for (var index = 0; index < 2; index++)
            if (_hands[index].Id != null && _hands[index].Gate.Held)
                RefreshWristDock(index, true);
            else
                _hands[index].Dock.Reset();
    }

    private static float DockScale(HudWidgetPose pose) => pose.Anchor == HudWidgetAnchor.Cockpit
        ? Mathf.Clamp(pose.Scale, HudWidgetProfiles.MinScale, .6f) : pose.Scale;

    private bool CompleteWristDrop(int index)
    {
        var drag = _hands[index];
        var id = drag.Id!;
        var shownReady = drag.Dock.Gate.Ready;
        RefreshWristDock(index, false);
        var attach = drag.Dock.Gate.Release(shownReady);
        var pose = _poses[id];
        if (attach)
        {
            var scale = DockScale(ActivePose(id));
            // Keep the pre-grab cockpit home while the card is on the wrist.
            if (drag.Dock.Before.Anchor == HudWidgetAnchor.Cockpit)
            {
                pose.Position = drag.Dock.Before.Position;
                pose.Euler = drag.Dock.Before.Euler;
                pose.Scale = drag.Dock.Before.Scale;
            }
            pose.Anchor = drag.Dock.Target;
            pose.WristPosition = pose.WristEuler = Vector3.zero;
            pose.WristScale = scale;
        }
        else if (drag.Dock.Detach)
        {
            var displayed = CockpitPose(id, ActivePose(id));
            var forward = Quaternion.Euler(displayed.Euler) * Vector3.forward;
            var angle = HudWidgetAngleMath.Direction(forward.x, forward.y, forward.z, 0, 0, true);
            pose.Anchor = HudWidgetAnchor.Cockpit;
            pose.Position = displayed.Position;
            pose.Euler = new Vector3(angle.pitch, angle.yaw, 0);
            pose.Scale = displayed.Scale;
        }
        else
            return false;
        _poses[id] = HudWidgetProfiles.Sanitize(id, pose);
        _widgets[id].WristFrozen = false;
        _widgets[id].WristVisibility.Reset();
        return true;
    }

    private bool WristDropReady(string id)
    {
        foreach (var hand in _hands)
            if (hand.Id == id && hand.Dock.Gate.Ready)
                return true;
        return false;
    }
}
