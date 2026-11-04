using NOVR.Installer.Models;

// Only OS/network/file-operation boundaries are replaced. Commands, busy-state
// ownership, picker callbacks, error handling and destination capture are real.
namespace NOVR.Installer.Services;

internal static class Boundary
{
    internal static readonly string Root = Path.Combine(Path.GetTempPath(), "novr-ui-check-" + Guid.NewGuid().ToString("N"));
    internal static readonly string GameA = Path.Combine(Root, "game-a");
    internal static readonly string GameB = Path.Combine(Root, "game-b");
    internal static readonly string Package = Path.Combine(Root, "NOVR.zip");
    internal static bool GameAvailable = true;
    internal static InstallState ModState = InstallState.NotInstalled;
    internal static int Downloads, Scans, Uninstalls;
    internal static readonly List<(string Game, string Package)> Installs = new();
    internal static TaskCompletionSource? PendingInstall;

    internal static void Reset()
    {
        GameAvailable = true;
        ModState = InstallState.NotInstalled;
        Downloads = Scans = Uninstalls = 0;
        Installs.Clear();
        PendingInstall = null;
    }
}

public sealed class GameLocator
{
    public GameInstallInfo? FindGame()
    {
        Boundary.Scans++;
        return null;
    }
    public GameInstallInfo Inspect(string path) => new(path,
        Boundary.GameAvailable && (path == Boundary.GameA || path == Boundary.GameB),
        true, Boundary.ModState, "The game folder is unavailable.");
}

public sealed class GitHubReleaseClient
{
    public ReleaseVersion FoundNOVRRelease => (ReleaseVersion)"1.0.0";
    public Task<string> DownloadLatestNovrReleaseAsync(string dir, IProgress<string> progress, CancellationToken token)
    {
        Boundary.Downloads++;
        return Task.FromResult(Boundary.Package);
    }
    public Task<string> DownloadLatestBepInEx5Async(string dir, IProgress<string> progress, CancellationToken token)
        => throw new InvalidOperationException("Fixture already has BepInEx; no download is expected.");
}

public sealed class FileSystemInstaller
{
    public Task InstallBepInExAsync(GameInstallInfo game, string zip, IProgress<string> progress, CancellationToken token)
        => throw new InvalidOperationException("Fixture already has BepInEx; no installation is expected.");
    public Task InstallOrUpdateNovrAsync(GameInstallInfo game, string zip, string version, IProgress<string> progress, CancellationToken token)
    {
        Boundary.Installs.Add((game.GameDir, zip));
        return Boundary.PendingInstall?.Task ?? Task.CompletedTask;
    }
    public Task UninstallNovrAsync(GameInstallInfo game, IProgress<string> progress, CancellationToken token)
    {
        Boundary.Uninstalls++;
        return Task.CompletedTask;
    }
    public Task UninstallBepInExAsync(GameInstallInfo game, IProgress<string> progress, CancellationToken token)
    {
        Boundary.Uninstalls++;
        return Task.CompletedTask;
    }
}

public sealed class ProtonPrefixService
{
    public Task<string> TryConfigureWinHttpOverrideAsync(GameInstallInfo game, IProgress<string> progress, CancellationToken token)
        => Task.FromResult("No prefix changes in this fixture.");
}

public static class NovrPackage
{
    public static ReleaseVersion ReadVersion(string path) => (ReleaseVersion)"1.0.0";
    public static void Validate(string path, string version)
    {
    }
}
