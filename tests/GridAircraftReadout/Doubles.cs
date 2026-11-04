namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type, string method)
        {
        }
    }
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class HarmonyPostfix : Attribute
    {
    }
}
namespace UnityEngine
{
    internal sealed class Transform
    {
        internal Transform? parent;
    }
}
namespace UnityEngine.UI
{
    internal sealed class Text
    {
        internal bool enabled = true; internal readonly UnityEngine.Transform transform = new();
    }
}
namespace UnityEngine.XR
{
    internal static class XRSettings
    {
        internal static bool enabled;
    }
}
internal sealed class GridLabels
{
}
internal sealed class DynamicMap
{
    internal static bool mapMaximized;
    internal UnityEngine.Transform? hudMapAnchor;
}
internal static class SceneSingleton<T>
{
    internal static T? i;
}
