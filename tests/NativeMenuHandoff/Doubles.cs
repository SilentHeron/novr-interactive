namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static bool operator ==(Object? a, Object? b) =>
            (ReferenceEquals(a, null) || a.Destroyed) && (ReferenceEquals(b, null) || b.Destroyed) || ReferenceEquals(a, b);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class MonoBehaviour : Object
    {
        public GameObject gameObject = new();
    }
    public sealed class Canvas : MonoBehaviour
    {
        public bool enabled = true;
    }
    public sealed class CanvasGroup : MonoBehaviour
    {
        public float alpha = 1; public bool interactable = true, blocksRaycasts = true;
    }
    public sealed class GameObject : Object
    {
        public string name = "";
        public SceneManagement.Scene scene;
        public bool activeSelf = true;
        public GameObject? Parent;
        public void SetActive(bool value) => activeSelf = value;
        public bool activeInHierarchy => activeSelf && (Parent?.activeInHierarchy ?? true);
        public readonly List<MonoBehaviour> Components = new();
        public int HierarchySearches;
        public T[] GetComponentsInChildren<T>(bool includeInactive)
        {
            HierarchySearches++;
            return Components.Where(item => !item.Destroyed && (includeInactive || item.gameObject.activeInHierarchy)).OfType<T>().ToArray();
        }
    }
    public static class Resources
    {
        public static readonly List<Object> Loaded = new();
        public static int Searches;
        public static T[] FindObjectsOfTypeAll<T>()
        {
            Searches++;
            return Loaded.OfType<T>().ToArray();
        }
    }
    public static class Mathf
    {
        public static float Min(float a, float b) => Math.Min(a, b);
        public static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
    }
}
namespace UnityEngine.SceneManagement
{
    public readonly record struct Scene(int handle, bool isLoaded = true)
    {
        public bool IsValid() => handle != 0;
    }
    public enum LoadSceneMode
    {
        Single, Additive
    }
    public static class SceneManager
    {
        public static event Action<Scene, LoadSceneMode>? sceneLoaded;
        public static event Action<Scene>? sceneUnloaded;
        public static event Action<Scene, Scene>? activeSceneChanged;
        public static Scene Active;
        public static Scene GetActiveScene() => Active;
        public static void Load(Scene scene) => sceneLoaded?.Invoke(scene, LoadSceneMode.Additive);
        public static void Unload(Scene scene) => sceneUnloaded?.Invoke(scene);
        public static void SetActive(Scene scene)
        {
            var before = Active;
            Active = scene;
            activeSceneChanged?.Invoke(before, scene);
        }
        public static int Subscribers => (sceneLoaded?.GetInvocationList().Length ?? 0) +
            (sceneUnloaded?.GetInvocationList().Length ?? 0) + (activeSceneChanged?.GetInvocationList().Length ?? 0);
    }
}
namespace NuclearOption.MissionEditorScripts
{
    public sealed class FileMenu : UnityEngine.MonoBehaviour
    {
    }
}
public sealed class CustomizeMissionMenu : UnityEngine.MonoBehaviour
{
    private readonly UnityEngine.GameObject holder;
    public CustomizeMissionMenu(UnityEngine.GameObject content) => holder = content;
}
namespace NuclearOption.SavedMission
{
    public readonly partial record struct MissionKey(string Name);
    public sealed partial class Mission
    {
        public string Name = "", Description = "";
    }
}
public sealed class MissionSelectPanel
{
    public NuclearOption.SavedMission.MissionKey SelectedMission
    {
        get; private set;
    }
    public Action<NuclearOption.SavedMission.MissionKey>? OnSelect;
    public void SetSelectedMission(NuclearOption.SavedMission.MissionKey key)
    {
        SelectedMission = key;
        OnSelect?.Invoke(key);
    }
}
public sealed class MissionPicker
{
    private readonly MissionSelectPanel? missionSelect;
    public NuclearOption.SavedMission.Mission? Mission;
    public int Loads;
    public MissionPicker(MissionSelectPanel? selection)
    {
        missionSelect = selection;
        if (selection != null)
            selection.OnSelect = SelectMission;
    }
    public void SelectMission(NuclearOption.SavedMission.MissionKey key)
    {
        Loads++;
        Mission = new()
        {
            Name = key.Name
        };
    }
}
