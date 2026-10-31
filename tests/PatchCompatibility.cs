using Mono.Cecil;

internal static class PatchCompatibility
{
    public static void VerifyInputBackend(string gameManagedDirectory)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(gameManagedDirectory, "Rewired_Core.dll"));
        var input = assembly.MainModule.Types.Single(t => t.FullName == "Rewired.ReInput");
        var initialized = input.Events.Single(e => e.Name == "InitializedEvent");
        var ready = input.Properties.Single(p => p.Name == "isReady");
        var config = input.NestedTypes.Single(t => t.Name == "ConfigHelper");
        var xinput = config.Properties.Single(p => p.Name == "useXInput");
        if (initialized.EventType.FullName != "System.Action" || !initialized.AddMethod.IsPublic || !initialized.AddMethod.IsStatic ||
            ready.PropertyType.FullName != "System.Boolean" || !ready.GetMethod.IsPublic || !ready.GetMethod.IsStatic ||
            xinput.PropertyType.FullName != "System.Boolean" || xinput.SetMethod?.IsPublic != true || xinput.GetMethod?.IsPublic != true)
            throw new Exception("Rewired initialization/configuration API changed; Proton compatibility needs review.");
    }

    public static void Verify(string modPath, string gameManagedDirectory)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(gameManagedDirectory);
        resolver.AddSearchDirectory(Path.GetFullPath(Path.Combine(gameManagedDirectory, "..", "..", "BepInEx", "core")));
        resolver.AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(modPath))!);
        using var mod = AssemblyDefinition.ReadAssembly(modPath, new ReaderParameters { AssemblyResolver = resolver });
        var failures = new List<string>();
        var verified = 0;
        foreach (var type in AllTypes(mod.MainModule.Types))
        foreach (var patch in type.CustomAttributes.Where(a => a.AttributeType.FullName == "HarmonyLib.HarmonyPatch"))
        {
            var target = patch.ConstructorArguments.Select(a => a.Value).OfType<TypeReference>().FirstOrDefault();
            var method = patch.ConstructorArguments.Select(a => a.Value).OfType<string>().FirstOrDefault();
            if (target == null || method == null) continue;
            var definition = target.Resolve();
            var signature = patch.ConstructorArguments.FirstOrDefault(a => a.Type.FullName == "System.Type[]").Value as CustomAttributeArgument[];
            var candidates = definition.Methods.Where(m => m.Name == method);
            if (signature != null)
                candidates = candidates.Where(m => m.Parameters.Select(p => p.ParameterType.FullName)
                    .SequenceEqual(signature.Select(a => ((TypeReference)a.Value).FullName)));
            if (!candidates.Any())
            {
                failures.Add($"Missing method or overload {target.FullName}.{method}");
                continue;
            }
            foreach (var parameter in type.Methods.SelectMany(m => m.Parameters).Where(p => p.Name.StartsWith("___")))
            {
                var fieldName = parameter.Name[3..];
                var owner = definition;
                while (owner != null && !owner.Fields.Any(f => f.Name == fieldName)) owner = owner.BaseType?.Resolve();
                if (owner == null) failures.Add($"Missing injected field {target.FullName}.{fieldName}");
            }
            verified++;
        }
        // Native guidance is mirrored read-only. Verify the private fields used
        // by that bridge as well as the Harmony-injected fields above.
        using var game = AssemblyDefinition.ReadAssembly(Path.Combine(gameManagedDirectory, "Assembly-CSharp.dll"));
        foreach (var contract in new[] {
            (owner: "CombatHUD", field: "weaponState", type: "HUDWeaponState"),
            (owner: "CombatHUD", field: "markers", type: "System.Collections.Generic.List`1<HUDUnitMarker>"),
            (owner: "CombatHUD", field: "targetText", type: "TMPro.TextMeshProUGUI"),
            (owner: "CombatHUD", field: "targetInfo", type: "TMPro.TextMeshProUGUI"),
            (owner: "CombatHUD", field: "targetArrow", type: "UnityEngine.UI.Image"),
            (owner: "AllyInfo", field: "hoveredAllyInfo", type: "TMPro.TextMeshProUGUI"),
            (owner: "AllyInfo", field: "hoveredAlly", type: "Aircraft"),
            (owner: "TacScreen", field: "displaySize", type: "System.Single"),
            (owner: "TacScreen", field: "metersPerPixel", type: "System.Single"),
            (owner: "TacScreen", field: "iconLayer", type: "UnityEngine.Transform"),
            (owner: "TacScreen", field: "cam", type: "UnityEngine.Camera"),
            (owner: "TacScreen", field: "canvas", type: "UnityEngine.Canvas"),
            (owner: "TacScreen", field: "screenMaterial", type: "UnityEngine.Material"),
            (owner: "TargetCam", field: "cam", type: "UnityEngine.Camera"),
            (owner: "TargetCam", field: "currentMode", type: "TargetCam/CamMode"),
            (owner: "TargetCam", field: "canvasObjectTarget", type: "UnityEngine.GameObject"),
            (owner: "TargetCam", field: "canvasObjectLanding", type: "UnityEngine.GameObject"),
            (owner: "HUDMissileState", field: "hint", type: "TMPro.TextMeshProUGUI"),
            (owner: "HUDAppManager", field: "aircraft", type: "Aircraft"),
            (owner: "HUDAppManager", field: "apps", type: "HUDApp[]"),
            (owner: "NozzleGauge", field: "system", type: "INozzleGauge"),
            (owner: "NozzleGauge", field: "fade", type: "System.Boolean"),
            (owner: "NozzleGauge", field: "fadeImages", type: "UnityEngine.UI.Image[]"),
            (owner: "NozzleGauge", field: "imageAlphas", type: "System.Single[]"),
            (owner: "NozzleGauge", field: "label", type: "TMPro.TextMeshProUGUI"),
            (owner: "NozzleGauge", field: "nozzleIcon", type: "UnityEngine.Transform"),
            (owner: "NozzleGauge", field: "inputPivot", type: "UnityEngine.Transform"),
            (owner: "NozzleGauge", field: "inputRange", type: "System.Single"),
            (owner: "NozzleGauge", field: "inputs", type: "ControlInputs"),
            (owner: "ThreatList", field: "itemLookup", type: "System.Collections.Generic.Dictionary`2<PersistentID,ThreatItem>"),
            (owner: "ThreatItem", field: "missile", type: "Missile"),
            (owner: "ThreatItem", field: "notchLine", type: "UnityEngine.GameObject"),
            (owner: "ThreatItem", field: "notchIndicator", type: "UnityEngine.GameObject"),
            (owner: "ThreatItem", field: "notchIndicatorBox", type: "UnityEngine.UI.Image"),
            (owner: "ThreatItem", field: "notchIndicatorLabel", type: "UnityEngine.UI.Text"),
            (owner: "ThreatItem", field: "playerAircraftIconTransform", type: "UnityEngine.Transform"),
            (owner: "MessageUI", field: "killFeedText", type: "TMPro.TextMeshProUGUI"),
            (owner: "MessageUI", field: "messageFeed", type: "MessageFeed"),
            (owner: "MessageUI", field: "chat", type: "ChatBox"),
            (owner: "MessageUI", field: "backgroundImage", type: "UnityEngine.UI.Image"),
            (owner: "KillDisplay", field: "killText", type: "TMPro.TextMeshProUGUI"),
            (owner: "KillDisplay", field: "rankText", type: "TMPro.TextMeshProUGUI"),
            (owner: "KillDisplay", field: "rankBackground", type: "UnityEngine.UI.Image"),
            (owner: "HUDLaserGuidedState", field: "hint", type: "TMPro.TextMeshProUGUI") })
        {
            var field = game.MainModule.Types.Single(t => t.FullName == contract.owner).Fields.SingleOrDefault(f => f.Name == contract.field);
            if (field?.FieldType.FullName != contract.type) failures.Add($"Native readout field mismatch {contract.owner}.{contract.field}");
        }
        using var urp = AssemblyDefinition.ReadAssembly(Path.Combine(gameManagedDirectory, "Unity.RenderPipelines.Universal.Runtime.dll"));
        if (urp.MainModule.Types.Single(t => t.Name == "UniversalAdditionalCameraData").Fields.SingleOrDefault(f => f.Name == "m_ClearDepth")?.FieldType.FullName != "System.Boolean")
            failures.Add("Foreground overlay depth-clear contract changed.");
        if (verified < 20) throw new Exception($"Only {verified} patch targets were checked; wrong build?");
        // These nested native types are private, so their Harmony targets are
        // resolved dynamically. Verify return values and injected fields too.
        var radarWarning = game.MainModule.Types.Single(t => t.Name == "RadarWarning");
        foreach (var name in new[] { "RadarWarningIcon", "JammingIcon" })
        {
            var icon = radarWarning.NestedTypes.Single(t => t.Name == name);
            var position = icon.Methods.Single(m => m.Name == "Position");
            if (position.ReturnType.FullName != "System.Boolean" || position.Parameters.Count != 0 ||
                icon.Fields.Single(f => f.Name == "image").FieldType.FullName != "UnityEngine.UI.Image" ||
                (name == "JammingIcon" && icon.Fields.Single(f => f.Name == "text").FieldType.FullName != "UnityEngine.UI.Text"))
                failures.Add("Native threat-graphic contract changed: " + name);
        }
        var selector = game.MainModule.Types.Single(t => t.Name == "CombatHUD").Methods.Single(m => m.Name == "TargetSelect").Body.Instructions;
        var acquisitionSites = selector.Count(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Ldc_R4 && i.Operand is float f && f == 100 &&
            i.Next?.Operand is MethodReference call && call.DeclaringType.Name == "FastMath" && call.Name == "InRange");
        if (acquisitionSites != 1) failures.Add("Native optical selection acquisition-circle IL contract changed.");
        if (failures.Count != 0) throw new Exception(string.Join(Environment.NewLine, failures));
        Console.WriteLine($"Verified {verified} declared patch targets (metadata only; no game code executed).");
    }

    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> roots)
    {
        foreach (var type in roots)
        {
            yield return type;
            foreach (var nested in AllTypes(type.NestedTypes)) yield return nested;
        }
    }
}
