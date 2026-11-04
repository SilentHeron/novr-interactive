using System.Reflection;
using UnityEngine;
using Q = System.Numerics.Quaternion;

var patch = typeof(NOVR.VrUi.HarmonyPatches.ThreatItemVectorLineRotationSafePatch)
    .GetNestedType("AlignVectorLinePatch", BindingFlags.NonPublic)!
    .GetMethod("Prefix", BindingFlags.NonPublic | BindingFlags.Static)!;
var passed = 0;
void Run(ThreatItem item)
{
    if (!Equals(patch.Invoke(null, [item]), false))
        throw new Exception("Native screen-space alignment was not skipped.");
}
void Near(float actual, float expected, float tolerance = .002f)
{
    if (!float.IsFinite(actual) || Math.Abs(actual - expected) > tolerance)
        throw new Exception($"Expected {expected}, got {actual}.");
    passed++;
}
void True(bool condition) { if (!condition) throw new Exception("Assertion failed."); passed++; }

// Invoke the real Harmony prefix with transform doubles. Its native prefab is
// 2 pixels wide, 1 pixel tall, and pivoted at its bottom center.
foreach (var worldScale in new[] { .000455f, .003f, 1f })
    foreach (var zoom in new[] { .5f, 1f, 2f, 8f })
        foreach (var angle in new[] { 0f, .7f })
        {
            var map = new DynamicMap();
            SceneSingleton<DynamicMap>.i = map;
            map.transform.localScale = new(worldScale, worldScale, worldScale);
            map.transform.localPosition = new(.32f, -.21f, .38f);
            map.transform.rotation = Q.CreateFromYawPitchRoll(angle, .35f, -.2f);
            map.iconLayer.transform.parent = map.transform;
            map.iconLayer.transform.localScale = new(zoom, zoom, zoom);
            map.iconLayer.transform.rotation = Q.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ, -.6f);
            var player = new Transform { parent = map.iconLayer.transform, localPosition = new(14, -20, 0) };
            var missile = new Transform { parent = map.iconLayer.transform, localPosition = new(44, 20, 0) };
            var line = new GameObject();
            line.transform.parent = map.iconLayer.transform;
            Run(new ThreatItem(missile, player, line));
            True(line.activeSelf);
            Near(line.transform.localScale.x, 1 / zoom);
            Near(line.transform.localScale.y, 50 + 1 / zoom);
            Near((line.transform.position - player.position).magnitude, 0);
            var endLocal = map.iconLayer.transform.InverseTransformPoint(line.transform.TransformPoint(new(0, 1, 0)));
            // End is exactly one unzoomed UI pixel past the missile icon, as in native.
            Near(endLocal.x, 44 + .6f / zoom);
            Near(endLocal.y, 20 + .8f / zoom);
            Near(line.transform.TransformVector(new(2, 0, 0)).magnitude, 2 * worldScale);
            // Missing/destroyed icons or map must immediately stop stale red geometry.
            Run(new ThreatItem(null, player, line));
            True(!line.activeSelf);
            line.SetActive(true);
            Run(new ThreatItem(missile, null, line));
            True(!line.activeSelf);
            line.SetActive(true);
            SceneSingleton<DynamicMap>.i = null!;
            Run(new ThreatItem(missile, player, line));
            True(!line.activeSelf);
            SceneSingleton<DynamicMap>.i = map;
            line.SetActive(true);
            missile.localPosition = new(float.NaN, 0, 0);
            Run(new ThreatItem(missile, player, line));
            True(!line.activeSelf);
        }
Console.WriteLine($"{passed} source-linked missile vector assertions passed (transform doubles; no GPU/game launch).");
