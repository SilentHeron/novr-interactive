// Engine and game boundaries only: what each call was asked to do is recorded,
// nothing is drawn. Whether the result looks right needs a headset.
namespace UnityEngine
{
    public class Object
    {
        internal bool Dead;
        public string name = "";
        public HideFlags hideFlags;
        public static bool operator ==(Object? a, Object? b) => ReferenceEquals(a, b) || (a is null || a.Dead) && (b is null || b.Dead);
        public static bool operator !=(Object? a, Object? b) => !(a == b);
        public override bool Equals(object? other) => ReferenceEquals(this, other);
        public override int GetHashCode() => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        public static void Destroy(Object? value)
        {
            if (value is not null)
                value.Dead = true;
        }
    }
    public enum HideFlags { None, HideAndDontSave = 61 }
    public class Component : Object
    {
        public Transform transform = null!;
    }
    public class MonoBehaviour : Component
    {
    }
    public static class Debug
    {
        public static readonly List<string> Warnings = new();
        public static void LogWarning(object message) => Warnings.Add(message.ToString()!);
    }
    public static class Time
    {
        public static float unscaledTime;
    }
    public static class SystemInfo
    {
        public static bool FloatTargets = true;
        public static bool IsFormatSupported(Experimental.Rendering.GraphicsFormat format, Experimental.Rendering.FormatUsage usage) =>
            format != Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat || FloatTargets;
    }
    public static class Mathf
    {
        public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
        public static int Max(int a, int b) => Math.Max(a, b);
    }
    public record struct Vector2(float x, float y)
    {
        public readonly float magnitude => MathF.Sqrt(x * x + y * y);
        public static explicit operator Vector2(Vector3 v) => new(v.x, v.y);
    }
    public record struct Vector3(float x, float y, float z)
    {
        public static Vector3 right => new(1, 0, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float s) => new(a.x * s, a.y * s, a.z * s);
    }
    public record struct Vector4(float x, float y, float z, float w)
    {
        public static Vector4 operator *(Vector4 a, float s) => new(a.x * s, a.y * s, a.z * s, a.w * s);
    }
    public record struct Rect(float x, float y, float width, float height);
    public enum FilterMode { Point, Bilinear, Trilinear }
    public enum TextureWrapMode { Repeat, Clamp }
    public enum SpriteMeshType { FullRect, Tight }

    public class Transform : Component
    {
        public Vector3 position;
        // World length of one local unit along each axis.
        public float Scale = 1;
        public Vector3 TransformVector(Vector3 local) => local * Scale;
    }
    public sealed class RectTransform : Transform
    {
        public Rect rect;
    }

    public class Texture : Object
    {
        public int width, height, mipmapCount = 1, anisoLevel = 1;
        public FilterMode filterMode = FilterMode.Bilinear;
        public TextureWrapMode wrapMode;
        public Experimental.Rendering.GraphicsFormat graphicsFormat;
    }
    public sealed class Texture2D : Texture
    {
        public Experimental.Rendering.TextureCreationFlags Flags;
        public Texture2D()
        {
        }
        public Texture2D(int width, int height, Experimental.Rendering.GraphicsFormat format, Experimental.Rendering.TextureCreationFlags flags)
        {
            this.width = width;
            this.height = height;
            graphicsFormat = format;
            Flags = flags;
            mipmapCount = (flags & Experimental.Rendering.TextureCreationFlags.MipChain) != 0 ? 1 + (int)Math.Log2(Math.Max(width, height)) : 1;
            Graphics.Calls.Add("texture " + width + "x" + height + " " + format);
            Graphics.Made.Add(this);
        }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) =>
            Graphics.Calls.Add("apply mips=" + updateMipmaps + " release=" + makeNoLongerReadable);
    }
    public struct RenderTextureDescriptor
    {
        public int width, height, depthBufferBits;
        public Experimental.Rendering.GraphicsFormat graphicsFormat;
        public bool useMipMap, autoGenerateMips;
        public RenderTextureDescriptor(int width, int height, Experimental.Rendering.GraphicsFormat format, int depthBufferBits)
        {
            this.width = width;
            this.height = height;
            graphicsFormat = format;
            this.depthBufferBits = depthBufferBits;
            useMipMap = false;
            autoGenerateMips = true;
        }
    }
    public sealed class RenderTexture : Texture
    {
        public static RenderTexture? active;
        public static int Temporaries;
        public static RenderTexture GetTemporary(RenderTextureDescriptor descriptor)
        {
            Temporaries++;
            return new RenderTexture { width = descriptor.width, height = descriptor.height, graphicsFormat = descriptor.graphicsFormat, name = "step " + descriptor.width + "x" + descriptor.height, Temporary = true };
        }
        public static void ReleaseTemporary(RenderTexture texture)
        {
            if (!texture.Temporary || texture.Dead)
                throw new InvalidOperationException("released twice or not temporary");
            texture.Dead = true;
            Temporaries--;
        }
        public bool Temporary;
        // Test controls: whether Create succeeds with or without mipmaps.
        public static bool FailNextCreate;
        public bool RefuseMipmaps;
        public bool useMipMap, autoGenerateMips = true;
        public int antiAliasing = 1, Creates, Releases;
        private bool _created;
        public RenderTexture()
        {
        }
        public RenderTexture(RenderTextureDescriptor descriptor)
        {
            width = descriptor.width;
            height = descriptor.height;
            graphicsFormat = descriptor.graphicsFormat;
            useMipMap = descriptor.useMipMap;
            autoGenerateMips = descriptor.autoGenerateMips;
            Graphics.Calls.Add("target " + width + "x" + height + " " + graphicsFormat + " mips=" + useMipMap + " auto=" + autoGenerateMips);
            Graphics.Targets.Add(this);
        }
        public void MarkCreated() => _created = true;
        public bool IsCreated() => _created;
        public bool Create()
        {
            Creates++;
            if (FailNextCreate || RefuseMipmaps && useMipMap)
            {
                FailNextCreate = false;
                return false;
            }
            return _created = true;
        }
        public void Release()
        {
            Releases++;
            _created = false;
        }
        public void GenerateMips() => Graphics.Calls.Add("generate mips");
    }
    public static class Graphics
    {
        public static readonly List<string> Calls = new();
        public static readonly List<Texture2D> Made = new();
        public static readonly List<RenderTexture> Targets = new();
        public static bool FailCopy;
        public static void Blit(Texture source, RenderTexture target)
        {
            // As in the engine, a blit leaves its target active.
            RenderTexture.active = target;
            Calls.Add("blit " + source.name + " (" + source.filterMode + ") -> " + target.width + "x" + target.height);
        }
        public static void CopyTexture(Texture source, int sourceElement, int sourceMip, Texture target, int targetElement, int targetMip)
        {
            if (FailCopy)
                throw new InvalidOperationException("copy refused");
            Calls.Add("copy " + source.width + "x" + source.height + " -> level " + targetMip);
        }
        public static void Reset()
        {
            Calls.Clear();
            Made.Clear();
            Targets.Clear();
            FailCopy = false;
            RenderTexture.FailNextCreate = false;
        }
    }
    public sealed class Sprite : Object
    {
        public Texture2D texture = null!;
        public Rect rect;
        public Vector2 pivot;
        public float pixelsPerUnit = 100;
        public Vector4 border;
        public bool packed;
        public SpriteMeshType MeshType;
        public static Sprite Create(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit, uint extrude, SpriteMeshType meshType, Vector4 border)
        {
            Graphics.Calls.Add("sprite");
            // Unity takes a normalised pivot here and reports it in pixels.
            return new Sprite
            {
                texture = texture, rect = rect, pivot = new(pivot.x * rect.width, pivot.y * rect.height),
                pixelsPerUnit = pixelsPerUnit, border = border, MeshType = meshType
            };
        }
    }
    public sealed class Camera : Component
    {
        public RenderTexture? targetTexture;
        public int Projections;
        // A pinhole at the origin looking along +z: this many pixels per metre at one metre.
        public float PixelsPerMetreAtOne = 1000;
        public Vector3 WorldToScreenPoint(Vector3 world)
        {
            Projections++;
            var depth = world.z == 0 ? 1e-6f : world.z;
            return new(world.x / depth * PixelsPerMetreAtOne, world.y / depth * PixelsPerMetreAtOne, world.z);
        }
    }
}
namespace UnityEngine.Experimental.Rendering
{
    public enum GraphicsFormat { None, R8G8B8A8_SRGB, R8G8B8A8_UNorm, RGBA_DXT1_SRGB, RGBA_DXT1_UNorm, R16G16B16A16_SFloat }
    public enum FormatUsage { Sample, Render }
    [Flags]
    public enum TextureCreationFlags { None = 0, MipChain = 1, DontInitializePixels = 4 }
    public static class GraphicsFormatUtility
    {
        public static bool IsSRGBFormat(GraphicsFormat format) => format is GraphicsFormat.R8G8B8A8_SRGB or GraphicsFormat.RGBA_DXT1_SRGB;
    }
}
namespace UnityEngine.UI
{
    public sealed class Image : UnityEngine.Component
    {
        public UnityEngine.Sprite? sprite;
        public UnityEngine.RectTransform rectTransform = new();
        public int OverrideWrites;
        private UnityEngine.Sprite? _override;
        // As in uGUI: reading gives the sprite in use, writing sets only the override.
        public UnityEngine.Sprite? overrideSprite
        {
            get => _override ?? sprite;
            set
            {
                OverrideWrites++;
                _override = value;
            }
        }
        public UnityEngine.Sprite? Override => _override;
    }
}
namespace UnityEngine.XR
{
    public static class XRSettings
    {
        public static bool enabled;
    }
}
namespace UnityEngine.Rendering
{
    public sealed class VolumeProfile : UnityEngine.Object
    {
        public readonly List<object> Components = new();
        public bool TryGet<T>(out T component) where T : class
        {
            component = Components.OfType<T>().FirstOrDefault()!;
            return component != null;
        }
    }
    public sealed class Volume : UnityEngine.Component
    {
        public VolumeProfile? sharedProfile;
        public VolumeProfile? Instance;
        // Asking for profile makes an instance in the engine; count it.
        public int ProfileReads;
        public bool HasInstantiatedProfile() => Instance != null;
        public VolumeProfile? profile
        {
            get
            {
                ProfileReads++;
                return Instance ??= new VolumeProfile();
            }
        }
    }
    public sealed class FloatParameter
    {
        public float value;
    }
}
namespace UnityEngine.Rendering.Universal
{
    public sealed class Vignette : UnityEngine.Object
    {
        public readonly FloatParameter intensity = new();
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class HarmonyPatch(Type type, string method) : Attribute
    {
        public Type Type => type;
        public string Method => method;
    }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class HarmonyPostfix : Attribute
    {
    }
}
public static class SceneSingleton<T>
{
    public static T? i;
}
public sealed class CameraStateManager : UnityEngine.MonoBehaviour
{
    public UnityEngine.Rendering.Volume? Volume;
    public int Queries;
    public UnityEngine.Rendering.Volume? GetPostProcessVolume()
    {
        Queries++;
        return Volume;
    }
}
public sealed class TacScreen
{
    public void Initialize()
    {
    }
}
namespace NOVR
{
    internal sealed class Setting(bool value)
    {
        internal bool Value = value;
    }
    internal sealed class ModConfiguration
    {
        internal static ModConfiguration Instance = new();
        internal readonly ImageQualitySettings ImageQuality = new();
        internal sealed class ImageQualitySettings
        {
            internal readonly Setting RemoveRestingVignette = new(true), FilterCockpitScreens = new(true);
        }
    }
    internal static class APIBus
    {
        internal static UnityEngine.Camera? MainCamera;
    }
}
