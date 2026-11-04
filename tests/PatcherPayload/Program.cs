using System.Reflection;
using Mono.Cecil;
using Attributes = Mono.Cecil.TypeAttributes;

internal static class Program
{
    private static int _passed;
    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
        _passed++;
    }
    private static void Throws(Action action, string message)
    {
        try
        {
            action();
        }
        catch { _passed++; return; }
        throw new Exception(message);
    }
    private static void Write(string path, string name, int marker, bool validType = true, int version = 0)
    {
        using var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(name, new Version(version, 0, 0, 0)), name, ModuleKind.Dll);
        var xr = name == "Unity.XR.OpenXR";
        var type = new TypeDefinition(xr ? "UnityEngine.XR.OpenXR" : "UnityEngine.XR.Management",
            validType ? xr ? "OpenXRSettings" : "XRManagerSettings" : "WrongType", Attributes.Public | Attributes.Class, assembly.MainModule.TypeSystem.Object);
        type.Fields.Add(new FieldDefinition("Marker", Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static | Mono.Cecil.FieldAttributes.Literal,
            assembly.MainModule.TypeSystem.Int32)
        {
            Constant = marker
        });
        assembly.MainModule.Types.Add(type);
        assembly.Write(path);
    }
    private static int Marker(AssemblyDefinition assembly) => (int)assembly.MainModule.Types[1].Fields[0].Constant;
    private static void Writable(AssemblyDefinition assembly)
    {
        using var stream = new MemoryStream();
        assembly.Write(stream);
        Check(stream.Length > 0, "Returned definition remains writable.");
    }

    // The installed BepInEx wrapper propagates ref through an object[] only
    // after Invoke succeeds. Exercise that exact boundary on a copy failure.
    private static void RefPatch(ref AssemblyDefinition assembly, string source, string destination, Action<string> log)
    {
        ManagedPayloadUpgrade.Apply(ref assembly, source, destination, log, (_, _) => throw new IOException("Simulated second reader"));
    }

    private static void Main(string[] args)
    {
        var root = Path.Combine(Path.GetTempPath(), "novr-payload-" + Guid.NewGuid().ToString("N"));
        var payload = Path.Combine(root, "payload");
        var managed = Path.Combine(root, "Managed");
        Directory.CreateDirectory(payload);
        Directory.CreateDirectory(managed);
        try
        {
            foreach (var name in new[] { "Unity.XR.OpenXR", "Unity.XR.Management" })
            {
                var source = Path.Combine(payload, name + ".dll");
                var target = Path.Combine(managed, name + ".dll");
                Write(source, name, 1);
                Check(!ManagedPayloadUpgrade.DeferExisting(source, target, payload, managed), "Fresh install must copy normally.");
                Check(PayloadFile.CopyIfChanged(source, target), "Fresh payload installed.");
                var original = AssemblyDefinition.ReadAssembly(target);
                Check(ManagedPayloadUpgrade.DeferExisting(source, target, payload, managed), "Existing valid owned DLL must defer.");
                Check(!ManagedPayloadUpgrade.Apply(ref original, source, target, _ => throw new Exception("No log needed")), "Identical payload avoids replacement/persistence.");
                Writable(original);
                Write(source, name, 2);
                if (OperatingSystem.IsWindows())
                    Throws(() => { using var denied = new FileStream(target, FileMode.Open, FileAccess.Write, FileShare.Read); }, "Cecil's preloader reader should prohibit Windows writes.");
                var previous = original;
                Check(ManagedPayloadUpgrade.Apply(ref original, source, target, Console.WriteLine), "Changed payload replaces pending definition.");
                Check(!ReferenceEquals(previous, original) && Marker(original) == 2, "The returned definition contains new code.");
                Writable(original);
                Check(PayloadFile.SameContents(source, target), "Updated payload persists after previous reader closes.");
                Check(!Directory.GetFiles(managed, "*.tmp").Any(), "Atomic replacement leaves no staging files.");
                original.Dispose();
                previous.Dispose();

                Write(target, name, 1);
                var beforeBytes = File.ReadAllBytes(target);
                var before = AssemblyDefinition.ReadAssembly(target);
                var log = new List<string>();
                object[] parameters = { before, source, target, (Action<string>)log.Add };
                typeof(Program).GetMethod(nameof(RefPatch), BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, parameters);
                var after = (AssemblyDefinition)parameters[0];
                Check(!ReferenceEquals(before, after) && Marker(after) == 2, "Ref replacement survives failed persistence through BepInEx-style Invoke.");
                Writable(after);
                Check(File.ReadAllBytes(target).SequenceEqual(beforeBytes), "Failed persistence retains old destination.");
                Check(log.Any(line => line.Contains("Close the game")), "Failure gives an actionable offline recovery instruction.");
                after.Dispose();
                before.Dispose();

                original = AssemblyDefinition.ReadAssembly(target);
                previous = original;
                Throws(() => ManagedPayloadUpgrade.Apply(ref original, source, target, _ => { }, prepare: _ => throw new InvalidDataException("Invalid IL patch")), "Preparation failure must be reported.");
                Check(ReferenceEquals(original, previous) && Marker(original) == 1, "Preparation failure keeps original usable.");
                Writable(original);
                original.Dispose();
            }

            var xrSource = Path.Combine(payload, "Unity.XR.OpenXR.dll");
            var xrTarget = Path.Combine(managed, "Unity.XR.OpenXR.dll");
            foreach (var invalid in new[] { "identity", "api", "image" })
            {
                Write(xrSource, "Unity.XR.OpenXR", 2, validType: invalid != "api", version: invalid == "identity" ? 1 : 0);
                if (invalid == "image")
                    File.WriteAllText(xrSource, "not an assembly");
                var original = AssemblyDefinition.ReadAssembly(xrTarget);
                var previous = original;
                Throws(() => ManagedPayloadUpgrade.Apply(ref original, xrSource, xrTarget, _ => { }), "Reject invalid " + invalid);
                Check(ReferenceEquals(original, previous) && Marker(original) == 1, "Invalid candidate preserves original.");
                Writable(original);
                original.Dispose();
            }
            Write(xrSource, "Unity.XR.OpenXR", 2);
            Check(!ManagedPayloadUpgrade.DeferExisting(xrSource, xrTarget, payload, Path.Combine(root, "Other")), "No deferral outside the game's exact Managed directory.");
            File.WriteAllText(xrTarget, "corrupt installed image");
            Check(!ManagedPayloadUpgrade.DeferExisting(xrSource, xrTarget, payload, managed), "Cecil-skipped corrupt image is repaired via normal initializer copy.");
            PayloadFile.CopyIfChanged(xrSource, xrTarget);

            // Optional private dry-load: copies supplied XR binaries to this
            // temporary directory, never edits a game or launches Unity.
            if (args.Length == 2)
                foreach (var name in new[] { "Unity.XR.Management", "Unity.XR.OpenXR" })
                {
                    var source = Path.Combine(args[0], name + ".dll");
                    var target = Path.Combine(args[1], name + ".dll");
                    var copy = Path.Combine(managed, name + ".dll");
                    File.Copy(target, copy, true);
                    // An extra byte changes content without changing valid PE metadata.
                    using (var extra = new FileStream(copy, FileMode.Append))
                        extra.WriteByte(0);
                    var original = AssemblyDefinition.ReadAssembly(copy);
                    ((BaseAssemblyResolver)original.MainModule.AssemblyResolver).AddSearchDirectory(args[1]);
                    Check(ManagedPayloadUpgrade.Apply(ref original, source, copy, Console.WriteLine), "Actual XR payload dry upgrade succeeds: " + name);
                    Writable(original);
                    original.Dispose();
                }

            Write(xrSource, "Unity.XR.OpenXR", 2);
            Write(xrTarget, "Unity.XR.OpenXR", 1);
            var loaded = Assembly.Load(File.ReadAllBytes(xrSource));
            Check(loaded.GetName().Name == "Unity.XR.OpenXR", "Test target genuinely CLR-loaded.");
            var current = AssemblyDefinition.ReadAssembly(xrTarget);
            var unchanged = current;
            Throws(() => ManagedPayloadUpgrade.Apply(ref current, xrSource, xrTarget, _ => { }), "Already CLR-loaded XR cannot be silently replaced.");
            Check(ReferenceEquals(current, unchanged) && Marker(current) == 1, "CLR-loaded rejection keeps old definition usable.");
            current.Dispose();
            Console.WriteLine($"{_passed} source-linked managed payload checks passed (real Cecil; Windows sharing assertion runs only on Windows; no game launched).");
        }
        finally { Directory.Delete(root, true); }
    }
}
