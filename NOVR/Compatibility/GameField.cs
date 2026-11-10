using System;
using HarmonyLib;

namespace NOVR.Compatibility;

// Typed access to private game fields: a field-reference delegate reads the
// field directly, where FieldInfo.GetValue costs a reflection call and boxes
// every bool, float and struct it returns.
internal static class GameField
{
    // For code outside Harmony patch classes, which the startup handle check
    // does not cover. Null when this game build has no such field or its type
    // changed, so the caller keeps its fallback instead of failing to load.
    internal static AccessTools.FieldRef<TOwner, TField>? Optional<TOwner, TField>(string name)
    {
        try
        {
            return AccessTools.FieldRefAccess<TOwner, TField>(name);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
