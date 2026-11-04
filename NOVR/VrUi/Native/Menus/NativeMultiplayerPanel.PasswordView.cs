using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi.Native;

public partial class NativeMultiplayerPanel
{
    private RectTransform? _passwordPanel;

    private Text? _passwordTitleText;

    private Text? _passwordStatusText;

    private InputField? _joinPasswordInput;

    private NativeLobbyEntry? _pendingPasswordLobby;

    private void BuildPasswordPanel()
    {
        if (_container == null)
            return;

        _passwordPanel = CreatePanel("Join Password Panel", _container, new Color(0.02f, 0.026f, 0.032f, 0.98f), new Vector2(0f, -20f), new Vector2(640f, 320f));
        _passwordTitleText = CreateText("Join Password Title", _passwordPanel, "PASSWORD REQUIRED", new Vector2(0f, 110f), new Vector2(560f, 34f), 20, TextAnchor.MiddleCenter, Color.white);
        _joinPasswordInput = CreateInputField("Join Password Input", _passwordPanel, new Vector2(0f, 40f), new Vector2(460f, 40f), "Server password", InputField.ContentType.Password);
        _passwordStatusText = CreateText("Join Password Status", _passwordPanel, "", new Vector2(0f, -28f), new Vector2(560f, 44f), 13, TextAnchor.MiddleCenter, new Color(0.84f, 0.90f, 0.92f, 1f));
        CreateMenuButton("CANCEL", _passwordPanel, new Vector2(-150f, -105f), new Vector2(150f, 38f), BackButtonColor, HidePasswordPanel, 14);
        CreateMenuButton("JOIN", _passwordPanel, new Vector2(150f, -105f), new Vector2(150f, 38f), JoinButtonColor, SubmitJoinPassword, 14);
        _passwordPanel.gameObject.SetActive(false);
    }

    private void ShowPasswordPanel(NativeLobbyEntry lobby)
    {
        _pendingPasswordLobby = lobby;
        if (_passwordTitleText != null)
        {
            _passwordTitleText.text = $"PASSWORD REQUIRED\n{Shorten(lobby.LobbyName, 42)}";
        }

        if (_passwordStatusText != null)
        {
            _passwordStatusText.text = "";
        }

        if (_joinPasswordInput != null)
        {
            _joinPasswordInput.text = "";
            _joinPasswordInput.ActivateInputField();
        }

        if (_passwordPanel != null)
        {
            _passwordPanel.gameObject.SetActive(true);
            _passwordPanel.SetAsLastSibling();
        }
    }

    private void HidePasswordPanel()
    {
        _pendingPasswordLobby = null;
        if (_passwordPanel != null)
        {
            _passwordPanel.gameObject.SetActive(false);
        }
    }
}
