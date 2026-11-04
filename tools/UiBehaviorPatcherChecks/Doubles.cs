// Small native lifecycle boundary. The actual patcher is source-linked. These
// checks cover scheduling/ownership, not Harmony detouring or Unity rendering.
using System.Reflection;
namespace UnityEngine
{
    public class Object
    {
        private static int _nextId;
        public bool Destroyed;
        public int NativeId;
        public void Bind() => NativeId = Interlocked.Increment(ref _nextId);
        public int GetInstanceID() => NativeId;
        private string _name = "";
        public virtual string name { get { if (Destroyed) throw new InvalidOperationException("destroyed native object"); return _name; } set => _name = value; }
        public static bool operator ==(Object? a, Object? b) => ReferenceEquals(a, b) || (a is null ? b!.Destroyed || b.NativeId == 0 : b is null ? a.Destroyed || a.NativeId == 0 : a.NativeId == b.NativeId);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => this == other as Object;
        public override int GetHashCode() => NativeId;
    }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public override string name { get { if (Destroyed) throw new InvalidOperationException("destroyed native component"); return gameObject.name; } set => gameObject.name = value; }
    }
    public class MonoBehaviour : Component
    {
        public bool enabled = true;
        public bool isActiveAndEnabled => !Destroyed && enabled && gameObject.activeInHierarchy;
        public int Awakes, Enables, Disables, Starts;
        protected virtual void Awake() => Awakes++;
        protected virtual void OnEnable() => Enables++;
        protected virtual void OnDisable() => Disables++;
        protected virtual void Start() => Starts++;
    }
    public sealed class Transform : Component { }
    public sealed class GameObject : Object
    {
        public readonly Transform transform;
        public readonly List<Component> Components = new();
        public SceneManagement.Scene scene = new(true, true);
        public bool activeSelf = true;
        public bool activeInHierarchy => !Destroyed && activeSelf;
        public int Deactivations;
        public static Action<GameObject, Type>? BeforeAdd, AfterAdd;
        public GameObject(string name) { Bind(); this.name = name; transform = new Transform { gameObject = this }; transform.Bind(); Components.Add(transform); Resources.Objects.Add(this); }
        public T AddComponent<T>() where T : Component, new() => (T)AddComponent(typeof(T));
        public Component AddComponent(Type type)
        {
            BeforeAdd?.Invoke(this, type);
            var result = (Component)Activator.CreateInstance(type)!;
            result.gameObject = this; result.Bind(); Components.Add(result);
            if (activeInHierarchy) { Life.Invoke(result, "Awake"); Life.Invoke(result, "OnEnable"); }
            AfterAdd?.Invoke(this, type);
            return result;
        }
        public bool TryGetComponent(Type type, out Component component)
        { component = Components.FirstOrDefault(x => x != null && type.IsInstanceOfType(x))!; return component != null; }
        public T? Get<T>() where T : Component => Components.OfType<T>().FirstOrDefault(x => x != null);
        public int Count<T>() where T : Component => Components.OfType<T>().Count(x => x != null);
        public void SetActive(bool value)
        {
            if (activeSelf == value) return;
            activeSelf = value;
            if (!value) Deactivations++;
            foreach (var component in Components.OfType<MonoBehaviour>())
            {
                if (value && component.Awakes == 0) Life.Invoke(component, "Awake");
                Life.Invoke(component, value ? "OnEnable" : "OnDisable");
            }
        }
        public void StartPending()
        { foreach (var component in Components.OfType<MonoBehaviour>()) if (component.isActiveAndEnabled && component.Starts == 0) Life.Invoke(component, "Start"); }
    }
    public static class Resources
    {
        public static readonly List<GameObject> Objects = new(); public static int Scans;
        public static T[] FindObjectsOfTypeAll<T>() where T : Object { Scans++; return Objects.OfType<T>().ToArray(); }
    }
    public static class Time { public static float timeScale = 1; }
    public static class Debug
    {
        public static readonly List<string> Warnings = new();
        public static void LogWarning(object value) => Warnings.Add(value.ToString()!);
    }
}
namespace UnityEngine.SceneManagement
{
    public readonly record struct Scene(bool Valid, bool isLoaded) { public bool IsValid() => Valid; }
    public enum LoadSceneMode { Single, Additive }
    public static class SceneManager
    {
        public static event Action<Scene, LoadSceneMode>? sceneLoaded;
        public static int Subscribers => sceneLoaded?.GetInvocationList().Length ?? 0;
        public static void Load() => sceneLoaded?.Invoke(new Scene(true, true), LoadSceneMode.Single);
    }
}
namespace HarmonyLib
{
    public sealed class HarmonyMethod(MethodInfo method) { public MethodInfo Method => method; }
    public sealed class Harmony(string id)
    {
        public static readonly Dictionary<ConstructorInfo, int> Calls = new();
        public static Type? FailOnce;
        public void Patch(ConstructorInfo ctor, HarmonyMethod postfix)
        {
            if (FailOnce == ctor.DeclaringType) { FailOnce = null; throw new Exception("one registration failure"); }
            Calls[ctor] = Calls.GetValueOrDefault(ctor) + 1;
            if (postfix.Method.Name != "UniversalPostfix" || id != "novr.vrui.behavioradder") throw new Exception("wrong constructor hook");
        }
    }
}
namespace NOVR
{
    public class NOVRBehaviour : UnityEngine.MonoBehaviour
    {
        public int BaseEnables, BaseDisables;
        protected override void OnEnable() { base.OnEnable(); BaseEnables++; }
        protected override void OnDisable() { base.OnDisable(); BaseDisables++; }
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    public sealed class NOVRFlightHudBehavior : UnityEngine.MonoBehaviour { }
    public sealed class NOVRGameplayUIBehaviour : UnityEngine.MonoBehaviour { }
    public sealed class NOVRStatusDisplayBehavior : UnityEngine.MonoBehaviour { }
    public sealed class NOVRMainMenuBehavior : UnityEngine.MonoBehaviour { }
    public sealed class NOVRBlackoutCanvasBehavior : UnityEngine.MonoBehaviour { }
    public sealed class PositionZeroBehavior : UnityEngine.MonoBehaviour { }
}
public class FlightHud : UnityEngine.MonoBehaviour { }
public sealed class ModFlightHud : FlightHud { }
public sealed class GameplayUI : UnityEngine.MonoBehaviour { }
public sealed class MessageUI : UnityEngine.MonoBehaviour { }
public sealed class StatusDisplay : UnityEngine.MonoBehaviour { }
public static class Life
{
    public static void Invoke(object target, string method) => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(target, null);
}
