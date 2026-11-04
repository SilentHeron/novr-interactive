namespace NOVR.Installer;

public static class InstallerConstants
{
    public const string AppName = "NOVR Interactive";
    // This build must never silently replace the Steam Frame fork with upstream NOVR.
    public const string GitHubOwner = "SilentHeron";
    public const string GitHubRepo = "novr-interactive";
    public const string ReleaseChannel = "NOVR Interactive (SilentHeron/novr-interactive)";
    public const string UserAgent = "NOVR.Installer";

    public const string PluginsFolderName = "plugins";
    public const string PatchersFolderName = "patchers";
    public const string ModFolderName = "NOVR";

    public const string BepInExOwner = "BepInEx";
    public const string BepInExRepo = "BepInEx";

    public const string VersionFileName = "version.txt";
}
