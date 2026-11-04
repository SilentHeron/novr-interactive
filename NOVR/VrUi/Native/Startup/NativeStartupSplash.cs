using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

// Shares the native menu's stereo world canvas, tracked pointer and recenter control.
// There is no moving progress indicator: synchronous loader work can stop Unity
// frames, and a fake progress bar would misrepresent readiness.
internal sealed class NativeStartupSplash
{
    private readonly StartupSplashState _state = new();
    private readonly NativeGameActionAdapter _actions;
    private readonly RectTransform _container;
    private readonly Text _title, _body, _actionLabel, _backLabel;
    private readonly Button _back;
    private EventSystem? _fallbackEvents;
    private GraphicRaycaster? _loaderBlocker;
    private bool _loaderBlockerEnabled;
    private float _nextBlockerScan;
    private float _nextEventScan;
    private bool _shown;
    internal bool IsVisible => _state.Visible;
    internal bool Finished => _state.Finished;

    internal NativeStartupSplash(RectTransform parent, NativeGameActionAdapter actions)
    {
        _actions = actions;
        _container = Rect("NOVR Startup Loading", parent, Vector2.zero, parent.sizeDelta);
        Image("Loading backdrop", _container, Vector2.zero, new Vector2(6400, 4200), new Color(.018f, .025f, .035f, 1f));
        var card = Image("Loading card", _container, Vector2.zero, new Vector2(1120, 630), new Color(.035f, .05f, .065f, 1f));
        Image("Accent", card, new Vector2(0, 310), new Vector2(1120, 5), new Color(.35f, .72f, .82f, 1f));
        Label("Brand", card, "NUCLEAR OPTION  /  VR", new Vector2(0, 230), new Vector2(1000, 44), 25, new Color(.64f, .8f, .85f, 1));
        _title = Label("Loading title", card, "Loading aircraft & mods", new Vector2(0, 130), new Vector2(1000, 70), 42, Color.white);
        _body = Label("Loading details", card, "", new Vector2(0, 5), new Vector2(980, 150), 26, new Color(.83f, .88f, .91f, 1));
        var action = Button("Exit game", card, new Vector2(0, -150), new Vector2(350, 70), ToggleOrConfirmExit);
        _actionLabel = action.GetComponentInChildren<Text>();
        _back = Button("KEEP WAITING", card, new Vector2(0, -240), new Vector2(350, 60), CancelExit);
        _backLabel = _back.GetComponentInChildren<Text>();
        Label("Input hint", _container, "Point either controller and press Trigger  •  HOME to recenter  •  F10 for exit options", new Vector2(0, -375), new Vector2(1400, 55), 21, new Color(.66f, .74f, .79f, 1));
        _container.gameObject.SetActive(false);
    }

    internal bool Update(MissionContentPhase phase, bool vrEnabled, bool inAircraft, bool multiplayer)
    {
        var visible = _state.Update(phase, vrEnabled, inAircraft, multiplayer);
        if (_shown != visible)
        {
            _shown = visible;
            _container.gameObject.SetActive(visible);
            Debug.Log(visible ? "[NOVR] Startup loading splash shown; menu input suspended." : "[NOVR] Startup loading splash released; menu input restored.");
        }
        if (!visible)
        {
            ReleaseFallbackEvents();
            RestoreLoaderBlocker();
            return false;
        }
        SuppressLoaderBlocker();
        EnsureEventSystem();
        if (Keyboard.current?.f10Key.wasPressedThisFrame == true)
            _state.ToggleExit();
        UpdateText();
        // Keep the splash above panels whose visibility transitions were started before loading began.
        _container.SetAsLastSibling();
        return true;
    }

    private void ToggleOrConfirmExit()
    {
        if (_state.CanQuit)
        {
            _actions.QuitGame();
            return;
        }
        _state.ToggleExit();
        UpdateText();
    }
    private void CancelExit()
    {
        _state.CancelExit();
        UpdateText();
    }
    private void UpdateText()
    {
        var confirming = _state.ExitConfirmation;
        _title.text = confirming ? "Exit Nuclear Option?" : _state.Failed ? "Aircraft & mod loading stopped" : "Loading aircraft & mods";
        _body.text = confirming
            ? "Exit to Steam, then launch the game again when ready.\nThe current startup will be stopped."
            : _state.Failed
                ? "Additional content did not finish loading.\nExit, check BepInEx/LogOutput.log on the PC,\nthen launch Nuclear Option again."
                : "Preparing additional aircraft, missions and cockpit assets.\nThe main menu will open automatically when ready.\nNo input is needed.";
        _actionLabel.text = confirming ? "EXIT NOW" : "EXIT GAME";
        _back.gameObject.SetActive(confirming);
        _backLabel.text = _state.Failed ? "BACK" : "KEEP WAITING";
    }

    private void EnsureEventSystem()
    {
        // Some loading scenes have no EventSystem. Supply one only for this splash,
        // then yield to the game's next system so pointers never depend on a hidden menu.
        if (_fallbackEvents != null && Time.unscaledTime >= _nextEventScan)
        {
            _nextEventScan = Time.unscaledTime + .5f;
            foreach (var system in UnityEngine.Object.FindObjectsOfType<EventSystem>())
                if (system != _fallbackEvents && system.isActiveAndEnabled)
                {
                    ReleaseFallbackEvents();
                    EventSystem.current = system;
                    break;
                }
        }
        if (EventSystem.current != null && EventSystem.current.isActiveAndEnabled)
            return;
        if (_fallbackEvents != null)
        {
            EventSystem.current = _fallbackEvents;
            return;
        }
        var root = new GameObject("NOVR Startup Input");
        UnityEngine.Object.DontDestroyOnLoad(root);
        _fallbackEvents = root.AddComponent<EventSystem>();
        var module = root.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
        EventSystem.current = _fallbackEvents;
    }
    private void ReleaseFallbackEvents()
    {
        if (_fallbackEvents == null)
            return;
        _fallbackEvents.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(_fallbackEvents.gameObject);
        _fallbackEvents = null;
    }
    private void SuppressLoaderBlocker()
    {
        if (_loaderBlocker != null)
        {
            _loaderBlocker.enabled = false;
            return;
        }
        if (Time.unscaledTime < _nextBlockerScan)
            return;
        _nextBlockerScan = Time.unscaledTime + .25f;
        var loader = GameObject.Find("BlueprinterLoadingScreen");
        if (loader == null)
            return;
        // Only the verified loader's UI blocker is replaced; no arbitrary mod canvas is disabled.
        foreach (var component in loader.GetComponents<MonoBehaviour>())
            if (component != null && component.GetType().FullName == "Blueprinter.BlueprinterLoadingScreen")
            {
                _loaderBlocker = loader.GetComponent<GraphicRaycaster>();
                if (_loaderBlocker != null)
                {
                    _loaderBlockerEnabled = _loaderBlocker.enabled;
                    _loaderBlocker.enabled = false;
                }
                break;
            }
    }
    private void RestoreLoaderBlocker()
    {
        if (_loaderBlocker != null)
            _loaderBlocker.enabled = _loaderBlockerEnabled;
        _loaderBlocker = null;
    }
    internal void Dispose()
    {
        ReleaseFallbackEvents();
        RestoreLoaderBlocker();
    }

    private static RectTransform Rect(string name, RectTransform parent, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        LayerHelper.SetLayerRecursive(go.transform, LayerHelper.GetVrUiLayer());
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
    private static RectTransform Image(string name, RectTransform parent, Vector2 position, Vector2 size, Color color)
    {
        var rect = Rect(name, parent, position, size);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return rect;
    }
    private static Text Label(string name, RectTransform parent, string value, Vector2 position, Vector2 size, int font, Color color)
    {
        var rect = Rect(name, parent, position, size);
        var text = rect.gameObject.AddComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = font;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }
    private static Button Button(string label, RectTransform parent, Vector2 position, Vector2 size, Action action)
    {
        var color = new Color(.14f, .22f, .27f, 1);
        var rect = Image(label, parent, position, size, color);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        button.onClick.AddListener(() => action());
        NativeButtonFeedback.Configure(button, color);
        Label(label + " label", rect, label.ToUpperInvariant(), Vector2.zero, size, 24, Color.white);
        return button;
    }
}
