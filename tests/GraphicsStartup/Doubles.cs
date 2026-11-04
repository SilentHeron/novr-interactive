// Unity and OS process creation are boundaries. Production argument construction,
// restart decisions and preloader identity/wait policy run unchanged.
namespace UnityEngine
{
    public enum RuntimePlatform
    {
        WindowsPlayer, LinuxPlayer
    }
    public static class Application
    {
        public static RuntimePlatform platform = RuntimePlatform.WindowsPlayer;
        public static int QuitCalls;
        public static void Quit() => QuitCalls++;
    }
    public static class SystemInfo
    {
        public static Rendering.GraphicsDeviceType graphicsDeviceType = Rendering.GraphicsDeviceType.Direct3D11;
    }
    public static class Debug
    {
        public static readonly List<string> Warnings = new();
        public static void LogWarning(object message) => Warnings.Add(message.ToString()!);
    }
}
namespace UnityEngine.Rendering
{
    public enum GraphicsDeviceType
    {
        Null, Direct3D11, Vulkan
    }
}
