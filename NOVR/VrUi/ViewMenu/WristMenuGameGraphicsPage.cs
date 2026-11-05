using UnityEngine;

namespace NOVR.VrUi;

internal sealed partial class WristMenu
{
    private GameObject BuildGameGraphicsPage()
    {
        var page = Page("GAME GRAPHICS");
        var parent = page.transform;
        Section(parent, "Scene rendering", 214);
        var trees = Setting(parent, "Tree distance", 159, GameGraphicsSettings.TreeMinimum, GameGraphicsSettings.TreeMaximum,
            () => GameGraphicsSettings.TreeDistance, GameGraphicsSettings.SetTreeDistance, value => $"{value:0.0}×", .1f);
        Note(parent, "Lower: fewer distant trees; more visible pop-in.", 105, 32);
        Choice(parent, "Shadow quality", GameGraphicsSettings.ShadowOptions,
            () => GameGraphicsSettings.ShadowQuality, GameGraphicsSettings.SetShadowQuality, 47,
            _ => GameGraphicsSettings.IsAvailable);
        Note(parent, "Lower: cheaper shadows with less detail.", -12, 32);
        var clouds = Setting(parent, "Cloud detail", -75, 0, 1,
            () => GameGraphicsSettings.CloudDetail, GameGraphicsSettings.SetCloudDetail, Percent, .05f);
        Note(parent, "Lower: fewer cloud particles; simpler appearance.", -130, 32);
        var status = ViewMenuControls.Text(parent, _font!, "", new Vector2(0, -195), new Vector2(608, 64), 17);
        status.color = ViewMenuControls.Muted;
        _liveLabels.Add((status, () => GameGraphicsSettings.IsAvailable
            ? "Uses your saved game settings. Changes apply live.\nBenefit depends on tree, cloud and shadow load."
            : "Waiting for the game's graphics settings…"));
        _settingRefresh.Add((page, () =>
        {
            trees.Slider.interactable = GameGraphicsSettings.IsAvailable;
            clouds.Slider.interactable = GameGraphicsSettings.IsAvailable;
        }
        ));
        return page;
    }
}
