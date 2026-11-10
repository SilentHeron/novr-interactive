using System.Reflection;

// Only shader/property identity and UI hierarchy lifecycle are modeled here.
// GPU depth/blending/stereo remain the installed shader/runtime's responsibility.
namespace UnityEngine
{
    public class Object
    {
        internal bool Dead;
        public string name = "";
        public static bool operator ==(Object? a, Object? b) => ReferenceEquals(a, b) || (a is null || a.Dead) && (b is null || b.Dead);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static void Destroy(Object? o)
        {
            if (o == null)
                return;
            o!.Dead = true;
            if (o is Component c)
                c.gameObject.Components.Remove(c);
            o.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(o, null);
        }
    }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        // How many times any hierarchy was walked, so checks can tell a frame
        // that searched from one that only tested for change.
        public static int HierarchyWalks;
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> result) where T : Component
        {
            HierarchyWalks++;
            void Visit(Transform root)
            {
                if (!includeInactive && !root.gameObject.activeInHierarchy)
                    return;
                result.AddRange(root.gameObject.Components.OfType<T>().Where(c => !c.Dead));
                foreach (var child in root.Children)
                    Visit(child);
            }
            Visit(transform);
        }
    }
    public class MonoBehaviour : Component
    {
        public bool enabled = true; public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }
    public sealed class GameObject : Object
    {
        internal readonly List<Component> Components = new();
        public readonly Transform transform;
        public bool activeSelf = true;
        public bool activeInHierarchy => !Dead && activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name)
        {
            this.name = name;
            transform = new Transform { gameObject = this };
            Components.Add(transform);
        }
        public T? GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault(c => !c.Dead);
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T { gameObject = this };
            Components.Add(c);
            return c;
        }
    }
    public class Transform : Component
    {
        public Transform? parent;
        internal readonly List<Transform> Children = new();
        public void SetParent(Transform? next)
        {
            parent?.Children.Remove(this);
            parent = next;
            next?.Children.Add(this);
        }
        public bool IsChildOf(Transform root)
        {
            for (var n = parent; n != null; n = n.parent)
                if (n == root)
                    return true;
            return false;
        }
        // Unity: the number of transforms in this transform's whole hierarchy.
        public int hierarchyCount
        {
            get
            {
                var top = this;
                while (top.parent != null)
                    top = top.parent;
                static int Count(Transform node) => 1 + node.Children.Sum(Count);
                return Count(top);
            }
        }
    }
    public static class Time
    {
        public static float unscaledTime;
    }
    public enum RenderMode
    {
        ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace
    }
    public sealed class Canvas : Component
    {
        public RenderMode renderMode;
    }
    public sealed class Shader : Object
    {
        public int NameReads;
        public new string name
        {
            get
            {
                NameReads++;
                return base.name;
            }
            set => base.name = value;
        }
        private static readonly Dictionary<string, Shader> All = new();
        public Shader(string name)
        {
            this.name = name;
            All[name] = this;
        }
        public static Shader? Find(string name) => All.TryGetValue(name, out var shader) ? shader : null;
    }
    public sealed class Material : Object
    {
        public Shader shader;
        public int renderQueue;
        internal readonly Dictionary<string, object> Properties = new();
        internal string[] Keywords = Array.Empty<string>();
        public Material(Shader shader)
        {
            this.shader = shader;
            name = shader.name;
        }
        public Material(Material source)
        {
            shader = source.shader;
            name = source.name;
            CopyPropertiesFromMaterial(source);
        }
        public void CopyPropertiesFromMaterial(Material source)
        {
            Properties.Clear();
            foreach (var p in source.Properties)
                Properties[p.Key] = p.Value;
            Keywords = (string[])source.Keywords.Clone();
            renderQueue = source.renderQueue;
        }
        public bool HasProperty(string p) => Properties.ContainsKey(p);
        public void SetInt(string p, int value) => Properties[p] = value;
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DefaultExecutionOrder(int order) : Attribute
    {
        public int Order => order;
    }
}
namespace UnityEngine.Rendering
{
    public enum RenderQueue
    {
        Transparent = 3000
    }
    public enum CompareFunction
    {
        LessEqual = 4
    }
}
namespace UnityEngine.UI
{
    public interface IMaterialModifier
    {
        UnityEngine.Material GetModifiedMaterial(UnityEngine.Material source);
    }
    public class Graphic : UnityEngine.MonoBehaviour
    {
        public UnityEngine.Material material = null!;
        public int Dirties;
        public UnityEngine.Canvas? canvas
        {
            get
            {
                for (var t = transform; t != null; t = t.parent)
                {
                    var c = t.GetComponent<UnityEngine.Canvas>();
                    if (c != null)
                        return c;
                }
                return null;
            }
        }
        public void SetMaterialDirty() => Dirties++;
    }
}
namespace NOVR.VrUi.SpecialBehavior
{
    internal static class HudWidgetProfiles
    {
        internal static readonly string[] Ids = { "map", "weapons", "countermeasures", "power", "threats", "ew", "feed", "damage", "objectives" };
    }
    internal static class CockpitHudWidgets
    {
        internal static readonly Dictionary<string, UnityEngine.Transform> Slots = new();
        internal static bool TryGetContent(string id, out UnityEngine.Transform content) => Slots.TryGetValue(id, out content!);
    }
}
