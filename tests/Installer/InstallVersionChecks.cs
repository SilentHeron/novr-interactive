using System.IO.Compression;
using System.Text.Json;
using NOVR.Installer.Models;
using NOVR.Installer.Services;

// Real source-linked installer operations, all confined to the suite's temporary
// game directories. The pause is before the transaction; neither test writes
// nor callbacks bypass an acquired installation lock.
internal static class InstallVersionChecks
{
    internal static async Task Run(string root, Action<bool, string> check)
    {
        var installer = new FileSystemInstaller();
        var quiet = new ImmediateProgress();
        var oldPackage = Package(root, "0.4.8.10");
        var newerPackage = Package(root, "0.4.8.11");
        var newestPackage = Package(root, "0.4.8.12");

        foreach (var initiallyInstalled in new[] { true, false })
        {
            var game = Game(root, initiallyInstalled ? "version-race-installed" : "version-race-empty");
            if (initiallyInstalled)
                await installer.InstallOrUpdateNovrAsync(game, oldPackage, "0.4.8.10", quiet, default);
            var captured = new GameLocator().Inspect(game.GameDir);
            check(captured.ModState == (initiallyInstalled ? InstallState.FullyInstalled : InstallState.NotInstalled),
                "older request captures the initial installation state");
            check(captured.ModState != InstallState.FullyInstalled || captured.Version <= new ReleaseVersion("0.4.8.10"),
                "older request passes the same early no-downgrade condition as the view model");

            var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var resume = new ManualResetEventSlim();
            var pending = Task.Run(async () =>
            {
                try
                {
                    await installer.InstallOrUpdateNovrAsync(captured, oldPackage, "0.4.8.10", new ImmediateProgress(message =>
                    {
                        if (!message.StartsWith("Validating NOVR", StringComparison.Ordinal))
                            return;
                        reached.TrySetResult();
                        if (!resume.Wait(TimeSpan.FromSeconds(10)))
                            throw new TimeoutException("Version race fixture did not release the pending install.");
                    }), default);
                    return (Exception?)null;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            });
            Dictionary<string, string> newestFiles;
            try
            {
                await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await new FileSystemInstaller().InstallOrUpdateNovrAsync(game, newerPackage, "0.4.8.11", quiet, default);
                newestFiles = Snapshot(game);
            }
            finally
            {
                resume.Set();
                await pending.WaitAsync(TimeSpan.FromSeconds(10));
            }
            var failure = await pending.WaitAsync(TimeSpan.FromSeconds(10));
            check(failure is InvalidOperationException && failure.Message.Contains("0.4.8.11") && failure.Message.Contains("0.4.8.10"),
                $"older pending package is rejected after another installer commits a newer release (observed {game.Version}, {failure?.GetType().Name ?? "success"})");
            check(SameFiles(newestFiles, Snapshot(game)), "rejected stale request preserves newer plugin, patcher, version, settings and other mods");
            check(!Directory.Exists(Transaction(game)), "rejected stale request creates no transaction directory or journal");
            AssertLocksReleased(game, check);
            await installer.InstallOrUpdateNovrAsync(game, newestPackage, "0.4.8.12", quiet, default);
            check(game.Version == new ReleaseVersion("0.4.8.12"), "later newer update succeeds after rejected stale request");
        }

        // A previous interrupted swap may temporarily expose older staged files.
        // Recover the authoritative installation before deciding whether to reject.
        var interrupted = Game(root, "version-after-recovery");
        await installer.InstallOrUpdateNovrAsync(interrupted, newestPackage, "0.4.8.12", quiet, default);
        var beforeRecovery = Snapshot(interrupted);
        var transaction = Transaction(interrupted);
        Directory.CreateDirectory(transaction);
        File.WriteAllText(Path.Combine(transaction, "journal.json"), JsonSerializer.Serialize(new
        {
            HadPlugin = true,
            HadPatcher = true
        }));
        Directory.Move(interrupted.PluginDir, Path.Combine(transaction, "old-plugins"));
        Directory.Move(interrupted.PatcherDir, Path.Combine(transaction, "old-patchers"));
        Directory.CreateDirectory(interrupted.PluginDir);
        Directory.CreateDirectory(interrupted.PatcherDir);
        File.WriteAllText(Path.Combine(interrupted.PluginDir, "NOVR.dll"), "interrupted old plugin");
        File.WriteAllText(interrupted.VersionFile, "0.4.8.9");
        File.WriteAllText(Path.Combine(interrupted.PatcherDir, "NOVR.Patcher.dll"), "interrupted old patcher");
        await MustReject(() => installer.InstallOrUpdateNovrAsync(interrupted, newerPackage, "0.4.8.11", quiet, default), check,
            "downgrade check uses the recovered version instead of interrupted staged files");
        check(SameFiles(beforeRecovery, Snapshot(interrupted)) && !Directory.Exists(transaction),
            "interrupted update is restored and cleaned before stale package rejection");
        AssertLocksReleased(interrupted, check);

        var committed = Game(root, "version-after-commit");
        await installer.InstallOrUpdateNovrAsync(committed, newestPackage, "0.4.8.12", quiet, default);
        var beforeCleanup = Snapshot(committed);
        Directory.CreateDirectory(Transaction(committed));
        File.WriteAllText(Path.Combine(Transaction(committed), "committed"), "0.4.8.12");
        await MustReject(() => installer.InstallOrUpdateNovrAsync(committed, oldPackage, "0.4.8.10", quiet, default), check,
            "committed installation remains authoritative during leftover transaction cleanup");
        check(SameFiles(beforeCleanup, Snapshot(committed)) && !Directory.Exists(Transaction(committed)),
            "committed cleanup never rolls back the newer installation before rejecting a stale package");

        var repair = Game(root, "equal-version-repair");
        await installer.InstallOrUpdateNovrAsync(repair, newerPackage, "0.4.8.11", quiet, default);
        File.WriteAllText(Path.Combine(repair.PluginDir, "NOVR.dll"), "damaged");
        await installer.InstallOrUpdateNovrAsync(repair, newerPackage, "0.4.8.11", quiet, default);
        check(File.ReadAllText(Path.Combine(repair.PluginDir, "NOVR.dll")) == "plugin 0.4.8.11", "same-version repair remains permitted");

        var legacyPackage = Package(root, "0.4.7", metadata: false);
        File.Delete(repair.VersionFile);
        await installer.InstallOrUpdateNovrAsync(repair, legacyPackage, "0.4.7", quiet, default);
        check(repair.Version == new ReleaseVersion("0.4.7"), "legacy installations without version metadata and legacy packages remain repairable");
        await installer.InstallOrUpdateNovrAsync(repair, legacyPackage, "0.4.7.0", quiet, default);
        check(File.ReadAllText(repair.VersionFile) == "0.4.7.0", "three- and four-part equivalent versions still allow repair");
        File.WriteAllText(repair.VersionFile, "not-a-version");
        await MustReject(() => installer.InstallOrUpdateNovrAsync(repair, oldPackage, "0.4.8.10", quiet, default), check,
            "invalid version metadata on a complete install is not silently ignored", typeof(ArgumentException));
        check(File.ReadAllText(repair.VersionFile) == "not-a-version", "metadata rejection preserves installed files");
        AssertLocksReleased(repair, check);

        foreach (var keepPlugin in new[] { true, false })
        {
            var partial = Game(root, keepPlugin ? "partial-plugin-repair" : "partial-patcher-repair");
            await installer.InstallOrUpdateNovrAsync(partial, newestPackage, "0.4.8.12", quiet, default);
            // Leave an empty owned directory too, matching GameLocator's existing
            // rule that an empty directory is not a complete half-install.
            var missing = keepPlugin ? partial.PatcherDir : partial.PluginDir;
            Directory.Delete(missing, true);
            Directory.CreateDirectory(missing);
            var info = new GameLocator().Inspect(partial.GameDir);
            check(info.ModState == InstallState.PartiallyInstalled, "partial repair fixture retains native install-state classification");
            await installer.InstallOrUpdateNovrAsync(info, oldPackage, "0.4.8.10", quiet, default);
            check(partial.Version == new ReleaseVersion("0.4.8.10") && File.Exists(Path.Combine(partial.PatcherDir, "NOVR.Patcher.dll")),
                "partial installations remain repairable even when their retained metadata is newer");
        }
    }

    private static async Task MustReject(Func<Task> operation, Action<bool, string> check, string message, Type? type = null)
    {
        Exception? failure = null;
        try
        {
            await operation();
        }
        catch (Exception exception) { failure = exception; }
        check(failure?.GetType() == (type ?? typeof(InvalidOperationException)), message);
    }

    private static void AssertLocksReleased(GameInstallInfo game, Action<bool, string> check)
    {
        using var gameLock = new FileStream(Path.Combine(game.GameDir, ".novr-install.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var legacyLock = new FileStream(Path.Combine(game.BepInExDir, ".novr-install.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        check(true, "version rejection releases both installation locks");
    }

    private static string Transaction(GameInstallInfo game) => Path.Combine(game.BepInExDir, ".novr-update");

    private static Dictionary<string, string> Snapshot(GameInstallInfo game) =>
        Directory.EnumerateFiles(game.BepInExDir, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != ".novr-install.lock")
            .ToDictionary(path => Path.GetRelativePath(game.GameDir, path), File.ReadAllText);

    private static bool SameFiles(Dictionary<string, string> before, Dictionary<string, string> after) =>
        before.Count == after.Count && before.All(pair => after.TryGetValue(pair.Key, out var value) && value == pair.Value);

    private static GameInstallInfo Game(string root, string name)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.Combine(path, "NuclearOption_Data", "Managed"));
        Directory.CreateDirectory(Path.Combine(path, "BepInEx", "core"));
        Directory.CreateDirectory(Path.Combine(path, "BepInEx", "config"));
        Directory.CreateDirectory(Path.Combine(path, "BepInEx", "plugins", "Other"));
        File.WriteAllText(Path.Combine(path, "BepInEx", "config", "deltawing.novr.cfg"), "user settings");
        File.WriteAllText(Path.Combine(path, "BepInEx", "plugins", "Other", "Other.dll"), "other mod");
        return new GameLocator().Inspect(path);
    }

    private static string Package(string root, string version, bool metadata = true)
    {
        var path = Path.Combine(root, "transaction-version-" + version + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        var files = new Dictionary<string, string>
        {
            ["plugins/NOVR/NOVR.dll"] = "plugin " + version,
            ["patchers/NOVR/NOVR.Patcher.dll"] = "patcher " + version
        };
        if (metadata)
            files["plugins/NOVR/version.txt"] = version;
        foreach (var (name, content) in files)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(content);
        }
        return path;
    }
}
