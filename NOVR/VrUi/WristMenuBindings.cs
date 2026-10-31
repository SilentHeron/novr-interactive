using System;
using System.Collections.Generic;
using System.Linq;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private GameObject? _bindingEditPage, _bindingConflictPage, _bindingResetPage;
    private readonly List<(ControllerButton value, Button button)> _bindingChoices = new();
    private ControllerActionId _editingAction;
    private ControllerActionBinding _pendingBinding;
    private ControllerBindingProfile _editingProfile;
    private Text? _bindingSummary, _bindingConflictText, _bindingProfileInfo;
    private Button? _bindingSave, _bindingUndo;
    private int _bindingVersion = -1;
    private static readonly ControllerActionId[] BindingActions = (ControllerActionId[])Enum.GetValues(typeof(ControllerActionId));
    private static ControllerBindings Bindings => ModConfiguration.Instance.ControllerBindings;
    private static bool StickLeft => ModConfiguration.Instance.MotionControls.LeftHanded.Value;

    private void CreateBindingPages()
    {
        _bindingsPage = Page("CONTROLLER BINDINGS");
        GameObject? weapons = null, flight = null, system = null;
        AddLiveButton(() => "Preset: " + Bindings.Profile.Value, 0, 0, () =>
        {
            Bindings.Profile.Value = (ControllerBindingProfile)(((int)Bindings.Profile.Value + 1) % 3);
            _nextHelpRefresh = 0;
        });
        AddButton("Controller guide", 1, 0, () => ShowPage(_helpPage!));
        AddButton("Weapons & targets", 0, 1, () => ShowPage(weapons!));
        AddButton("Flight & defense", 1, 1, () => ShowPage(flight!));
        AddButton("Menu, map & zoom", 0, 2, () => ShowPage(system!));
        AddButton("Restore defaults…", 1, 2, () => { _editingProfile = Bindings.EffectiveProfile; ShowPage(_bindingResetPage!); });
        _bindingUndo = AddButton("Undo last change", 0, 3, () =>
        {
            Bindings.UndoEdit(); _nextHelpRefresh = 0; RefreshBindingEditor();
        }).transform.parent.GetComponent<Button>();
        Label(_bindingsPage.transform, "Restores replaced\nassignments too", new Vector2(132, 6), new Vector2(238, 49), 18);
        _bindingProfileInfo = Label(_bindingsPage.transform, "", new Vector2(0, -136), new Vector2(510, 190), 18);
        weapons = BindingGroup("WEAPONS & TARGETS", new[] { ControllerActionId.Guns, ControllerActionId.WeaponRelease,
            ControllerActionId.SelectTarget, ControllerActionId.UnlockTarget, ControllerActionId.NextWeapon, ControllerActionId.ClearTargets });
        flight = BindingGroup("FLIGHT & DEFENSE", new[] { ControllerActionId.Countermeasures, ControllerActionId.NextCountermeasure,
            ControllerActionId.Brake, ControllerActionId.Gear, ControllerActionId.Radar, ControllerActionId.FlightAssist,
            ControllerActionId.HoverAssist, ControllerActionId.Eots });
        system = BindingGroup("MENU, MAP & ZOOM", new[] { ControllerActionId.WristMenu, ControllerActionId.Map, ControllerActionId.Zoom });
        AddLiveButton(() => "Zoom: " + (Bindings.ZoomEnabled.Value ? "ON" : "OFF"), 1, 1,
            () => Bindings.ZoomEnabled.Value = !Bindings.ZoomEnabled.Value);

        _bindingEditPage = Page("EDIT BINDING");
        AddLiveButton(() => "Hand: " + RoleName(_pendingBinding.Hand), 0, 0, () =>
        {
            var hand = (ControllerHandRole)(((int)_pendingBinding.Hand + 1) % 4);
            _pendingBinding = new ControllerActionBinding(_pendingBinding.Button, hand);
            RefreshBindingEditor();
        });
        AddButton("Leave unassigned", 1, 0, () => { _pendingBinding = new ControllerActionBinding(ControllerButton.None, _pendingBinding.Hand); RefreshBindingEditor(); });
        var choices = new[] { ControllerButton.Trigger, ControllerButton.Primary, ControllerButton.Secondary,
            ControllerButton.StickClick, ControllerButton.Shoulder, ControllerButton.Menu, ControllerButton.Top, ControllerButton.Outside };
        for (var i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            var text = AddButton("", i % 3, 1 + i / 3, () =>
            {
                _pendingBinding = new ControllerActionBinding(choice, _pendingBinding.Hand);
                RefreshBindingEditor();
            }, 3);
            text.fontSize = 17;
            _bindingChoices.Add((choice, text.transform.parent.GetComponent<Button>()));
            _liveLabels.Add((text, () =>
            {
                var left = ControllerBindings.ResolveLeft(_pendingBinding.Hand, StickLeft);
                var frame = Bindings.ButtonProfile(left) == ControllerBindingProfile.Frame;
                var selected = ControllerButtonNames.Resolve(_pendingBinding.Button, frame) == choice;
                return (selected ? "✓ " : "") + ControllerButtonNames.Describe(choice, left, frame);
            }));
        }
        _bindingSummary = Label(_bindingEditPage.transform, "", new Vector2(0, -84), new Vector2(510, 112), 18);
        _bindingSave = AddButton("Save binding", 0, 6, SaveBinding).transform.parent.GetComponent<Button>();
        AddButton("Cancel", 1, 6, Back);

        _bindingConflictPage = Page("SHARED BUTTON");
        _bindingConflictText = Label(_bindingConflictPage.transform, "", new Vector2(0, 45), new Vector2(510, 280), 21);
        AddButton("Choose another", 0, 5, Back);
        AddButton("Replace assignments", 1, 5, () => ApplyBinding(true));
        _bindingResetPage = Page("RESTORE BINDINGS");
        var resetText = Label(_bindingResetPage.transform, "", new Vector2(0, 45), new Vector2(510, 230), 22);
        _liveLabels.Add((resetText, () => $"Restore the {ProfileName(_editingProfile)} default bindings?\n\nOnly this controller preset will change.\nUndo last change can restore your bindings\nduring this game session."));
        AddButton("Keep my bindings", 0, 5, Back);
        AddButton("Restore defaults", 1, 5, () => { if (Bindings.EffectiveProfile == _editingProfile) Bindings.ResetProfile(); Back(); });
    }

    private GameObject BindingGroup(string title, ControllerActionId[] actions)
    {
        var page = Page(title);
        for (var i = 0; i < actions.Length; i++)
        {
            var action = actions[i];
            var text = AddButton("", i % 2, i / 2, () =>
            {
                _editingAction = action; _editingProfile = Bindings.EffectiveProfile;
                _pendingBinding = Bindings.Get(action); ShowPage(_bindingEditPage!); RefreshBindingEditor();
            });
            text.fontSize = 17;
            _liveLabels.Add((text, () => ControllerBindings.ActionLabel(action) + "\n" + DescribeBinding(Bindings.Get(action))));
        }
        Label(page.transform, "Choose an action, then its hand and button.\nFlight actions need that hand's handle held.\nMap works with a free hand; View and zoom work\nwith the handles held or released. Changes save here.",
            new Vector2(0, -156), new Vector2(510, 130), 18);
        return page;
    }

    private static string ProfileName(ControllerBindingProfile profile) => profile == ControllerBindingProfile.Frame ? "Steam Frame" : "Touch / Quest";
    private static string RoleName(ControllerHandRole role) => role switch
    {
        ControllerHandRole.Stick => "Stick", ControllerHandRole.Throttle => "Throttle",
        ControllerHandRole.Left => "Left", _ => "Right"
    };
    private static string DescribeBinding(ControllerActionBinding binding)
    {
        var left = ControllerBindings.ResolveLeft(binding.Hand, StickLeft);
        var text = ControllerButtonNames.Describe(binding.Button, left, Bindings.ButtonProfile(left) == ControllerBindingProfile.Frame);
        return binding.Button == ControllerButton.None ? "Unassigned" : (left ? "L · " : "R · ") + text;
    }
    private void RefreshBindingEditor()
    {
        if (_bindingUndo != null) _bindingUndo.interactable = Bindings.CanUndoEdit;
        if (_bindingProfileInfo != null && _bindingsPage?.activeSelf == true)
            _bindingProfileInfo.text = "Active: " + ProfileName(Bindings.EffectiveProfile) +
                "\nAuto follows the connected controllers.\nEach preset keeps its saved bindings.\n\n" +
                (Bindings.CanUndoEdit ? "Undo available: " + Bindings.UndoEditLabel : "No recent change to undo for this preset.") +
                "\nUndo lasts for this game session.\nF9 opens View as a keyboard fallback.";
        if (_bindingEditPage?.activeSelf != true && _bindingConflictPage?.activeSelf != true) return;
        if (Bindings.EffectiveProfile != _editingProfile) { Back(); return; }
        var left = ControllerBindings.ResolveLeft(_pendingBinding.Hand, StickLeft);
        var allowed = ControllerBindings.IsBindingAvailable(Bindings.ButtonProfile(left), left, _editingAction, _pendingBinding.Button);
        foreach (var item in _bindingChoices)
            item.button.interactable = ControllerBindings.IsBindingAvailable(Bindings.ButtonProfile(left), left, _editingAction, item.value);
        var conflicts = Bindings.GetConflicts(_editingAction, _pendingBinding, StickLeft);
        if (_bindingSave != null) _bindingSave.interactable = allowed;
        if (_bindingSummary != null)
            _bindingSummary.text = ControllerBindings.ActionLabel(_editingAction) + " · " + DescribeBinding(_pendingBinding) + "\n" +
                ControllerBindings.ContextLabel(_editingAction) + "\n" +
                (!allowed ? "This button is unavailable or reserved for menu clicks." : conflicts.Length > 0 ? "Shared with " + string.Join(", ", conflicts.Select(ControllerBindings.ActionLabel)) : _editingAction == ControllerActionId.WristMenu && _pendingBinding.Button == ControllerButton.None ? "Unassigned: use F9 to reopen View." : "Ready to save. Cancel keeps the current binding.");
        if (_bindingConflictText != null)
            _bindingConflictText.text = DescribeBinding(_pendingBinding) + " is assigned to:\n\n" +
                string.Join(", ", conflicts.Select(ControllerBindings.ActionLabel)) +
                "\n\nReplace leaves those actions unassigned\nand uses this button for " + ControllerBindings.ActionLabel(_editingAction) +
                ".\nUndo last change restores them together.";
    }
    private void SaveBinding()
    {
        if (Bindings.EffectiveProfile != _editingProfile) return;
        if (Bindings.GetConflicts(_editingAction, _pendingBinding, StickLeft).Length > 0)
        { ShowPage(_bindingConflictPage!); RefreshBindingEditor(); }
        else ApplyBinding(false);
    }
    private void ApplyBinding(bool replace)
    {
        if (Bindings.EffectiveProfile != _editingProfile || !ControllerBindings.IsBindingAvailable(Bindings.ButtonProfile(ControllerBindings.ResolveLeft(_pendingBinding.Hand, StickLeft)),
                ControllerBindings.ResolveLeft(_pendingBinding.Hand, StickLeft), _editingAction, _pendingBinding.Button)) return;
        if (!Bindings.TryApplyEdit(_editingAction, _pendingBinding, replace)) return;
        if (_bindingConflictPage?.activeSelf == true) Back();
        Back();
    }

    private string HandHelp(bool stick)
    {
        var left = stick ? StickLeft : !StickLeft;
        var text = (stick ? "STICK" : CollectiveControls ? "COLLECTIVE" : "THROTTLE") + (left ? " · LEFT\n\n" : " · RIGHT\n\n");
        var count = 0;
        foreach (var action in BindingActions)
        {
            var binding = Bindings.Get(action);
            if (binding.Button == ControllerButton.None || action == ControllerActionId.Map || action == ControllerActionId.WristMenu || action == ControllerActionId.Zoom ||
                ControllerBindings.ResolveLeft(binding.Hand, StickLeft) != left) continue;
            if (++count > 7) { text += "More: Controller bindings"; break; }
            text += ControllerBindings.ActionLabel(action) + ": " +
                ControllerButtonNames.Describe(binding.Button, left, Bindings.ButtonProfile(left) == ControllerBindingProfile.Frame) + "\n\n";
        }
        return text.TrimEnd();
    }
}
