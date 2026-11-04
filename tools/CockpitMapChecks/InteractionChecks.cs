using System.Reflection;
using NOVR;
using NOVR.MotionControls;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using Object=UnityEngine.Object;

internal static class InteractionChecks
{
    internal static int Run()
    {
        var count=0;
        void Check(bool condition,string why) { if(!condition)throw new Exception(why); count++; }
        void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.00001f,why);
        foreach(var scale in new[]{.35f,.65f,1f})
        foreach(var direction in new[]{-1,1})
        using(var f=new TouchFixture(scale))
        {
            var before=f.Map.Map.GetZoomLevel();
            // Both expanded keys contain this location at these widget sizes.
            // Its sign indicates the nearer key, despite + being tested first.
            var point=new Vector2(190,direction);
            f.Press(point);
            Near(f.Map.Map.GetZoomLevel(),before*(direction>0 ? 1.3f:1f/1.3f),"Overlapping touch faces choose nearest zoom key at scale "+scale);
            Check(f.Map.Map.Changes==1,"A single overlapping contact issues exactly one native zoom");
            Check(f.Clicks==1,"Successful zoom gives exactly one click/audio feedback cue");
            var cue=InteractionHaptics.Events.Single(e=>e.Cue==InteractionCue.Click);
            Check(cue.Strength==MapInteractionFeedback.ZoomStrength&&cue.Seconds==MapInteractionFeedback.ZoomSeconds,"Zoom contact uses its intended haptic profile");
        }
        using(var f=new TouchFixture(.65f))
        {
            f.Press(new(190,32)); var once=f.Map.Map.GetZoomLevel();
            f.Step(new(190,-32),0,.08f); f.Step(new(190,-32),0,.5f);
            Near(f.Map.Map.GetZoomLevel(),once,"Sliding from + into - without withdrawal cannot press again");
            Check(f.Clicks==1,"Held or sliding contact cannot repeat sound/strong feedback");
            f.Step(new(190,-32),.03f,.6f); f.Step(new(190,-32),0,.62f);
            Near(f.Map.Map.GetZoomLevel(),2,"Withdrawal rearms the newly selected opposite key");
            Check(f.Clicks==2,"Second deliberate press confirms once");
            f.Step(new(190,32),.03f,.65f); f.Step(new(190,32),.03f,.67f); f.Step(new(190,32),0,.69f);
            Near(f.Map.Map.GetZoomLevel(),2,"Fresh press during global contact cooldown is ignored");
            f.Step(new(190,32),0,1.0f); Near(f.Map.Map.GetZoomLevel(),2,"Cooldown expiry cannot replay a finger held on the key");
            f.Step(new(190,32),.03f,1.02f); f.Step(new(190,32),0,1.04f);
            Near(f.Map.Map.GetZoomLevel(),2.6f,"Withdrawing after cooldown allows a fresh zoom press");
            var changes=f.Map.Map.Changes;
            f.Step(new(300,32),.03f,1.4f); f.Step(new(300,32),.03f,1.42f); f.Step(new(300,32),0,1.44f);
            Check(f.Map.Map.Changes==changes,"Contact outside both expanded faces cannot zoom");
        }
        foreach(var missingAxis in new[]{false,true})
        using(var f=new TouchFixture(.65f))
        {
            var right=TrackedControllers.Samples[1]; right.Valid=missingAxis; right.HasAxis=!missingAxis; TrackedControllers.Samples[1]=right;
            f.Press(new(190,32)); Near(f.Map.Map.GetZoomLevel(),2.6f,"Left fingertip can zoom while right tracking or thumbstick data is unavailable");
            var clicks=f.Clicks;
            f.Step(Vector2.zero,.03f,.4f); f.Step(Vector2.zero,.03f,.42f); f.Step(Vector2.zero,0,.44f);
            Check(!CockpitMapWidget.Navigation!.Selected,"Left face touch cannot reactivate map with unavailable right controller");
            Check(f.Clicks==clicks,"Unavailable map selection cannot report a successful press");
            right.Valid=right.HasAxis=true; TrackedControllers.Samples[1]=right;
            f.Step(Vector2.zero,0,.6f); f.Step(Vector2.zero,0,.9f);
            Check(!CockpitMapWidget.Navigation.Selected,"Right-controller recovery does not inherit a finger already touching the map");
            f.Step(Vector2.zero,.03f,1.0f); f.Step(Vector2.zero,0,1.02f);
            Check(CockpitMapWidget.Navigation.Selected&&f.Clicks==clicks+1,"Fresh withdrawal/contact after tracking recovery selects exactly once");
            right.Valid=false; TrackedControllers.Samples[1]=right; f.Step(Vector2.zero,0,1.04f);
            Check(!CockpitMapWidget.Navigation.Selected,"Losing right tracking immediately cancels an already selected map");
        }
        using(var f=new TouchFixture(.65f))
        {
            f.Both(new(190,32),.03f,0); f.Both(new(190,32),.03f,.02f); f.Both(new(190,32),0,.04f);
            Near(f.Map.Map.GetZoomLevel(),2.6f,"Simultaneous two-hand contact cannot double-zoom");
            Check(f.Clicks==1,"Two-hand shared contact produces one successful feedback cue");
            f.Step(Vector2.zero,.03f,.4f); f.Step(Vector2.zero,.03f,.42f); f.Step(Vector2.zero,0,.44f);
            Check(CockpitMapWidget.Navigation!.Selected,"Available direct face contact selects map");
            f.Step(Vector2.zero,.03f,.8f); f.Step(Vector2.zero,.03f,.82f); f.Step(Vector2.zero,0,.84f);
            Check(!CockpitMapWidget.Navigation.Selected,"Next deliberate face contact deselects map");
            Near(f.Map.Map.GetZoomLevel(),2.6f,"Face selection and deselection preserve native zoom");
        }
        return count;
    }

    private sealed class TouchFixture:IDisposable
    {
        internal readonly Fixture Map=new();
        private readonly Transform _game,_ui;
        private readonly CockpitMapInteraction _interaction;
        internal int Clicks=>InteractionHaptics.Events.Count(e=>e.Cue==InteractionCue.Click);
        internal TouchFixture(float scale)
        {
            TrackedControllers.Available=true; CockpitLayout.Active=false; NOVR.VrUi.WristMenu.IsOpen=false;
            Cursor.visible=false; CursorManager.Flags=CursorFlags.None; GameManager.flightControlsEnabled=true; GameManager.controlMapper=null;
            RadialMenuMain.InUse=false; SceneSingleton<RadialMenuMain>.i=null; CameraStateManager.cameraMode=CameraMode.cockpit;
            Time.timeScale=1; NativeHandOptionWheel.Owner=HelmetNightVision.Owner=-1;
            MotionFlightController.Instance=new() {FlightActive=true,StickHeld=false,ThrottleHeld=false};
            ModConfiguration.Instance.MotionControls.LeftHanded.Value=false; InteractionHaptics.Events.Clear();
            _game=new GameObject("tracking game").transform; _game.localPosition=new(12,7,-4); _game.localRotation=Quaternion.Euler(5,35,-10);
            _ui=new GameObject("tracking UI").transform; _ui.localPosition=new(-2,1,3); _ui.localRotation=Quaternion.Euler(-8,-12,6);
            var gameCamera=new GameObject("game camera").AddComponent<Camera>(); gameCamera.transform.SetParent(_game,false); APIBus.MainCamera=gameCamera;
            var uiCamera=new GameObject("UI camera").AddComponent<Camera>(); uiCamera.transform.SetParent(_ui,false); APIBus.CockpitHudCamera=uiCamera;
            Map.Slot.localScale=Vector3.one*(.0007f*scale); Map.Slot.localRotation=Quaternion.Euler(12,-20,7); Map.Tick();
            for(var i=0;i<2;i++)TrackedControllers.Samples[i]=new ControllerSample {Valid=true,HasAxis=true,NativeFrame=true,Device=i,Axis=Vector2.zero,StickClick=false,Position=new(10,10,10)};
            _interaction=new GameObject("map interaction").AddComponent<CockpitMapInteraction>();
        }
        private void Place(int side,Vector2 point,float depth)
        {
            var sample=TrackedControllers.Samples[side];
            var local=new Vector3(point.x,point.y,-depth/Map.Slot.TransformVector(Vector3.forward).magnitude);
            sample.Position=_ui.InverseTransformPoint(Map.Slot.TransformPoint(local));
            TrackedControllers.Samples[side]=sample;
        }
        internal void Step(Vector2 point,float depth,float time)
        {
            Place(0,point,depth); Time.unscaledTime=time;
            typeof(CockpitMapInteraction).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(_interaction,null);
        }
        internal void Both(Vector2 point,float depth,float time)
        { Place(1,point,depth); Step(point,depth,time); }
        internal void Press(Vector2 point) { Step(point,.03f,0); Step(point,.03f,.02f); Step(point,0,.04f); }
        public void Dispose()
        {
            typeof(CockpitMapInteraction).GetMethod("OnDestroy",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(_interaction,null);
            Object.Destroy(_interaction.gameObject); Object.Destroy(_game.gameObject); Object.Destroy(_ui.gameObject);
            APIBus.MainCamera=APIBus.CockpitHudCamera=null; MotionFlightController.Instance=null; Map.Dispose();
        }
    }
}
