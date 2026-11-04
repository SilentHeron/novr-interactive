using System.IO.Compression;
using System.Text.Json;
using NOVR.Installer.Models;

namespace NOVR.Installer.Services;

public sealed class FileSystemInstaller
{
    public async Task InstallBepInExAsync(GameInstallInfo game, string bepInExZip, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Installing BepInEx...");
        await Task.Run(() =>
        {
            using var gameLock = AcquireGameLock(game);
            using var legacyLock = AcquireLegacyLock(game);
            ZipFile.ExtractToDirectory(bepInExZip, game.GameDir, overwriteFiles: true);
        }, cancellationToken);
    }

    private const string TransactionFolder = ".novr-update";
    private sealed record InstallJournal(bool HadPlugin, bool HadPatcher);

    public async Task InstallOrUpdateNovrAsync(GameInstallInfo game, string novrZip, string releaseName, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Validating NOVR " + releaseName + "...");
        await Task.Run(() => InstallTransaction(game, novrZip, releaseName, progress, cancellationToken), cancellationToken);
    }

    private static void InstallTransaction(GameInstallInfo game, string zip, string release, IProgress<string> progress, CancellationToken cancellationToken)
    {
        // Validate before touching the live installation. Staging beside it keeps directory moves on one volume.
        NovrPackage.Validate(zip, release);
        using var gameLock = AcquireGameLock(game);
        EnsureNormalPath(game.BepInExDir);
        Directory.CreateDirectory(game.BepInExDir);
        using var installLock = AcquireLegacyLock(game);
        var transaction = Path.Combine(game.BepInExDir, TransactionFolder);
        EnsureNormalPath(transaction);
        EnsureNormalPath(Path.GetDirectoryName(game.PluginDir)!);
        EnsureNormalPath(game.PluginDir);
        EnsureNormalPath(Path.GetDirectoryName(game.PatcherDir)!);
        EnsureNormalPath(game.PatcherDir);
        RecoverTransaction(game, transaction, progress);
        Directory.CreateDirectory(transaction);
        var staged = Path.Combine(transaction, "staged");
        try
        {
            NovrPackage.Extract(zip, staged, release, cancellationToken);
            File.WriteAllText(Path.Combine(staged, "plugins", "NOVR", InstallerConstants.VersionFileName), release);
            cancellationToken.ThrowIfCancellationRequested();
            var journal = new InstallJournal(Directory.Exists(game.PluginDir), Directory.Exists(game.PatcherDir));
            // Persist the journal before any installed file moves; a subsequent attempt can recover an interrupted update.
            WriteDurableText(Path.Combine(transaction, "journal.json"), JsonSerializer.Serialize(journal));
            progress.Report("Installing validated files; preserving your settings and other mods...");
            SwapDirectory(game.PluginDir, Path.Combine(staged, "plugins", "NOVR"), Path.Combine(transaction, "old-plugins"), journal.HadPlugin);
            cancellationToken.ThrowIfCancellationRequested();
            SwapDirectory(game.PatcherDir, Path.Combine(staged, "patchers", "NOVR"), Path.Combine(transaction, "old-patchers"), journal.HadPatcher);
            // Once committed, cleanup failure must not roll back a successful installation.
            WriteDurableText(Path.Combine(transaction, "committed"), release);
        }
        catch (Exception installError)
        {
            try { RecoverTransaction(game, transaction, progress); }
            catch (Exception recoveryError)
            {
                throw new IOException("The update failed and automatic recovery could not finish. Your previous NOVR files are retained in " + transaction + ". Close the game and retry the installer. " + recoveryError.Message, installError);
            }
            throw;
        }
        try { TryDeleteDirectory(transaction); }
        catch (IOException) { progress.Report("NOVR installed. Previous-file cleanup will finish on the next update."); }
        catch (UnauthorizedAccessException) { progress.Report("NOVR installed. Previous-file cleanup will finish on the next update."); }
    }

    private static void WriteDurableText(string path, string content)
    {
        // Never leave a partially written journal or commit marker looking complete after a process interruption.
        var pending = path + ".writing";
        using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(content);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        File.Move(pending, path);
    }

    private static void SwapDirectory(string target, string staged, string backup, bool existed)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (existed) Directory.Move(target, backup);
        Directory.Move(staged, target);
    }

    private static void RecoverTransaction(GameInstallInfo game, string transaction, IProgress<string> progress)
    {
        if (!Directory.Exists(transaction)) return;
        if (File.Exists(Path.Combine(transaction, "committed")))
        {
            TryDeleteDirectory(transaction);
            return;
        }
        var journalPath = Path.Combine(transaction, "journal.json");
        if (File.Exists(journalPath))
        {
            var journal = JsonSerializer.Deserialize<InstallJournal>(File.ReadAllText(journalPath))
                          ?? throw new IOException("Cannot read the previous update's recovery journal.");
            progress.Report("Restoring the previous NOVR installation...");
            RestoreDirectory(game.PluginDir, Path.Combine(transaction, "old-plugins"), journal.HadPlugin);
            RestoreDirectory(game.PatcherDir, Path.Combine(transaction, "old-patchers"), journal.HadPatcher);
        }
        TryDeleteDirectory(transaction);
    }

    private static void RestoreDirectory(string target, string backup, bool existed)
    {
        EnsureNormalPath(backup);
        if (Directory.Exists(backup))
        {
            TryDeleteDirectory(target);
            Directory.Move(backup, target);
        }
        else if (!existed) TryDeleteDirectory(target);
        else if (!Directory.Exists(target)) throw new IOException("A previous NOVR directory is missing: " + target);
    }

    private static void EnsureNormalPath(string path)
    {
        // Game-library aliases are legitimate; only refuse links in the directories this installer owns or replaces.
        if (new DirectoryInfo(path).LinkTarget is not null)
            throw new IOException("NOVR cannot update a linked mod directory: " + path);
    }

    private static FileStream AcquireGameLock(GameInstallInfo game) =>
        new(Path.Combine(game.GameDir, ".novr-install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

    private static FileStream? AcquireLegacyLock(GameInstallInfo game)
    {
        EnsureNormalPath(game.BepInExDir);
        if (!Directory.Exists(game.BepInExDir)) return null;
        // Also coordinate with older installers that only lock inside BepInEx.
        return new FileStream(Path.Combine(game.BepInExDir, ".novr-install.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    public async Task UninstallNovrAsync(GameInstallInfo game, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Removing NOVR files...");
        await Task.Run(() =>
        {
            using var gameLock = AcquireGameLock(game);
            using var legacyLock = AcquireLegacyLock(game);
            TryDeleteDirectory(game.PluginDir);
            TryDeleteDirectory(game.PatcherDir);
        }, cancellationToken);
    }

    public async Task UninstallBepInExAsync(GameInstallInfo game, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Removing BepInEx...");
        await Task.Run(() =>
        {
            using var gameLock = AcquireGameLock(game);
            // Reject an active legacy updater, then close this handle before
            // deleting its tree. Windows can keep deleted files pending while
            // handles remain open, preventing removal of the parent directory.
            // The stable game-root lock protects all current installer versions.
            AcquireLegacyLock(game)?.Dispose();
            TryDeleteDirectory(game.BepInExDir);
            TryDeleteFile(Path.Combine(game.GameDir, "winhttp.dll"));
            TryDeleteFile(Path.Combine(game.GameDir, "doorstop_config.ini"));
        }, cancellationToken);
    }

    private static void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        Directory.Delete(path, recursive: true);
    }

    private static void TryDeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
