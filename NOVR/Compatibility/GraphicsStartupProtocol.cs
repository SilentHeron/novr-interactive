using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NOVR.Compatibility;

// Shared by the plugin and preloader. These variables exist only in the child
// process; no Steam launch options, game assets or user settings are rewritten.
internal static class GraphicsStartupProtocol
{
    internal const string RetryVariable = "NOVR_D3D11_RELAUNCH";
    internal const string ParentIdVariable = "NOVR_D3D11_PARENT_ID";
    internal const string ParentStartVariable = "NOVR_D3D11_PARENT_START";
    internal const string FlatArgument = "-novr-flat";
    internal const int ParentWaitMilliseconds = 15000;

    internal static bool IsNuclearOptionSteamContext(string? appId, string? gameId) =>
        appId == "2168680" || gameId == "2168680";

    private static readonly HashSet<string> RendererArguments = new(StringComparer.OrdinalIgnoreCase)
    {
        "-force-d3d11", "-force-d3d12", "-force-d3d9", "-force-vulkan",
        "-force-opengl", "-force-glcore", "-force-glcore32", "-force-glcore33",
        "-force-glcore40", "-force-glcore41", "-force-glcore42", "-force-glcore43",
        "-force-glcore44", "-force-glcore45", "-force-gles", "-force-gles20",
        "-force-gles30", "-force-gles31", "-force-gles31aep", "-force-gles32", "-force-metal"
    };

    internal static bool IsFlatLaunch(IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
            if (string.Equals(argument, FlatArgument, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "-batchmode", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, "-nographics", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // Input excludes argv[0]. Keep unknown flags and values, including the user's
    // display, GPU-selection and log options. Replace only exact API selectors.
    internal static string BuildD3D11Arguments(IEnumerable<string> arguments)
    {
        var result = new StringBuilder();
        foreach (var argument in arguments)
        {
            if (RendererArguments.Contains(argument))
                continue;
            if (result.Length > 0)
                result.Append(' ');
            result.Append(QuoteWindowsArgument(argument));
        }
        if (result.Length > 0)
            result.Append(' ');
        return result.Append("-force-d3d11").ToString();
    }

    // Windows CreateProcess/CommandLineToArgvW quoting, also used by the Windows
    // player under Proton. No shell expansion or shell command is involved.
    internal static string QuoteWindowsArgument(string value)
    {
        var result = new StringBuilder("\"");
        var slashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                slashes++;
                continue;
            }
            result.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
            result.Append(character);
            slashes = 0;
        }
        result.Append('\\', slashes * 2);
        return result.Append('"').ToString();
    }

    internal static bool TryReadParent(string? id, string? start, out int processId, out long startTicks)
    {
        startTicks = 0;
        return int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out processId) && processId > 0 &&
            long.TryParse(start, NumberStyles.None, CultureInfo.InvariantCulture, out startTicks) &&
            startTicks > DateTime.MinValue.Ticks && startTicks <= DateTime.MaxValue.Ticks;
    }
}
