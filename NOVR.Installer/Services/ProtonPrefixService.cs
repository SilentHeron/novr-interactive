using System.Runtime.InteropServices;
using NOVR.Installer.Models;

namespace NOVR.Installer.Services;

public sealed class ProtonPrefixService
{
    public Task<string> TryConfigureWinHttpOverrideAsync(GameInstallInfo game, IProgress<string> progress, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var linux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        if (linux) progress.Report("Proton setup needs a manual per-game launch option.");
        return Task.FromResult(SetupGuidance(game.GameDir, linux));
    }

    internal static string SetupGuidance(string gameDir, bool linux)
    {
        if (!linux) return "Proton/Wine override is only needed on Linux.";
        var prefix = FindPrefix(gameDir);
        return "Manual Proton setup: in Steam, open Nuclear Option > Properties > General > Launch Options and add " +
               "WINEDLLOVERRIDES=\"winhttp=n,b\" %command%. " +
               "Preserve existing launch options; if %command% or WINEDLLOVERRIDES is already present, merge this override instead of adding a duplicate. " +
               "The installer has not changed Wine/Proton settings." + Environment.NewLine +
               (prefix is null
                   ? "No existing Nuclear Option prefix was found in the selected Steam library."
                   : "Nuclear Option prefix: " + prefix);
    }

    internal static string? FindPrefix(string gameDir)
    {
        // Never guess from another game's timestamps or launch host Wine against
        // a prefix managed by a potentially different Proton version.
        var common = new DirectoryInfo(Path.GetFullPath(gameDir)).Parent;
        var steamapps = common?.Parent;
        if (common?.Name != "common" || steamapps?.Name != "steamapps") return null;
        var prefix = Path.Combine(steamapps.FullName, "compatdata", "2168680", "pfx");
        return Directory.Exists(prefix) ? prefix : null;
    }
}
