using System.Collections.Generic;
using UnityEngine;

namespace NOVR.VrUi.SpecialBehavior;

// Searches of the flight HUD that leave out its marker layer. The game keeps
// one object per unit marker there (hundreds in a busy mission) and no
// instruments, so a periodic search for an instrument has no reason to visit
// them. The layer is the HUD's first child, which made every depth-first
// search walk all markers before reaching anything else.
internal static class HudHierarchy
{
    internal static Transform? MarkerLayer
    {
        get
        {
            var combat = SceneSingleton<CombatHUD>.i;
            return combat != null ? combat.iconLayer : null;
        }
    }

    // Every T on root or below it, except below skip (a direct child of root;
    // a null or deeper skip only means nothing is left out).
    internal static void Collect<T>(Transform root, Transform? skip, List<T> results, List<T> scratch) where T : Component
    {
        results.Clear();
        scratch.Clear();
        root.GetComponents(scratch);
        results.AddRange(scratch);
        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (ReferenceEquals(child, skip))
                continue;
            scratch.Clear();
            child.GetComponentsInChildren(true, scratch);
            results.AddRange(scratch);
        }
        scratch.Clear();
    }

    // The first T on root or below it, searched the same way.
    internal static T? Find<T>(Transform root, Transform? skip) where T : Component
    {
        var own = root.GetComponent<T>();
        if (own != null)
            return own;
        for (var i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (ReferenceEquals(child, skip))
                continue;
            var found = child.GetComponentInChildren<T>(true);
            if (found != null)
                return found;
        }
        return null;
    }
}
