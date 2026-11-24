using NOVR.Compatibility;
using UnityEngine;

namespace NOVR.VrTogglers;

// Shown on the desktop window while VR has not started: without it the only
// sign of a headset that was not ready is a line in the log. Disabled (and so
// free of any per-frame GUI work) whenever there is nothing to show. Its
// button restarts the game without VR, for a session at the desk.
internal sealed class VrStartNotice : MonoBehaviour
{
    private string _reason = string.Empty;
    private string? _restartMessage;
    private GUIStyle? _style, _buttonStyle;
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
        "\nTo play without VR, use the button below or start the game with -novr-flat.";

    private void OnGUI()
    {
        _style ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 16, wordWrap = true };
        _buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 15 };
        var width = Mathf.Min(720f, Screen.width - 40f);
        var left = (Screen.width - width) * .5f;
        GUI.Box(new Rect(left, 24f, width, 112f), _restartMessage ?? Text(_reason, SecondsUntilRetry), _style);
        var buttonWidth = Mathf.Min(260f, width);
        if (GUI.Button(new Rect(left + (width - buttonWidth) * .5f, 144f, buttonWidth, 34f), "Restart without VR", _buttonStyle))
            _restartMessage = VrModeRestart.Restart(flat: true) ?? "NOVR: restarting without VR...";
    }
}
