using System;
using System.Collections.Generic;
using System.Linq;
using NOVR.MotionControls;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal sealed partial class ControllerBindingPanel
{
    private readonly GameObject _root;
    private readonly Font _font;
    private readonly Action _close;
    private readonly List<GameObject> _pages = new(), _history = new();
    private readonly List<(Text label, Func<string> text)> _liveLabels = new();
    private Transform _buttonParent = null!;
    private GameObject _bindingsPage = null!, _diagramPage = null!, _diagramButtonPage = null!;
    private ControllerDiagramView _diagram = null!;
    private Text _diagramInfo = null!, _diagramPageNumber = null!;
    private Button _diagramPrevious = null!, _diagramNext = null!;
    private readonly List<(Button button, Text label)> _diagramActions = new();
    private bool _diagramLeft;
    private ControllerButton? _diagramButton;
    private ControllerBindingProfile _diagramProfile, _diagramHardware;
    private int _diagramActionPage;
    private const int ActionsPerPage = 6;

    internal ControllerBindingPanel(Transform parent, Font font, Action close)
    {
        _font = font; _close = close;
        _root = new GameObject("Visual controller bindings", typeof(RectTransform));
        _root.transform.SetParent(parent, false);
        ((RectTransform)_root.transform).sizeDelta = new Vector2(540, 500);
        CreateBindingPages();
        Footer("Back", -176, Back); Footer("Bindings", 0, Home); Footer("Done", 176, _close);
        Home(); OpenDiagram();
        LayerHelper.SetLayerRecursive(_root.transform, LayerHelper.GetVrUiLayer());
    }
    internal void Refresh()
    {
        if (_root == null || !_root.activeInHierarchy) { _diagram?.ClearActivity(); return; }
        foreach (var item in _liveLabels)
            if (item.label != null && item.label.gameObject.activeInHierarchy) item.label.text = item.text();
        RefreshBindingEditor();
    }
    private GameObject Page(string title)
    {
        var page = new GameObject(title, typeof(RectTransform)); page.transform.SetParent(_root.transform, false);
        page.SetActive(false); _pages.Add(page); _buttonParent = page.transform; return page;
    }
    private void ShowPage(GameObject page)
    {
        if (_history.Count == 0 || _history[_history.Count - 1] != page) _history.Add(page);
        foreach (var item in _pages) item.SetActive(item == page);
        Refresh();
    }
    private void Back()
    {
        if (_history.Count <= 1) { _close(); return; }
        _history.RemoveAt(_history.Count - 1);
        var page = _history[_history.Count - 1];
        foreach (var item in _pages) item.SetActive(item == page);
        Refresh();
    }
    private void Home() { _history.Clear(); ShowPage(_bindingsPage); }
    private void OpenDiagram() => ShowPage(_diagramPage);
    private void AddLiveButton(Func<string> text, int column, int row, Action action) =>
        _liveLabels.Add((AddButton(text(), column, row, action), text));
    private Text AddButton(string text, int column, int row, Action action, int columns = 2)
    {
        var width = columns == 3 ? 160f : 248f;
        return Button(_buttonParent, text, new Vector2((column - (columns - 1) * .5f) * (width + 16), 177 - row * 57),
            new Vector2(width, 49), action, 19);
    }
    private void Footer(string text, float x, Action action) => Button(_root.transform, text, new Vector2(x, -229), new Vector2(160, 44), action, 18);
    private Text Button(Transform parent, string text, Vector2 position, Vector2 size, Action action, int fontSize)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
        var button = go.GetComponent<Button>(); button.targetGraphic = go.GetComponent<Image>();
        button.navigation = new Navigation { mode = Navigation.Mode.None }; button.onClick.AddListener(() => action());
        NativeButtonFeedback.Configure(button, new Color(.1f, .24f, .29f));
        return Label(rect, text, Vector2.zero, new Vector2(size.x - 10, size.y - 4), fontSize);
    }
    private Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        var rect = (RectTransform)go.transform; rect.SetParent(parent, false); rect.anchoredPosition = position; rect.sizeDelta = size;
        var label = go.GetComponent<Text>(); label.font = _font; label.fontSize = fontSize; label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white; label.raycastTarget = false; label.text = text; return label;
    }
    private void CreateDiagramPages()
    {
        _diagramPage = Page("CONTROLLER DIAGRAM");
        _diagram = new ControllerDiagramView(_diagramPage.transform, _font, SelectDiagramButton);
        Label(_diagramPage.transform, "Choose Left / Right, then a callout to edit.\nTouch: amber · Press: green · Free click: wheel",
            new Vector2(0, -184), new Vector2(510, 42), 17);
        _diagramButtonPage = Page("CONTROLLER BUTTON");
        _diagramInfo = Label(_diagramButtonPage.transform, "", new Vector2(0, 149), new Vector2(510, 102), 18);
        for (var i = 0; i < ActionsPerPage; i++)
        {
            var index = i;
            var label = AddButton("", i % 2, 2 + i / 2, () => EditDiagramAction(index)); label.fontSize = 17;
            _diagramActions.Add((label.transform.parent.GetComponent<Button>(), label));
        }
        _diagramPrevious = AddButton("Previous actions", 0, 5, () => { _diagramActionPage--; RefreshDiagram(); }).transform.parent.GetComponent<Button>();
        _diagramNext = AddButton("More actions", 1, 5, () => { _diagramActionPage++; RefreshDiagram(); }).transform.parent.GetComponent<Button>();
        _diagramPageNumber = Label(_diagramButtonPage.transform, "", new Vector2(0, -165), new Vector2(510, 69), 17);
    }
    private void SelectDiagramButton(bool left, ControllerButton? button)
    {
        _diagramLeft = left; _diagramButton = button; _diagramActionPage = 0;
        _diagramProfile = Bindings.EffectiveProfile; _diagramHardware = Bindings.ButtonProfile(left);
        ShowPage(_diagramButtonPage);
    }
    private void RefreshDiagram()
    {
        if (_diagramPage == null) return;
        if (_diagramPage.activeSelf) _diagram.Refresh(Bindings, StickLeft);
        else _diagram.ClearActivity();
        if (!_diagramButtonPage.activeSelf) return;
        if (_diagramProfile != Bindings.EffectiveProfile || _diagramHardware != Bindings.ButtonProfile(_diagramLeft)) { Back(); return; }
        var fixedGrip = _diagramButton == null;
        var actions = ControllerBindingDefaults.Actions;
        _diagramActionPage = Mathf.Clamp(_diagramActionPage, 0, (actions.Length - 1) / ActionsPerPage);
        var selected = _diagramButton.GetValueOrDefault();
        var assigned = fixedGrip ? Array.Empty<ControllerActionId>() : ControllerDiagramModel.Assigned(_diagramLeft, selected,
            _diagramHardware, StickLeft, Bindings.Get);
        _diagramInfo.text = (_diagramLeft ? "LEFT · " : "RIGHT · ") + (fixedGrip ? "GRIP" : ControllerButtonNames.Describe(selected, _diagramLeft, _diagramHardware == ControllerBindingProfile.Frame)) + "\n" +
            (fixedGrip ? "Holds the nearby stick or throttle.\nSet HOLD / TOGGLE in Flight controls.\nGrip and thumbstick axes cannot be rebound here." :
             assigned.Length == 0 ? "Unassigned. Choose an action below." : string.Join("\n", assigned.Take(3).Select(a => ControllerBindings.ActionLabel(a) +
                (a == ControllerActionId.Map ? " · free" : a == ControllerActionId.WristMenu ? " · any grip" : " · gripped"))) + (assigned.Length > 3 ? "\nMore assignments below." : ""));
        for (var i = 0; i < _diagramActions.Count; i++)
        {
            var index = _diagramActionPage * ActionsPerPage + i; var item = _diagramActions[i];
            var visible = !fixedGrip && index < actions.Length; item.button.gameObject.SetActive(visible);
            if (!visible) continue;
            var action = actions[index];
            item.label.text = (assigned.Contains(action) ? "Edit · " : "Assign · ") + ControllerBindings.ActionLabel(action);
            item.button.interactable = ControllerBindings.IsBindingAvailable(_diagramHardware, _diagramLeft, action, selected);
        }
        _diagramPrevious.gameObject.SetActive(!fixedGrip); _diagramNext.gameObject.SetActive(!fixedGrip);
        _diagramPrevious.interactable = _diagramActionPage > 0; _diagramNext.interactable = (_diagramActionPage + 1) * ActionsPerPage < actions.Length;
        _diagramPageNumber.text = fixedGrip ? "Thumbstick axes retain their flight / nozzle functions.\nFree click opens the native option wheel.\nThese fixed interactions are not binding assignments." :
            $"Actions {_diagramActionPage + 1} / {(actions.Length + ActionsPerPage - 1) / ActionsPerPage}\n" +
            (selected == ControllerButton.StickClick ? "Held click is editable; free click stays the wheel." : "Choose an action to review before saving.") + "\nExisting sharing and undo rules still apply.";
    }
    private void EditDiagramAction(int index)
    {
        if (_diagramButton == null || _diagramProfile != Bindings.EffectiveProfile || _diagramHardware != Bindings.ButtonProfile(_diagramLeft)) return;
        index += _diagramActionPage * ActionsPerPage;
        if (index < 0 || index >= ControllerBindingDefaults.Actions.Length) return;
        var action = ControllerBindingDefaults.Actions[index];
        if (!ControllerBindings.IsBindingAvailable(_diagramHardware, _diagramLeft, action, _diagramButton.Value)) return;
        _editingAction = action; _editingProfile = Bindings.EffectiveProfile;
        _pendingBinding = ControllerDiagramModel.Proposed(Bindings.Get(action), _diagramLeft, _diagramButton.Value, StickLeft);
        ShowPage(_bindingEditPage!); RefreshBindingEditor();
    }
}
