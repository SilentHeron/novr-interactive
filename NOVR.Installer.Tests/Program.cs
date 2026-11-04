using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NOVR.Installer;
using NOVR.Installer.Models;
using NOVR.Installer.Services;

var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); checks++; }
async Task Reject(Func<Task> action, string name)
{
    try { await action(); } catch (Exception ex) when (ex is not TestFailure) { Check(true, name + " (" + ex.GetType().Name + ")"); return; }
    throw new TestFailure("Expected rejection: " + name);
}
void RejectSync(Action action, string name) => Reject(() => { action(); return Task.CompletedTask; }, name).GetAwaiter().GetResult();
var root = Path.Combine(Path.GetTempPath(), "novr-installer-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    Check(new ReleaseVersion("v0.4.7.3\nmetadata") > new ReleaseVersion("0.4.7.2"), "four-part tags compare their revision");
    Check(new ReleaseVersion("0.4.7") == new ReleaseVersion("0.4.7.0"), "three- and four-part equivalents compare equally");
    Check(new ReleaseVersion("0.4.7").GetHashCode() == new ReleaseVersion("0.4.7.0").GetHashCode(), "version equality hash contract");
    Check(default(ReleaseVersion) < new ReleaseVersion("0.4.7.3"), "default version comparison is safe");
    foreach (var value in new[] { "", "0.4", "0.4.7.-1", "0.4.7.3-preview", "0.4.7.3.1" }) RejectSync(() => _ = new ReleaseVersion(value), "reject invalid tag " + value);

    var library = Path.Combine(root, "external Steam library", "steamapps");
    var protonGame = Path.Combine(library, "common", "Nuclear Option");
    var exactPrefix = Path.Combine(library, "compatdata", "2168680", "pfx");
    var unrelatedPrefix = Path.Combine(library, "compatdata", "999999", "pfx");
    Directory.CreateDirectory(protonGame);
    Directory.CreateDirectory(unrelatedPrefix);
    File.WriteAllText(Path.Combine(unrelatedPrefix, "user.reg"), "other game settings");
    Check(ProtonPrefixService.FindPrefix(protonGame) is null, "another game's prefix is never selected when Nuclear Option has none");
    Directory.CreateDirectory(exactPrefix);
    File.WriteAllText(Path.Combine(exactPrefix, "user.reg"), "Nuclear Option settings");
    Directory.SetLastWriteTimeUtc(exactPrefix, DateTime.UtcNow.AddDays(-1));
    Check(ProtonPrefixService.FindPrefix(protonGame) == exactPrefix, "selected external library resolves only app2168680 despite a newer unrelated prefix");
    Check(ProtonPrefixService.FindPrefix(protonGame + Path.DirectorySeparatorChar) == exactPrefix, "trailing game-directory separator resolves the same prefix");
    var otherLibraryGame = Path.Combine(root, "other library", "steamapps", "common", "Nuclear Option");
    Directory.CreateDirectory(otherLibraryGame);
    Check(ProtonPrefixService.FindPrefix(otherLibraryGame) is null, "missing selected-library prefix never falls back to another library");
    Check(ProtonPrefixService.FindPrefix(Path.Combine(root, "custom game")) is null, "custom installation receives guidance without guessing a Steam prefix");
    var guidance = ProtonPrefixService.SetupGuidance(protonGame, linux: true);
    Check(guidance.Contains("WINEDLLOVERRIDES=\"winhttp=n,b\" %command%") && guidance.Contains("Preserve existing launch options"), "Linux guidance gives the exact per-game override and preserves existing options");
    Check(guidance.Contains("Manual Proton setup") && guidance.Contains("has not changed Wine/Proton settings") && guidance.Contains(exactPrefix), "Linux result clearly reports a manual step and the matching prefix without claiming configuration");
    Check(ProtonPrefixService.SetupGuidance(otherLibraryGame, linux: true).Contains("No existing Nuclear Option prefix"), "missing prefix is reported honestly with launch-option guidance still available");
    Check(ProtonPrefixService.SetupGuidance("\0not a path", linux: false) == "Proton/Wine override is only needed on Linux.", "Windows behavior is unchanged and never inspects Proton paths");
    var prefixFiles = Directory.GetFiles(library, "*", SearchOption.AllDirectories).Order().ToArray();
    var prefixProgress = new List<string>();
    var prefixService = new ProtonPrefixService();
    var prefixResult = await prefixService.TryConfigureWinHttpOverrideAsync(
        new GameInstallInfo(protonGame, true, true, InstallState.FullyInstalled, null),
        new ImmediateProgress(prefixProgress.Add), default);
    Check(Directory.GetFiles(library, "*", SearchOption.AllDirectories).Order().SequenceEqual(prefixFiles) &&
          File.ReadAllText(Path.Combine(exactPrefix, "user.reg")) == "Nuclear Option settings" &&
          File.ReadAllText(Path.Combine(unrelatedPrefix, "user.reg")) == "other game settings", "actual installer setup leaves both registries and prefix files untouched");
    Check(prefixResult == ProtonPrefixService.SetupGuidance(protonGame, OperatingSystem.IsLinux()), "public setup path reports the current platform's guidance");
    if (OperatingSystem.IsLinux())
        Check(prefixProgress.Single().Contains("manual"), "Linux progress never presents automatic configuration as successful");
    else
        Check(prefixProgress.Count == 0, "Windows has no Proton setup progress or side effects");
    await Reject(() => prefixService.TryConfigureWinHttpOverrideAsync(
        new GameInstallInfo(protonGame, true, true, InstallState.FullyInstalled, null),
        new ImmediateProgress(_ => throw new TestFailure("Cancelled setup reported progress")), new CancellationToken(true)),
        "cancelled setup stops before inspecting or reporting Proton setup");

    var zip = MakeZip(root, "valid.zip");
    NovrPackage.Validate(zip, "v0.4.7.3"); Check(true, "validate complete package");
    Check(NovrPackage.ReadVersion(zip).ToString() == "0.4.7.3", "offline package identifies own version");
    foreach (var (name, extra, omit) in new[] {
        ("traversal", "../outside.dll", ""), ("backslash", "plugins\\NOVR\\evil.dll", ""),
        ("absolute", "/tmp/evil.dll", ""), ("case alias", "plugins/NOVR/novr.dll", ""),
        ("other plugin", "plugins/Other/Other.dll", ""), ("colon", "plugins/NOVR/a:b.dll", ""),
        ("missing patcher", "", "patchers/NOVR/NOVR.Patcher.dll") })
    {
        var malformed = MakeZip(root, Guid.NewGuid() + ".zip", extra, omit);
        RejectSync(() => NovrPackage.Validate(malformed, "0.4.7.3"), name + " is refused before install");
    }
    RejectSync(() => NovrPackage.Validate(zip, "0.4.7.2"), "mismatched release metadata rejected");
    var linkZip = MakeZip(root, "link.zip");
    using (var archive = ZipFile.Open(linkZip, ZipArchiveMode.Update)) archive.GetEntry("plugins/NOVR/NOVR.dll")!.ExternalAttributes = unchecked((int)0xA1FF0000);
    RejectSync(() => NovrPackage.Validate(linkZip, "0.4.7.3"), "archive symbolic links rejected");

    var game = FixtureGame(root, "update");
    var installer = new FileSystemInstaller();
    var messages = new ImmediateProgress();
    await installer.InstallOrUpdateNovrAsync(game, zip, "0.4.7.3", messages, default);
    Check(File.ReadAllText(Path.Combine(game.PluginDir, "NOVR.dll")) == "new-plugin", "new plugin installed");
    Check(File.ReadAllText(Path.Combine(game.PatcherDir, "NOVR.Patcher.dll")) == "new-patcher", "new patcher installed");
    Check(!File.Exists(Path.Combine(game.PluginDir, "obsolete.dll")), "obsolete NOVR binaries removed");
    Check(File.ReadAllText(Path.Combine(game.BepInExDir, "config", "deltawing.novr.cfg")) == "user settings", "user settings preserved");
    Check(File.ReadAllText(Path.Combine(game.BepInExDir, "plugins", "AnotherMod", "Other.dll")) == "other mod", "other mods preserved");
    Check(!File.Exists(Path.Combine(game.BepInExDir, "docs", "guide.txt")), "documentation not copied into game");
    Check(!Directory.Exists(Path.Combine(game.BepInExDir, ".novr-update")), "successful transaction cleaned up");
    Check(File.ReadAllText(game.VersionFile) == "0.4.7.3", "committed version metadata matches package");

    var broken = FixtureGame(root, "failed-second-swap");
    var sabotage = new ImmediateProgress(message => {
        if (message.StartsWith("Installing validated")) Directory.Delete(Path.Combine(broken.BepInExDir, ".novr-update", "staged", "patchers", "NOVR"), true);
    });
    await Reject(() => installer.InstallOrUpdateNovrAsync(broken, zip, "0.4.7.3", sabotage, default), "failure after first swap reported");
    Check(IsOld(broken.PluginDir, "NOVR.dll") && IsOld(broken.PatcherDir, "NOVR.Patcher.dll"), "both directories restored after second swap fails");
    Check(File.Exists(Path.Combine(broken.PluginDir, "obsolete.dll")), "rollback retains previous ancillary files");
    Check(!Directory.Exists(Path.Combine(broken.BepInExDir, ".novr-update")), "rolled-back transaction cleaned up");

    var cancelled = FixtureGame(root, "cancelled");
    using var cancellation = new CancellationTokenSource();
    var cancelProgress = new ImmediateProgress(message => { if (message.StartsWith("Installing validated")) cancellation.Cancel(); });
    await Reject(() => installer.InstallOrUpdateNovrAsync(cancelled, zip, "0.4.7.3", cancelProgress, cancellation.Token), "cancellation during swap reported");
    Check(IsOld(cancelled.PluginDir, "NOVR.dll") && IsOld(cancelled.PatcherDir, "NOVR.Patcher.dll"), "cancellation restores previous installation");

    var interrupted = FixtureGame(root, "interrupted");
    var transaction = Path.Combine(interrupted.BepInExDir, ".novr-update");
    Directory.CreateDirectory(transaction);
    File.WriteAllText(Path.Combine(transaction, "journal.json"), "{\"HadPlugin\":true,\"HadPatcher\":true}");
    Directory.Move(interrupted.PluginDir, Path.Combine(transaction, "old-plugins"));
    Directory.CreateDirectory(interrupted.PluginDir); File.WriteAllText(Path.Combine(interrupted.PluginDir, "NOVR.dll"), "interrupted new plugin");
    var recovered = false;
    await installer.InstallOrUpdateNovrAsync(interrupted, zip, "0.4.7.3", new ImmediateProgress(message => {
        if (message.StartsWith("Installing validated")) recovered = IsOld(interrupted.PluginDir, "NOVR.dll") && IsOld(interrupted.PatcherDir, "NOVR.Patcher.dll");
    }), default);
    Check(recovered, "interrupted previous transaction restored before next update");
    Check(File.ReadAllText(Path.Combine(interrupted.PluginDir, "NOVR.dll")) == "new-plugin", "update proceeds after recovery");

    var partialJournal = FixtureGame(root, "interrupted-journal");
    var partialTransaction = Path.Combine(partialJournal.BepInExDir, ".novr-update");
    Directory.CreateDirectory(partialTransaction);
    File.WriteAllText(Path.Combine(partialTransaction, "journal.json.writing"), "{incomplete");
    await installer.InstallOrUpdateNovrAsync(partialJournal, zip, "0.4.7.3", messages, default);
    Check(File.ReadAllText(Path.Combine(partialJournal.PluginDir, "NOVR.dll")) == "new-plugin", "incomplete journal write can be retried without changing old files first");

    var locked = FixtureGame(root, "concurrent");
    using (var heldLock = new FileStream(Path.Combine(locked.BepInExDir, ".novr-install.lock"), FileMode.Create, FileAccess.Write, FileShare.None))
        await Reject(() => installer.InstallOrUpdateNovrAsync(locked, zip, "0.4.7.3", messages, default), "concurrent update rejected");
    Check(IsOld(locked.PluginDir, "NOVR.dll"), "concurrent update leaves live files untouched");

    var bytes = File.ReadAllBytes(zip);
    var hash = Convert.ToHexString(SHA256.HashData(bytes));
    var releaseName = "NOVR-steam-frame-0.4.7.3.zip";
    var urls = new List<string>();
    var client = MockClient(hash, bytes, releaseName, urls: urls);
    var downloaded = await client.DownloadLatestNovrReleaseAsync(Path.Combine(root, "download"), messages, default);
    Check(File.ReadAllBytes(downloaded).SequenceEqual(bytes), "verified release downloaded intact");
    Check(client.FoundNOVRRelease == new ReleaseVersion("0.4.7.3"), "verified release version published after download");
    Check(urls[0] == "https://api.github.com/repos/SilentHeron/novr-interactive/releases/latest", "only fork channel queried");
    Check(urls.All(u => !u.Contains("InfernoSuperNova")), "no upstream fallback requests");
    var badHashClient = MockClient(new string('0', 64), bytes, releaseName);
    var badDir = Path.Combine(root, "bad-hash");
    await Reject(() => badHashClient.DownloadLatestNovrReleaseAsync(badDir, messages, default), "hash mismatch refused");
    Check(!Directory.EnumerateFiles(badDir).Any(), "failed downloads remove partial files");
    Check(badHashClient.FoundNOVRRelease == new ReleaseVersion("0.0.0"), "failed checksum does not publish found version");
    var noHash = MockClient(null, bytes, releaseName);
    await Reject(() => noHash.DownloadLatestNovrReleaseAsync(Path.Combine(root, "no-hash"), messages, default), "release with no checksum refused");
    var sidecar = MockClient(null, bytes, releaseName, hash + "  " + releaseName + "\n");
    var sidecarPath = await sidecar.DownloadLatestNovrReleaseAsync(Path.Combine(root, "sidecar"), messages, default);
    Check(File.Exists(sidecarPath), "standard SHA-256 sidecar supported");
    var wrongFile = MockClient(null, bytes, releaseName, hash + "  another.zip\n");
    await Reject(() => wrongFile.DownloadLatestNovrReleaseAsync(Path.Combine(root, "wrong-sidecar"), messages, default), "checksum for another filename rejected");
    var wrongZip = MockClient(hash, bytes, "AnotherMod.zip");
    await Reject(() => wrongZip.DownloadLatestNovrReleaseAsync(Path.Combine(root, "wrong-package"), messages, default), "unrelated ZIP asset never selected");
    var requests = new List<string>();
    var missing = new GitHubReleaseClient(new HttpClient(new MockHandler(request => { requests.Add(request.RequestUri!.ToString()); return new HttpResponseMessage(HttpStatusCode.NotFound); })));
    await Reject(() => missing.DownloadLatestNovrReleaseAsync(Path.Combine(root, "missing-release"), messages, default), "unpublished fork release fails safely");
    Check(requests.Count == 1 && requests[0].Contains("SilentHeron/novr-interactive"), "unpublished fork never falls back upstream");

    if (args.Length > 0)
    {
        NovrPackage.Validate(args[0], NovrPackage.ReadVersion(args[0]).ToString());
        Check(true, "provided real package passes archive validation");
    }
    Console.WriteLine($"{checks} installer checks passed; all filesystem writes stayed in {root}.");
}
finally { Directory.Delete(root, true); }

static bool IsOld(string directory, string name) => File.Exists(Path.Combine(directory, name)) && File.ReadAllText(Path.Combine(directory, name)) == "old";
static GameInstallInfo FixtureGame(string root, string name)
{
    var game = new GameInstallInfo(Path.Combine(root, name), true, true, InstallState.FullyInstalled, null);
    Directory.CreateDirectory(game.PluginDir); Directory.CreateDirectory(game.PatcherDir);
    File.WriteAllText(Path.Combine(game.PluginDir, "NOVR.dll"), "old");
    File.WriteAllText(Path.Combine(game.PluginDir, "obsolete.dll"), "old");
    File.WriteAllText(Path.Combine(game.PatcherDir, "NOVR.Patcher.dll"), "old");
    Directory.CreateDirectory(Path.Combine(game.BepInExDir, "config"));
    File.WriteAllText(Path.Combine(game.BepInExDir, "config", "deltawing.novr.cfg"), "user settings");
    Directory.CreateDirectory(Path.Combine(game.BepInExDir, "plugins", "AnotherMod"));
    File.WriteAllText(Path.Combine(game.BepInExDir, "plugins", "AnotherMod", "Other.dll"), "other mod");
    return game;
}
static string MakeZip(string root, string name, string extra = "", string omit = "")
{
    var path = Path.Combine(root, name);
    using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
    foreach (var (file, content) in new[] { ("plugins/NOVR/NOVR.dll", "new-plugin"), ("patchers/NOVR/NOVR.Patcher.dll", "new-patcher"), ("plugins/NOVR/version.txt", "0.4.7.3"), ("docs/guide.txt", "guide"), ("config/deltawing.novr.cfg", "package default settings") })
    {
        if (file == omit) continue;
        using var writer = new StreamWriter(archive.CreateEntry(file).Open()); writer.Write(content);
    }
    if (extra.Length > 0) { using var writer = new StreamWriter(archive.CreateEntry(extra).Open()); writer.Write("extra"); }
    return path;
}
static GitHubReleaseClient MockClient(string? hash, byte[] bytes, string name, string? sidecar = null, List<string>? urls = null)
{
    var assets = new List<object> { new { name, browser_download_url = "https://github.com/SilentHeron/novr-interactive/releases/download/v0.4.7.3/" + name, digest = hash is null ? null : "sha256:" + hash } };
    if (sidecar is not null) assets.Add(new { name = name + ".sha256", browser_download_url = "https://github.com/SilentHeron/novr-interactive/releases/download/v0.4.7.3/" + name + ".sha256" });
    return new GitHubReleaseClient(new HttpClient(new MockHandler(request =>
    {
        var url = request.RequestUri!.ToString(); urls?.Add(url);
        if (url.StartsWith("https://api.github.com/")) return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { tag_name = "v0.4.7.3", assets })) };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = url.EndsWith(".sha256") ? new StringContent(sidecar!) : new ByteArrayContent(bytes) };
    })));
}
sealed class ImmediateProgress(Action<string>? callback = null) : IProgress<string> { public void Report(string value) => callback?.Invoke(value); }
sealed class MockHandler(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(send(request));
}
sealed class TestFailure(string message) : Exception(message);
