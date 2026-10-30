using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace NOVR.Installer.Services;

public sealed class GitHubReleaseClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    public ReleaseVersion FoundNOVRRelease { get; private set; } = (ReleaseVersion)"0.0.0";

    public GitHubReleaseClient() : this(new HttpClient { Timeout = TimeSpan.FromMinutes(3) }) { }
    internal GitHubReleaseClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(InstallerConstants.UserAgent);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    }

    public async Task<string> DownloadLatestNovrReleaseAsync(string tempDir, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Checking " + InstallerConstants.ReleaseChannel + "...");
        GitHubRelease release;
        try { release = await GetLatestReleaseAsync(InstallerConstants.GitHubOwner, InstallerConstants.GitHubRepo, cancellationToken); }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException("No published release is available on " + InstallerConstants.ReleaseChannel +
                ". Your installed files have not been changed. Use a provided Steam Frame package until a release is published.", ex);
        }
        var version = new ReleaseVersion(release.TagName);
        var preferredName = "NOVR-steam-frame-" + version + ".zip";
        var asset = release.Assets.SingleOrDefault(a => a.Name.Equals(preferredName, StringComparison.OrdinalIgnoreCase))
                    ?? release.Assets.SingleOrDefault(a => a.Name.Equals("NOVR.zip", StringComparison.OrdinalIgnoreCase));
        if (asset is null)
            throw new InvalidOperationException("The fork release has no supported NOVR package. Expected " + preferredName + " or NOVR.zip; no files have been changed.");
        var hash = await ExpectedHashAsync(asset, release.Assets, cancellationToken);
        if (hash is null)
            throw new InvalidOperationException("This NOVR release has no SHA-256 checksum. Publish " + asset.Name + ".sha256 alongside the package, or use a GitHub asset with a SHA-256 digest. No files have been changed.");
        var download = await DownloadAssetAsync(asset, tempDir, progress, release.TagName, hash, cancellationToken);
        try { NovrPackage.Validate(download, release.TagName); }
        catch { File.Delete(download); throw; }
        FoundNOVRRelease = version;
        return download;
    }

    public async Task<string> DownloadLatestBepInEx5Async(string tempDir, IProgress<string> progress, CancellationToken cancellationToken)
    {
        progress.Report("Checking latest BepInEx 5 release...");
        var release = await GetLatestBepInEx5ReleaseAsync(cancellationToken);
        var asset = release.Assets.SingleOrDefault(a => a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            a.Name.Contains("BepInEx", StringComparison.OrdinalIgnoreCase) && a.Name.Contains("win_x64", StringComparison.OrdinalIgnoreCase));
        if (asset is null) throw new InvalidOperationException("Latest BepInEx 5 release does not contain a Windows x64 ZIP asset.");
        return await DownloadAssetAsync(asset, tempDir, progress, release.TagName, DigestHash(asset.Digest), cancellationToken);
    }

    private async Task<string?> ExpectedHashAsync(GitHubAsset asset, GitHubAsset[] assets, CancellationToken cancellationToken)
    {
        var digest = DigestHash(asset.Digest);
        if (digest is not null) return digest;
        var checksum = assets.SingleOrDefault(a => a.Name.Equals(asset.Name + ".sha256", StringComparison.OrdinalIgnoreCase));
        if (checksum is null) return null;
        ValidateDownloadUrl(checksum.BrowserDownloadUrl);
        using var response = await _httpClient.GetAsync(checksum.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[4097];
        var length = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        if (length > 4096) throw new InvalidDataException("Release checksum file is too large.");
        var text = System.Text.Encoding.UTF8.GetString(buffer, 0, length).Trim();
        var fields = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length is < 1 or > 2 || !IsSha256(fields[0]) ||
            (fields.Length == 2 && fields[1].TrimStart('*') != asset.Name))
            throw new InvalidDataException("Release checksum does not identify " + asset.Name + ".");
        return fields[0];
    }

    private static string? DigestHash(string? digest)
    {
        if (string.IsNullOrEmpty(digest)) return null;
        if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) || !IsSha256(digest[7..]))
            throw new InvalidDataException("Unsupported release checksum: expected SHA-256.");
        return digest[7..];
    }
    private static bool IsSha256(string text) => text.Length == 64 && text.All(Uri.IsHexDigit);

    private async Task<GitHubRelease> GetLatestReleaseAsync(string owner, string repo, CancellationToken cancellationToken)
    {
        await using var stream = await _httpClient.GetStreamAsync($"https://api.github.com/repos/{owner}/{repo}/releases/latest", cancellationToken);
        return await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException($"Could not read latest release for {owner}/{repo}.");
    }

    private async Task<GitHubRelease> GetLatestBepInEx5ReleaseAsync(CancellationToken cancellationToken)
    {
        await using var stream = await _httpClient.GetStreamAsync($"https://api.github.com/repos/{InstallerConstants.BepInExOwner}/{InstallerConstants.BepInExRepo}/releases?per_page=30", cancellationToken);
        var releases = await JsonSerializer.DeserializeAsync<GitHubRelease[]>(stream, JsonOptions, cancellationToken);
        return releases?.FirstOrDefault(r => r.TagName.StartsWith("v5.", StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException("Could not find a BepInEx 5 release.");
    }

    private async Task<string> DownloadAssetAsync(GitHubAsset asset, string tempDir, IProgress<string> progress, string releaseName, string? expectedHash, CancellationToken cancellationToken)
    {
        if (Path.GetFileName(asset.Name) != asset.Name || asset.Name.Contains('\\') || asset.Name.Contains(':'))
            throw new InvalidDataException("Release asset filename is invalid.");
        ValidateDownloadUrl(asset.BrowserDownloadUrl);
        Directory.CreateDirectory(tempDir);
        var destination = Path.Combine(tempDir, asset.Name);
        var partial = destination + ".partial";
        try
        {
            progress.Report($"Downloading {asset.Name} from release {releaseName}...");
            using var response = await _httpClient.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using (var remote = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var local = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                await remote.CopyToAsync(local, cancellationToken);
            if (expectedHash is not null)
            {
                progress.Report("Verifying package checksum...");
                await using var file = File.OpenRead(partial);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken));
                if (!hash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded package checksum does not match the release. No installed files have been changed; retry the download.");
            }
            File.Move(partial, destination, overwrite: true);
            return destination;
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
    }

    private static void ValidateDownloadUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com")
            throw new InvalidDataException("Release download must originate from GitHub over HTTPS.");
    }
    private sealed record GitHubRelease([property: System.Text.Json.Serialization.JsonPropertyName("tag_name")] string TagName, GitHubAsset[] Assets);
    private sealed record GitHubAsset(string Name,
        [property: System.Text.Json.Serialization.JsonPropertyName("browser_download_url")] string BrowserDownloadUrl, string? Digest);
}
