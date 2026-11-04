// Small hierarchy/failure doubles for the actual native lease. Rendering and
// Unity event timing still require the user's headset session.
using System.Reflection;
namespace HarmonyLib
{
    public static class AccessTools
    {
        public static FieldInfo? Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
namespace UnityEngine
{
    public class Object
    {
        internal static bool FailInstantiate;
        internal static int InstantiateCalls, FailInstantiateAt;
        internal bool Dead;
        public static GameObject Instantiate(GameObject source, Transform parent)
        {
            InstantiateCalls++;
            if (FailInstantiate || InstantiateCalls == FailInstantiateAt)
            {
                FailInstantiate = false;
                FailInstantiateAt = 0;
                throw new InvalidOperationException("injected clone failure");
            }
            var clone = new GameObject(source.name + " clone");
            clone.transform.SetParent(parent, false);
            clone.transform.localPosition = source.transform.localPosition;
            clone.transform.localRotation = source.transform.localRotation;
            clone.transform.localScale = source.transform.localScale;
            var original = source.GetComponent<UnityEngine.UI.Image>();
            if (original != null)
            {
                var image = clone.AddComponent<UnityEngine.UI.Image>();
                image.fillAmount = original.fillAmount;
                image.color = original.color;
                image.raycastTarget = original.raycastTarget;
            }
            return clone;
        }
        public static bool operator ==(Object? a, Object? b) => ReferenceEquals(a, b) || (a is null || a.Dead) && (b is null || b.Dead);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static void Destroy(Object obj)
        {
            if (obj is GameObject go)
            {
                foreach (var child in go.transform.Children.ToArray())
                    Destroy(child.gameObject);
                foreach (var component in go.Components)
                    component.Dead = true;
                go.transform.SetParent(null, false);
            }
            obj.Dead = true;
        }
    }
    public class Component : Object
    {
        public GameObject gameObject = null!;
        public Transform transform => gameObject.transform;
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : Component
        {
            var found = new List<T>();
            void Visit(Transform node)
            {
                if (!includeInactive && !node.gameObject.activeInHierarchy)
                    return;
                found.AddRange(node.gameObject.Components.OfType<T>());
                foreach (var child in node.Children)
                    Visit(child);
            }
            Visit(transform);
            return found.ToArray();
        }
    }
    public class MonoBehaviour : Component
    {
        public bool isActiveAndEnabled => !Dead && gameObject.activeInHierarchy;
    }
    public class GameObject : Object
    {
        public readonly List<Component> Components = new();
        public RectTransform transform;
        public string name;
        public int layer;
        public bool activeSelf = true, FailActivation;
        public bool activeInHierarchy => !Dead && activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name, params Type[] components)
        {
            this.name = name;
            transform = new RectTransform { gameObject = this };
            Components.Add(transform);
            foreach (var type in components)
                if (type != typeof(RectTransform))
                    Components.Add((Component)Activator.CreateInstance(type)!);
            foreach (var component in Components)
                component.gameObject = this;
        }
        public T GetComponent<T>() where T : Component => Components.OfType<T>().FirstOrDefault()!;
        public T AddComponent<T>() where T : Component, new()
        {
            var result = new T { gameObject = this };
            Components.Add(result);
            return result;
        }
        public void SetActive(bool active)
        {
            if (FailActivation)
            {
                FailActivation = false;
                throw new InvalidOperationException("injected native activation failure");
            }
            activeSelf = active;
        }
    }
    public class Transform : Component
    {
        public Transform parent = null!;
        public readonly List<Transform> Children = new();
        public Vector3 localPosition, localScale = Vector3.one;
        public Quaternion localRotation = Quaternion.identity;
        public void SetParent(Transform? next, bool worldPositionStays)
        {
            parent?.Children.Remove(this);
            parent = next!;
            next?.Children.Add(this);
        }
        public int GetSiblingIndex() => parent?.Children.IndexOf(this) ?? 0;
        public void SetSiblingIndex(int i)
        {
            if (parent == null)
                return;
            parent.Children.Remove(this);
            parent.Children.Insert(Math.Clamp(i, 0, parent.Children.Count), this);
        }
        public bool IsChildOf(Transform other)
        {
            for (var node = parent; node != null; node = node.parent)
                if (node == other)
                    return true;
            return false;
        }
    }
    public class RectTransform : Transform
    {
        public Vector2 anchorMin = new(.1f, .2f), anchorMax = new(.8f, .9f), pivot = new(.5f, .7f), sizeDelta = new(100, 100);
    }
    public class CanvasGroup : Component
    {
        public float alpha;
    }
    public readonly record struct Vector2(float x, float y);
    public readonly record struct Vector3(float x, float y, float z)
    {
        public static Vector3 zero => new(0, 0, 0);
        public static Vector3 one => new(1, 1, 1);
        public Vector3 normalized
        {
            get
            {
                var length = MathF.Sqrt(x * x + y * y + z * z);
                return this * (1 / length);
            }
        }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
    }
    public readonly record struct Quaternion(float x, float y, float z, float w)
    {
        public static Quaternion identity => new(0, 0, 0, 1);
        public static Quaternion Euler(float x, float y, float z) => new(x, y, z, 0);
    }
    public readonly record struct Color(float r, float g, float b, float a = 1)
    {
        public static Color white => new(1, 1, 1);
    }
    public sealed class Font : Object
    {
    }
    public enum TextAnchor
    {
        MiddleCenter
    }
    public static class Resources
    {
        public static T GetBuiltinResource<T>(string name) where T : Object, new() => new();
    }
    public static class Mathf
    {
        public const float Deg2Rad = MathF.PI / 180;
        public static float Sin(float x) => MathF.Sin(x);
        public static float Cos(float x) => MathF.Cos(x);
        public static float Max(float a, float b) => Math.Max(a, b);
    }
}
namespace UnityEngine.UI
{
    public class Graphic : UnityEngine.Component
    {
        public bool raycastTarget = true;
        public UnityEngine.Color color;
        public UnityEngine.RectTransform rectTransform => gameObject.transform;
    }
    public class Image : Graphic
    {
        public float fillAmount = 1;
    }
    public class Text : Graphic
    {
        public UnityEngine.Font font = null!;
        public int fontSize;
        public string text = "";
        public UnityEngine.TextAnchor alignment;
    }
}
namespace TMPro
{
    public class TMP_Text : UnityEngine.UI.Graphic
    {
        public string text = "";
    }
}

public class RadialMenuAction : UnityEngine.Object
{
    public string DisplayName = "native action";
    public int Hovers, Unhovers, Activations;
    public void Hover() => Hovers++;
    public void UnHover() => Unhovers++;
    public void TriggerAction() => Activations++;
}
public class RadialMenuMain : UnityEngine.MonoBehaviour
{
    public enum RadialMenuType
    {
        Closed, Main, Weapons
    }
    public UnityEngine.GameObject menuObject = null!, actionsMainContainer = null!, actionsWeaponsContainer = null!, sectorObject = null!;
    public List<UnityEngine.GameObject> actionObjectsMain = new();
    public List<RadialMenuAction> allowedActionsMain = new(), allowedActionsWeapons = new();
    public RadialMenuType currentState;
    public RadialMenuAction? selectedAction;
    public TMPro.TMP_Text selectedActionText = null!;
    public UnityEngine.CanvasGroup canvasGroup = null!;
    public int Opens, Closes, Refreshes;
    public bool FailOpen;
    public void OpenMenu()
    {
        Opens++;
        menuObject.SetActive(true);
        if (FailOpen)
            throw new InvalidOperationException("injected native setup failure");
    }
    public void CloseMenu()
    {
        Closes++;
        selectedAction = null;
        currentState = RadialMenuType.Closed;
        selectedActionText.text = "";
    }
    public void RefreshWeapons() => Refreshes++;
}
