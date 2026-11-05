using System.Reflection;
using NOVR.VrCamera;
using UnityEngine;
using UnityEngine.XR;

internal static class SelectionViewChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        var prefix = typeof(CameraSelectionStatePatch).GetNestedType("UpdateOrbit", BindingFlags.NonPublic)!
            .GetMethod("Prefix", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool Native(CameraSelectionState? state, CameraStateManager? camera) =>
            (bool)prefix.Invoke(null, new object?[] { state, camera })!;
        bool Same(Quaternion a, Quaternion b) => Math.Abs(System.Numerics.Quaternion.Dot(a.Value, b.Value)) > .99999f;
        var target = new Transform { rotation = Quaternion.Euler(0, 80, 0) };
        var selected = new CameraSelectionState(target);
        var camera = new CameraStateManager();
        var initial = camera.cameraPivot!.rotation;
        XRSettings.enabled = false;
        Check(Native(selected, camera) && Same(camera.cameraPivot.rotation, initial), "Non-VR selection retains native auto-orbit and pivot orientation");
        XRSettings.enabled = true;
        Check(!Native(selected, camera) && Same(camera.cameraPivot.rotation, Quaternion.Euler(0, 125, 0)),
            "VR selection uses the established fixed target-relative 45-degree vantage");
        var fixedView = camera.cameraPivot.rotation;
        for (var i = 0; i < 100; i++) { Time.frameCount++; Native(selected, camera); }
        Check(Same(camera.cameraPivot.rotation, fixedView), "Selection does not accumulate automatic orbit across frames");
        target.rotation = Quaternion.Euler(15, -20, 5);
        Check(!Native(selected, camera) && Same(camera.cameraPivot.rotation, target.rotation * Quaternion.Euler(0, 45, 0)),
            "The fixed vantage follows a changed preview target orientation");
        var saved = camera.cameraPivot.rotation;
        Check(Native(new CameraSelectionState(null), camera) && Same(camera.cameraPivot.rotation, saved), "Missing preview target safely falls through without moving the pivot");
        target.Destroyed = true;
        Check(Native(selected, camera), "Destroyed Unity target safely falls through");
        target.Destroyed = false;
        camera.cameraPivot.Destroyed = true;
        Check(Native(selected, camera), "Destroyed Unity camera pivot safely falls through");
        camera.cameraPivot = null;
        Check(Native(selected, camera) && Native(selected, null) && Native(null, new CameraStateManager()),
            "Incomplete selection scene and null native instances are harmless");
        XRSettings.enabled = false;
        return checks;
    }
}
