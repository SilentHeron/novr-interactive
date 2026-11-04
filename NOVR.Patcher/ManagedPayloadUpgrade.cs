using System;
using System.IO;
using Mono.Cecil;

// BepInEx 5 opens Managed assemblies with Cecil before calling Initialize.
// Its supported Patch(ref AssemblyDefinition) hook lets us replace our owned
// XR payloads and release that reader before updating their installed files.
internal static class ManagedPayloadUpgrade
{
    internal static bool IsOwned(string name) => name == "Unity.XR.OpenXR" || name == "Unity.XR.Management";

    internal static bool DeferExisting(string source, string destination, string payloadDirectory, string managedDirectory)
    {
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!(IsOwned(Path.GetFileNameWithoutExtension(source)) &&
            string.Equals(Path.GetExtension(source), ".dll", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetFileName(source), Path.GetFileName(destination), comparison) &&
            string.Equals(Path.GetFullPath(Path.GetDirectoryName(source)!), Path.GetFullPath(payloadDirectory), comparison) &&
            string.Equals(Path.GetFullPath(Path.GetDirectoryName(destination)!), Path.GetFullPath(managedDirectory), comparison) &&
            File.Exists(destination))) return false;
        try
        {
            using var installed = AssemblyDefinition.ReadAssembly(destination);
            if (installed.Name.Name != Path.GetFileNameWithoutExtension(source))
                throw new InvalidDataException("The installed XR file has an unexpected assembly identity: " + destination);
            return true;
        }
        catch (BadImageFormatException)
        {
            // BepInEx skips invalid managed images before Initialize, so there
            // is no pending definition to replace. Repair through normal copy.
            return false;
        }
    }

    internal static bool Apply(ref AssemblyDefinition assembly, string source, string destination, Action<string> log,
        Action<string, string>? persist = null, Action<AssemblyDefinition>? prepare = null)
    {
        if (!IsOwned(assembly.Name.Name)) return false;
        if (File.Exists(destination) && PayloadFile.SameContents(source, destination)) return false;
        foreach (var loaded in AppDomain.CurrentDomain.GetAssemblies())
            if (string.Equals(loaded.GetName().Name, assembly.Name.Name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Cannot upgrade already loaded " + assembly.Name.Name +
                    ". Close the game and install its XR payload before starting again.");

        AssemblyDefinition? replacement = null;
        try
        {
            replacement = AssemblyDefinition.ReadAssembly(source, new ReaderParameters
            { InMemory = true, ReadingMode = ReadingMode.Deferred });
            // Keep resolver ownership independent: disposing the old module
            // must not dispose the replacement's dependency resolver as well.
            if (replacement.MainModule.AssemblyResolver is BaseAssemblyResolver resolver)
            {
                resolver.AddSearchDirectory(Path.GetDirectoryName(source)!);
                resolver.AddSearchDirectory(Path.GetDirectoryName(destination)!);
                if (assembly.MainModule.AssemblyResolver is BaseAssemblyResolver previousResolver)
                    foreach (var directory in previousResolver.GetSearchDirectories()) resolver.AddSearchDirectory(directory);
            }
            if (!string.Equals(replacement.Name.FullName, assembly.Name.FullName, StringComparison.Ordinal))
                throw new InvalidDataException("XR payload identity does not match " + assembly.Name.FullName + ".");
            var requiredType = assembly.Name.Name == "Unity.XR.OpenXR"
                ? "UnityEngine.XR.OpenXR.OpenXRSettings" : "UnityEngine.XR.Management.XRManagerSettings";
            if (replacement.MainModule.GetType(requiredType) == null)
                throw new InvalidDataException("XR payload is missing " + requiredType + ".");
            // Finish IL edits while the original remains usable. A later
            // throwing patch would prevent BepInEx propagating the new ref.
            prepare?.Invoke(replacement);
            // Exercise the same serialization BepInEx uses before releasing its
            // original. This resolves lazy bodies/attributes with our resolver.
            using var check = new MemoryStream();
            replacement.Write(check);
        }
        catch
        {
            replacement?.Dispose();
            throw; // The preloader's original definition is still open and usable.
        }

        // NOVR supplies these complete XR assemblies. Replacing their baseline
        // on upgrade intentionally precedes NOVR's IL patch; earlier third-party
        // Cecil edits to these two owned assemblies cannot be retained.
        var previous = assembly;
        Report(log, "Applying packaged " + assembly.Name.Name + "; earlier preloader edits to this NOVR-owned XR assembly are replaced.");
        assembly = replacement;
        // Never let a failure after this point escape: BepInEx propagates the
        // replacement ref only when its reflection invocation returns normally.
        try { previous.Dispose(); }
        catch (Exception error) { Report(log, "Could not release the previous XR reader: " + error.Message); }
        try
        {
            (persist ?? Persist)(source, destination);
            Report(log, "Updated managed XR payload: " + assembly.Name.Name + ".");
        }
        catch (Exception error)
        {
            Report(log, "Using the updated " + assembly.Name.Name + " for this session, but could not save it: " +
                error.Message + " Close the game and copy the matching CopyToGame/Data/Managed payload into the game's Managed folder before the next launch.");
        }
        return true;
    }

    private static void Report(Action<string> log, string message)
    {
        try { log("[NOVR.Patcher] " + message); }
        catch { /* Logging must not discard a usable replacement definition. */ }
    }

    private static void Persist(string source, string destination)
    {
        // Stage on the same filesystem. A second reader or denied replacement
        // leaves the old destination intact rather than partially overwriting it.
        var staged = destination + ".novr-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.Copy(source, staged, false);
            if (!PayloadFile.SameContents(source, staged)) throw new IOException("XR payload changed while staging.");
            if (File.Exists(destination)) File.Replace(staged, destination, null);
            else File.Move(staged, destination);
        }
        finally
        {
            try { if (File.Exists(staged)) File.Delete(staged); }
            catch { /* Do not mask the replacement error or invalidate the session. */ }
        }
    }
}
