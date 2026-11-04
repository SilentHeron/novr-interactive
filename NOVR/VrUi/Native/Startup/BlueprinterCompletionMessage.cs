using System;
using System.Globalization;

namespace NOVR.VrUi.Native;

internal static class BlueprinterCompletionMessage
{
    internal static bool IsSuccess(string? message)
    {
        const string prefix = "[Plugin] Done in ";
        if (message == null || !message.StartsWith(prefix, StringComparison.Ordinal) ||
            !message.EndsWith("s", StringComparison.Ordinal))
            return false;
        var duration = message.Substring(prefix.Length, message.Length - prefix.Length - 1);
        // The loader uses F2 in its current culture. This only recognizes its
        // explicit terminal message; the duration is never a readiness threshold.
        return double.TryParse(duration, NumberStyles.Float, CultureInfo.CurrentCulture, out var seconds) &&
               !double.IsNaN(seconds) && !double.IsInfinity(seconds) && seconds >= 0;
    }
}
