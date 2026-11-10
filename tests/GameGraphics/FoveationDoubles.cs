namespace NOVR
{
    internal sealed class ModConfiguration
    {
        internal static ModConfiguration Instance = new();
        internal readonly Rendering.FixedFoveationSettings FixedFoveation = new();
        internal readonly Rendering.ImageQualitySettings ImageQuality = new();
    }
}

namespace NOVR.Rendering
{
    internal sealed class SettingValue<T>(T value)
    {
        private T _value = value;
        internal int Writes;
        internal T Value
        {
            get => _value;
            set { Writes++; _value = value; }
        }
    }

    internal sealed class ImageQualitySettings
    {
        internal readonly SettingValue<bool> DitherGradients = new(true), RemoveRestingVignette = new(true),
            FilterCockpitScreens = new(true), SmoothCockpitMap = new(true);
    }

    internal sealed class FixedFoveationSettings
    {
        internal readonly SettingValue<FixedFoveationLevel> Level = new(FixedFoveationLevel.Off);
        internal readonly SettingValue<bool> UseEyeTracking = new(false), MeasureGpuCost = new(false);
        internal FixedFoveationLevel RequestedLevel => FixedFoveationPolicy.Normalize((int)Level.Value);
    }

    // The native/runtime boundary records page intent only. Runtime eligibility
    // and application/failure behavior is production-linked in its own suite.
    internal static class FixedFoveationRuntime
    {
        internal static bool IsAvailable;
        internal static string Status = "Unavailable on Linux/Proton.";
        internal static string EyeTrackingStatus = "Centre: fixed.";
        internal static readonly List<string> Calls = new();

        internal static void SetLevel(int value)
        {
            Calls.Add("level:" + value);
            if (value != 0 && !IsAvailable) return;
            ModConfiguration.Instance.FixedFoveation.Level.Value = FixedFoveationPolicy.Normalize(value);
        }
        internal static void SetEyeTracking(bool value)
        {
            Calls.Add("eyes:" + value);
            if (value && !IsAvailable) return;
            ModConfiguration.Instance.FixedFoveation.UseEyeTracking.Value = value;
        }
        internal static void SetDiagnostics(bool value)
        {
            Calls.Add("measure:" + value);
            if (value && !IsAvailable) return;
            ModConfiguration.Instance.FixedFoveation.MeasureGpuCost.Value = value;
        }
        internal static void Reset(bool available)
        {
            ModConfiguration.Instance = new();
            IsAvailable = available;
            Status = available ? "Off. Compatible NVIDIA hardware detected." : "Unavailable on Linux/Proton.";
            EyeTrackingStatus = "Centre: fixed.";
            Calls.Clear();
        }
    }
}

namespace NOVR.GameplayPatches
{
    // The cloud option applies through its own patch; the page only calls it.
    internal static class DistantCloudBanks
    {
        internal static bool HideInVr = true;
        internal static readonly List<bool> Calls = new();
        internal static void SetHideInVr(bool hide)
        {
            Calls.Add(hide);
            HideInVr = hide;
        }
    }
}
