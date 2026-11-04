using NOVR.VrUi.Native;
using UnityEngine;

internal static class PanelVisibilityChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool condition, string message)
        { if (!condition) throw new Exception(message); checks++; }
        var root = new NativeVrUiRoot();
        for (var from = 0; from <= 6; from++)
        for (var to = 0; to <= 6; to++)
        {
            root.ShowForCheck(from);
            root.ShowForCheck(to);
            Check(root.Panels.Select((panel, index) => panel.Visible == (to == index + 1)).All(value => value),
                $"Panel transition {from}->{to} leaves exactly the requested panel visible");
        }
        root.Root.SetActive(false);
        root.ActivateForCheck(2);
        Check(root.Root.activeSelf && root.Suppressed && root.Panels[1].Visible,
            "Immediate mission navigation activates the canvas and suppresses the stock menu");
        var calls = root.Panels.Sum(panel => panel.Updates);
        root.Root.SetActive(false);
        root.Root.SetActive(true);
        Check(root.Panels[1].Visible && root.Panels.Sum(panel => panel.Updates) == calls,
            "Stock-dialog root suspension does not reset the retained panel state");
        root.ShowForCheck(0);
        Check(root.Panels.All(panel => !panel.Visible), "Startup splash can hide every panel without selecting a destination");
        var empty = new NativeVrUiRoot(false);
        empty.ShowForCheck(1); empty.ActivateForCheck(6);
        Check(empty.Suppressed, "Panel changes tolerate scene initialization without panel instances");
        return checks;
    }
}

namespace NOVR.VrUi.Native
{
    // Panel transitions are source-linked. Only Unity construction, native panel
    // implementations and stock-canvas suppression are replaced at the boundary.
    public partial class NativeVrUiRoot
    {
        internal sealed class PanelProbe
        {
            internal bool Visible;
            internal int Updates;
            public void SetVisible(bool visible) { Visible = visible; Updates++; }
        }
        private readonly PanelProbe? _mainMenuShell, _singlePlayerMissionPanel, _multiplayerPanel,
            _settingsPanel, _workshopPanel, _vrUiSettingsPanel;
        private readonly GameObject? _root;
        internal readonly PanelProbe[] Panels;
        internal GameObject Root => _root!;
        internal bool Suppressed;
        internal NativeVrUiRoot(bool create = true)
        {
            Panels = Enumerable.Range(0, 6).Select(_ => new PanelProbe()).ToArray();
            if (!create) return;
            _root = new GameObject();
            _mainMenuShell = Panels[0]; _singlePlayerMissionPanel = Panels[1]; _multiplayerPanel = Panels[2];
            _settingsPanel = Panels[3]; _workshopPanel = Panels[4]; _vrUiSettingsPanel = Panels[5];
        }
        private void SuppressOriginalMainCanvas(bool value) => Suppressed = value;
        internal void ShowForCheck(int panel) => ShowPanel((MenuPanel)panel);
        internal void ActivateForCheck(int panel) => ActivatePanel((MenuPanel)panel);
    }
}
