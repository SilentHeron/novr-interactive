namespace NOVR.Installer.Services;

/// <summary>Numeric release tags, including this fork's major.minor.patch.revision tags.</summary>
public readonly struct ReleaseVersion : IEquatable<ReleaseVersion>, IComparable<ReleaseVersion>
{
    private readonly Version? _number;
    public ReleaseVersion(string meta)
    {
        var tag = meta.Split('\n')[0].Trim();
        if (tag.StartsWith('v') || tag.StartsWith('V')) tag = tag[1..];
        var parts = tag.Split('.');
        if (parts.Length is not (3 or 4) || parts.Any(p => p.Length == 0 || p.Any(c => c < '0' || c > '9')) ||
            !System.Version.TryParse(tag, out var version))
            throw new ArgumentException("Expected a release version such as 0.4.7.3 or v0.4.7.3.", nameof(meta));
        _number = new Version(version.Major, version.Minor, version.Build, Math.Max(0, version.Revision));
        Version = tag;
    }

    public string Version { get; } = "0.0.0";
    private Version Number => _number ?? new Version(0, 0, 0, 0);
    public int Major => Number.Major;
    public int Minor => Number.Minor;
    public int Patch => Number.Build;
    public int Revision => Number.Revision;
    public int CompareTo(ReleaseVersion other) => Number.CompareTo(other.Number);
    public bool Equals(ReleaseVersion other) => CompareTo(other) == 0;
    public override bool Equals(object? obj) => obj is ReleaseVersion other && Equals(other);
    public override int GetHashCode() => Number.GetHashCode();
    public static bool operator ==(ReleaseVersion lhs, ReleaseVersion rhs) => lhs.Equals(rhs);
    public static bool operator !=(ReleaseVersion lhs, ReleaseVersion rhs) => !lhs.Equals(rhs);
    public static bool operator >(ReleaseVersion lhs, ReleaseVersion rhs) => lhs.CompareTo(rhs) > 0;
    public static bool operator <(ReleaseVersion lhs, ReleaseVersion rhs) => lhs.CompareTo(rhs) < 0;
    public static bool operator >=(ReleaseVersion lhs, ReleaseVersion rhs) => lhs.CompareTo(rhs) >= 0;
    public static bool operator <=(ReleaseVersion lhs, ReleaseVersion rhs) => lhs.CompareTo(rhs) <= 0;
    public static explicit operator ReleaseVersion(string text) => new(text);
    public override string ToString() => Version ?? "0.0.0";
}
