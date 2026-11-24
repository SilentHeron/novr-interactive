using NOVR.Compatibility;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NOVR.VrTogglers;

// The only part of NOVR that runs in a -novr-flat session. No patch is
// installed and no VR code starts; at the main menu it offers the way back
// into VR, by a button on the desktop window or F11. It stays out of a
// mission, where a restart would end the flight.
internal sealed class FlatModeSwitch : MonoBehaviour
{
    internal const string Label = "NOVR: VR is off. Restart in VR (F11)";
    private static FlatModeSwitch? _instance;
    private string _message = Label;
    private GUIStyle? _style;

    // The plugin constructor can run more than once; one switch is enough.
    internal static void Create()
    {
        if (_instance != null)
            return;
        var host = new GameObject("NOVR flat mode");
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<FlatModeSwitch>();
    }

    private static bool AtMainMenu => GameManager.gameState == GameState.Menu;

    private void Update()
    {
        if (AtMainMenu && Keyboard.current?.f11Key.wasPressedThisFrame == true)
            Restart();
    }

    private void Restart() => _message = VrModeRestart.Restart(flat: false) ?? "NOVR: restarting in VR...";

    private void OnGUI()
    {
        if (!AtMainMenu)
            return;
        _style ??= new GUIStyle(GUI.skin.button) { fontSize = 14, wordWrap = true };
        // A failure message is longer than the label; give it room to wrap.
        var tall = _message.Length > Label.Length + 12;
        var size = tall ? new Vector2(420f, 72f) : new Vector2(300f, 34f);
        if (GUI.Button(new Rect(16f, Screen.height - size.y - 16f, size.x, size.y), _message, _style))
            Restart();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }
}
