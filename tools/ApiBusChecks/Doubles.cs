// Unity's CLR reference and destroyed-object comparisons differ. Preserve that
// distinction here: two different destroyed objects are not equal to each other.
namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;
        public string name="";
        public static bool operator ==(Object? a,Object? b)
        {
            if(ReferenceEquals(a,b))return true;
            if(a is null)return b!.Destroyed;
            if(b is null)return a.Destroyed;
            return false;
        }
        public static bool operator !=(Object? a,Object? b)=>!(a==b);
        public override bool Equals(object? other)=>this==other as Object;
        public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
    public class Component:Object
    {
        public GameObject gameObject=null!;
        public Transform transform=>gameObject.transform;
        public T? GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();
    }
    public class MonoBehaviour:Component
    {
        public bool enabled=true, activeInHierarchy=true;
        public bool isActiveAndEnabled=>!Destroyed&&enabled&&activeInHierarchy;
    }
    public sealed class Camera:MonoBehaviour
    {
        public static Camera? main;
        public StereoTargetEyeMask stereoTargetEye;
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
        public float depth,nearClipPlane,farClipPlane;
        public int cullingMask;
        public bool allowHDR,allowMSAA;
        public object? targetTexture;
        public Rect rect;
    }
    public enum RenderMode { ScreenSpaceOverlay, WorldSpace }
    public sealed class Canvas:Component
    {
        private Camera? _worldCamera;
        public int CameraAssignments;
        public Camera? worldCamera { get=>_worldCamera; set { _worldCamera=value;CameraAssignments++; } }
        public RenderMode renderMode;
        public float planeDistance;
    }
    public sealed class GameObject:Object
    {
        private readonly List<Component> _components=new();
        public readonly Transform transform;
        public bool activeSelf=true;
        public GameObject(string name){this.name=name;transform=new Transform{gameObject=this};_components.Add(transform);}
        public T AddComponent<T>() where T:Component,new(){var value=new T{gameObject=this};_components.Add(value);return value;}
        public T? GetComponent<T>() where T:Component=>_components.OfType<T>().FirstOrDefault(x=>!x.Destroyed);
        public void SetActive(bool value)=>activeSelf=value;
    }
    public sealed class Transform:Component
    {
        public Transform? parent;
        public Vector3 position,localPosition,localScale;
        public Quaternion rotation,localRotation;
        private readonly List<Transform> _children=new();
        public int childCount=>_children.Count;
        public Transform GetChild(int index)=>_children[index];
        public void SetParent(Transform value)=>parent=value;
    }
    public readonly record struct Vector3(float x,float y,float z);
    public readonly record struct Quaternion(float x,float y,float z,float w)
    { public static Quaternion identity=>new(0,0,0,1); public static Quaternion Slerp(Quaternion a,Quaternion b,float t)=>b; }
    public readonly record struct Color(float r,float g,float b,float a)
    { public static Color clear=>new(0,0,0,0); }
    public readonly record struct Rect(float x,float y,float width,float height);
    public enum CameraClearFlags { SolidColor,Depth }
    public enum StereoTargetEyeMask { Both }
    public static class Time { public static float unscaledDeltaTime=.01f; }
    [AttributeUsage(AttributeTargets.Class)] public sealed class DefaultExecutionOrder(int order):Attribute { public int Order=>order; }
    public static class StackTraceUtility { public static string ExtractStackTrace()=>"test"; }
    public static class Debug
    {
        public static int Logs,Warnings,Errors,Exceptions;
        public static void Log(object message)=>Logs++;
        public static void LogWarning(object message)=>Warnings++;
        public static void LogError(object message)=>Errors++;
        public static void LogException(Exception exception)=>Exceptions++;
    }
}
namespace UnityEngine.Rendering.Universal
{
    public enum CameraRenderType { Base,Overlay }
    public sealed class UniversalAdditionalCameraData:UnityEngine.Component
    {
        public CameraRenderType renderType;
        public bool renderPostProcessing;
        public readonly List<UnityEngine.Camera> cameraStack=new();
    }
}
namespace NOVR
{
    public class NOVRBehaviour:UnityEngine.MonoBehaviour
    {
        public int DisableCallbacks;
        protected virtual void Awake(){}
        protected virtual void OnDisable(){DisableCallbacks++;}
        protected virtual void OnSettingChanged(){}
        public static T Create<T>(UnityEngine.Transform parent) where T:NOVRBehaviour,new()
        {var go=new UnityEngine.GameObject(typeof(T).Name);go.transform.SetParent(parent);return go.AddComponent<T>();}
    }
    public sealed class NOVRPoseDriver:NOVRBehaviour { }
    public static class LayerHelper
    {
        public static int LayerAssignments;
        public static int GetVrUiLayer()=>30;
        public static void SetLayerRecursive(UnityEngine.Transform root,int layer)=>LayerAssignments++;
    }
    public sealed class ModConfiguration
    {
        public static readonly ModConfiguration Instance=new();
        public readonly Setting HmdFollowResponse=new();
        public sealed class Setting { public float Value; }
    }
}
namespace NOVR.MotionControls { public static class FlightControlMath { public static float SmoothingWeight(float dt,float response)=>1; } }
namespace NOVR.VrCamera { public static class VrCameraManager { public static readonly HashSet<UnityEngine.Camera> IgnoredCameras=new(); } }
namespace NOVR.VrUi
{
    public sealed class UIBehaviorPatcher:NOVR.NOVRBehaviour { public static void DoPatching(){} }
    public sealed class VrUiCursor:NOVR.NOVRBehaviour { }
}
namespace NOVR.VrUi.Native { public sealed class NativeVrUiRoot:NOVR.NOVRBehaviour { } }
namespace NOVR.VrUi.SpecialBehavior { public sealed class CockpitAlerts:UnityEngine.MonoBehaviour { } }
