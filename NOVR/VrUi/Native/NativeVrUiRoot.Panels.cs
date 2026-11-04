namespace NOVR.VrUi.Native;

public partial class NativeVrUiRoot
{
    private enum MenuPanel
    {
        None,
        Main,
        SinglePlayer,
        Multiplayer,
        Settings,
        Workshop,
        VrUiSettings
    }

    // Requested-menu timing and native availability stay in Update. All actual
    // panel visibility changes go through this one place, including immediate
    // navigation and the startup splash. Stock dialogs suspend the root instead
    // so a temporary native handoff retains each panel's edited state.
    private void ShowPanel(MenuPanel panel)
    {
        _mainMenuShell?.SetVisible(panel == MenuPanel.Main);
        _multiplayerPanel?.SetVisible(panel == MenuPanel.Multiplayer);
        _singlePlayerMissionPanel?.SetVisible(panel == MenuPanel.SinglePlayer);
        _settingsPanel?.SetVisible(panel == MenuPanel.Settings);
        _workshopPanel?.SetVisible(panel == MenuPanel.Workshop);
        _vrUiSettingsPanel?.SetVisible(panel == MenuPanel.VrUiSettings);
    }

    private void ActivatePanel(MenuPanel panel)
    {
        if (_root != null && !_root.activeSelf)
            _root.SetActive(true);
        ShowPanel(panel);
        SuppressOriginalMainCanvas(true);
    }
}
