using System;
using System.Collections.Generic;

namespace NOVR.Compatibility;

internal sealed class PatchInstallReport
{
    internal int Applied;
    internal readonly List<KeyValuePair<string, string>> Skipped = new();
}

// Harmony's PatchAll stops at the first patch class that fails: the classes
// before it stay installed, the ones after it never are, and the exception
// used to abort the rest of plugin startup as well. Install each patch class
// on its own so one target that a game update renamed costs one adjustment.
internal static class PatchInstaller
{
    internal static PatchInstallReport Apply(IEnumerable<Type> types, Func<Type, bool> isPatchClass,
        Func<Type, string?> unresolvedHandle, Action<Type> install)
    {
        var report = new PatchInstallReport();
        foreach (var type in types)
        {
            try
            {
                if (type == null || !isPatchClass(type))
                    continue;
            }
            catch (Exception)
            {
                // Attributes that cannot be read cannot declare a patch either.
                continue;
            }
            string reason;
            try
            {
                var missing = unresolvedHandle(type);
                if (missing == null)
                {
                    install(type);
                    report.Applied++;
                    continue;
                }
                reason = "game member not found: " + missing;
            }
            catch (Exception error)
            {
                reason = Describe(error);
            }
            report.Skipped.Add(new KeyValuePair<string, string>(Name(type), reason));
        }
        return report;
    }

    private static string Name(Type type) =>
        type.DeclaringType == null ? type.Name : Name(type.DeclaringType) + "." + type.Name;

    // Harmony wraps the useful message ("Undefined target method …") one or
    // two levels down; keep the outer type for context and the inner text.
    private static string Describe(Exception error)
    {
        var inner = error;
        while (inner.InnerException != null)
            inner = inner.InnerException;
        var message = inner.Message ?? string.Empty;
        var line = message.IndexOf('\n');
        if (line >= 0)
            message = message.Substring(0, line).TrimEnd('\r');
        return error.GetType().Name + ": " + message;
    }
}
