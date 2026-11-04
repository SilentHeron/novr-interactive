using NOVR.VrUi.HarmonyPatches;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

// Runs the production postfix. Unity/Harmony are boundary doubles; native
// GridLabels.LateUpdate's visibility decision is supplied as its input state.
var count = 0;
var anchor = new Transform();
var insideMap = new Transform();
var map = new DynamicMap { hudMapAnchor = anchor };
SceneSingleton<DynamicMap>.i = map;
var aircraft = new Text();
foreach (var vr in new[] { false, true })
foreach (var maximized in new[] { false, true })
foreach (var detached in new[] { false, true })
foreach (var nativeVisible in new[] { false, true })
{
    XRSettings.enabled = vr; DynamicMap.mapMaximized = maximized;
    aircraft.transform.parent = detached ? anchor : insideMap;
    aircraft.enabled = nativeVisible;
    GridAircraftReadoutPatch.Postfix(aircraft);
    Check(aircraft.enabled == (nativeVisible && !(vr && !maximized && detached)),
        "Only the detached minimized-map coordinate is hidden in VR");
}
XRSettings.enabled = true; DynamicMap.mapMaximized = false; aircraft.transform.parent = anchor;
for (var frame = 0; frame < 4; frame++)
{
    aircraft.enabled = true; // Native LateUpdate re-enables it with an aircraft.
    GridAircraftReadoutPatch.Postfix(aircraft);
    Check(!aircraft.enabled, "Native per-frame enabling cannot leak the detached coordinate");
}
DynamicMap.mapMaximized = true;
aircraft.transform.parent = insideMap; aircraft.enabled = true;
GridAircraftReadoutPatch.Postfix(aircraft);
Check(aircraft.enabled, "Opening full map restores native current-coordinate display immediately");
DynamicMap.mapMaximized = false; aircraft.transform.parent = anchor; aircraft.enabled = true;
XRSettings.enabled = false; GridAircraftReadoutPatch.Postfix(aircraft);
Check(aircraft.enabled, "Leaving VR retains the native desktop readout");
XRSettings.enabled = true;
SceneSingleton<DynamicMap>.i = null; aircraft.enabled = true;
GridAircraftReadoutPatch.Postfix(aircraft);
Check(aircraft.enabled, "No map during menu or scene handoff does not claim unrelated text");
SceneSingleton<DynamicMap>.i = map; map.hudMapAnchor = null; aircraft.transform.parent = null;
GridAircraftReadoutPatch.Postfix(aircraft);
Check(aircraft.enabled, "Missing anchor cannot match an unparented label during teardown");
GridAircraftReadoutPatch.Postfix(null!);
Check(true, "Destroyed or missing readout is safe");
Console.WriteLine($"Grid aircraft readout: {count} source-linked visibility checks passed.");
void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
