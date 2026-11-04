using System;

namespace NOVR.MotionControls;

internal enum PilotUniformTeam { Native, Bdf, Pala }

internal static class PilotUniformPalette
{
    internal static PilotUniformTeam Resolve(string? tag, string? name)
    {
        static bool Is(string? value, string expected) => string.Equals(value?.Trim(), expected, StringComparison.OrdinalIgnoreCase);
        if (Is(tag, "BDF") || Is(name, "Boscali")) return PilotUniformTeam.Bdf;
        if (Is(tag, "PALA") || Is(name, "Primeva")) return PilotUniformTeam.Pala;
        return PilotUniformTeam.Native;
    }

    internal static (byte red, byte green, byte blue) Color(PilotUniformTeam team, bool glove) => (team, glove) switch
    {
        (PilotUniformTeam.Bdf, true) => (63, 100, 151),
        (PilotUniformTeam.Bdf, false) => (71, 109, 65),
        (PilotUniformTeam.Pala, true) => (154, 42, 38),
        (PilotUniformTeam.Pala, false) => (26, 29, 33),
        _ => (255, 255, 255)
    };
}
