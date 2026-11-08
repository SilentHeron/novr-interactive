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
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
