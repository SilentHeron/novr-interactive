using System.Reflection;
using N = System.Numerics;

// Source-linked lifecycle/position checks. This models Unity destroyed-object
// identity and hierarchy, not UGUI rendering, GPU projection, or headset vision.
namespace UnityEngine
{
    public class Object
    {
        internal bool Dead;
        public static bool operator ==(Object? a,Object? b)=>ReferenceEquals(a,b)||(a is null&&b!.Dead)||(b is null&&a!.Dead);
        public static bool operator !=(Object? a,Object? b)=>!(a==b);
        public override bool Equals(object? b)=>ReferenceEquals(this,b);
        public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static void Destroy(Object? value)
        {
            if(value==null)return;
            if(value is GameObject go)
            {
                foreach(var child in go.transform.Children.ToArray())Destroy(child.gameObject);
                foreach(var c in go.Components)c.Dead=true;
                go.transform.SetParent(null,false);
            }
            value!.Dead=true;
        }
        public static GameObject Instantiate(GameObject source)
        {
            var copy=new GameObject(source.name);
            var from=(RectTransform)source.transform;var to=(RectTransform)copy.transform;
            to.localPosition=from.localPosition;to.localRotation=from.localRotation;to.localScale=from.localScale;
            to.anchorMin=from.anchorMin;to.anchorMax=from.anchorMax;to.pivot=from.pivot;to.sizeDelta=from.sizeDelta;
            foreach(var graphic in source.Components.OfType<UI.RawImage>())
            {var next=copy.AddComponent<UI.RawImage>();next.texture=graphic.texture;next.material=graphic.material;next.color=graphic.color;next.uvRect=graphic.uvRect;next.raycastTarget=graphic.raycastTarget;}
            foreach(var child in from.Children)Instantiate(child.gameObject).transform.SetParent(to,false);
            return copy;
        }
    }
    public class Component:Object
    {
        public GameObject gameObject=null!;
        public Transform transform=>gameObject.transform;
        public string name=>gameObject.name;
        public T GetComponent<T>() where T:Component=>gameObject.GetComponent<T>();
    }
    public class Behaviour:Component {public bool enabled=true;}
    public class MonoBehaviour:Behaviour {}
    public sealed class GameObject:Object
    {
        internal readonly List<Component> Components=new();
        public readonly Transform transform;
        public readonly string name;
        public bool activeSelf=true;
        public bool activeInHierarchy=>!Dead&&activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
        public GameObject(string name,params Type[] components)
        {
            this.name=name;transform=new RectTransform{gameObject=this};Components.Add(transform);
            foreach(var t in components)if(t!=typeof(RectTransform)){var c=(Component)Activator.CreateInstance(t)!;c.gameObject=this;Components.Add(c);}
        }
        public T AddComponent<T>() where T:Component,new(){var c=new T{gameObject=this};Components.Add(c);return c;}
        public T GetComponent<T>() where T:Component=>Components.OfType<T>().FirstOrDefault(c=>!c.Dead)!;
        public void SetActive(bool value)=>activeSelf=value;
    }
    public class Transform:Component
    {
        internal readonly List<Transform> Children=new();
        public Transform? parent;
        public Vector3 localPosition,localScale=Vector3.one;
        public Quaternion localRotation=Quaternion.identity;
        public Vector3 position {get=>parent==null?localPosition:parent.position+parent.rotation*localPosition;set=>localPosition=parent==null?value:Quaternion.Inverse(parent.rotation)*(value-parent.position);}
        public Quaternion rotation {get=>parent==null?localRotation:parent.rotation*localRotation;set=>localRotation=parent==null?value:Quaternion.Inverse(parent.rotation)*value;}
        public Vector3 right=>rotation*Vector3.right;
        public Vector3 up=>rotation*Vector3.up;
        public Vector3 forward=>rotation*Vector3.forward;
        public int childCount=>Children.Count;
        public Transform GetChild(int i)=>Children[i];
        public Transform? Find(string name)=>Children.FirstOrDefault(c=>c.name==name);
        public void SetParent(Transform? next,bool worldPositionStays)
        {var p=position;var r=rotation;parent?.Children.Remove(this);parent=next;parent?.Children.Add(this);if(worldPositionStays){position=p;rotation=r;}}
        public int GetSiblingIndex()=>parent?.Children.IndexOf(this)??0;
        public void SetSiblingIndex(int i){if(parent==null)return;parent.Children.Remove(this);parent.Children.Insert(Math.Clamp(i,0,parent.Children.Count),this);}
        public void Rotate(Vector3 axis,float degrees,Space space=Space.Self)
        {var q=Quaternion.AngleAxis(degrees,axis);rotation=space==Space.Self?rotation*q:q*rotation;}
    }
    public sealed class RectTransform:Transform
    {
        public Vector2 anchorMin=new(.5f,.5f),anchorMax=new(.5f,.5f),pivot=new(.5f,.5f),sizeDelta=new(100,100),anchoredPosition;
        public Rect rect=>new(0,0,sizeDelta.x,sizeDelta.y);
    }
    public sealed class Texture:Object {}
    public sealed class Material:Object {}
    public sealed class Camera:Behaviour {}
    public sealed class Rigidbody:Component {public Vector3 velocity;}
    public enum Space {Self,World}
    public readonly record struct Vector2(float x,float y)
    {public static Vector2 zero=>new(0,0);public float magnitude=>MathF.Sqrt(x*x+y*y);}
    public readonly record struct Vector3(float x,float y,float z)
    {
        public static Vector3 zero=>new(0,0,0);public static Vector3 one=>new(1,1,1);public static Vector3 right=>new(1,0,0);public static Vector3 up=>new(0,1,0);public static Vector3 forward=>new(0,0,1);
        public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>MathF.Sqrt(sqrMagnitude);public Vector3 normalized=>magnitude>1e-6f?this*(1/magnitude):zero;
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);public static Vector3 operator *(float b,Vector3 a)=>a*b;
        public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
    }
    public readonly struct Quaternion(N.Quaternion value)
    {
        public readonly N.Quaternion Value=value;
        public static Quaternion identity=>new(N.Quaternion.Identity);
        public static Quaternion Inverse(Quaternion q)=>new(N.Quaternion.Inverse(q.Value));
        public static Quaternion AngleAxis(float degrees,Vector3 axis)=>new(N.Quaternion.CreateFromAxisAngle(new(axis.x,axis.y,axis.z),degrees*MathF.PI/180));
        public static Quaternion operator *(Quaternion a,Quaternion b)=>new(a.Value*b.Value);
        public static Vector3 operator *(Quaternion q,Vector3 v){var p=N.Vector3.Transform(new(v.x,v.y,v.z),q.Value);return new(p.X,p.Y,p.Z);}
        public static Quaternion LookRotation(Vector3 forward)=>LookRotation(forward,Vector3.up);
        public static Quaternion LookRotation(Vector3 forward,Vector3 up)
        {
            if(forward.sqrMagnitude<1e-10f)throw new InvalidOperationException("zero LookRotation direction");
            forward=forward.normalized;var right=Vector3.Cross(up,forward).normalized;up=Vector3.Cross(forward,right);
            return new(N.Quaternion.CreateFromRotationMatrix(new(right.x,right.y,right.z,0,up.x,up.y,up.z,0,forward.x,forward.y,forward.z,0,0,0,0,1)));
        }
    }
    public readonly record struct Rect(float x,float y,float width,float height);
    public readonly record struct Color(float r,float g,float b,float a=1);
    public static class Mathf
    {public const float Epsilon=float.Epsilon,Rad2Deg=180/MathF.PI;public static float Atan2(float a,float b)=>MathF.Atan2(a,b);public static float Abs(float v)=>MathF.Abs(v);public static float Lerp(float a,float b,float t)=>a+(b-a)*t;public static float Clamp01(float v)=>Math.Clamp(v,0,1);}
    [AttributeUsage(AttributeTargets.Class)]public sealed class DefaultExecutionOrder(int order):Attribute{public int Order=>order;}
}
namespace UnityEngine.UI
{
    public class Graphic:UnityEngine.Behaviour {public UnityEngine.Color color;public UnityEngine.Material? material;public bool raycastTarget=true;public UnityEngine.RectTransform rectTransform=>(UnityEngine.RectTransform)transform;}
    public sealed class RawImage:Graphic {public UnityEngine.Texture? texture;public UnityEngine.Rect uvRect=new(0,0,1,1);}
    public sealed class Image:Graphic {}
}
namespace TMPro {public class TMP_Text:UnityEngine.UI.Graphic {}}
namespace HarmonyLib
{
    public static class AccessTools {public static FieldInfo? Field(Type type,string name)=>type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);}
    [AttributeUsage(AttributeTargets.Class)]public sealed class HarmonyPatch(Type type,string method):Attribute {public Type Type=>type;public string Method=>method;}
    [AttributeUsage(AttributeTargets.Method)]public sealed class HarmonyPostfix:Attribute {}
}
namespace NOVR
{
    public static class APIBus {public static UnityEngine.Camera? MainCamera,CockpitHudCamera;}
    public static class LayerHelper {public static int GetVrUiLayer()=>30;public static void SetLayerRecursive(UnityEngine.Transform t,int layer){}}
    public sealed class ModConfiguration
    {public static readonly ModConfiguration Instance=new();public readonly Setting<bool> FocusedFlightHud=new(true);public readonly Setting<float> FlightHudScale=new(.38f);}
    public sealed class Setting<T>(T value) {public T Value=value;}
}
namespace NOVR.VrUi.SpecialBehavior
{
    public static class InstrumentBrightness {public static int Added;public static void Add(UnityEngine.Transform t)=>Added++;}
    public enum HudElementKind {Marker,Label,Reticle}
    public static class HudElementStyle {public static void Add(UnityEngine.UI.Graphic? g,HudElementKind kind){}}
}
namespace NOVR.VrUi.HarmonyPatches
{
    public static class NavigationHudProjection
    {
        public static readonly List<UnityEngine.Vector3> Projected=new();
        public static bool PinToEdge(UnityEngine.Vector3 p,out UnityEngine.Vector3 q,out float angle){Projected.Add(p);q=p;angle=0;return false;}
        public static UnityEngine.Vector3 LabelPosition(UnityEngine.Vector3 p,TMPro.TMP_Text label)=>p;
    }
    public static class VrHudProjection
    {
        public static readonly List<UnityEngine.Vector3> Projected=new();
        public static readonly List<(UnityEngine.Vector3 start,UnityEngine.Vector3 end)> Lines=new();
        public static bool Succeeds=true;
        public static bool TryProjectToCockpitHud(UnityEngine.Vector3 p,out UnityEngine.Vector3 q){Projected.Add(p);q=p;return Succeeds;}
        public static void SetVerticalLine(UnityEngine.Transform line,UnityEngine.Vector3 start,UnityEngine.Vector3 end,UnityEngine.Camera camera,float offset=0){Lines.Add((start,end));}
    }
}
public sealed class FlightHud:UnityEngine.MonoBehaviour
{
    // Real private serialized field names are the reflection boundary.
    private UnityEngine.UI.RawImage? pitchCompass;
    private UnityEngine.Transform? cockpitTransform;
    public void Bind(UnityEngine.UI.RawImage? source,UnityEngine.Transform? cockpit){pitchCompass=source;cockpitTransform=cockpit;}
    public void ReadBoundary(out UnityEngine.UI.RawImage? source,out UnityEngine.Transform? cockpit){source=pitchCompass;cockpit=cockpitTransform;}
}
public class SceneSingleton<T> where T:UnityEngine.Object {public static T? i;}
public sealed class CombatHUD:UnityEngine.MonoBehaviour {public Aircraft? aircraft;}
public sealed class Pilot:UnityEngine.MonoBehaviour {public FlightInfo? flightInfo=new();}
public sealed class FlightInfo {public bool HasTakenOff;}
public sealed class Aircraft:UnityEngine.MonoBehaviour {public Pilot[]? pilots;public float radarAlt=100;public UnityEngine.Rigidbody? rb;}
public sealed class Airbase:UnityEngine.MonoBehaviour
{
    public UnityEngine.Transform? center;
    public sealed class Runway
    {
        public UnityEngine.Transform Start=null!,End=null!;
        public UnityEngine.Vector3 LastAimDistanceAndTime;
        public float GetWidth()=>20;
        public UnityEngine.Vector3 GetVelocity()=>UnityEngine.Vector3.zero;
        public readonly struct RunwayUsage(Runway runway,bool reverse)
        {
            public readonly Runway Runway=runway;
            public UnityEngine.Transform GetStart()=>reverse?Runway.End:Runway.Start;
            public UnityEngine.Transform GetEnd()=>reverse?Runway.Start:Runway.End;
            public GlobalPosition GetTouchdownPoint()=>new(GetStart().position+(GetEnd().position-GetStart().position).normalized*30+GlobalPosition.Origin);
            public UnityEngine.Vector3 GetGlideslopeAimpoint(Aircraft aircraft,float distance,float time){Runway.LastAimDistanceAndTime=new(distance,time,0);return GetTouchdownPoint().ToLocalPosition()+UnityEngine.Vector3.up*100;}
        }
    }
}
public readonly record struct GlobalPosition(UnityEngine.Vector3 Position)
{
    public static UnityEngine.Vector3 Origin;
    public UnityEngine.Vector3 ToLocalPosition()=>Position-Origin;
}
public static class FastMath {public static float Distance(UnityEngine.Vector3 a,UnityEngine.Vector3 b)=>(a-b).magnitude;}
public sealed class AirbaseOverlay:UnityEngine.MonoBehaviour
{
    public UnityEngine.UI.Image? airbaseMarker;
    public TMPro.TMP_Text? airbaseLabel;
    public Airbase? nearestAirbase;
    public Airbase.Runway.RunwayUsage? runwayUsage;
    public bool landing,taxiingToRunway,reachedRunway;
    public UnityEngine.UI.Image?[]? runwayBorders;
    public UnityEngine.UI.Image? glideslope,glideslopeAimPoint;
}
