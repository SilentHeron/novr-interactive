using System.Reflection;
using NuclearOption.SavedMission;

namespace NOVR.VrUi.Native;

internal static class NativeMissionSelection
{
    private static readonly FieldInfo? SelectionField = typeof(MissionPicker)
        .GetField("missionSelect", BindingFlags.Instance | BindingFlags.NonPublic);

    internal static void Select(MissionPicker picker, MissionKey key)
    {
        if (SelectionField?.GetValue(picker) is MissionSelectPanel selection)
        {
            if (!selection.SelectedMission.Equals(key) || picker.Mission == null)
                selection.SetSelectedMission(key);
        }
        else picker.SelectMission(key);
    }

    internal static bool TryGet(MissionPicker picker, MissionKey key, out Mission? mission)
    {
        mission = null;
        if (picker.Mission == null || SelectionField?.GetValue(picker) is not MissionSelectPanel selection ||
            !selection.SelectedMission.Equals(key)) return false;
        mission = picker.Mission;
        return true;
    }
}
