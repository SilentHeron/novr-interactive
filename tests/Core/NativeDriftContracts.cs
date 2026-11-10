using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Opt-in local assembly contracts (metadata only; no game code runs). They
// catch the two ways a game update breaks patches without any build error:
// a private member that a cached reflection handle names no longer exists, and
// a game method that NOVR replaces outright changed underneath the copy.
internal static class NativeDriftContracts
{
    private const string BaselineFile = "ReplacedMethodFingerprints.txt";
    // Set to 1 to rewrite the baseline after reviewing each reported method.
    private const string UpdateVariable = "NOVR_UPDATE_FINGERPRINTS";

    // Every static readonly handle initialised with AccessTools.Field(typeof(T), "name")
    // or AccessTools.FieldRefAccess<T, F>("name") must name a field that exists.
    public static void VerifyHandles(string modPath, string gameManagedDirectory)
    {
        using var mod = Read(modPath, gameManagedDirectory);
        var failures = new List<string>();
        var verified = 0;
        foreach (var type in AllTypes(mod.MainModule.Types))
        {
            var initializer = type.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic && m.HasBody);
            if (initializer == null)
                continue;
            foreach (var call in initializer.Body.Instructions)
            {
                if (call.OpCode != OpCodes.Call || call.Operand is not MethodReference method ||
                    method.DeclaringType.FullName != "HarmonyLib.AccessTools" || call.Previous?.OpCode != OpCodes.Ldstr)
                    continue;
                var name = (string)call.Previous.Operand;
                TypeReference? owner = null;
                if (method.Name == "Field" && method.Parameters.Count == 2 &&
                    call.Previous.Previous?.Operand is MethodReference { Name: "GetTypeFromHandle" } &&
                    call.Previous.Previous.Previous?.Operand is TypeReference token)
                    owner = token;
                else if (method.Name == "FieldRefAccess" && method is GenericInstanceMethod { GenericArguments.Count: 2 } generic &&
                         method.Parameters.Count == 1)
                    owner = generic.GenericArguments[0];
                if (owner == null)
                    continue;
                // Optional handles (declared nullable and null-checked) may name
                // members of other mods or older game builds; only fields that
                // are stored into a non-nullable handle must exist.
                var stored = call.Next?.OpCode == OpCodes.Stsfld ? call.Next.Operand as FieldReference : null;
                var definition = owner.Resolve();
                var found = false;
                for (var current = definition; current != null && !found; current = current.BaseType?.Resolve())
                    found = current.Fields.Any(f => f.Name == name);
                if (found)
                    verified++;
                else if (stored == null || !IsNullable(stored.Resolve()))
                    failures.Add($"{type.FullName}: {owner.FullName}.{name} does not exist");
            }
        }
        if (failures.Count != 0)
            throw new Exception(string.Join(Environment.NewLine, failures));
        Console.WriteLine($"Verified {verified} cached private-field handles against the installed game.");
    }

    // A prefix that returns bool can skip the game's method and usually
    // re-implements it. Fingerprint each such target; a changed fingerprint
    // means the copy in NOVR must be reviewed against the new game code.
    public static void VerifyReplacedMethods(string modPath, string gameManagedDirectory)
    {
        using var mod = Read(modPath, gameManagedDirectory);
        var current = new SortedDictionary<string, (string Hash, string Patch)>(StringComparer.Ordinal);
        foreach (var type in AllTypes(mod.MainModule.Types))
            foreach (var prefix in type.Methods.Where(IsSkippingPrefix))
            {
                var target = Target(type, prefix);
                if (target?.HasBody != true)
                    continue;
                current[target.FullName] = (Fingerprint(target), type.FullName);
            }
        var path = Path.Combine(Path.GetDirectoryName(SourcePath())!, BaselineFile);
        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllLines(path, new[]
            {
                "# Fingerprints of game methods that a NOVR prefix can skip (and so re-implements).",
                "# Checked by `python tools/verify.py contracts`. When one changes after a game update,",
                "# compare the new game method with the named NOVR patch, then regenerate this file with",
                "# " + UpdateVariable + "=1."
            }.Concat(current.Select(entry => entry.Value.Hash + " " + entry.Key)));
            Console.WriteLine($"Wrote {current.Count} replaced-method fingerprints to {path}.");
            return;
        }
        var baseline = File.Exists(path)
            ? File.ReadAllLines(path).Where(line => line.Length > 0 && line[0] != '#')
                .ToDictionary(line => line[(line.IndexOf(' ') + 1)..], line => line[..line.IndexOf(' ')], StringComparer.Ordinal)
            : new Dictionary<string, string>();
        var failures = new List<string>();
        foreach (var entry in current)
        {
            if (!baseline.TryGetValue(entry.Key, out var expected))
                failures.Add($"No baseline for {entry.Key} (replaced by {entry.Value.Patch}).");
            else if (expected != entry.Value.Hash)
                failures.Add($"Game method changed: {entry.Key}. Review {entry.Value.Patch} against it.");
        }
        foreach (var stale in baseline.Keys.Where(key => !current.ContainsKey(key)))
            failures.Add($"Baseline lists {stale}, which no NOVR prefix replaces any more.");
        if (failures.Count != 0)
            throw new Exception(string.Join(Environment.NewLine, failures) + Environment.NewLine +
                $"After reviewing, regenerate tests/Core/{BaselineFile} with {UpdateVariable}=1.");
        Console.WriteLine($"Verified {current.Count} replaced game methods are unchanged since their NOVR copies were written.");
    }

    private static bool IsSkippingPrefix(MethodDefinition method) =>
        method.IsStatic && method.HasBody && method.ReturnType.FullName == "System.Boolean" &&
        (method.Name == "Prefix" || method.CustomAttributes.Any(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPrefix"));

    // Class-level and method-level [HarmonyPatch] arguments combine.
    private static MethodDefinition? Target(TypeDefinition patch, MethodDefinition prefix)
    {
        var arguments = patch.CustomAttributes.Concat(prefix.CustomAttributes)
            .Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch")
            .SelectMany(a => a.ConstructorArguments).ToArray();
        var owner = arguments.Select(a => a.Value).OfType<TypeReference>().FirstOrDefault()?.Resolve();
        var name = arguments.Select(a => a.Value).OfType<string>().FirstOrDefault();
        if (owner == null)
            return null;
        var kind = arguments.Where(a => a.Type.FullName == "HarmonyLib.MethodType").Select(a => Convert.ToInt32(a.Value)).FirstOrDefault();
        if (kind == 1) name = "get_" + name;
        if (kind == 2) name = "set_" + name;
        if (kind == 3) name = ".ctor";
        var signature = arguments.FirstOrDefault(a => a.Type.FullName == "System.Type[]").Value as CustomAttributeArgument[];
        var candidates = owner.Methods.Where(m => m.Name == name);
        if (signature != null)
            candidates = candidates.Where(m => m.Parameters.Select(p => p.ParameterType.FullName)
                .SequenceEqual(signature.Select(a => ((TypeReference)a.Value).FullName)));
        var matches = candidates.ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    // Stable across a rebuild that leaves the method alone: names rather than
    // metadata tokens, branch targets as instruction indices, and compiler
    // counters removed from generated names.
    private static string Fingerprint(MethodDefinition method)
    {
        var text = new StringBuilder(method.ReturnType.FullName);
        foreach (var parameter in method.Parameters)
            text.Append('|').Append(parameter.ParameterType.FullName);
        foreach (var local in method.Body.Variables)
            text.Append("|L:").Append(local.VariableType.FullName);
        var index = method.Body.Instructions.Select((instruction, i) => (instruction, i)).ToDictionary(x => x.instruction, x => x.i);
        foreach (var instruction in method.Body.Instructions)
        {
            text.Append('\n').Append(instruction.OpCode.Name).Append(' ');
            text.Append(instruction.Operand switch
            {
                null => "",
                Instruction target => "@" + index[target],
                Instruction[] targets => string.Join(",", targets.Select(t => "@" + index[t])),
                VariableDefinition local => "V" + local.Index,
                ParameterDefinition parameter => "A" + parameter.Index,
                MemberReference member => member.FullName,
                string value => "\"" + value + "\"",
                var value => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            });
        }
        foreach (var handler in method.Body.ExceptionHandlers)
            text.Append("\nEH ").Append(handler.HandlerType).Append(' ').Append(index[handler.TryStart]).Append(' ').Append(index[handler.HandlerStart]);
        var normalized = Regex.Replace(text.ToString(), @"(<[^>]*>[A-Za-z_]+)(\d+(_\d+)?)", "$1");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)))[..16];
    }

    private static bool IsNullable(FieldDefinition? field)
    {
        if (field == null)
            return false;
        byte? flag = Flag(field.CustomAttributes, "System.Runtime.CompilerServices.NullableAttribute");
        for (var owner = field.DeclaringType; flag == null && owner != null; owner = owner.DeclaringType)
            flag = Flag(owner.CustomAttributes, "System.Runtime.CompilerServices.NullableContextAttribute");
        return flag == 2;
    }

    private static byte? Flag(IEnumerable<CustomAttribute> attributes, string name)
    {
        var argument = attributes.FirstOrDefault(a => a.AttributeType.FullName == name && a.ConstructorArguments.Count == 1)?.ConstructorArguments[0].Value;
        return argument switch
        {
            byte single => single,
            CustomAttributeArgument[] { Length: > 0 } parts => parts[0].Value as byte?,
            _ => null
        };
    }

    private static AssemblyDefinition Read(string modPath, string gameManagedDirectory)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(gameManagedDirectory);
        resolver.AddSearchDirectory(Path.GetFullPath(Path.Combine(gameManagedDirectory, "..", "..", "BepInEx", "core")));
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(modPath))!);
        return AssemblyDefinition.ReadAssembly(modPath, new ReaderParameters { AssemblyResolver = resolver });
    }

    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> roots)
    {
        foreach (var type in roots)
        {
            yield return type;
            foreach (var nested in AllTypes(type.NestedTypes))
                yield return nested;
        }
    }

    private static string SourcePath([CallerFilePath] string path = "") => path;
}
