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
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
