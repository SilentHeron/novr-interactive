using System.Reflection;
using NOVR;
using NOVR.VrUi.SpecialBehavior;
using NOVR.VrUi.HarmonyPatches;
using UnityEngine;
using UnityEngine.UI;
using U = UnityEngine.Object;

var count=0;
void Check(bool condition,string message){count++;if(!condition)throw new Exception(message);}
T Component<T>(string name)where T:Component,new()=>new GameObject(name).AddComponent<T>();
void Call(object instance,string method)=>instance.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(instance,null);
Transform? Root(FlightHud hud)=>hud.transform.Find("NOVR_PitchCompassSlices");
void Tick(PitchCompassBehavior behavior)=>Call(behavior,"LateUpdate");
RawImage Source(FlightHud hud)
{
    var source=Component<RawImage>("Native pitch compass");source.transform.SetParent(hud.transform,false);
    source.rectTransform.sizeDelta=new(300,126);source.uvRect=new(0,0,1,.126f);source.texture=new Texture();source.material=new Material();return source;
}
APIBus.MainCamera=Component<Camera>("World camera");APIBus.CockpitHudCamera=Component<Camera>("HUD camera");
var hud=Component<FlightHud>("HUD without parent");var behavior=hud.gameObject.AddComponent<PitchCompassBehavior>();
Call(behavior,"OnEnable");Tick(behavior);Check(Root(hud)==null,"HUD missing its serialized source waits without throwing");
var source=Source(hud);hud.Bind(source,null);Tick(behavior);
var firstRoot=Root(hud)!;
Check(firstRoot!=null&&firstRoot.childCount==72,"Source creates one complete 36-step mirrored ladder before aircraft initialization");
Check(!firstRoot!.gameObject.activeSelf&&source.enabled,"No cockpit leaves native source unmodified and slices hidden");
var cockpit=Component<Transform>("Cockpit");hud.Bind(source,cockpit);Tick(behavior);
Check(firstRoot.gameObject.activeSelf&&!source.enabled,"Late SetAircraft initializes visible replacement without a re-enable");
Check(firstRoot.position==APIBus.CockpitHudCamera.transform.position,"Ladder is positioned at current HUD camera");
for(var i=0;i<20;i++)Tick(behavior);
Check(ReferenceEquals(firstRoot,Root(hud))&&InstrumentBrightness.Added==1,"Steady HUD never rebuilds slices or attaches duplicate brightness owners");
Check(firstRoot.GetChild(1).localScale==Vector3.one*.304f,"Existing focused scale is preserved");
ModConfiguration.Instance.FocusedFlightHud.Value=false;Tick(behavior);
Check(firstRoot.Children.All(x=>x.gameObject.activeSelf)&&firstRoot.GetChild(0).localScale==Vector3.one*.8f,"Unfocused ladder retains full-angle visibility and original size");
ModConfiguration.Instance.FocusedFlightHud.Value=true;
// Missing native binding must not leave the previous aircraft's transform live.
hud.Bind(source,null);Tick(behavior);Check(!firstRoot.gameObject.activeSelf&&source.enabled,"Null cockpit hides stale slices and restores source");
hud.Bind(source,cockpit);Tick(behavior);U.Destroy(cockpit);Tick(behavior);
Check(!firstRoot.gameObject.activeSelf&&source.enabled,"Destroyed Unity cockpit wrapper also hides stale slices");
cockpit=Component<Transform>("Replacement cockpit");hud.Bind(source,cockpit);Tick(behavior);
Check(firstRoot.gameObject.activeSelf&&!source.enabled,"Replacement aircraft reuses valid slices and resumes suppression");
var camera=APIBus.CockpitHudCamera;APIBus.CockpitHudCamera=null;Tick(behavior);
Check(!firstRoot.gameObject.activeSelf&&source.enabled,"Missing HUD camera suspends replacement without dereference");
APIBus.CockpitHudCamera=camera;Tick(behavior);U.Destroy(camera);Tick(behavior);
Check(!firstRoot.gameObject.activeSelf&&source.enabled,"Destroyed HUD camera is treated as missing");
APIBus.CockpitHudCamera=Component<Camera>("Replacement HUD camera");Tick(behavior);
source.gameObject.SetActive(false);Tick(behavior);Check(!firstRoot.gameObject.activeSelf&&source.enabled,"Inactive native pitch subtree cannot leave slices visible elsewhere");
source.gameObject.SetActive(true);Tick(behavior);
Call(behavior,"OnDisable");Check(!firstRoot.gameObject.activeSelf&&source.enabled,"Disabling component restores original source visibility");
source.enabled=false;Call(behavior,"OnEnable");Tick(behavior);
Check(!source.enabled&&!firstRoot.gameObject.activeSelf,"A source already hidden by the game stays hidden on re-enable");
source.enabled=true;Tick(behavior);Check(!source.enabled&&firstRoot.gameObject.activeSelf,"Later native visibility resumes without building again");
source.texture=new Texture();Tick(behavior);
var changedRoot=Root(hud)!;Check(firstRoot==null&&!ReferenceEquals(firstRoot,changedRoot)&&changedRoot.childCount==72,"Changed texture releases old slices and builds exactly one replacement");
var secondSource=Source(hud);hud.Bind(secondSource,cockpit);Tick(behavior);
Check(source.enabled&&!secondSource.enabled&&changedRoot==null,"Changed RawImage restores old source before borrowing replacement");
var replacementRoot=Root(hud)!;U.Destroy(secondSource.texture);Tick(behavior);
Check(replacementRoot==null&&Root(hud)==null&&secondSource.enabled,"Destroyed texture releases slices and restores its source");
secondSource.texture=new Texture();Tick(behavior);replacementRoot=Root(hud)!;
Check(replacementRoot!=null&&!secondSource.enabled,"Reloaded texture rebuilds the replacement");
U.Destroy(secondSource.texture);U.Destroy(secondSource);Tick(behavior);
Check(replacementRoot==null&&Root(hud)==null,"Simultaneously destroyed source and texture release their generated hierarchy");
var incomplete=Source(hud);incomplete.uvRect=new(0,0,1,0);hud.Bind(incomplete,cockpit);Tick(behavior);
Check(Root(hud)==null&&incomplete.enabled,"Zero-height UV cannot create infinite geometry");
incomplete.uvRect=new(0,0,1,.126f);Tick(behavior);Check(Root(hud)!=null&&!incomplete.enabled,"A completed source retries successfully after incomplete UV initialization");
cockpit.rotation=Quaternion.AngleAxis(90,Vector3.right);Tick(behavior);
Check(float.IsFinite(Root(hud)!.rotation.Value.W),"Vertical pitch never emits zero-vector LookRotation");
Call(behavior,"OnDestroy");Check(Root(hud)==null&&incomplete.enabled,"Destroying only the mod component removes owned slices and restores native source");

var overlay=Component<AirbaseOverlay>("Airbase overlay");
var patch=typeof(AirbaseOverlayViewPositionPatch).GetNestedType("LateUpdatePatch",BindingFlags.NonPublic)!.GetMethod("Postfix",BindingFlags.Static|BindingFlags.NonPublic)!;
void AirbaseTick()=>patch.Invoke(null,new object[]{overlay});
void ResetProjection(){NavigationHudProjection.Projected.Clear();VrHudProjection.Projected.Clear();VrHudProjection.Lines.Clear();}
SceneSingleton<CombatHUD>.i=null;AirbaseTick();Check(NavigationHudProjection.Projected.Count==0,"Absent CombatHUD is harmless during scene unload");
var combat=Component<CombatHUD>("Combat HUD");SceneSingleton<CombatHUD>.i=combat;U.Destroy(combat);AirbaseTick();
Check(NavigationHudProjection.Projected.Count==0,"Destroyed CombatHUD wrapper is harmless");
combat=Component<CombatHUD>("New combat HUD");SceneSingleton<CombatHUD>.i=combat;AirbaseTick();Check(NavigationHudProjection.Projected.Count==0,"No aircraft leaves native overlay untouched");
var aircraft=Component<Aircraft>("Aircraft");combat.aircraft=aircraft;aircraft.rb=Component<Rigidbody>("Flight rigidbody");aircraft.rb.velocity=new(0,0,100);
var pilot=Component<Pilot>("Pilot");aircraft.pilots=new[]{pilot};
overlay.airbaseMarker=Component<Image>("Marker");overlay.airbaseLabel=Component<TMPro.TMP_Text>("Label");
overlay.nearestAirbase=Component<Airbase>("Airbase");overlay.nearestAirbase.center=Component<Transform>("Airbase center");overlay.nearestAirbase.center.position=new(300,0,500);
var runway=new Airbase.Runway{Start=Component<Transform>("Start"),End=Component<Transform>("End")};runway.Start.position=new(100,0,200);runway.End.position=new(100,0,1200);
overlay.taxiingToRunway=true;
foreach(var reverse in new[]{false,true})
{
    ResetProjection();overlay.runwayUsage=new Airbase.Runway.RunwayUsage(runway,reverse);pilot.flightInfo!.HasTakenOff=false;AirbaseTick();
    Check(NavigationHudProjection.Projected.Single()==overlay.runwayUsage.Value.GetStart().position,"Taxi guidance uses native entry threshold in each runway direction");
    Check(overlay.airbaseMarker.transform.position==overlay.runwayUsage.Value.GetStart().position,"Marker remains on the native taxi location after projection");
    pilot.flightInfo.HasTakenOff=true;ResetProjection();AirbaseTick();
    Check(NavigationHudProjection.Projected.Single()==overlay.nearestAirbase.center.position,"Airborne airbase marker retains native airbase center");
}
foreach(var missing in new[]{0,1,2,3})
{
    aircraft.pilots=missing==0?null:missing==1?Array.Empty<Pilot>():new[]{pilot};pilot.flightInfo=missing==3?null:new FlightInfo();
    if(missing==2)U.Destroy(pilot);ResetProjection();AirbaseTick();Check(NavigationHudProjection.Projected.Count==0,"Missing pilot initialization does not index or dereference stale pilot data");
    if(missing==2)pilot=Component<Pilot>("New pilot");
}
aircraft.pilots=new[]{pilot};pilot.flightInfo=new(){HasTakenOff=true};overlay.landing=true;
overlay.glideslope=Component<Image>("Glideslope");overlay.glideslopeAimPoint=Component<Image>("Glideslope aim");
overlay.runwayBorders=new Image?[4];for(var i=0;i<4;i++)overlay.runwayBorders[i]=Component<Image>("Border"+i);
foreach(var reverse in new[]{false,true})
{
    overlay.runwayUsage=new Airbase.Runway.RunwayUsage(runway,reverse);GlobalPosition.Origin=new(80000,-2400,-150000);ResetProjection();AirbaseTick();
    var expected=overlay.runwayUsage.Value.GetTouchdownPoint().ToLocalPosition();
    Check(VrHudProjection.Projected.Contains(expected)&&VrHudProjection.Lines.Last().start==expected,"Glideslope uses native inset touchdown in local coordinates for both directions");
    Check(VrHudProjection.Lines.Count==5,"Four runway edges and one glideslope remain projected");
    Check(Math.Abs(runway.LastAimDistanceAndTime.x-(expected-aircraft.transform.position).magnitude*.9f)<.001f,"Native aimpoint distance uses touchdown rather than opposite endpoint");
}
var borders=typeof(AirbaseOverlayViewPositionPatch).GetMethod("GetBehaviours",BindingFlags.Static|BindingFlags.NonPublic)!;
Check(ReferenceEquals(borders.Invoke(null,new object?[]{overlay.runwayBorders}),overlay.runwayBorders),"Native image array is reused rather than allocated per frame");
overlay.runwayBorders[1]=null;ResetProjection();AirbaseTick();Check(VrHudProjection.Lines.Count==4,"Missing individual runway edge is skipped safely");
VrHudProjection.Succeeds=false;ResetProjection();AirbaseTick();
Check(overlay.runwayBorders.Where(x=>x!=null).All(x=>!x!.enabled)&&!overlay.glideslope.enabled&&!overlay.glideslopeAimPoint.enabled,"Projection failure hides affected native lines and aim point");
VrHudProjection.Succeeds=true;overlay.glideslope.enabled=true;overlay.glideslopeAimPoint.enabled=true;ResetProjection();AirbaseTick();
Check(overlay.runwayBorders.Where(x=>x!=null).All(x=>x!.enabled)&&VrHudProjection.Lines.Count==4,"Native next-frame visibility can recover after projection failure");
var oldStart=runway.Start;U.Destroy(oldStart);ResetProjection();AirbaseTick();Check(VrHudProjection.Lines.Count==0,"Destroyed runway endpoint cannot reach line or glide calculations");
runway.Start=Component<Transform>("Reloaded start");runway.Start.position=new(100,0,200);
U.Destroy(aircraft.rb);ResetProjection();AirbaseTick();Check(VrHudProjection.Lines.Count==3,"Lost aircraft rigidbody stops only glide projection while valid runway borders remain");
Console.WriteLine($"{count} source-linked HUD projection lifecycle checks passed (Unity/game boundary doubles; no rendered/headset claim).");
