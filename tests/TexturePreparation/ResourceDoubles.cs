// Minimal ownership/readback boundary for the production material owner. No Unity
// rendering is emulated; allocation, upload failure and destruction are observable.
internal sealed class Faction
{
    internal string? factionTag, factionName;
}
namespace UnityEngine
{
    internal class Object
    {
        internal static readonly List<Object> Created = new();
        internal string name = "";
        internal int DestroyCount;
        internal Object() => Created.Add(this);
        internal static void Destroy(Object? value)
        {
            if (value != null)
                value.DestroyCount++;
        }
    }
    internal enum TextureFormat
    {
        RGBA32
    }
    internal enum FilterMode
    {
        Trilinear
    }
    internal enum TextureWrapMode
    {
        Repeat, Clamp
    }
    internal enum RenderTextureFormat
    {
        ARGB32
    }
    internal enum RenderTextureReadWrite
    {
        Linear, sRGB
    }
    internal class Texture : Object
    {
        internal int width = 8, height = 4, anisoLevel;
        internal TextureWrapMode wrapMode, wrapModeU, wrapModeV;
    }
    internal sealed class Texture2D : Texture
    {
        internal static Func<Texture2D, string, bool>? Fail;
        internal readonly bool Linear;
        internal FilterMode filterMode;
        internal byte[] Pixels = Array.Empty<byte>();
        internal Texture2D(int width, int height, TextureFormat format, bool mipmaps, bool linear = false)
        {
            this.width = width;
            this.height = height;
            Linear = linear;
        }
        private void Check(string operation)
        {
            if (Fail?.Invoke(this, operation) == true)
                throw new InvalidOperationException("Injected " + operation + " failure");
        }
        internal bool LoadImage(byte[] data, bool discard)
        {
            if (Fail?.Invoke(this, "decode") == true)
                return false;
            width = 2;
            height = name.Contains("Fabric") ? 3 : 2;
            Pixels = Enumerable.Range(0, width * height).SelectMany(i => new byte[] { (byte)(96 + i), 128, 150, 255 }).ToArray();
            return true;
        }
        internal Color32[] GetPixels32()
        {
            Check("read");
            return Enumerable.Range(0, Pixels.Length / 4).Select(i => new Color32(Pixels[i * 4], Pixels[i * 4 + 1], Pixels[i * 4 + 2], Pixels[i * 4 + 3])).ToArray();
        }
        internal void SetPixels32(Color32[] value)
        {
            Check("pixels");
            Pixels = value.SelectMany(p => new[] { p.r, p.g, p.b, p.a }).ToArray();
        }
        internal void SetPixelData(byte[] value, int mip)
        {
            Check("upload");
            Pixels = value;
        }
        internal void Apply(bool mipmaps = true, bool discard = false) => Check("apply");
        internal void ReadPixels(Rect rect, int x, int y, bool recalculate = true) => Pixels = Enumerable.Repeat(new byte[] { 96, 96, 96, 255 }, width * height).SelectMany(x => x).ToArray();
        internal byte[] GetRawTextureData() => Pixels;
        internal Color GetPixelBilinear(float u, float v) => Color.white;
    }
    internal sealed class Material : Object
    {
        internal readonly Dictionary<string, Texture?> Maps = new();
        internal readonly Dictionary<string, float> Floats = new();
        internal readonly Dictionary<string, Color> Colors = new();
        internal readonly float NormalStrength = .65f;
        internal readonly (float, float) Scale = (2f, 3f), Offset = (.3f, .1f);
        internal readonly bool Keyword = true;
        internal Material()
        {
        }
        internal Material(Material native)
        {
            foreach (var p in native.Maps)
                Maps.Add(p.Key, p.Value);
            foreach (var p in native.Floats)
                Floats.Add(p.Key, p.Value);
            foreach (var p in native.Colors)
                Colors.Add(p.Key, p.Value);
        }
        internal Texture? mainTexture => Maps.GetValueOrDefault("_MainTex");
        internal bool HasProperty(string name) => Maps.ContainsKey(name) || Floats.ContainsKey(name) || Colors.ContainsKey(name);
        internal Texture? GetTexture(string name) => Maps[name];
        internal void SetTexture(string name, Texture? texture) => Maps[name] = texture;
        internal void SetTextureScale(string name, Vector2 value)
        {
        }
        internal void SetTextureOffset(string name, Vector2 value)
        {
        }
        internal void SetColor(string name, Color color) => Colors[name] = color;
        internal float GetFloat(string name) => Floats[name];
        internal void SetFloat(string name, float value) => Floats[name] = value;
        internal void EnableKeyword(string name)
        {
        }
        internal void DisableKeyword(string name)
        {
        }
    }
    internal sealed class RenderTexture : Texture
    {
        internal static RenderTexture? active;
        internal static RenderTexture GetTemporary(int w, int h, int depth, RenderTextureFormat format, RenderTextureReadWrite space) => new() { width = w, height = h };
        internal static void ReleaseTemporary(RenderTexture texture) => Destroy(texture);
    }
    internal static class Graphics
    {
        internal static void Blit(Texture source, RenderTexture target)
        {
        }
    }
    internal static class Debug
    {
        internal static void Log(object value)
        {
        }
        internal static void LogWarning(object value)
        {
        }
    }
    internal static class Mathf
    {
        internal static int Max(int a, int b) => Math.Max(a, b);
        internal static float Max(float a, float b) => Math.Max(a, b);
        internal static float Min(float a, float b) => Math.Min(a, b);
        internal static int Clamp(int value, int min, int max) => Math.Clamp(value, min, max);
        internal static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        internal static int RoundToInt(float value) => (int)Math.Round(value);
        internal static int NextPowerOfTwo(int value)
        {
            var result = 1;
            while (result < value)
                result *= 2;
            return result;
        }
    }
    internal readonly struct Vector2
    {
        internal static Vector2 one => new(1, 1);
        internal static Vector2 zero => new(0, 0);
        internal readonly float x, y;
        internal Vector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }
    }
    internal readonly struct Rect
    {
        internal readonly float x, y, width, height;
        internal Rect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }
    }
    internal readonly struct Color32
    {
        internal readonly byte r, g, b, a;
        internal Color32(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }
    }
    internal readonly struct Color
    {
        internal readonly float r, g, b, a;
        internal Color(float r, float g, float b, float a = 1)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }
        internal static Color white => new(1, 1, 1);
        internal static Color black => new(0, 0, 0);
        internal float maxColorComponent => Math.Max(r, Math.Max(g, b));
        public static Color operator +(Color x, Color y) => new(x.r + y.r, x.g + y.g, x.b + y.b, x.a + y.a);
        public static Color operator /(Color x, float y) => new(x.r / y, x.g / y, x.b / y, x.a / y);
    }
}
