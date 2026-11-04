using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

internal static class NavigationChecks
{
    internal static int Run()
    {
        var count = 0;
        void Check(bool condition, string why)
        {
            if (!condition)
                throw new Exception(why);
            count++;
        }
        void Near(float a, float b, string why) => Check(Math.Abs(a - b) < .002f, why);
        void Near3(Vector3 a, Vector3 b, string why) => Check((a - b).sqrMagnitude < .00001f, why);
        using (var f = new Fixture())
        {
            var image = f.Map.mapImage.transform;
            image.localRotation = Quaternion.Euler(0, 0, 57);
            image.localPosition = new(20, -40, 0);
            var originalPosition = image.localPosition;
            var originalRotation = image.localRotation;
            var originalScale = image.localScale;
            f.Slot.localPosition = new(17, -9, 2);
            f.Slot.localRotation = Quaternion.Euler(20, 35, 17);
            f.Slot.localScale = Vector3.one * .003f;
            f.Tick();
            var nav = CockpitMapWidget.Navigation!;
            Check(CockpitMapWidget.Available && CockpitMapWidget.Viewport == f.Slot, "Valid visible live map exposes navigation and viewport");
            Check(nav.Activate() && nav.Selected, "Direct-touch selection captures the current map center");
            var center = image.InverseTransformPoint(f.Slot.TransformPoint(f.Slot.rect.center));
            nav.Pan(new(.5f, .25f), .05f);
            var oldCenterOnScreen = f.Slot.InverseTransformPoint(image.TransformPoint(center));
            Near(oldCenterOnScreen.x, -300 * .65f * .05f * .5f, "Pan right shifts old center left in rotated panel screen space");
            Near(oldCenterOnScreen.y, -300 * .65f * .05f * .25f, "Pan up shifts old center down without a second deadzone");
            center = image.InverseTransformPoint(f.Slot.TransformPoint(f.Slot.rect.center));
            Check(CockpitMapNavigation.TryCenter(f.Map), "Selected native centering/jump hook consumes only owned map");
            Check(!CockpitMapNavigation.TryCenter(new GameObject("other map").AddComponent<DynamicMap>()), "Unowned map is never intercepted");
            Check(nav.Zoom(1), "Plus changes native zoom");
            Near(f.Map.GetZoomLevel(), 2.6f, "Plus is bounded multiplicative zoom");
            Near3(center, image.InverseTransformPoint(f.Slot.TransformPoint(f.Slot.rect.center)), "Zoom preserves the crosshair's terrain coordinate");
            Check(f.Map.Changes == 1, "Zoom keeps native change-event path");
            f.Slot.sizeDelta = new(440, 220);
            f.Tick();
            Near3(center, image.InverseTransformPoint(f.Slot.TransformPoint(f.Slot.rect.center)), "Live rectangular resize preserves selected map center");
            Near(f.Map.transform.localScale.x, f.Map.transform.localScale.y, "Rectangular crop does not stretch terrain or icons");
            Near(((RectTransform)f.Map.transform).rect.width * f.Map.transform.localScale.x, 440, "Rectangular crop fills entire width");
            Near(((RectTransform)f.Map.transform).rect.height * f.Map.transform.localScale.y, 220, "Rectangular crop fills entire height");
            var before = image.localPosition;
            nav.Pan(new(float.NaN, 1), .02f);
            nav.Pan(new(1, 0), float.PositiveInfinity);
            nav.Pan(new(0, 0), .03f);
            Near3(before, image.localPosition, "Invalid and neutral input cannot corrupt or move map");
            for (var i = 0; i < 20; i++)
                nav.Zoom(1);
            Near(f.Map.GetZoomLevel(), 12, "Cockpit zoom respects upper native-safe bound");
            Check(!nav.Zoom(1), "Zoom at limit has no false success");
            for (var i = 0; i < 30; i++)
                nav.Zoom(-1);
            Near(f.Map.GetZoomLevel(), 1, "Cockpit zoom respects lower bound");
            nav.Deactivate();
            Check(!nav.Selected && CockpitMapNavigation.TryCenter(f.Map), "Release follows ownship through the owned map hook, without native lookahead");
            Check(nav.Zoom(1), "Plus/minus remains available while unselected");
            nav.Activate();
            CockpitHudWidgets.Editing = true;
            f.Tick();
            Check(!nav.Selected && !nav.Activate() && !nav.Zoom(1), "Layout editing clears selection and disables navigation");
            CockpitHudWidgets.Editing = false;
            f.Tick();
            Check(!nav.Selected, "Leaving edit does not resurrect selected state");
            nav.Activate();
            CockpitHudWidgets.Visible = false;
            f.Tick();
            Check(!CockpitMapWidget.Available && !nav.Selected, "Hidden saved map cannot own navigation");
            CockpitHudWidgets.Visible = true;
            f.Tick();
            CockpitMapWidget.ReleaseAll();
            Check(CockpitMapWidget.Navigation == null && CockpitMapWidget.Viewport == null && !nav.Valid, "Release clears navigation and viewport ownership");
            Near3(image.localPosition, originalPosition, "Release restores native image position");
            Check(image.localRotation == originalRotation, "Release restores native image heading");
            Near3(image.localScale, originalScale, "Release restores native image zoom scale");
            Near(f.Map.GetZoomLevel(), 2, "Release restores native zoom state before full map opens");
            Check(((RectTransform)f.Map.mapBackground.transform).sizeDelta == new Vector2(320, 320), "Release restores original clipping/background dimensions");
        }
        using (var f = new Fixture())
        {
            f.Tick();
            var nav = CockpitMapWidget.Navigation!;
            nav.Activate();
            UnitMapIcon Icon(Vector2 point)
            {
                var go = new GameObject("sensor icon");
                go.transform.SetParent(f.Map.mapImage.transform, false);
                var image = go.AddComponent<Image>();
                image.color = new(1, 1, 1, 1);
                var icon = go.AddComponent<UnitMapIcon>();
                icon.unit = new Unit();
                icon.iconImage = image;
                go.transform.position = f.Slot.TransformPoint(point);
                f.Map.mapIcons.Add(icon);
                f.Hud.Markers.Add(icon.unit);
                return icon;
            }
            var nearest = Icon(new(3, 0));
            var farther = Icon(new(10, 0));
            Check(nav.SelectCenter() && f.Hud.Selected.SequenceEqual(new[] { nearest.unit }), "Center selects nearest eligible sensor icon, not farther target");
            f.Hud.Selected.Clear();
            f.Hud.aircraft!.weaponManager!.Targets.Clear();
            f.Selector.Excluded.Add(nearest.unit);
            Check(nav.SelectCenter() && f.Hud.Selected.Last() == farther.unit, "Native targeting exclusion skips closer excluded target");
            f.Selector.Excluded.Add(farther.unit);
            Check(!nav.SelectCenter(), "All excluded icons cannot create a target");
            f.Selector.Excluded.Clear();
            nearest.iconImage.enabled = false;
            farther.iconImage.color = new(1, 1, 1, 0);
            Check(!nav.SelectCenter(), "Disabled or transparent sensor icons cannot be locked");
            nearest.iconImage.enabled = true;
            f.Hud.Markers.Remove(nearest.unit);
            Check(!nav.SelectCenter(), "Visible icon without native HUD tracking marker cannot fabricate target acquisition");
            f.Hud.Markers.Add(nearest.unit);
            nearest.iconImage.raycastTarget = false;
            Check(!nav.SelectCenter(), "Native non-interactable icon remains non-interactable");
            nearest.iconImage.raycastTarget = true;
            nearest.transform.position = f.Slot.TransformPoint(new Vector3(40, 0, 0));
            Check(!nav.SelectCenter(), "Selection has a finite center capture radius");
            nearest.transform.position = f.Slot.TransformPoint(Vector3.zero);
            nearest.unit = f.Hud.aircraft;
            f.Hud.Markers.Add(nearest.unit);
            Check(!nav.SelectCenter(), "Ownship cannot be locked from map");
            nearest.unit = new Unit();
            f.Hud.Markers.Add(nearest.unit);
            f.Hud.RefuseSelection = true;
            Check(!nav.SelectCenter(), "Native refusal does not produce successful selection feedback");
            f.Hud.RefuseSelection = false;
            f.Hud.aircraft.disabled = true;
            Check(!nav.SelectCenter(), "Dead aircraft cannot issue weapon target selection");
            f.Hud.aircraft.disabled = false;
            nav.Deactivate();
            Check(!nav.SelectCenter(), "Unselected map cannot target");
        }
        using (var f = new Fixture())
        {
            var aircraft = GameManager.Aircraft!;
            aircraft.transform.position = new(235, 80, -420);
            aircraft.transform.rotation = Quaternion.Euler(22, 63, -31);
            GlobalPositions.Origin = new(23000, 0, -17000);
            f.Map.mapDisplayFactor = .012f;
            var camera = new GameObject("world camera").AddComponent<CameraStateManager>();
            camera.transform.position = new(800, 150, 950);
            SceneSingleton<CameraStateManager>.i = camera;
            var head = new GameObject("VR head").AddComponent<Camera>();
            head.transform.rotation = Quaternion.Euler(0, -78, 0);
            NOVR.APIBus.MainCamera = head;
            f.Map.viewIndicator = new GameObject("view indicator");
            f.Map.viewIndicator.transform.SetParent(f.Map.mapImage.transform, false);
            f.Slot.localPosition = new(17, -9, 2);
            f.Slot.localRotation = Quaternion.Euler(20, 35, 17);
            f.Slot.localScale = Vector3.one * .003f;
            f.Slot.sizeDelta = new(440, 220);
            f.Slot.pivot = new(.2f, .6f);
            f.Map.mapBackground.transform.localRotation = Quaternion.Euler(0, 0, -31);
            f.Map.mapBackground.transform.localScale = Vector3.one * 1.4f;
            var image = f.Map.mapImage.transform;
            Vector3 OwnshipMapPoint()
            {
                var position = aircraft.GlobalPosition().AsVector3() * f.Map.mapDisplayFactor;
                return new(position.x, position.z, 0);
            }
            Vector3 OwnshipPanelPoint() => f.Slot.InverseTransformPoint(image.TransformPoint(OwnshipMapPoint()));
            void Centered(string why) => Near3(OwnshipPanelPoint(), f.Slot.rect.center, why);
            void HeadingUp(string why)
            {
                var forward = aircraft.transform.forward;
                var panelForward = f.Slot.InverseTransformVector(image.TransformVector(new(forward.x, forward.z, 0)));
                Near(panelForward.x, 0, why + " horizontal component");
                Check(panelForward.y > 0, why + " forward points up");
                Near(panelForward.z, 0, why + " stays in panel plane");
            }
            f.Tick();
            var nav = CockpitMapWidget.Navigation!;
            Centered("Fresh borrowed map centers exact global ownship, not camera or 4 km ahead");
            HeadingUp("Follow heading respects tilted panel and rotated native ancestors");
            Check(nav.Activate(), "Ownship-follow can enter manual slave");
            nav.Pan(new(1, .5f), .05f);
            nav.Zoom(1);
            Check((OwnshipPanelPoint() - (Vector3)f.Slot.rect.center).sqrMagnitude > 1, "Manual pan moves ownship away from center");
            var zoom = f.Map.GetZoomLevel();
            var scale = image.localScale;
            var events = f.Map.Changes;
            nav.Deactivate();
            Check(!nav.Selected, "Deliberate deactivation immediately releases map selection");
            Centered("Deactivate recenters synchronously before another adapter or native update");
            Near(zoom, f.Map.GetZoomLevel(), "Deactivate retains chosen map zoom");
            Near3(scale, image.localScale, "Deactivate preserves map scale");
            Check(events == f.Map.Changes, "Recentering does not issue a zoom change event");
            var heading = image.rotation;
            var position = image.position;
            head.transform.rotation = Quaternion.Euler(11, 152, 20);
            camera.transform.position += new Vector3(300, 50, -400);
            Check(CockpitMapNavigation.TryCenter(f.Map), "Owned native center callback maintains unselected ownship-follow");
            Centered("Head and camera motion do not move map center away from aircraft");
            Near3(position, image.position, "Head motion does not introduce lookahead or offset");
            Near3(heading * Vector3.up, image.rotation * Vector3.up, "Head motion cannot rotate the map");
            var cameraGlobal = camera.transform.position.ToGlobalPosition().AsVector3() * f.Map.mapDisplayFactor;
            Near3(f.Map.viewIndicator.transform.localPosition, new(cameraGlobal.x, cameraGlobal.z, 0), "View indicator remains at its native global camera position");
            aircraft.transform.position += new Vector3(120, 30, 240);
            aircraft.transform.rotation = Quaternion.Euler(-15, -122, 27);
            Check(CockpitMapNavigation.TryCenter(f.Map), "Moving aircraft remains owned by follow callback");
            Centered("Unselected map follows moving aircraft");
            HeadingUp("Aircraft heading updates in panel space");
            position = image.position;
            heading = image.rotation;
            var shift = new Vector3(10000, 0, -8000);
            GlobalPositions.Origin += shift;
            aircraft.transform.position -= shift;
            camera.transform.position -= shift;
            f.Tick();
            Centered("Floating-origin shift preserves global ownship centering");
            Near3(position, image.position, "Floating-origin shift leaves visible map position continuous");
            Near3(heading * Vector3.up, image.rotation * Vector3.up, "Floating-origin shift leaves map heading continuous");
            f.Slot.sizeDelta = new(220, 400);
            f.Slot.localRotation = Quaternion.Euler(-15, 10, -24);
            f.Tick();
            Centered("Follow remains centered after live rectangular resize and panel move");
            HeadingUp("Follow tracks the new panel plane");
            nav.Activate();
            nav.Pan(new(-.6f, .8f), .05f);
            var heldCenter = image.InverseTransformPoint(f.Slot.TransformPoint(f.Slot.rect.center));
            heading = image.localRotation;
            aircraft.transform.position += new Vector3(350, 0, 170);
            aircraft.transform.rotation = Quaternion.Euler(0, 40, 0);
            CockpitMapNavigation.TryCenter(f.Map);
            Near3(heldCenter, image.InverseTransformPoint(f.Slot.TransformPoint(f.Slot.rect.center)), "Selected map preserves its manual center while ownship moves");
            Near3(heading * Vector3.up, image.localRotation * Vector3.up, "Selected map preserves its captured heading");
            nav.Deactivate();
            Centered("Repeated slave deactivation returns to latest aircraft position");
            HeadingUp("Deactivation restores latest aircraft heading");
            position = image.position;
            heading = image.rotation;
            DynamicMap.mapMaximized = true;
            aircraft.transform.position += new Vector3(250, 0, 50);
            Check(!CockpitMapNavigation.TryCenter(f.Map), "Fullscreen native map is never intercepted by ownship-follow");
            nav.Deactivate();
            nav.Tick();
            Near3(position, image.position, "Inactive fullscreen ownership cannot mutate native position");
            Near3(heading * Vector3.up, image.rotation * Vector3.up, "Inactive fullscreen ownership cannot mutate native heading");
            DynamicMap.mapMaximized = false;
            CockpitMapWidget.ReleaseAll();
            Check(!CockpitMapNavigation.TryCenter(f.Map), "Disposed map is no longer intercepted");
            UnityEngine.Object.Destroy(camera.gameObject);
            UnityEngine.Object.Destroy(head.gameObject);
        }
        return count;
    }
}
