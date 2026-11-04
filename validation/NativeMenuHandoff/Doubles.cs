namespace UnityEngine
{
    public class MonoBehaviour { public GameObject gameObject = new(); }
    public sealed class GameObject
    {
        public bool activeSelf = true;
        public GameObject? Parent;
        public bool activeInHierarchy => activeSelf && (Parent?.activeInHierarchy ?? true);
        public readonly List<MonoBehaviour> Components = new();
        public T[] GetComponentsInChildren<T>(bool includeInactive) => Components.OfType<T>().ToArray();
    }
}
namespace NuclearOption.MissionEditorScripts
{
    public sealed class FileMenu : UnityEngine.MonoBehaviour { }
}
public sealed class CustomizeMissionMenu : UnityEngine.MonoBehaviour
{
    private readonly UnityEngine.GameObject holder;
    public CustomizeMissionMenu(UnityEngine.GameObject content) => holder = content;
}
namespace NuclearOption.SavedMission
{
    public readonly record struct MissionKey(string Name);
    public sealed class Mission { public string Name = "", Description = ""; }
}
public sealed class MissionSelectPanel
{
    public NuclearOption.SavedMission.MissionKey SelectedMission { get; private set; }
    public Action<NuclearOption.SavedMission.MissionKey>? OnSelect;
    public void SetSelectedMission(NuclearOption.SavedMission.MissionKey key) { SelectedMission = key; OnSelect?.Invoke(key); }
}
public sealed class MissionPicker
{
    private readonly MissionSelectPanel? missionSelect;
    public NuclearOption.SavedMission.Mission? Mission;
    public int Loads;
    public MissionPicker(MissionSelectPanel? selection)
    { missionSelect = selection; if (selection != null) selection.OnSelect = SelectMission; }
    public void SelectMission(NuclearOption.SavedMission.MissionKey key)
    { Loads++; Mission = new() { Name = key.Name }; }
}
