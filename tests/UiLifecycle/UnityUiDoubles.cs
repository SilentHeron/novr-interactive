// Small lifecycle/geometry doubles for exercising the ACTUAL transition component
// offline. These do not simulate rendering, EventSystem or physical VR input.
using System.Reflection;
namespace UnityEngine;

public class Component
{
    public GameObject gameObject = null!;
    public RectTransform transform => gameObject.Rect;
    public T? GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
}
public class MonoBehaviour : Component { }
public sealed class GameObject
{
    private readonly Dictionary<Type, Component> _components = new();
    public bool activeSelf = true;
    public RectTransform Rect { get; }
    public GameObject(Vector2 anchor)
    {
        Rect = new RectTransform { gameObject = this, anchoredPosition = anchor };
        _components[typeof(RectTransform)] = Rect;
    }
    public T? GetComponent<T>() where T : Component => _components.GetValueOrDefault(typeof(T)) as T;
    public T AddComponent<T>() where T : Component, new()
    {
        var component = new T { gameObject = this };
        _components[typeof(T)] = component;
        typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(component, null);
        return component;
    }
    public void SetActive(bool value) => activeSelf = value;
}
public sealed class RectTransform : Component { public Vector2 anchoredPosition; }
public sealed class CanvasGroup : Component
{
    public float alpha = 1;
    public bool interactable = true, blocksRaycasts = true;
}
public readonly struct Vector2(float x, float y)
{
    public readonly float x = x, y = y;
    public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x + b.x, a.y + b.y);
    public static Vector2 Lerp(Vector2 a, Vector2 b, float t)
    {
        t = Math.Clamp(t, 0, 1);
        return new(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
    }
}
public static class Time { public static float unscaledDeltaTime = 1f / 90; }
public static class Mathf
{
    public static float Clamp01(float value) => Math.Clamp(value, 0, 1);
    public static float MoveTowards(float current, float target, float delta) =>
        Math.Abs(target - current) <= delta ? target : current + Math.Sign(target - current) * delta;
}
