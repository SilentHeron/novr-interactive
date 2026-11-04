using System.Reflection;
using NuclearOption.Networking.Lobbies;
using UnityEngine;

namespace NOVR.VrUi.Native;

public partial class NativeMultiplayerPanel : MonoBehaviour
{
    private const float SourceRefreshIntervalSeconds = 1f;

    private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly FieldInfo? LobbyItemShownField = typeof(LobbyListItem).GetField("shown", PrivateInstanceFlags);

    private NativeGameActionAdapter? _actions;

    private RectTransform? _container;

    private float _nextSourceRefreshTime;

    private bool _wasVisible;

    public void Initialize(NativeGameActionAdapter actions, RectTransform root)
    {
        _actions = actions;
        _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        BuildLayout(root);
    }

    public void SetVisible(bool visible)
    {
        if (_container == null)
            return;

        if (visible && !_wasVisible)
        {
            ResetLobbySelection();
            _page = 0;
            SetStatus("Refreshing multiplayer lobbies.");
            _actions?.TryRefreshMultiplayerLobbies();
            RefreshFromOriginalLobbyList();
            _nextSourceRefreshTime = Time.unscaledTime + SourceRefreshIntervalSeconds;
        }

        _wasVisible = visible;
        NativePanelTransition.SetVisible(_container, visible);
    }

    private void Update()
    {
        if (!_wasVisible || Time.unscaledTime < _nextSourceRefreshTime)
            return;

        _nextSourceRefreshTime = Time.unscaledTime + SourceRefreshIntervalSeconds;
        RefreshFromOriginalLobbyList();
    }

    private void BuildLayout(RectTransform root)
    {
        _container = CreateContainer("Native Multiplayer", root, root.sizeDelta);
        CreateImage("Background", _container, BackgroundColor, Vector2.zero, _container.sizeDelta);
        CreateText("Header", _container, "MULTIPLAYER", new Vector2(0f, NativeUiLayout.HeaderY), NativeUiLayout.HeaderSize, 22, TextAnchor.MiddleCenter, Color.white);

        BuildBrowserView(_container);

        CreateMenuButton("BACK", _container, new Vector2(NativeUiLayout.FooterLeftX, NativeUiLayout.FooterY), NativeUiLayout.FooterButtonSize, BackButtonColor, BackToMainMenu, 15);
        _joinButton = CreateMenuButton("JOIN", _container, new Vector2(NativeUiLayout.FooterRightX, NativeUiLayout.FooterY), NativeUiLayout.FooterButtonSize, JoinButtonColor, JoinSelectedLobby, 15);
        _joinButton.interactable = false;

        BuildHostSetupPanel();
        BuildPasswordPanel();
        NativePanelTransition.SetVisible(_container, false, instant: true);
    }

    private void RefreshFromOriginalLobbyList()
    {
        _lobbies.Clear();

        if (_actions?.TryGetActiveLobbyList(out var lobbyList) != true || lobbyList == null)
        {
            SetStatus("Waiting for the multiplayer lobby list.");
            _visibleLobbies.Clear();
            RefreshList();
            SelectLobby(-1);
            return;
        }

        var lobbyItems = lobbyList.GetComponentsInChildren<LobbyListItem>(true);
        for (var index = 0; index < lobbyItems.Length; index++)
        {
            var item = lobbyItems[index];
            if (item == null || !IsLobbyItemShown(item) || item.lobby == null)
                continue;

            _lobbies.Add(new NativeLobbyEntry(item));
        }

        ApplyFiltersAndSort();
    }

    private void RefreshClicked()
    {
        SetStatus("Refreshing lobby list.");
        _actions?.TryRefreshMultiplayerLobbies();
        _nextSourceRefreshTime = 0f;
    }

    private void BackToMainMenu()
    {
        _actions?.TryInvokeCurrentMenuButton("Main Menu", "MAIN MENU", "BACK", "< BACK", "MenuExit_Button");
    }

    private static bool IsLobbyItemShown(LobbyListItem item)
    {
        if (LobbyItemShownField?.GetValue(item) is bool shown)
        {
            return shown;
        }

        return item.gameObject.activeInHierarchy;
    }
}
