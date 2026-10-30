using System.Collections.Generic;
using NuclearOption.SavedMission;
using UnityEngine;

namespace NOVR.VrUi.Native;

internal static class NativeMissionHeaders
{
    // QuickLoadMany was removed in Nuclear Option 0.34. Skip broken headers individually.
    public static IEnumerable<(MissionKey key, MissionQuickLoad mission)> QuickLoadMany(IEnumerable<MissionKey> keys)
    {
        var mapLoader = Object.FindObjectOfType<global::MissionSelectPanel>(true)?.MapLoader;
        foreach (var key in keys)
        {
            if (MissionSaveLoad.QuickLoadOne(key, out var mission, out var error))
            {
                // QuickLoadOne only reads saved fields. Match MissionSelectPanel:
                // player mode, time of day and PvE/PvP tags are generated afterward.
                // Filtering the raw header by SinglePlayer/Multiplayer hides missions.
                try
                {
                    MissionTag.AddAutoTags(mission, mapLoader);
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning($"[NOVR] Could not prepare mission '{key}': {exception}");
                    continue;
                }
                yield return (key, mission);
            }
            else
                Debug.LogWarning($"[NOVR] Could not read mission '{key}': {error}");
        }
    }
}
