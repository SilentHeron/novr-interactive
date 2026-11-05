using System;
using System.Text;

namespace NOVR.Comfort;

// Discovery hints, not a semantic mesh classifier. Unknown or combined geometry
// stays untouched; material sharing does not establish seat ownership.
internal static class SeatCandidatePolicy
{
    internal static bool IsEligible(string? objectName, string? meshName,
        bool nearestPilotIsLocal, bool isSeparateMeshRenderer, int vertexCount,
        float widthMeters, float heightMeters, float depthMeters)
    {
        // The caller must resolve the nearest Pilot ancestor, so another crew
        // member nested below the local pilot cannot inherit its eligibility.
        if (!nearestPilotIsLocal || !isSeparateMeshRenderer || vertexCount < 3 || vertexCount > 30000)
            return false;

        // Use mesh-local axis lengths multiplied by absolute inherited scale,
        // not a world-axis bounding box that changes with aircraft attitude.
        if (!Within(widthMeters, .03f, 1.4f) || !Within(heightMeters, .03f, 2.2f) ||
            !Within(depthMeters, .03f, 1.8f))
            return false;

        return IsSeatIdentifier(objectName) && IsSeatIdentifier(meshName);
    }

    private static bool Within(float value, float minimum, float maximum) =>
        !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;

    private static bool IsSeatIdentifier(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name!.Length > 160)
            return false;
        name = name.Trim();
        if (name.EndsWith("(Clone)", StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - 7).TrimEnd();

        var normalized = new StringBuilder(name.Length);
        foreach (var character in name)
        {
            if (character == ' ' || character == '_' || character == '-')
                continue;
            if (!((character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') ||
                (character >= '0' && character <= '9')))
                return false;
            normalized.Append(char.ToLowerInvariant(character));
        }
        var identifier = normalized.ToString();
        var lod = identifier.LastIndexOf("lod", StringComparison.Ordinal);
        if (lod >= 0 && lod + 3 < identifier.Length)
        {
            for (var i = lod + 3; i < identifier.Length; i++)
                if (identifier[i] < '0' || identifier[i] > '9')
                    return false;
            identifier = identifier.Substring(0, lod);
        }

        // These are seat/component names, never aircraft names. Both the object
        // and mesh must match; a cockpit merely parented under a seat is rejected.
        return identifier is "seat" or "ejectionseat" or "fixedseat" or "fixedseatarmor" or
            "pilotseat" or "headrest";
    }
}
