using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

public class Patcher
{
    private static readonly HashSet<string> LegacyVrPluginNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "openvr_api.dll", "openxr_loader.dll", "UnityOpenXR.dll", "XRSDKOpenVR.dll"
    };

    private static string? _managedPayloadDirectory;
    private static string? _managedInstallDirectory;
    public static IEnumerable<string> TargetDLLs { get; } = new[] { "Unity.XR.Management.dll", "Unity.XR.OpenXR.dll" };

    public static void Patch(ref AssemblyDefinition assembly)
    {
        if (_managedPayloadDirectory != null && _managedInstallDirectory != null && ManagedPayloadUpgrade.IsOwned(assembly.Name.Name))
        {
            var file = assembly.Name.Name + ".dll";
            if (ManagedPayloadUpgrade.Apply(ref assembly, Path.Combine(_managedPayloadDirectory, file),
                    Path.Combine(_managedInstallDirectory, file), Console.WriteLine, prepare: PatchManagedAssembly))
                return;
        }
        PatchManagedAssembly(assembly);
    }

    private static void PatchManagedAssembly(AssemblyDefinition assembly)
    {
        if (assembly.Name.Name == "Unity.XR.OpenXR")
        {
            PatchOpenXrSettings(assembly);
        }
    }

#if MONO
    private static void PatchOpenXrSettings(AssemblyDefinition assembly)
    {
        var openXrSettingsType = assembly.MainModule.GetType("UnityEngine.XR.OpenXR.OpenXRSettings");
        if (openXrSettingsType == null)
        {
            Console.WriteLine("[NOVR.Patcher] Failed to find UnityEngine.XR.OpenXR.OpenXRSettings.");
            return;
        }

        var renderModeField = openXrSettingsType.Fields.FirstOrDefault(field => field.Name == "m_renderMode");
        if (renderModeField == null)
        {
            Console.WriteLine("[NOVR.Patcher] Failed to find OpenXRSettings.m_renderMode.");
            return;
        }

        var applySettingsMethod = openXrSettingsType.Methods.FirstOrDefault(method => method.Name == "ApplySettings");
        if (applySettingsMethod != null)
        {
            ForceMultiPass(applySettingsMethod, renderModeField, openXrSettingsType);
        }

        var awakeMethod = openXrSettingsType.Methods.FirstOrDefault(method => method.Name == "Awake");
        if (awakeMethod != null)
        {
            ForceMultiPass(awakeMethod, renderModeField, openXrSettingsType);
        }

        Console.WriteLine("[NOVR.Patcher] Patched OpenXRSettings to force MultiPass.");
    }

    private static void ForceMultiPass(MethodDefinition method, FieldDefinition renderModeField, TypeDefinition openXrSettingsType)
    {
        if (!method.HasBody)
        {
            Console.WriteLine($"[NOVR.Patcher] Skipping {method.FullName} because it has no body.");
            return;
        }

        var multiPassValue = openXrSettingsType.NestedTypes
            .First(type => type.Name == "RenderMode")
            .Fields
            .First(field => field.Name == "MultiPass");

        var il = method.Body.GetILProcessor();
        var firstInstruction = method.Body.Instructions.First();

        il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldarg_0));
        il.InsertBefore(firstInstruction, il.Create(OpCodes.Ldc_I4, multiPassValue.Constant is int value ? value : 1));
        il.InsertBefore(firstInstruction, il.Create(OpCodes.Stfld, renderModeField));
    }
#endif

    // BepInEx calls Initialize before processing TargetDLLs, including on first
    // install when the XR assemblies do not yet exist in the game's Managed folder.
    public static void Initialize()
    {
        if (!StartupHandoff.WaitForOriginalProcess())
        {
            Environment.Exit(1);
            return;
        }
        Console.WriteLine("[NOVR.Patcher] Preparing runtime payload.");
        using var process = Process.GetCurrentProcess();
        var gameExePath = process.MainModule?.FileName
            ?? throw new InvalidOperationException("Cannot locate the running game executable.");
        var gamePath = Path.GetDirectoryName(gameExePath)
            ?? throw new InvalidOperationException("Cannot locate the game directory.");
        var gameName = Path.GetFileNameWithoutExtension(gameExePath);
        var dataPath = Path.Combine(gamePath, gameName + "_Data");
        var patcherPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? throw new InvalidOperationException("Cannot locate the NOVR patcher directory.");

        CopyFilesToGame(patcherPath, dataPath);
        Console.WriteLine("[NOVR.Patcher] Runtime payload prepared; pending XR assemblies will be patched before loading.");
    }

    private static void CopyFilesToGame(string patcherPath, string dataPath)
    {
        var copyToGameFolderPath = Path.Combine(patcherPath, "CopyToGame");
        _managedPayloadDirectory = Path.Combine(copyToGameFolderPath, "Data", "Managed");
        _managedInstallDirectory = Path.Combine(dataPath, "Managed");

        Console.WriteLine($"Installing changed NOVR runtime files. Payload source: {copyToGameFolderPath}");

        CopyDirectory(Path.Combine(copyToGameFolderPath, "Data"), dataPath);

        var gamePluginsPath = Path.Combine(dataPath, "Plugins");
        Directory.CreateDirectory(gamePluginsPath);

        var nativePluginsPath = Path.Combine(copyToGameFolderPath, "Plugins");

        // IntPtr size is 4 on x86, 8 on x64.
        var is64Bit = IntPtr.Size == 8;
        Console.WriteLine($"Detected game as being {(is64Bit ? "x64" : "x86")}");

        var nativePayload = Path.Combine(nativePluginsPath, is64Bit ? "x64" : "x86");
        DeleteExistingVrPlugins(gamePluginsPath, nativePayload);

        // Unity plugins are often in a subfolder of the Plugins folder, but they also get detected from the root folder,
        // so we don't need to worry about the subfolders.
        CopyDirectory(nativePayload, gamePluginsPath);
    }

    // Legacy VR libraries in subdirectories can conflict with the copies Unity
    // discovers in Plugins. Limit cleanup to the known VR library filenames.
    private static void DeleteExistingVrPlugins(string gamePluginsPath, string nativePayload)
    {
        var pluginPaths = Directory
            .GetFiles(gamePluginsPath, "*.dll", SearchOption.AllDirectories)
            .Where(pluginPath => LegacyVrPluginNames.Contains(Path.GetFileName(pluginPath)));

        foreach (var pluginPath in pluginPaths)
        {
            // Current payload files are updated by CopyIfChanged. Only remove obsolete
            // libraries or duplicates in subdirectories; deleting a current DLL first
            // would defeat the unchanged-file check and can fail when Unity maps it.
            if (Path.GetDirectoryName(pluginPath) == gamePluginsPath &&
                File.Exists(Path.Combine(nativePayload, Path.GetFileName(pluginPath))))
                continue;
            try
            {
                Console.WriteLine($"Deleting plugin `{pluginPath}`");
                File.Delete(pluginPath);
            }
            catch (Exception exception)
            {
                Console.WriteLine($"Failed to delete plugin before patching. Path: `{pluginPath}`. Exception: `{exception}`");
            }
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        DirectoryInfo dir = new(sourceDir);

        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

        var dirs = dir.GetDirectories();

        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            if (_managedPayloadDirectory != null && _managedInstallDirectory != null &&
                ManagedPayloadUpgrade.DeferExisting(file.FullName, targetFilePath, _managedPayloadDirectory, _managedInstallDirectory))
                continue;
            PayloadFile.CopyIfChanged(file.FullName, targetFilePath);
        }

        foreach (var subDir in dirs)
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }

        Console.WriteLine($"Copied files from:\n> {sourceDir}\nto:\n> {destinationDir}");
    }
}
