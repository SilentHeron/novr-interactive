using System.Runtime.CompilerServices;
using System.Text;
using TMPro;

namespace NOVR.VrUi.SpecialBehavior;

// Observe native refresh/expiry rather than creating another message queue.
// This also avoids TMP's one-shot StringBuilder text getter: subsequent reads
// can return stale text even though the native backing array is up to date.
internal static class NativeNotificationText
{
    private sealed class Snapshot
    {
        internal string Text = "";
    }

    private static readonly ConditionalWeakTable<TMP_Text, Snapshot> Snapshots = new();

    internal static void Record(TMP_Text source, StringBuilder text, bool hasContent)
    {
        if (source == null)
            return;
        Snapshots.GetValue(source, _ => new Snapshot()).Text = hasContent ? text.ToString() : "";
    }

    internal static string Read(TMP_Text source) =>
        Snapshots.TryGetValue(source, out var snapshot) ? snapshot.Text : source.text;
}
