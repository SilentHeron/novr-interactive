using System.Reflection;
using NuclearOption.SavedMission;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public partial class NativeGameActionAdapter
{
    private static readonly FieldInfo? MissionPreviewField = typeof(MissionPicker)
        .GetField("previewImage", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo? MissionPreviewFallbackField = typeof(MissionPicker)
        .GetField("defaultMissionImage", BindingFlags.Instance | BindingFlags.NonPublic);

    internal MissionPicker? GetOriginalMissionPreviewPicker()
    {
        if (_originalMainCanvas == null)
            return null;
        foreach (var picker in _originalMainCanvas.GetComponentsInChildren<MissionPicker>(true))
            if (picker.gameObject.activeInHierarchy)
                return picker;
        return null;
    }

    internal bool TryGetOriginalMissionPreview(MissionKey key, out Image? image, out Sprite? fallback)
    {
        image = null;
        fallback = null;
        var picker = GetOriginalMissionPreviewPicker();
        if (picker == null || !NativeMissionSelection.TryGet(picker, key, out _))
            return false;
        image = MissionPreviewField?.GetValue(picker) as Image;
        fallback = MissionPreviewFallbackField?.GetValue(picker) as Sprite;
        return image != null || fallback != null;
    }
}
