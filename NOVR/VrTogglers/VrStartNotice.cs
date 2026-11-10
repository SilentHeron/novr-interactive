using UnityEngine;

namespace NOVR.VrTogglers;

// Shown on the desktop window while VR has not started: without it the only
// sign of a headset that was not ready is a line in the log. Disabled (and so
// free of any per-frame GUI work) whenever there is nothing to show.
internal sealed class VrStartNotice : MonoBehaviour
{
    private string _reason = string.Empty;
    private GUIStyle? _style;
    internal float SecondsUntilRetry = -1f;

    private void Awake() => enabled = false;

    internal void Show(string reason, float secondsUntilRetry)
    {
        _reason = reason;
        SecondsUntilRetry = secondsUntilRetry;
        enabled = true;
    }

    internal void Hide() => enabled = false;

    internal static string Text(string reason, float secondsUntilRetry) =>
        "NOVR: VR has not started.\n" + reason + "\n" +
        (secondsUntilRetry >= 0
            ? "Trying again in " + Mathf.CeilToInt(secondsUntilRetry) + " s."
            : "Press F11 to try again once your headset is ready.") +
        "\nTo play without VR, start the game with -novr-flat.";

    private void OnGUI()
    {
        _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = true };
        var width = Mathf.Min(720f, Screen.width - 40f);
        GUI.Box(new Rect((Screen.width - width) * .5f, 24f, width, 112f), Text(_reason, SecondsUntilRetry), _style);
    }
}
