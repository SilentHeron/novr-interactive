using System;
using System.Collections.Generic;
using NOVR.MotionControls;
using NOVR.Rendering;
using NOVR.VrUi.Native;
using NOVR.VrUi.SpecialBehavior;
using UnityEngine;
using UnityEngine.UI;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private GameObject BuildHomePage()
    {
        _homePage = Page("VIEW");
        var parent = _homePage.transform;
        ViewMenuControls.Panel(_homePage.transform, "Flight card", new Vector2(-160, 129), new Vector2(300, 202), ViewMenuControls.Surface);
        ViewMenuControls.Panel(_homePage.transform, "Cockpit card", new Vector2(-160, -60), new Vector2(300, 202), ViewMenuControls.Surface);
        ViewMenuControls.Panel(_homePage.transform, "Visual card", new Vector2(160, 105), new Vector2(300, 250), ViewMenuControls.Surface);
        ViewMenuControls.Panel(_homePage.transform, "System card", new Vector2(160, -111), new Vector2(300, 202), ViewMenuControls.Surface);
        Section(parent, "FLIGHT", 215, 300, -160);
        Section(parent, "COCKPIT", 26, 300, -160);
        Section(parent, "VISUALS", 215, 300, 160);
        Section(parent, "FEEL & SYSTEM", -23, 300, 160);
        WristMenuNavigation.BuildHome((item, action) =>
        {
            var label = AddButton(parent, item.Label, item.Column, item.Row, action);
            if (item.Destination == ViewMenuDestination.FlightActions || item.Destination == ViewMenuDestination.Seat ||
                item.Destination == ViewMenuDestination.Profiles || item.Destination == ViewMenuDestination.EditLayout)
                _cockpitButtons.Add(label.transform.parent.GetComponent<Button>());
            var bounds = WristMenuNavigation.HomeBounds(item);
            var rect = (RectTransform)label.transform.parent;
            rect.anchoredPosition = new Vector2(bounds.X, bounds.Y);
            rect.sizeDelta = new Vector2(bounds.Width, bounds.Height);
            var labelRect = (RectTransform)label.transform;
            labelRect.anchoredPosition = new Vector2(-10, 0);
            labelRect.sizeDelta = new Vector2(254, 40);
            label.alignment = TextAnchor.MiddleLeft;
            NativeButtonFeedback.Configure(rect.GetComponent<Button>(), item.OpensPage ? ViewMenuControls.Surface : new Color(.14f, .22f, .25f));
            if (item.OpensPage)
                ViewMenuControls.Text(rect, _font!, "›", new Vector2(132, 0), new Vector2(24, 34), 26, TextAnchor.MiddleCenter);
        },
            new Dictionary<ViewMenuDestination, Action>
            {
                [ViewMenuDestination.FlightActions] = () => ShowPage(_flightPage!),
                [ViewMenuDestination.FlightControls] = () => ShowPage(_controlsPage!),
                [ViewMenuDestination.Bindings] = () => ShowPage(_bindingsPage!),
                [ViewMenuDestination.EditLayout] = StartOrResumeLayout,
                [ViewMenuDestination.Seat] = () => ShowPage(_seatPage!),
                [ViewMenuDestination.Profiles] = OpenAircraftProfiles,
                [ViewMenuDestination.Recenter] = () => { NOVRHeadsetData.CalibrateTranslation(); NOVRHeadsetData.CalibrateRotation(); Close(); },
                [ViewMenuDestination.LookCue] = () => ShowPage(_lookCuePage!),
                [ViewMenuDestination.Hud] = () => ShowPage(_displayPage!),
                [ViewMenuDestination.Widgets] = OpenWidgetAppearance,
                [ViewMenuDestination.Hands] = () => ShowPage(_handsPage!),
                [ViewMenuDestination.Feedback] = () => ShowPage(_feedbackPage!),
                [ViewMenuDestination.Performance] = () => ShowPage(_performancePage!),
                [ViewMenuDestination.MenuOptions] = () => ShowPage(_menuOptionsPage!)
            });
        ViewMenuControls.Text(_homePage.transform, _font!, "SESSION ACTION", new Vector2(-160, -166), new Vector2(300, 24), 16);
        return _homePage;
    }

    private void BuildGuidePage()
    {
        _helpPage = Page("GUIDE");
        var parent = _helpPage.transform;
        _bindingHelp = Label(_helpPage.transform, "", new Vector2(-132, 12), new Vector2(248, 390), 18);
        _throttleHelp = Label(_helpPage.transform, "", new Vector2(132, 12), new Vector2(248, 390), 18);
        _helpFooter = Label(_helpPage.transform, "", new Vector2(0, -217), new Vector2(510, 77), 16);
    }

    private void BuildMenuOptionsPage()
    {
        _menuOptionsPage = Page("MENU OPTIONS");
        var parent = _menuOptionsPage.transform;
        var motion = ModConfiguration.Instance.MotionControls;
        Section(parent, "Menu placement", 214);
        Setting(parent, "Follow View hand", motion.ViewMenuFollowsHand, 0, 167);
        ActionButton(parent, "Bring menu forward", 1, 167, () => _anchored = false);
        Note(parent, "Off: keep this menu steady in front of your seat.\nOn: attach it to the controller assigned to View.", 99, 64);
        Section(parent, "Controller input", 31);
        ActionButton(parent, "Controller guide  ›", 0, -16, () => ShowPage(_helpPage!));
        Note(parent, "Either controller can point and click.\nPress a button on the other controller to switch.\nView closes this menu.", -100, 100);
    }

    private void BuildPerformancePage()
    {
        _performancePage = Page("VR PERFORMANCE");
        var parent = _performancePage.transform;
        var performanceInfo = Label(parent, "", new Vector2(0, 28), new Vector2(600, 350), 18);
        _liveLabels.Add((performanceInfo, () => PcVrCapabilities.Report));
        var foveationPage = BuildFixedFoveationPage();
        var graphicsPage = BuildGameGraphicsPage();
        Setting(parent, "Hide distant cloud banks", () => GameplayPatches.DistantCloudBanks.HideInVr,
            GameplayPatches.DistantCloudBanks.SetHideInVr, 0, -166);
        Setting(parent, "Share shadows (test)", () => Rendering.SharedEyeShadows.Enabled,
            Rendering.SharedEyeShadows.SetEnabled, 1, -166);
        ActionButton(parent, "Foveated rendering  ›", 0, -211, () => ShowPage(foveationPage));
        ActionButton(parent, "Game graphics  ›", 1, -211, () => ShowPage(graphicsPage));
    }

    private void BuildFeedbackPage()
    {
        _feedbackPage = Page("SOUND & HAPTICS");
        var parent = _feedbackPage.transform;
        var motion = ModConfiguration.Instance.MotionControls;
        Section(parent, "Interaction feedback", 214);
        Setting(parent, "Sound volume", motion.InteractionVolume, 159, Percent, .01f);
        Setting(parent, "Haptic strength", motion.HapticStrength, 89, Percent, .01f);
        Section(parent, "Try a sample", 17);
        AddFeedbackPreview(parent, "Touch", 0, 3, InteractionCue.Touch);
        AddFeedbackPreview(parent, "Grab", 1, 3, InteractionCue.Grab);
        AddFeedbackPreview(parent, "Switch", 0, 4, InteractionCue.Switch);
        AddFeedbackPreview(parent, "MIL detent", 1, 4, InteractionCue.Detent);
        Note(parent, "Point and press the trigger for a sample in that hand.\nSamples never operate aircraft controls.\nZero disables sound or haptics independently.", -159, 100);
    }
}
