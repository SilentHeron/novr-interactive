using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

// MSBuild-only metadata inspection: never loads or executes the game/plugin.
public sealed class ValidatePackageVersion : Task
{
    [Required] public string PluginFile { get; set; } = string.Empty;
    [Required] public string PatcherFile { get; set; } = string.Empty;
    [Required] public string SourceFile { get; set; } = string.Empty;
    public string RequestedVersion { get; set; } = string.Empty;
    [Output] public string ReleaseVersion { get; set; } = string.Empty;

    public override bool Execute()
    {
        try
        {
            if (new FileInfo(PatcherFile).Length == 0) throw new InvalidDataException("Staged NOVR.Patcher.dll is empty.");
            var source = Regex.Match(File.ReadAllText(SourceFile),
                @"(?s)\[BepInPlugin\(\s*""deltawing\.novr""\s*,\s*""[^""]*""\s*,\s*""([^""]+)""\s*\)\]");
            if (!source.Success) throw new InvalidDataException("Cannot determine NOVRPlugin's declared release version.");
            var declared = Parse(source.Groups[1].Value);
            var compiledText = ReadPluginVersion();
            var compiled = Parse(compiledText);
            if (compiled != declared)
                throw new InvalidDataException("Staged NOVR.dll version " + compiledText + " does not match NOVRPlugin source version " + source.Groups[1].Value + ". Rebuild before packaging.");
            if (!string.IsNullOrEmpty(RequestedVersion) && Parse(RequestedVersion) != compiled)
                throw new InvalidDataException("NovrReleaseVersion " + RequestedVersion + " does not match compiled NOVRPlugin version " + compiledText + ".");
            ReleaseVersion = compiledText;
            return true;
        }
        catch (Exception error)
        {
            Log.LogError("Cannot package NOVR: " + error.Message);
            return false;
        }
    }

    private string ReadPluginVersion()
    {
        using (var stream = File.OpenRead(PluginFile))
        using (var pe = new PEReader(stream))
        {
            if (!pe.HasMetadata) throw new InvalidDataException("Staged NOVR.dll has no managed assembly metadata.");
            var reader = pe.GetMetadataReader();
            foreach (var handle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(handle);
                if (reader.GetString(type.Namespace) != "NOVR" || reader.GetString(type.Name) != "NOVRPlugin") continue;
                foreach (var attributeHandle in type.GetCustomAttributes())
                {
                    var attribute = reader.GetCustomAttribute(attributeHandle);
                    EntityHandle declaringType;
                    if (attribute.Constructor.Kind == HandleKind.MemberReference)
                        declaringType = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
                    else if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
                        declaringType = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();
                    else continue;
                    string name, ns;
                    if (declaringType.Kind == HandleKind.TypeReference)
                    {
                        var reference = reader.GetTypeReference((TypeReferenceHandle)declaringType);
                        name = reader.GetString(reference.Name); ns = reader.GetString(reference.Namespace);
                    }
                    else if (declaringType.Kind == HandleKind.TypeDefinition)
                    {
                        var definition = reader.GetTypeDefinition((TypeDefinitionHandle)declaringType);
                        name = reader.GetString(definition.Name); ns = reader.GetString(definition.Namespace);
                    }
                    else continue;
                    if (ns != "BepInEx" || name != "BepInPlugin") continue;
                    var blob = reader.GetBlobReader(attribute.Value);
                    if (blob.ReadUInt16() != 1 || blob.ReadSerializedString() != "deltawing.novr")
                        throw new InvalidDataException("Staged NOVRPlugin has invalid plugin metadata.");
                    blob.ReadSerializedString(); // Display name is not release identity.
                    return blob.ReadSerializedString() ?? throw new InvalidDataException("Staged NOVRPlugin version is empty.");
                }
            }
        }
        throw new InvalidDataException("Staged NOVR.dll is missing its NOVRPlugin release attribute.");
    }

    private static Version Parse(string text)
    {
        Version? version;
        if (text == null || !Regex.IsMatch(text, @"\A[0-9]+\.[0-9]+\.[0-9]+(?:\.[0-9]+)?\z") || !Version.TryParse(text, out version))
            throw new InvalidDataException("Expected a numeric three- or four-part release version, received: " + text);
        return new Version(version.Major, version.Minor, version.Build, Math.Max(0, version.Revision));
    }
}
