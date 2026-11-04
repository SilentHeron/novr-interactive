namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;
        public static bool operator ==(Object? a,Object? b)=>ReferenceEquals(a,b)||(a is null&&b!.Destroyed)||(b is null&&a!.Destroyed);
        public static bool operator !=(Object? a,Object? b)=>!(a==b);
        public override bool Equals(object? obj)=>this==obj as Object;
        public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class Component:Object
    {
        public GameObject gameObject=null!;
        public Transform transform=>gameObject.transform;
        public string tag { get=>gameObject.tag;set=>gameObject.tag=value; }
        public bool CompareTag(string value)=>tag==value;
        public T? GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();
        public T[] GetComponentsInChildren<T>() where T:Component=>gameObject.GetComponentsInChildren<T>();
    }
    public class MonoBehaviour:Component { public bool enabled=true; }
    public sealed class Camera:MonoBehaviour
    {
        internal static readonly List<Camera> All=new();
        internal static int Creations;
        public Camera(){All.Add(this);Creations++;}
        public static int allCamerasCount { get {var n=0;foreach(var c in All)if(c!=null&&c.enabled&&c.gameObject.active)n++;return n;} }
        public static int GetAllCameras(Camera[] buffer)
        {var n=0;foreach(var c in All)if(c!=null&&c.enabled&&c.gameObject.active)buffer[n++]=c;return n;}
        public float fieldOfView=60;
        public void CopyFrom(Camera camera)=>fieldOfView=camera.fieldOfView;
    }
    public sealed class AudioListener:MonoBehaviour { }
    public sealed class GameObject:Object
    {
        public string name,tag="Untagged";
        public bool active=true;
        public readonly Transform transform;
        private readonly List<Component> components=new();
        public GameObject(string name){this.name=name;transform=new Transform{gameObject=this};components.Add(transform);}
        public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};components.Add(c);return c;}
        public T? GetComponent<T>() where T:Component
        {foreach(var c in components)if(c!=null&&c is T found)return found;return null;}
        public int Count<T>() where T:Component=>components.Count(c=>c!=null&&c is T);
        public T[] GetComponentsInChildren<T>() where T:Component
        {var result=new List<T>();Collect(result);return result.ToArray();}
        private void Collect<T>(List<T> result) where T:Component
        {if(!active)return;foreach(var c in components)if(c!=null&&c is T found)result.Add(found);foreach(var child in transform.Children)child.gameObject.Collect(result);}
    }
    public sealed class Transform:Component
    {
        public Transform? parent;
        internal readonly List<Transform> Children=new();
        public void SetParent(Transform value,bool worldPositionStays){parent?.Children.Remove(this);parent=value;value.Children.Add(this);}
        public Transform? Find(string name){foreach(var c in Children)if(c!=null&&c.gameObject.name==name)return c;return null;}
    }
    public static class Mathf { public static int NextPowerOfTwo(int x){var n=1;while(n<x)n*=2;return n;} }
}
namespace UnityEngine.Rendering.Universal
{
    public enum CameraRenderType { Base,Overlay }
    public sealed class UniversalAdditionalCameraData:UnityEngine.Component
    {
        public CameraRenderType renderType;
        public bool allowXRRendering;
        public readonly List<UnityEngine.Camera> cameraStack=new();
    }
}
namespace NOVR.VrCamera
{
    // Camera wrapping is production code; tracking behaviour is outside this fixture.
    public class StereoCamera:UnityEngine.MonoBehaviour { }
    public sealed class VrCamera:StereoCamera { }
}
