using System.IO.Compression;

namespace NOVR.Installer.Services;

/// <summary>Validates the whole archive before extracting the two owned install directories.</summary>
public static class NovrPackage
{
    private const long MaxExpandedBytes = 1024L * 1024 * 1024;
    private const int MaxFiles = 10000;
    private static readonly string[] RequiredFiles = ["plugins/NOVR/NOVR.dll", "patchers/NOVR/NOVR.Patcher.dll"];

    public static ReleaseVersion ReadVersion(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        var metadata = archive.GetEntry("plugins/NOVR/version.txt");
        if (metadata is null || metadata.Length > 4096)
            throw new InvalidDataException("The selected package needs plugins/NOVR/version.txt identifying its release.");
        using var reader = new StreamReader(metadata.Open());
        var version = new ReleaseVersion(reader.ReadToEnd());
        Inspect(archive, version.ToString());
        return version;
    }

    public static void Validate(string path, string releaseName)
    {
        using var archive = ZipFile.OpenRead(path);
        Inspect(archive, releaseName);
    }

    internal static void Extract(string path, string destination, string releaseName, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(path);
        Inspect(archive, releaseName);
        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.FullName.EndsWith('/') || !IsOwnedFile(entry.FullName)) continue;
            var target = Path.Combine(destination, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    private static void Inspect(ZipArchive archive, string releaseName)
    {
        var release = new ReleaseVersion(releaseName);
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        if (archive.Entries.Count > MaxFiles) throw new InvalidDataException("NOVR package contains too many files.");
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName;
            // ZIP paths must work identically on Windows and Linux; refuse links and aliases.
            var parts = path.TrimEnd('/').Split('/');
            if (string.IsNullOrWhiteSpace(path) || path.StartsWith('/') || path.Contains('\\') ||
                parts.Any(p => p.Length == 0 || p is "." or ".." || p.EndsWith(' ') || p.EndsWith('.') ||
                               p.Any(c => c < 32 || ":*?\"<>|".Contains(c))) ||
                ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000)
                throw new InvalidDataException("NOVR package contains an unsafe path: " + path);
            if (!paths.Add(path.TrimEnd('/'))) throw new InvalidDataException("NOVR package contains duplicate paths: " + path);
            expanded = checked(expanded + entry.Length);
            if (expanded > MaxExpandedBytes) throw new InvalidDataException("NOVR package expands beyond the supported size.");
            // Documentation may accompany a release. Only the two NOVR directories are installed.
            if ((path.StartsWith("plugins/", StringComparison.OrdinalIgnoreCase) || path.StartsWith("patchers/", StringComparison.OrdinalIgnoreCase)) &&
                path is not ("plugins/" or "patchers/" or "plugins/NOVR/" or "patchers/NOVR/") && !IsOwnedFile(path))
                throw new InvalidDataException("NOVR package contains another mod or an unexpected install path: " + path);
        }
        foreach (var required in RequiredFiles)
        {
            var entry = archive.GetEntry(required);
            if (entry is null || entry.Length == 0) throw new InvalidDataException("NOVR package is missing " + required + ".");
        }
        var metadata = archive.GetEntry("plugins/NOVR/version.txt");
        if (metadata is not null)
        {
            if (metadata.Length > 4096) throw new InvalidDataException("NOVR version metadata is too large.");
            using var reader = new StreamReader(metadata.Open());
            if (new ReleaseVersion(reader.ReadToEnd()) != release)
                throw new InvalidDataException("NOVR package version does not match the selected release.");
        }
    }

    private static bool IsOwnedFile(string path) => path.StartsWith("plugins/NOVR/", StringComparison.Ordinal) ||
                                                   path.StartsWith("patchers/NOVR/", StringComparison.Ordinal);
}
