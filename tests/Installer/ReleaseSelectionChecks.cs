using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using NOVR.Installer.Services;

internal static class ReleaseSelectionChecks
{
    private const string ListUrl = "https://api.github.com/repos/SilentHeron/novr-interactive/releases?per_page=100&page=";

    internal static async Task Run(string root, Action<bool, string> check)
    {
        var progress = new QuietProgress();
        var old = Package("0.4.8.8");
        var latest = Package("0.4.8.10");
        var first = new Release("v0.4.8.8", [new Asset("NOVR.zip", old)]);
        var newest = new Release("v0.4.8.10", [new Asset("NOVR.zip", latest)]);
        var published = new Fixture([[first, newest]]);
        var client = new GitHubReleaseClient(published.Client);
        var result = await client.DownloadLatestNovrReleaseAsync(Path.Combine(root, "experimental-releases"), progress, default);
        check(File.ReadAllBytes(result).SequenceEqual(latest) && client.FoundNOVRRelease == new ReleaseVersion("0.4.8.10"),
            "published experimental releases select the greatest numeric version, not list order");
        check(published.Urls[0] == ListUrl + "1" && published.Urls.All(url => !url.Contains("/latest") && !url.Contains("InfernoSuperNova")),
            "experimental channel queries only the fork release list");
        check(published.Contents.All(content => content.Stream.Disposed), "release metadata and successful asset streams are released");
        check(published.Contents[^1].Disposed, "successful asset response releases its content");

        // A newly created old tag may precede the highest version on another page.
        // Do not rely on GitHub's creation order, nor on draft visibility to the caller.
        var pageOne = Enumerable.Range(0, 100).Select(i => new Release("not-a-version-" + i, [])).ToArray();
        pageOne[0] = first;
        var laterPage = new Fixture([pageOne, [
            new Release("v99.0.0", [new Asset("NOVR.zip", old)], Draft: true),
            new Release("v0.4.9-preview", []),
            new Release(null, []),
            newest
        ]]);
        client = new GitHubReleaseClient(laterPage.Client);
        result = await client.DownloadLatestNovrReleaseAsync(Path.Combine(root, "paged-releases"), progress, default);
        check(File.ReadAllBytes(result).SequenceEqual(latest) && laterPage.Urls.Take(2).SequenceEqual(new[] { ListUrl + "1", ListUrl + "2" }),
            "release pagination finds a later-page version and skips drafts/malformed tags");
        check(laterPage.Contents.All(content => content.Stream.Disposed), "each release-list page releases its response stream");

        var preferred = Package("0.4.8.10", "preferred");
        var preferredName = "NOVR-steam-frame-0.4.8.10.zip";
        var named = new Fixture([[new Release("V0.4.8.10", [new Asset("NOVR.zip", latest), new Asset(preferredName, preferred)])]]);
        result = await new GitHubReleaseClient(named.Client).DownloadLatestNovrReleaseAsync(Path.Combine(root, "preferred-package"), progress, default);
        check(Path.GetFileName(result) == preferredName && File.ReadAllBytes(result).SequenceEqual(preferred),
            "versioned package remains preferred over NOVR.zip");

        var legacy = Package("0.4.7", includeMetadata: false);
        var legacyFixture = new Fixture([[new Release("v0.4.7", [new Asset("NOVR.zip", legacy)], Prerelease: false)]]);
        result = await new GitHubReleaseClient(legacyFixture.Client).DownloadLatestNovrReleaseAsync(Path.Combine(root, "legacy-release"), progress, default);
        check(File.ReadAllBytes(result).SequenceEqual(legacy), "older three-part stable releases without embedded metadata remain supported online");

        foreach (var (name, releases) in new (string, Release[])[]
        {
            ("empty", []),
            ("no-supported-tags", [new Release("preview", []), new Release("v1.0.0", [], Draft: true)])
        })
        {
            var unavailable = new Fixture([releases]);
            var error = await Error(() => new GitHubReleaseClient(unavailable.Client).DownloadLatestNovrReleaseAsync(Path.Combine(root, name), progress, default));
            check(error is InvalidOperationException && error.Message.Contains("No published release"), name + " reports no published numeric fork release");
            check(unavailable.Urls.Count == 1, name + " does not fall back to another repository");
        }

        var brokenLatest = new Fixture([[first, new Release("v0.4.8.10", [new Asset("AnotherMod.zip", latest)])]]);
        var failed = await Error(() => new GitHubReleaseClient(brokenLatest.Client).DownloadLatestNovrReleaseAsync(Path.Combine(root, "broken-newest"), progress, default));
        check(failed is InvalidOperationException && brokenLatest.Urls.Count == 1,
            "an invalid newest package fails explicitly instead of silently downgrading to an older release");

        foreach (var cancel in new[] { true, false })
        {
            using var cancellation = new CancellationTokenSource();
            var body = new InterruptedStream(cancellation, cancel);
            var interrupted = new Fixture([[newest]], () => new TrackedContent(body));
            var directory = Path.Combine(root, cancel ? "canceled-http-body" : "failed-http-body");
            client = new GitHubReleaseClient(interrupted.Client);
            failed = await Error(() => client.DownloadLatestNovrReleaseAsync(directory, progress, cancellation.Token));
            check(cancel ? failed is OperationCanceledException : failed is IOException,
                cancel ? "body cancellation reaches the caller" : "body failure reaches the caller");
            check(body.Disposed && interrupted.Contents[^1].Disposed, "interrupted download disposes the body and response");
            check(!Directory.EnumerateFiles(directory).Any(), "interrupted download removes its partial file");
            check(client.FoundNOVRRelease == new ReleaseVersion("0.0.0"), "interrupted download never publishes a verified version");
        }
    }

    private static async Task<Exception> Error(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception error) { return error; }
        throw new Exception("Expected release operation to fail.");
    }

    private static byte[] Package(string version, string label = "fixture", bool includeMetadata = true)
    {
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in new[] { "plugins/NOVR/NOVR.dll", "patchers/NOVR/NOVR.Patcher.dll" })
            {
                using var writer = new StreamWriter(zip.CreateEntry(file).Open());
                writer.Write(label);
            }
            if (includeMetadata)
            {
                using var writer = new StreamWriter(zip.CreateEntry("plugins/NOVR/version.txt").Open());
                writer.Write(version);
            }
        }
        return output.ToArray();
    }

    private sealed record Asset(string Name, byte[] Body);
    private sealed record Release(string? Tag, Asset[] Assets, bool Draft = false, bool Prerelease = true);
    private sealed class QuietProgress : IProgress<string>
    {
        public void Report(string value)
        {
        }
    }

    private sealed class Fixture
    {
        internal readonly List<string> Urls = new();
        internal readonly List<TrackedContent> Contents = new();
        internal readonly HttpClient Client;
        internal Fixture(Release[][] pages, Func<TrackedContent>? assetContent = null)
        {
            Client = new HttpClient(new Handler(request =>
            {
                var url = request.RequestUri!.ToString();
                Urls.Add(url);
                TrackedContent content;
                if (url.StartsWith(ListUrl, StringComparison.Ordinal))
                {
                    var page = int.Parse(url[ListUrl.Length..]) - 1;
                    var entries = page < pages.Length ? pages[page] : [];
                    content = new TrackedContent(new TrackedStream(JsonSerializer.SerializeToUtf8Bytes(entries.Select(release => new
                    {
                        tag_name = release.Tag,
                        draft = release.Draft,
                        prerelease = release.Prerelease,
                        assets = release.Assets.Select(asset => new
                        {
                            name = asset.Name,
                            browser_download_url = DownloadUrl(release, asset),
                            digest = "sha256:" + Convert.ToHexString(SHA256.HashData(asset.Body))
                        })
                    }))));
                }
                else
                {
                    var match = pages.SelectMany(page => page).SelectMany(release => release.Assets.Select(asset => (release, asset)))
                        .SingleOrDefault(pair => DownloadUrl(pair.release, pair.asset) == url);
                    if (match.asset is null)
                        return new HttpResponseMessage(HttpStatusCode.NotFound);
                    content = assetContent?.Invoke() ?? new TrackedContent(new TrackedStream(match.asset.Body));
                }
                Contents.Add(content);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
            }));
        }
        private static string DownloadUrl(Release release, Asset asset) =>
            "https://github.com/SilentHeron/novr-interactive/releases/download/" + release.Tag + "/" + asset.Name;
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
    }
    private class TrackedStream(byte[] data) : MemoryStream(data)
    {
        internal bool Disposed;
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
    private sealed class TrackedContent(TrackedStream stream) : StreamContent(stream)
    {
        internal readonly TrackedStream Stream = stream;
        internal bool Disposed;
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
    private sealed class InterruptedStream(CancellationTokenSource cancellation, bool cancel) : TrackedStream(new byte[10])
    {
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) => Copy(destination, cancellationToken);
        private async Task Copy(Stream destination, CancellationToken token)
        {
            await destination.WriteAsync(new byte[] { 1, 2, 3 }, token);
            if (cancel)
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            }
            throw new IOException("Simulated HTTP body failure.");
        }
    }
}
