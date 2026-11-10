using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace NOVR.VrUi.Controllers;

// Finds the controller models that SteamVR itself installs. NOVR ships none:
// they are Valve's and Meta's, and every player who streams through SteamVR
// already has the right ones on disk. Nothing is found when SteamVR is not
// installed, and the menus then simply show no controller, as before.
internal static class SteamVrRenderModels
{
    internal static string? ModelName(ControllerModelFamily family, bool left)
    {
        var side = left ? "_left" : "_right";
        return family switch
        {
            ControllerModelFamily.SteamFrame => "frame_controller" + side,
            ControllerModelFamily.Quest2 => "oculus_quest2_controller" + side,
            ControllerModelFamily.QuestPlus => "oculus_quest_plus_controller" + side,
            ControllerModelFamily.QuestPro => "oculus_quest_pro_controller" + side,
            _ => null
        };
    }

    // "runtime" in OpenVR's own path registry lists SteamVR's install folder.
    internal static List<string> OpenVrRuntimes(string vrpathJson)
    {
        var result = new List<string>();
        try
        {
            if (MiniJson.Parse(vrpathJson) is Dictionary<string, object?> root &&
                root.TryGetValue("runtime", out var value) && value is List<object?> list)
                foreach (var item in list)
                    if (item is string path && path.Length > 0)
                        result.Add(path);
        }
        catch (FormatException)
        {
        }
        return result;
    }

    // Steam's library list: one "path" entry per library folder.
    internal static List<string> LibraryFolders(string vdf)
    {
        var result = new List<string>();
        foreach (Match match in Regex.Matches(vdf ?? "", "\"path\"\\s+\"((?:[^\"\\\\]|\\\\.)*)\""))
            result.Add(match.Groups[1].Value.Replace("\\\\", "\\"));
        return result;
    }

    // Under Proton the Steam client's folder arrives as a Linux path; the game
    // sees the Linux filesystem as drive Z.
    internal static string? FromLinuxPath(string? path) =>
        string.IsNullOrEmpty(path) ? null : path![0] == '/' ? "Z:" + path.Replace('/', '\\') : path;

    // Where a model named `name` may live under one SteamVR install: the
    // shared folder, or the folder of the driver that owns that controller.
    internal static IEnumerable<string> ModelDirectories(string steamVrRoot, string name, Func<string, IEnumerable<string>> listDirectories)
    {
        yield return Path.Combine(steamVrRoot, "resources", "rendermodels", name);
        IEnumerable<string> drivers;
        try
        {
            drivers = listDirectories(Path.Combine(steamVrRoot, "drivers"));
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            yield break;
        }
        foreach (var driver in drivers)
            yield return Path.Combine(driver, "resources", "rendermodels", name);
    }

    // File system access, in order of how often it is right.
    internal static string? Find(string name, string gameDataPath)
    {
        if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;
        foreach (var root in SteamVrRoots(gameDataPath))
        {
            IEnumerable<string> candidates;
            try
            {
                if (!Directory.Exists(root))
                    continue;
                candidates = ModelDirectories(root, name, Directory.GetDirectories);
                foreach (var directory in candidates)
                    if (File.Exists(Path.Combine(directory, name + ".obj")) && File.Exists(Path.Combine(directory, name + ".json")))
                        return directory;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
            }
        }
        return null;
    }

    private static IEnumerable<string> SteamVrRoots(string gameDataPath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in RawRoots(gameDataPath))
            if (!string.IsNullOrEmpty(root) && seen.Add(root))
                yield return root;
    }

    private static IEnumerable<string> RawRoots(string gameDataPath)
    {
        // SteamVR usually sits beside the game, in the same Steam library.
        var common = Parent(Parent(gameDataPath));
        if (common != null)
            yield return Path.Combine(common, "SteamVR");
        var vrpath = ReadText(() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "openvr", "openvrpaths.vrpath"));
        if (vrpath != null)
            foreach (var runtime in OpenVrRuntimes(vrpath))
                yield return runtime;
        // The Steam client's own folder lists every library. It is two levels
        // above the game's library when the game is in the main one, and under
        // Proton it is also in the environment.
        foreach (var steam in new[] { Parent(Parent(common)), FromLinuxPath(Environment.GetEnvironmentVariable("STEAM_COMPAT_CLIENT_INSTALL_PATH")) })
        {
            if (string.IsNullOrEmpty(steam))
                continue;
            yield return Path.Combine(steam!, "steamapps", "common", "SteamVR");
            var libraries = ReadText(() => Path.Combine(steam!, "steamapps", "libraryfolders.vdf"));
            if (libraries == null)
                continue;
            foreach (var library in LibraryFolders(libraries))
                yield return Path.Combine(FromLinuxPath(library) ?? library, "steamapps", "common", "SteamVR");
        }
    }

    private static string? Parent(string? path)
    {
        try
        {
            return string.IsNullOrEmpty(path) ? null : Path.GetDirectoryName(path!.TrimEnd('/', '\\'));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static string? ReadText(Func<string> path)
    {
        try
        {
            var file = path();
            return File.Exists(file) && new FileInfo(file).Length < 1024 * 1024 ? File.ReadAllText(file) : null;
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException || e is System.Security.SecurityException)
        {
            return null;
        }
    }
}
