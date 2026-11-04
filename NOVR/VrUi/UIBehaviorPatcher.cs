using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NOVR.VrUi;

public class UIBehaviorPatcher : NOVRBehaviour
{
    private static readonly Dictionary<Type, Type> PatchMap = new()
    {
        { typeof(FlightHud), typeof(NOVRFlightHudBehavior) },
        { typeof(GameplayUI), typeof(NOVRGameplayUIBehaviour) },
        { typeof(MessageUI), typeof(NOVRGameplayUIBehaviour) },
        { typeof(StatusDisplay), typeof(NOVRStatusDisplayBehavior) }
    };

    private static readonly Dictionary<string, Type> ScenePatchMap = new()
    {
        { "MainCanvas", typeof(NOVRMainMenuBehavior) },
        { "MenuCanvas", typeof(NOVRGameplayUIBehaviour) },
        { "MaximizedMapCanvas", typeof(NOVRGameplayUIBehaviour) },
        { "BlackoutCanvas", typeof(NOVRBlackoutCanvasBehavior) },
        { "SceneEssentials", typeof(PositionZeroBehavior) },
        { "Canvas", typeof(PositionZeroBehavior) }
    };

    private readonly struct PendingAddition
    {
        internal readonly Type Behavior;
        internal readonly int Failures;
        internal PendingAddition(Type behavior, int failures = 0) { Behavior = behavior; Failures = failures; }
    }

    private sealed class ComponentReferenceComparer : IEqualityComparer<Component>
    {
        public bool Equals(Component? x, Component? y) => ReferenceEquals(x, y);
        public int GetHashCode(Component obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private static readonly object PendingLock = new();
    // Unity's instance ID may not be assigned inside a managed constructor yet.
    // CLR identity stays stable before binding and after native destruction.
    private static readonly Dictionary<Component, PendingAddition> Pending = new(new ComponentReferenceComparer());
    private static readonly HashSet<ConstructorInfo> PatchedConstructors = new();
    private readonly List<KeyValuePair<Component, PendingAddition>> _batch = new();
    private bool _scanSceneObjects;

    protected override void OnEnable()
    {
        base.OnEnable();
        SceneManager.sceneLoaded += SceneLoaded;
        // Also covers scenes loaded while this component was disabled.
        _scanSceneObjects = true;
    }

    protected override void OnDisable()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        base.OnDisable();
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode) => _scanSceneObjects = true;

    public static void DoPatching()
    {
        var harmony = new Harmony("novr.vrui.behavioradder");
        var postfix = new HarmonyMethod(typeof(UIBehaviorPatcher).GetMethod(nameof(UniversalPostfix), BindingFlags.Static | BindingFlags.NonPublic)!);
        foreach (var target in PatchMap.Keys)
        {
            var ctor = target.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (ctor == null || PatchedConstructors.Contains(ctor)) continue;
            try
            {
                harmony.Patch(ctor, postfix: postfix);
                PatchedConstructors.Add(ctor);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[NOVR] Could not register UI behavior for {target.Name}: {exception.Message}");
            }
        }
    }

    private static void UniversalPostfix(object __instance)
    {
        if (__instance is not Component component) return;
        // A patched base constructor can execute for a game/mod subclass too.
        // Avoid native object access here: Unity may still be deserializing it.
        for (Type? type = component.GetType(); type != null; type = type.BaseType)
            if (PatchMap.TryGetValue(type, out var behavior))
            {
                Enqueue(component, new PendingAddition(behavior));
                return;
            }
    }

    private static void Enqueue(Component component, PendingAddition addition)
    {
        lock (PendingLock)
        {
            if (Pending.TryGetValue(component, out var existing) && existing.Failures > addition.Failures) return;
            Pending[component] = addition;
        }
    }

    private void Update()
    {
        // UI must still initialize in paused menus. Drain a snapshot: Awake or
        // OnEnable of a new behavior may enqueue more native UI components.
        lock (PendingLock)
        {
            foreach (var item in Pending) _batch.Add(item);
            Pending.Clear();
        }
        for (var i = 0; i < _batch.Count; i++)
        {
            var item = _batch[i];
            if (isActiveAndEnabled) AddBehavior(item.Key, item.Value);
            else Enqueue(item.Key, item.Value);
        }
        _batch.Clear();
        if (!isActiveAndEnabled || !_scanSceneObjects) return;
        _scanSceneObjects = false;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!isActiveAndEnabled) { _scanSceneObjects = true; break; }
            // FindObjectsOfTypeAll includes prefab/assets; never mutate those.
            if (go == null || !go.scene.IsValid() || !go.scene.isLoaded) continue;
            if (ScenePatchMap.TryGetValue(go.name, out var behavior))
                AddBehavior(go.transform, new PendingAddition(behavior));
            // Constructor notifications can precede scene readiness, or even
            // registration of the constructor patch. Recover those components
            // after loading without keeping prefab assets in the retry queue.
            foreach (var mapping in PatchMap)
                if (go.TryGetComponent(mapping.Key, out Component source) &&
                    !go.TryGetComponent(mapping.Value, out Component _))
                    Enqueue(source, new PendingAddition(mapping.Value));
        }
    }

    private static void AddBehavior(Component source, PendingAddition addition)
    {
        if (source is null) return;
        try
        {
            if (source == null)
            {
                // A destroyed bound object is finished; a constructor whose
                // native ID is still zero gets the same bounded readiness retry.
                if (source!.GetInstanceID() != 0) return;
                throw new InvalidOperationException("UI component is not attached yet");
            }
            var go = source.gameObject;
            if (go == null) throw new InvalidOperationException("UI object is not attached yet");
            if (!go.scene.IsValid() || !go.scene.isLoaded) return;
            if (go.TryGetComponent(addition.Behavior, out Component _)) return;
            if (go.AddComponent(addition.Behavior) == null) throw new InvalidOperationException("AddComponent returned no behavior");
            // Unity calls Awake/OnEnable itself and schedules Start normally.
            // Toggling the native object here would also interrupt every game
            // component on it, and could leave it hidden if this worker stops.
        }
        catch (Exception exception)
        {
            var failures = addition.Failures + 1;
            if (failures < 3) Enqueue(source, new PendingAddition(addition.Behavior, failures));
            if (failures == 1 || failures == 3)
                Debug.LogWarning($"[NOVR] Could not attach {addition.Behavior.Name}{(failures < 3 ? "; retrying" : "; giving up after 3 attempts")}: {exception.Message}");
        }
    }
}
