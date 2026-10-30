using System.Reflection;
using NOVR.VrUi.Native;
using UnityEngine;

var checks = 0;
var update = typeof(NativePanelTransition).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance)!;
void Assert(bool value) { if (!value) throw new Exception("UI lifecycle assertion failed"); }
void Near(float value, float expected) => Assert(Math.Abs(value - expected) < .0001f);
void Check(string name, Action body) { body(); checks++; Console.WriteLine("PASS " + name); }
void Tick(GameObject go, bool visible, int frames)
{
    for (var i = 0; i < frames; i++)
    {
        NativePanelTransition.SetVisible(go.Rect, visible);
        if (go.activeSelf) update.Invoke(go.GetComponent<NativePanelTransition>(), null);
    }
}
Check("Per-frame visibility requests and 500 reopen cycles preserve a nonzero anchor", () =>
{
    var go = new GameObject(new Vector2(-45, 20));
    NativePanelTransition.SetVisible(go.Rect, false, instant: true);
    for (var i = 0; i < 500; i++)
    {
        Tick(go, false, 20); Near(go.Rect.anchoredPosition.x, -17);
        Tick(go, true, 20); Near(go.Rect.anchoredPosition.x, -45);
        Near(go.Rect.anchoredPosition.y, 20);
        Near(go.GetComponent<CanvasGroup>()!.alpha, 1);
        Tick(go, false, 20);
    }
});
Check("Interrupted fades reverse smoothly and hidden panels stop intercepting input", () =>
{
    var go = new GameObject(new Vector2(50, -70));
    NativePanelTransition.SetVisible(go.Rect, false, instant: true);
    Tick(go, true, 4);
    var x = go.Rect.anchoredPosition.x;
    NativePanelTransition.SetVisible(go.Rect, false);
    Near(go.Rect.anchoredPosition.x, x);
    Assert(!go.GetComponent<CanvasGroup>()!.blocksRaycasts);
    Tick(go, false, 2);
    NativePanelTransition.SetVisible(go.Rect, true);
    Assert(go.GetComponent<CanvasGroup>()!.blocksRaycasts);
    Tick(go, true, 20); Near(go.Rect.anchoredPosition.x, 50);
    Tick(go, false, 20); Assert(!go.activeSelf);
    Near(go.GetComponent<CanvasGroup>()!.alpha, 0);
});
Check("Instant visibility and slow frames converge to the same fixed endpoints", () =>
{
    var go = new GameObject(new Vector2(13, 27));
    for (var i = 0; i < 100; i++) NativePanelTransition.SetVisible(go.Rect, false, instant: true);
    Near(go.Rect.anchoredPosition.x, 41);
    NativePanelTransition.SetVisible(go.Rect, true, instant: true);
    Near(go.Rect.anchoredPosition.x, 13);
    Time.unscaledDeltaTime = .5f;
    Tick(go, false, 1); Near(go.Rect.anchoredPosition.x, 41); Assert(!go.activeSelf);
    Tick(go, true, 1); Near(go.Rect.anchoredPosition.x, 13); Assert(go.activeSelf);
    Time.unscaledDeltaTime = 1f / 90;
});
Check("A burst of launch events admits one operation until it completes", () =>
{
    var gate = new MissionLaunchGate();
    var starts = 0;
    for (var i = 0; i < 200; i++) if (gate.TryBegin(false)) starts++;
    Assert(starts == 1 && gate.IsBusy);
    // The gate remains owned even when panels hide/show; only completion releases it.
    Assert(!gate.TryBegin(false));
    gate.Finish();
    Assert(!gate.IsBusy && gate.TryBegin(false));
});
Check("Failure permits retry but an already-active session never admits a second host", () =>
{
    var gate = new MissionLaunchGate();
    Assert(!gate.TryBegin(true) && !gate.IsBusy);
    Assert(gate.TryBegin(false));
    gate.Finish(); // load failure or cancellation before a session opened
    Assert(gate.TryBegin(false));
    gate.Finish(); // host opened successfully
    Assert(!gate.TryBegin(true) && !gate.IsBusy);
    Assert(gate.TryBegin(false)); // later return to menu after that session ends
});
Console.WriteLine($"{checks} UI lifecycle checks passed (offline doubles; no game inputs).");
