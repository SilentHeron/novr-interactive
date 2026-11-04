using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

internal static class NavigationChecks
{
    internal static int Run()
    {
        var count = 0;
        void Check(bool condition, string why) { if (!condition) throw new Exception(why); count++; }
        void Near(float a, float b, string why) => Check(Math.Abs(a-b)<.002f, why);
        void Near3(Vector3 a, Vector3 b, string why) => Check((a-b).sqrMagnitude<.00001f,why);
        using (var f = new Fixture())
        {
            var image = f.Map.mapImage.transform;
            image.localRotation = Quaternion.Euler(0,0,57);
            image.localPosition = new(20,-40,0);
            var originalPosition = image.localPosition;
            var originalRotation = image.localRotation;
            var originalScale = image.localScale;
            f.Slot.localPosition = new(17,-9,2);
            f.Slot.localRotation = Quaternion.Euler(20,35,17);
            f.Slot.localScale = Vector3.one * .003f;
            f.Tick();
            var nav = CockpitMapWidget.Navigation!;
            Check(CockpitMapWidget.Available && CockpitMapWidget.Viewport == f.Slot, "Valid visible live map exposes navigation and viewport");
            Check(nav.Activate() && nav.Selected, "Direct-touch selection captures the current map center");
            var center = image.InverseTransformPoint(f.Slot.TransformPoint(f.Slot.rect.center));
            nav.Pan(new(.5f,.25f),.05f);
            var oldCenterOnScreen = f.Slot.InverseTransformPoint(image.TransformPoint(center));
            Near(oldCenterOnScreen.x, -300*.65f*.05f*.5f, "Pan right shifts old center left in rotated panel screen space");
            Near(oldCenterOnScreen.y, -300*.65f*.05f*.25f, "Pan up shifts old center down without a second deadzone");
            center = image.InverseTransformPoint(f.Slot.TransformPoint(f.Slot.rect.center));
            Check(CockpitMapNavigation.TryCenter(f.Map), "Selected native centering/jump hook consumes only owned map");
            Check(!CockpitMapNavigation.TryCenter(new GameObject("other map").AddComponent<DynamicMap>()), "Unowned map is never intercepted");
            Check(nav.Zoom(1), "Plus changes native zoom");
            Near(f.Map.GetZoomLevel(),2.6f,"Plus is bounded multiplicative zoom");
            Near3(center,image.InverseTransformPoint(f.Slot.TransformPoint(f.Slot.rect.center)),"Zoom preserves the crosshair's terrain coordinate");
            Check(f.Map.Changes==1,"Zoom keeps native change-event path");
            f.Slot.sizeDelta = new(440,220); f.Tick();
            Near3(center,image.InverseTransformPoint(f.Slot.TransformPoint(f.Slot.rect.center)),"Live rectangular resize preserves selected map center");
            Near(f.Map.transform.localScale.x,f.Map.transform.localScale.y,"Rectangular crop does not stretch terrain or icons");
            Near(((RectTransform)f.Map.transform).rect.width*f.Map.transform.localScale.x,440,"Rectangular crop fills entire width");
            Near(((RectTransform)f.Map.transform).rect.height*f.Map.transform.localScale.y,220,"Rectangular crop fills entire height");
            var before = image.localPosition;
            nav.Pan(new(float.NaN,1),.02f); nav.Pan(new(1,0),float.PositiveInfinity); nav.Pan(new(0,0),.03f);
            Near3(before,image.localPosition,"Invalid and neutral input cannot corrupt or move map");
            for (var i=0;i<20;i++) nav.Zoom(1);
            Near(f.Map.GetZoomLevel(),12,"Cockpit zoom respects upper native-safe bound");
            Check(!nav.Zoom(1),"Zoom at limit has no false success");
            for (var i=0;i<30;i++) nav.Zoom(-1);
            Near(f.Map.GetZoomLevel(),1,"Cockpit zoom respects lower bound");
            nav.Deactivate(); Check(!nav.Selected&&!CockpitMapNavigation.TryCenter(f.Map),"Release returns ownship-follow to native centering");
            Check(nav.Zoom(1),"Plus/minus remains available while unselected");
            nav.Activate(); CockpitHudWidgets.Editing=true; f.Tick();
            Check(!nav.Selected&&!nav.Activate()&&!nav.Zoom(1),"Layout editing clears selection and disables navigation");
            CockpitHudWidgets.Editing=false; f.Tick(); Check(!nav.Selected,"Leaving edit does not resurrect selected state");
            nav.Activate(); CockpitHudWidgets.Visible=false; f.Tick();
            Check(!CockpitMapWidget.Available&&!nav.Selected,"Hidden saved map cannot own navigation");
            CockpitHudWidgets.Visible=true; f.Tick();
            CockpitMapWidget.ReleaseAll();
            Check(CockpitMapWidget.Navigation==null&&CockpitMapWidget.Viewport==null&&!nav.Valid,"Release clears navigation and viewport ownership");
            Near3(image.localPosition,originalPosition,"Release restores native image position");
            Check(image.localRotation==originalRotation,"Release restores native image heading");
            Near3(image.localScale,originalScale,"Release restores native image zoom scale");
            Near(f.Map.GetZoomLevel(),2,"Release restores native zoom state before full map opens");
            Check(((RectTransform)f.Map.mapBackground.transform).sizeDelta==new Vector2(320,320),"Release restores original clipping/background dimensions");
        }
        using (var f = new Fixture())
        {
            f.Tick(); var nav=CockpitMapWidget.Navigation!; nav.Activate();
            UnitMapIcon Icon(Vector2 point)
            {
                var go=new GameObject("sensor icon"); go.transform.SetParent(f.Map.mapImage.transform,false);
                var image=go.AddComponent<Image>(); image.color=new(1,1,1,1);
                var icon=go.AddComponent<UnitMapIcon>(); icon.unit=new Unit(); icon.iconImage=image;
                go.transform.position=f.Slot.TransformPoint(point); f.Map.mapIcons.Add(icon); f.Hud.Markers.Add(icon.unit); return icon;
            }
            var nearest=Icon(new(3,0)); var farther=Icon(new(10,0));
            Check(nav.SelectCenter()&&f.Hud.Selected.SequenceEqual(new[]{nearest.unit}),"Center selects nearest eligible sensor icon, not farther target");
            f.Hud.Selected.Clear(); f.Hud.aircraft!.weaponManager!.Targets.Clear();
            f.Selector.Excluded.Add(nearest.unit);
            Check(nav.SelectCenter()&&f.Hud.Selected.Last()==farther.unit,"Native targeting exclusion skips closer excluded target");
            f.Selector.Excluded.Add(farther.unit); Check(!nav.SelectCenter(),"All excluded icons cannot create a target");
            f.Selector.Excluded.Clear(); nearest.iconImage.enabled=false; farther.iconImage.color=new(1,1,1,0);
            Check(!nav.SelectCenter(),"Disabled or transparent sensor icons cannot be locked");
            nearest.iconImage.enabled=true; f.Hud.Markers.Remove(nearest.unit);
            Check(!nav.SelectCenter(),"Visible icon without native HUD tracking marker cannot fabricate target acquisition");
            f.Hud.Markers.Add(nearest.unit); nearest.iconImage.raycastTarget=false;
            Check(!nav.SelectCenter(),"Native non-interactable icon remains non-interactable");
            nearest.iconImage.raycastTarget=true; nearest.transform.position=f.Slot.TransformPoint(new Vector3(40,0,0));
            Check(!nav.SelectCenter(),"Selection has a finite center capture radius");
            nearest.transform.position=f.Slot.TransformPoint(Vector3.zero); nearest.unit=f.Hud.aircraft;
            f.Hud.Markers.Add(nearest.unit); Check(!nav.SelectCenter(),"Ownship cannot be locked from map");
            nearest.unit=new Unit(); f.Hud.Markers.Add(nearest.unit); f.Hud.RefuseSelection=true;
            Check(!nav.SelectCenter(),"Native refusal does not produce successful selection feedback");
            f.Hud.RefuseSelection=false; f.Hud.aircraft.disabled=true;
            Check(!nav.SelectCenter(),"Dead aircraft cannot issue weapon target selection");
            f.Hud.aircraft.disabled=false; nav.Deactivate(); Check(!nav.SelectCenter(),"Unselected map cannot target");
        }
        return count;
    }
}
