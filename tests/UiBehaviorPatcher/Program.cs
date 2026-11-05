using System.Reflection;
using HarmonyLib;
using NOVR.VrUi;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.SceneManagement;

var checks = 0;
void Check(bool valid, string message) { if (!valid) throw new Exception(message); checks++; }

// A replacement manager may call DoPatching again. Successful constructor
// detours must not stack, including after one of the others fails to register.
Harmony.FailOnce = typeof(MessageUI);
UIBehaviorPatcher.DoPatching();
UIBehaviorPatcher.DoPatching();
UIBehaviorPatcher.DoPatching();
Check(Harmony.Calls.Count == 5 && Harmony.Calls.Values.All(x => x == 1), "Constructor patches are idempotent and independently retryable");

using (var world = new World())
{
    var pause = world.Native<NuclearOption.UI.LeaderboardMenu>("GameMenu");
    var unrelated = new GameObject("GameMenu");
    world.Tick();
    World.Queue(pause);
    world.Tick();
    Check(pause.gameObject.Count<VrGameMenuBackdrop>() == 1 && unrelated.Get<VrGameMenuBackdrop>() == null &&
        pause.gameObject.activeSelf && pause.gameObject.Deactivations == 0,
        "Only the typed native pause menu receives one backdrop adapter without disabling its content");
}

using (var world = new World())
{
    Check(SceneManager.Subscribers == 1 && world.Worker.BaseEnables == 1, "Active worker subscribes and preserves inherited enable registration");
    world.Tick();
    var scans = Resources.Scans;
    for (var i = 0; i < 20; i++)
        world.Tick();
    Check(Resources.Scans == scans, "Idle updates do not rescan all loaded resources");
    world.Disable();
    Check(SceneManager.Subscribers == 0 && world.Worker.BaseDisables == 1, "Disabled worker releases scene and inherited subscriptions");
    var canvas = new GameObject("MainCanvas");
    SceneManager.Load();
    world.Enable();
    world.Tick();
    Check(canvas.Get<NOVRMainMenuBehavior>() != null, "Re-enable catches a scene loaded while unsubscribed");
}
Check(SceneManager.Subscribers == 0, "Destroyed/disabled worker leaves no scene callback behind");

using (var world = new World())
{
    var destroyed = world.Native<FlightHud>("removed");
    destroyed.Destroyed = true;
    var unnamed = world.Native<StatusDisplay>("");
    var normal = world.Native<MessageUI>("messages");
    var subclass = world.Native<ModFlightHud>("modded HUD");
    Time.timeScale = 0;
    world.Tick();
    Check(unnamed.gameObject.Get<NOVRStatusDisplayBehavior>() != null, "An unnamed loaded object still receives its behavior");
    Check(normal.gameObject.Get<NOVRGameplayUIBehaviour>() != null, "Destroyed/unnamed entries do not starve following UI work");
    Check(subclass.gameObject.Get<NOVRFlightHudBehavior>() != null, "A constructor for a mapped base type safely handles mod subclasses");
    Check(destroyed.gameObject.Get<NOVRFlightHudBehavior>() == null, "Destroyed components are discarded without accessing native properties");
    Check(Time.timeScale == 0, "UI work completes through Update while physics time is paused");
    Check(normal.gameObject.activeSelf && normal.gameObject.Deactivations == 0, "Attaching behavior never disables the native UI object");
    var behavior = normal.gameObject.Get<NOVRGameplayUIBehaviour>()!;
    normal.gameObject.StartPending();
    world.Tick();
    normal.gameObject.StartPending();
    Check(behavior.Awakes == 1 && behavior.Enables == 1 && behavior.Starts == 1, "Normal AddComponent lifecycle runs Awake, OnEnable and Start exactly once");
    World.Queue(normal);
    World.Queue(normal);
    world.Tick();
    Check(normal.gameObject.Count<NOVRGameplayUIBehaviour>() == 1, "Repeated requests do not duplicate behaviors");
}

using (var world = new World())
{
    var asset = new GameObject("MainCanvas") { scene = new Scene(false, false) };
    var unloaded = new GameObject("MenuCanvas") { scene = new Scene(true, false) };
    var inactive = new GameObject("MenuCanvas") { activeSelf = false };
    world.Tick();
    Check(asset.Get<NOVRMainMenuBehavior>() == null && unloaded.Get<NOVRGameplayUIBehaviour>() == null, "Resource scan never patches prefab assets or unloaded scenes");
    var behavior = inactive.Get<NOVRGameplayUIBehaviour>()!;
    Check(behavior != null && !inactive.activeSelf && behavior.Enables == 0, "Inactive scene UI stays inactive while waiting for its normal lifecycle");
    inactive.SetActive(true);
    inactive.StartPending();
    Check(behavior!.Awakes == 1 && behavior.Enables == 1 && behavior.Starts == 1, "Native activation initializes an attached inactive behavior normally");
    SceneManager.Load();
    world.Tick();
    Check(inactive.Count<NOVRGameplayUIBehaviour>() == 1, "Repeated scene scans remain idempotent");
}

using (var world = new World())
{
    var loading = world.Native<ModFlightHud>("unnamed loading HUD");
    loading.gameObject.scene = new Scene(true, false);
    var alreadyLoaded = new GameObject("created before patch registration").AddComponent<StatusDisplay>();
    var prefab = new GameObject("HUD asset") { scene = new Scene(false, false) };
    prefab.AddComponent<FlightHud>();
    world.Tick();
    world.Tick();
    Check(loading.gameObject.Get<NOVRFlightHudBehavior>() == null, "Constructor notifications do not patch a still-loading scene");
    Check(alreadyLoaded.gameObject.Get<NOVRStatusDisplayBehavior>() != null, "Scene scan recovers typed UI created before constructor registration");
    loading.gameObject.scene = new Scene(true, true);
    SceneManager.Load();
    world.Tick();
    world.Tick();
    Check(loading.gameObject.Get<NOVRFlightHudBehavior>() != null, "Scene completion recovers a typed mod HUD skipped before loading finished");
    Check(prefab.Get<NOVRFlightHudBehavior>() == null, "Typed recovery still excludes prefab assets");
}

using (var world = new World())
{
    MessageUI? later = null;
    var first = world.Native<FlightHud>("first");
    GameObject.AfterAdd = (_, type) => { if (type == typeof(NOVRFlightHudBehavior)) later = world.Native<MessageUI>("enqueued by Awake"); };
    world.Tick();
    Check(first.gameObject.Get<NOVRFlightHudBehavior>() != null && later != null, "Behavior initialization can enqueue native UI safely");
    Check(later!.gameObject.Get<NOVRGameplayUIBehaviour>() == null, "Reentrant work stays queued for the next update");
    world.Tick();
    Check(later.gameObject.Count<NOVRGameplayUIBehaviour>() == 1, "Snapshot draining neither loses nor duplicates reentrant work");
}

using (var world = new World())
{
    var delayed = new StatusDisplay();
    World.Queue(delayed); // No Unity object access inside constructor hook.
    world.Tick();
    delayed.gameObject = new GameObject("attached after first attempt");
    delayed.Bind();
    world.Tick();
    Check(delayed.gameObject.Get<NOVRStatusDisplayBehavior>() != null, "A briefly unbound constructor request can retry after attachment");
}

using (var world = new World())
{
    var failing = world.Native<FlightHud>("temporary failure");
    var independent = world.Native<MessageUI>("independent");
    var attempts = 0;
    GameObject.BeforeAdd = (go, type) => { if (go == failing.gameObject && type == typeof(NOVRFlightHudBehavior) && ++attempts < 3) throw new Exception("not ready"); };
    world.Tick();
    Check(independent.gameObject.Get<NOVRGameplayUIBehaviour>() != null, "One AddComponent failure does not block independent UI");
    world.Tick();
    world.Tick();
    world.Tick();
    Check(attempts == 3 && failing.gameObject.Count<NOVRFlightHudBehavior>() == 1, "Transient attachment failure retries and then stops after success");
    Check(Debug.Warnings.Count == 1, "Repeated transient failure logs once, not once per frame");
}

using (var world = new World())
{
    var failing = world.Native<FlightHud>("permanent failure");
    var attempts = 0;
    GameObject.BeforeAdd = (go, _) => { if (go == failing.gameObject) { attempts++; throw new Exception("unsupported"); } };
    for (var i = 0; i < 12; i++)
        world.Tick();
    Check(attempts == 3 && Debug.Warnings.Count == 2, "Permanent failure stops after three attempts with first/final diagnostics");
    Check(failing.gameObject.activeSelf, "Exhausted retries never leave a native object hidden");
}

using (var world = new World())
{
    var partial = world.Native<StatusDisplay>("partial add");
    var attempts = 0;
    GameObject.AfterAdd = (go, type) => { if (go == partial.gameObject && type == typeof(NOVRStatusDisplayBehavior)) { attempts++; throw new Exception("after attachment"); } };
    world.Tick();
    world.Tick();
    Check(attempts == 1 && partial.gameObject.Count<NOVRStatusDisplayBehavior>() == 1, "A failed callback after attachment cannot create duplicate behaviors on retry");
}

using (var world = new World())
{
    var first = world.Native<FlightHud>("first");
    var later = world.Native<MessageUI>("later");
    GameObject.AfterAdd = (_, type) => { if (type == typeof(NOVRFlightHudBehavior)) world.Disable(); };
    world.Tick();
    Check(first.gameObject.activeSelf && later.gameObject.Get<NOVRGameplayUIBehaviour>() == null, "Disabling the worker during a callback preserves the unprocessed work");
    GameObject.AfterAdd = null;
    world.Enable();
    world.Tick();
    Check(later.gameObject.Get<NOVRGameplayUIBehaviour>() != null, "Preserved work resumes on re-enable without toggling native objects");
}

using (var world = new World())
{
    var sources = Enumerable.Range(0, 128).Select(_ => new StatusDisplay()).ToArray();
    Parallel.ForEach(sources, World.Queue);
    for (var i = 0; i < sources.Length; i++)
    {
        sources[i].gameObject = new GameObject($"deserialized {i}");
        sources[i].Bind();
    }
    world.Tick();
    Check(sources.All(x => x.gameObject.Count<NOVRStatusDisplayBehavior>() == 1), "Concurrent constructor notifications are safely drained on the UI thread");
    var other = new GameObject("second worker").AddComponent<UIBehaviorPatcher>();
    World.Queue(sources[0]);
    Life.Invoke(other, "Update");
    world.Tick();
    Check(sources[0].gameObject.Count<NOVRStatusDisplayBehavior>() == 1, "Overlapping managers cannot duplicate a behavior");
    other.enabled = false;
    Life.Invoke(other, "OnDisable");
}
Check(SceneManager.Subscribers == 0, "All scene subscriptions are released after lifecycle scenarios");
Console.WriteLine($"UI behavior patcher: {checks} lifecycle/queue checks passed.");

sealed class World : IDisposable
{
    private static readonly MethodInfo Postfix = typeof(UIBehaviorPatcher).GetMethod("UniversalPostfix", BindingFlags.Static | BindingFlags.NonPublic)!;
    public readonly UIBehaviorPatcher Worker;
    public World()
    {
        Resources.Objects.Clear();
        Debug.Warnings.Clear();
        GameObject.BeforeAdd = GameObject.AfterAdd = null;
        Time.timeScale = 1;
        Worker = new GameObject("patcher worker").AddComponent<UIBehaviorPatcher>();
    }
    public static void Queue(object source) => Postfix.Invoke(null, new[] { source });
    public T Native<T>(string name) where T : Component, new()
    {
        var source = new GameObject(name).AddComponent<T>();
        Queue(source);
        return source;
    }
    public void Tick() => Life.Invoke(Worker, "Update");
    public void Disable()
    {
        if (!Worker.enabled)
            return;
        Worker.enabled = false;
        Life.Invoke(Worker, "OnDisable");
    }
    public void Enable()
    {
        if (Worker.enabled)
            return;
        Worker.enabled = true;
        Life.Invoke(Worker, "OnEnable");
    }
    public void Dispose()
    {
        Disable();
        GameObject.BeforeAdd = GameObject.AfterAdd = null;
    }
}
