using Mono.Cecil;

// Opt-in local assembly contracts only. Read the installed URP metadata without
// loading Unity or running game code; portable boundary doubles cannot establish
// that these private reflection and Harmony entry points exist in the game.
internal static class FixedFoveationUrpContracts
{
    internal static void Run(Action<string, Action> check, string gameManagedDirectory)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(Path.Combine(gameManagedDirectory,
            "Unity.RenderPipelines.Universal.Runtime.dll"));
        var module = assembly.MainModule;
        check("Local URP metadata: fixed foveation Harmony entry point accepts ref RenderingData", () =>
        {
            var renderer = module.Types.Single(t => t.FullName == "UnityEngine.Rendering.Universal.ScriptableRenderer");
            var candidates = renderer.Methods.Where(m => m.Name == "AddRenderPasses").ToArray();
            Require(candidates.Length == 1, "ScriptableRenderer.AddRenderPasses is missing or has ambiguous overloads.");
            var method = candidates[0];
            Require(!method.IsStatic && method.HasBody && !method.HasGenericParameters &&
                    method.ReturnType.FullName == "System.Void" && method.Parameters.Count == 1 &&
                    method.Parameters[0].ParameterType.FullName == "UnityEngine.Rendering.Universal.RenderingData&" &&
                    method.Parameters[0].Name == "renderingData",
                "ScriptableRenderer.AddRenderPasses no longer matches the fixed-foveation postfix signature.");
        });
        check("Local URP metadata: native render-pass control remains nonpublic readable and writable", () =>
        {
            var pass = module.Types.Single(t => t.FullName == "UnityEngine.Rendering.Universal.ScriptableRenderPass");
            var property = pass.Properties.SingleOrDefault(p => p.Name == "useNativeRenderPass");
            Require(property?.PropertyType.FullName == "System.Boolean" && property.Parameters.Count == 0 &&
                    property.GetMethod is { IsPublic: false, IsStatic: false, HasBody: true } getter &&
                    getter.Parameters.Count == 0 && getter.ReturnType.FullName == "System.Boolean" &&
                    property.SetMethod is { IsPublic: false, IsStatic: false, HasBody: true } setter &&
                    setter.Parameters.Count == 1 && setter.Parameters[0].ParameterType.FullName == "System.Boolean" &&
                    setter.ReturnType.FullName == "System.Void",
                "ScriptableRenderPass.useNativeRenderPass cannot be read and disabled with the current reflection flags.");
        });
        check("Local URP metadata: renderer mode remains a nonpublic readable forward-mode enum", () =>
        {
            var renderer = module.Types.Single(t => t.FullName == "UnityEngine.Rendering.Universal.UniversalRenderer");
            var property = renderer.Properties.SingleOrDefault(p => p.Name == "renderingModeRequested");
            Require(property != null && property.Parameters.Count == 0 &&
                    property.GetMethod is { IsPublic: false, IsStatic: false, HasBody: true } getter && getter.Parameters.Count == 0,
                "UniversalRenderer.renderingModeRequested cannot be read with the current reflection flags.");
            var mode = module.Types.SingleOrDefault(t => t.FullName == property!.PropertyType.FullName);
            Require(mode is { IsEnum: true } && mode.Fields.Any(f => f.Name == "Forward") && mode.Fields.Any(f => f.Name == "ForwardPlus"),
                "UniversalRenderer.renderingModeRequested no longer exposes the expected Forward/ForwardPlus enum names.");
        });
        check("Local URP metadata: full-rate foliage can split the forward opaque pass", () =>
        {
            var renderer = module.Types.Single(t => t.FullName == "UnityEngine.Rendering.Universal.UniversalRenderer");
            var opaque = renderer.Fields.SingleOrDefault(f => f.Name == "m_RenderOpaqueForwardPass");
            Require(opaque is { IsStatic: false } && opaque.FieldType.FullName == "UnityEngine.Rendering.Universal.Internal.DrawObjectsPass",
                "UniversalRenderer.m_RenderOpaqueForwardPass is missing or no longer a DrawObjectsPass.");
            var pass = module.Types.Single(t => t.FullName == "UnityEngine.Rendering.Universal.Internal.DrawObjectsPass");
            var filtering = pass.Fields.SingleOrDefault(f => f.Name == "m_FilteringSettings");
            Require(filtering is { IsStatic: false } && filtering.FieldType.FullName == "UnityEngine.Rendering.FilteringSettings",
                "DrawObjectsPass.m_FilteringSettings is missing or changed type.");
            var execute = pass.Methods.Where(m => m.Name == "Execute" && !m.IsStatic).ToArray();
            Require(execute.Length == 1 && execute[0].HasBody && execute[0].Parameters.Count == 2 &&
                    execute[0].Parameters[0].ParameterType.FullName == "UnityEngine.Rendering.ScriptableRenderContext" &&
                    execute[0].Parameters[0].Name == "context" &&
                    execute[0].Parameters[1].ParameterType.FullName == "UnityEngine.Rendering.Universal.RenderingData&" &&
                    execute[0].Parameters[1].Name == "renderingData",
                "DrawObjectsPass.Execute no longer matches the full-rate foliage prefix/postfix signature.");
            // The patch reads the filter on every call; Execute must copy it into its pass data.
            Require(execute[0].Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "m_FilteringSettings"),
                "DrawObjectsPass.Execute no longer reads m_FilteringSettings, so narrowing it would not split the pass.");
        });
        check("Local URP metadata: the sun shadow pass can keep the first eye's map for the second eye", () =>
        {
            // SharedEyeShadows relies on this exact shape of the pass; any
            // difference must be reviewed before the option is trusted again.
            var pass = module.Types.Single(t => t.FullName == "UnityEngine.Rendering.Universal.Internal.MainLightShadowCasterPass");
            MethodDefinition One(string name) => pass.Methods.Single(m => m.Name == name);
            bool Calls(MethodDefinition method, string type, string name) => method.Body.Instructions.Any(i =>
                i.Operand is MethodReference call && call.DeclaringType.Name == type && call.Name == name);
            int First(MethodDefinition method, Func<Mono.Cecil.Cil.Instruction, bool> match) =>
                method.Body.Instructions.Select((instruction, index) => (instruction, index)).Where(x => match(x.instruction)).Select(x => x.index).DefaultIfEmpty(-1).First();
            var empty = pass.Fields.SingleOrDefault(f => f.Name == "m_CreateEmptyShadowmap");
            Require(empty is { IsStatic: false } && empty.FieldType.FullName == "System.Boolean", "m_CreateEmptyShadowmap is missing or changed type.");
            var setup = One("Setup");
            Require(setup.ReturnType.FullName == "System.Boolean" && setup.Parameters.Count == 1 &&
                    setup.Parameters[0].ParameterType.FullName == "UnityEngine.Rendering.Universal.RenderingData&" && setup.Parameters[0].Name == "renderingData",
                "MainLightShadowCasterPass.Setup no longer matches the patch signature.");
            // A camera without shadow support must leave an earlier map's cascade data alone.
            var supports = First(setup, i => i.Operand is FieldReference f && f.Name == "supportsMainLightShadows");
            var clear = First(setup, i => i.Operand is MethodReference m && m.Name == "Clear" && m.DeclaringType.Name == "MainLightShadowCasterPass");
            Require(supports >= 0 && clear > supports, "Setup no longer checks supportsMainLightShadows before clearing its cascade data.");
            Require(setup.Body.Instructions.Any(i => i.OpCode == Mono.Cecil.Cil.OpCodes.Stfld && i.Operand is FieldReference f && f.Name == "m_CreateEmptyShadowmap"),
                "Setup no longer decides m_CreateEmptyShadowmap.");
            var configure = One("Configure");
            Require(Calls(configure, "ScriptableRenderPass", "ConfigureTarget") && Calls(configure, "ScriptableRenderPass", "ConfigureClear") &&
                    configure.Body.Instructions.Any(i => i.Operand is FieldReference f && f.Name == "m_CreateEmptyShadowmap"),
                "Configure no longer selects and clears the shadow map target itself.");
            var execute = One("Execute");
            Require(execute.Parameters.Count == 2 && Calls(execute, "MainLightShadowCasterPass", "RenderMainLightCascadeShadowmap") &&
                    Calls(execute, "CommandBuffer", "SetGlobalTexture"), "Execute no longer draws the cascades and binds the map in one call.");
            var render = One("RenderMainLightCascadeShadowmap");
            var slice = First(render, i => i.Operand is MethodReference m && m.DeclaringType.Name == "ShadowUtils" && m.Name == "RenderShadowSlice" && m.Parameters.Count == 6);
            var constants = First(render, i => i.Operand is MethodReference m && m.Name == "SetupMainLightShadowReceiverConstants");
            Require(slice >= 0 && constants > slice, "The cascade draws are no longer separate from the receiver constants that follow them.");
            Require(render.Body.Instructions.Count(i => i.Operand is MethodReference m && m.DeclaringType.Name == "CommandBuffer" &&
                    (m.Name == "SetRenderTarget" || m.Name == "ClearRenderTarget")) == 0,
                "RenderMainLightCascadeShadowmap now clears or rebinds the map itself; skipping the slices would leave it empty.");
            var utils = module.Types.Single(t => t.FullName == "UnityEngine.Rendering.Universal.ShadowUtils");
            var sliceMethod = utils.Methods.SingleOrDefault(m => m.Name == "RenderShadowSlice" && m.Parameters.Count == 6);
            Require(sliceMethod is { IsStatic: true, IsPublic: true } &&
                    sliceMethod.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(new[]
                    {
                        "UnityEngine.Rendering.CommandBuffer", "UnityEngine.Rendering.ScriptableRenderContext&",
                        "UnityEngine.Rendering.Universal.ShadowSliceData&", "UnityEngine.Rendering.ShadowDrawingSettings&",
                        "UnityEngine.Matrix4x4", "UnityEngine.Matrix4x4"
                    }), "ShadowUtils.RenderShadowSlice no longer has the six-argument overload the patch targets.");
            // URP resets the shadow keywords at the start of every camera, which is why the pass must still run.
            var renderer = module.Types.Single(t => t.FullName == "UnityEngine.Rendering.Universal.ScriptableRenderer");
            Require(renderer.Methods.Any(m => m.HasBody && m.Body.Instructions.Any(i => i.Operand is MethodReference call && call.Name == "DisableShaderKeyword")),
                "ScriptableRenderer no longer resets rendering keywords per camera; the reuse path needs review.");
        });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
