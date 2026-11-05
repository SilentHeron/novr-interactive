using NOVR.MotionControls;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;

internal static class WristDropRuntimeChecks
{
    internal static void Run(Action<bool, string> check)
    {
        CockpitHudWidgets New()
        {
            TrackedControllers.Left = new()
            {
                Valid = true,
                Position = new(-.25f, -.25f, .4f),
                Rotation = Quaternion.identity,
                Device = 0
            };
            TrackedControllers.Right = new()
            {
                Valid = true,
                Position = new(.25f, -.25f, .4f),
                Rotation = Quaternion.identity,
                Device = 1
            };
            NativeHandOptionWheel.Left = NativeHandOptionWheel.Right = false;
            CockpitLayout.Left = CockpitLayout.Right = CockpitLayout.Element.None;
            InteractionHaptics.Pulses = 0;
            Time.unscaledTime = 0;
            return new CockpitHudWidgets();
        }
        void Arm(CockpitHudWidgets manager, int hand)
        {
            manager.MoveForDock(hand, manager.WristForDock(hand == 1) + new Vector3(0, 0, .035f));
            manager.ObserveDock(0);
            manager.ObserveDock(.1f);
            manager.ObserveDock(.2f);
            check(manager.DockReady(hand), "Hovering a held panel over the opposite free wrist arms the release preview");
        }
        foreach (var hand in new[] { 0, 1 })
        {
            var manager = New();
            var original = manager.Stored("map");
            manager.PickForDock(hand, "map");
            Arm(manager, hand);
            check(manager.Stored("map").Anchor == HudWidgetAnchor.Cockpit, "Readiness is only a preview, never an immediate attachment");
            check(InteractionHaptics.Pulses == 2, "Readiness gives one short tick on each involved hand");
            manager.ObserveDock(.21f);
            check(InteractionHaptics.Pulses == 2, "Holding over the wrist does not repeat the tick");
            check(manager.DropForDock(hand, .22f), "Explicit release commits the previously shown wrist preview");
            var pose = manager.Stored("map");
            check(pose.Anchor == (hand == 0 ? HudWidgetAnchor.RightWrist : HudWidgetAnchor.LeftWrist), "Both hands attach to the opposite wrist");
            check((pose.Position - original.Position).magnitude < .0001f && pose.Scale == original.Scale,
                "Attaching preserves the pre-grab cockpit position and size");
            check(pose.WristPosition.magnitude == 0 && pose.WristEuler.magnitude == 0 && pose.WristScale == .6f,
                "Released card uses the centered, upright, compact wrist preview exactly");
            check((manager.Frame("map").position - manager.WristForDock(hand == 1)).magnitude < .0001f,
                "Preview and final attachment share the same wrist frame");
        }
        foreach (var cause in new[] { "move", "raw-release-move", "tracking", "wheel", "other-grip", "hotas", "cancel" })
        {
            var manager = New();
            manager.PickForDock(1, "map");
            Arm(manager, 1);
            if (cause == "move")
                manager.MoveForDock(1, new(.6f, -.2f, .5f));
            if (cause == "raw-release-move")
                manager.RawPointForDock(1, new(.6f, -.2f, .5f));
            if (cause == "tracking")
                TrackedControllers.Left.Valid = false;
            if (cause == "wheel")
                NativeHandOptionWheel.Left = true;
            if (cause == "other-grip")
                TrackedControllers.Left.Grip = 1;
            if (cause == "hotas")
                CockpitLayout.Left = CockpitLayout.Element.Stick;
            if (cause == "cancel")
                manager.Release();
            else
                check(!manager.DropForDock(1, .22f), cause + " on the release frame invalidates the old preview");
            check(manager.Stored("map").Anchor == HudWidgetAnchor.Cockpit, cause + " cannot attach a widget");
            check(!manager.DockReady(1), cause + " clears readiness immediately");
        }
        {
            var manager = New();
            manager.PickForDock(1, "map");
            manager.MoveForDock(1, manager.WristForDock(true));
            manager.ObserveDock(0);
            manager.ObserveDock(.1f);
            check(!manager.DropForDock(1, .2f), "Release cannot arm and commit before the ready preview has ever been shown");
        }
        foreach (var attached in new[] { false, true })
        {
            var manager = New();
            if (attached)
                manager.SetAnchor("map", HudWidgetAnchor.LeftWrist);
            manager.PickForDock(1, "map");
            if (!attached)
                Arm(manager, 1);
            manager.PickForDock(0, "map");
            manager.MoveForDock(1, new(.6f, -.2f, .5f));
            manager.ObserveDock(.21f);
            check(!manager.DockReady(0) && !manager.DockReady(1), "A second resize grip clears every wrist preview");
            check(!manager.DropForDock(0, .22f) && !manager.DropForDock(1, .23f), "Neither release from a two-hand resize changes attachment");
            check(manager.Stored("map").Anchor == (attached ? HudWidgetAnchor.LeftWrist : HudWidgetAnchor.Cockpit),
                "Resizing preserves the existing anchor, including after one hand lets go");
        }
        {
            var manager = New();
            manager.SetAnchor("map", HudWidgetAnchor.LeftWrist);
            manager.PickForDock(1, "map");
            var destination = new Vector3(.1f, -.3f, .6f);
            manager.MoveForDock(1, destination);
            manager.ObserveDock(.1f);
            check(manager.DetachReady(1), "Pulling a wrist card clear shows a cockpit-placement hint");
            check(manager.DropForDock(1, .12f), "Release after pulling away detaches the wrist card");
            var pose = manager.Stored("map");
            check(pose.Anchor == HudWidgetAnchor.Cockpit && (pose.Position - destination).magnitude < .0001f && pose.Euler.z == 0,
                "Detached card stays where released with cockpit roll locked");
        }
        {
            var manager = New();
            manager.SetAnchor("map", HudWidgetAnchor.LeftWrist);
            manager.PickForDock(1, "map");
            TrackedControllers.Left.Position += new Vector3(-.4f, 0, 0);
            manager.ObserveDock(.1f);
            check(!manager.DetachReady(1) && !manager.DropForDock(1, .12f), "Moving only the anchor arm while fitting does not detach a stationary held card");
            check(manager.Stored("map").Anchor == HudWidgetAnchor.LeftWrist, "Ordinary wrist fitting still follows the wrist after release");
        }
    }
}

namespace NOVR.VrUi.SpecialBehavior
{
    internal sealed partial class CockpitHudWidgets
    {
        private void SamplesForDock()
        {
            _samples[0] = TrackedControllers.Left;
            _samples[1] = TrackedControllers.Right;
        }
        internal Vector3 WristForDock(bool left)
        {
            TryWristFrame(left, out var position, out _);
            return position;
        }
        internal void PickForDock(int index, string id)
        {
            if (index == 0)
                TrackedControllers.Left.Grip = 1;
            else
                TrackedControllers.Right.Grip = 1;
            SamplesForDock();
            FreezeWrist(id);
            var hand = _hands[index];
            hand.Id = id;
            hand.Gate.Held = true;
            var pose = ActivePose(id);
            _points[index] = CockpitPose(id, pose).Position;
            hand.LocalAnchor = Vector3.zero;
            hand.StartEuler = pose.Euler;
            hand.GripRotation = WidgetRotation(id, _handRotations[index]);
            BeginWristDrag(index);
        }
        internal void MoveForDock(int index, Vector3 position)
        {
            _points[index] = position;
            var id = _hands[index].Id!;
            var pose = ActivePose(id);
            pose.Position = WidgetPoint(id, position);
            StoreActivePose(id, pose);
        }
        internal void RawPointForDock(int index, Vector3 position) => _points[index] = position;
        internal void ObserveDock(float now)
        {
            Time.unscaledTime = now;
            SamplesForDock();
            UpdateWristDocking();
        }
        internal bool DockReady(int index) => _hands[index].Dock.Gate.Ready;
        internal bool DetachReady(int index) => _hands[index].Dock.Detach;
        internal bool DropForDock(int index, float now)
        {
            Time.unscaledTime = now;
            if (index == 0)
                TrackedControllers.Left.Grip = 0;
            else
                TrackedControllers.Right.Grip = 0;
            SamplesForDock();
            var hand = _hands[index];
            hand.Gate.Held = false;
            var changed = CompleteWristDrop(index);
            hand.Dock.Reset();
            hand.Id = null;
            ReleaseWristFrames();
            return changed;
        }
    }
}
