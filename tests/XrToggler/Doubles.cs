// Boundary doubles model the inspected XRManagerSettings lifecycle contract:
// deinitialize stops, clears activeLoader and clears initialization completion;
// StartSubsystems cannot initialize a loader itself. No native XR runtime runs.
namespace UnityEngine
{
    public class ScriptableObject
    {
        public static int Created;
        public static T CreateInstance<T>() where T : ScriptableObject, new()
        {
            Created++;
            return new T();
        }
    }
    public static class Application
    {
        public static bool runInBackground;
    }
}
namespace UnityEngine.XR.Management
{
    public class XRLoader : ScriptableObject
    {
        public int Initializes, Starts, Stops, Deinitializes;
        public bool CanInitialize = true, ThrowInitialize, ThrowStart, ThrowDeinitialize;
        public readonly List<string> Calls = new();
        public virtual bool Initialize()
        {
            Initializes++;
            Calls.Add("initialize");
            if (ThrowInitialize)
                throw new InvalidOperationException("initialize failed");
            return CanInitialize;
        }
        public virtual bool Start()
        {
            Starts++;
            Calls.Add("start");
            if (ThrowStart)
                throw new InvalidOperationException("start failed");
            return true;
        }
        public virtual bool Stop()
        {
            Stops++;
            Calls.Add("stop");
            return true;
        }
        public virtual bool Deinitialize()
        {
            Deinitializes++;
            Calls.Add("deinitialize");
            if (ThrowDeinitialize)
                throw new InvalidOperationException("deinitialize failed");
            return true;
        }
    }
    public class XRManagerSettings : ScriptableObject
    {
        public static XRManagerSettings? Last;
        public readonly List<XRLoader> loaders = new();
        public XRLoader? activeLoader;
        public bool isInitializationComplete;
        public int InitializeCalls, StartCalls, DeinitializeCalls;
        public XRManagerSettings() => Last = this;
        public void InitializeLoaderSync()
        {
            InitializeCalls++;
            if (activeLoader != null)
                return;
            foreach (var loader in loaders)
                if (loader.Initialize())
                {
                    activeLoader = loader;
                    isInitializationComplete = true;
                    return;
                }
        }
        public void StartSubsystems()
        {
            StartCalls++;
            if (isInitializationComplete && activeLoader != null)
                activeLoader.Start();
        }
        public void StopSubsystems()
        {
            if (isInitializationComplete && activeLoader != null)
                activeLoader.Stop();
        }
        public void DeinitializeLoader()
        {
            DeinitializeCalls++;
            if (!isInitializationComplete)
                return;
            StopSubsystems();
            activeLoader?.Deinitialize();
            activeLoader = null;
            isInitializationComplete = false;
        }
    }
    public class XRGeneralSettings : ScriptableObject
    {
        public XRManagerSettings Manager = null!;
    }
}
namespace UnityEngine.XR.OpenXR
{
    public sealed class OpenXRSettings
    {
        public enum RenderMode
        {
            SinglePassInstanced, MultiPass
        }
        public static readonly OpenXRSettings Instance = new();
        public RenderMode renderMode;
        public bool ProfilesEnabled;
        public void EnableNovrControllerProfiles() => ProfilesEnabled = true;
    }
    public class OpenXRLoader : Management.XRLoader
    {
        public override bool Initialize()
        {
            if (!OpenXRSettings.Instance.ProfilesEnabled || OpenXRSettings.Instance.renderMode != OpenXRSettings.RenderMode.MultiPass)
                throw new Exception("NOVR stereo and controller profiles must be configured before loader initialization");
            return base.Initialize();
        }
    }
}
