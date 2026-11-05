using NOVR.VrUi;

var checks = 0;
void Check(bool result, string message)
{
    if (!result)
        throw new InvalidOperationException(message);
    checks++;
}
NativeCalls.Reset();
var menu = new WristMenu();
Check(menu.Build().Name == "GAME GRAPHICS", "The new page has its own game-graphics destination.");
menu.Refresh();
Check(menu.Scalars.Keys.Order().SequenceEqual(new[] { "Cloud detail", "Tree distance" }) &&
      menu.Choices.Keys.SequenceEqual(new[] { "Shadow quality" }),
    "The page contains only three known native scene-rendering controls.");
Check(NativeCalls.Events.Count == 0 && GameGraphicsSettings.TreeDistance == 1.37f && GameGraphicsSettings.CloudDetail == .73f,
    "Building and refreshing preserves exact saved native values without applying or rounding them.");
Check(menu.Scalars["Tree distance"].Min == NuclearOption.Effects.DetailSettings.TreeRangeMultiplierMin &&
      menu.Scalars["Tree distance"].Max == NuclearOption.Effects.DetailSettings.TreeRangeMultiplierMax &&
      menu.Scalars["Cloud detail"].Min == 0 && menu.Scalars["Cloud detail"].Max == 1,
    "Sliders use the game's valid ranges.");
Check(menu.Choices["Shadow quality"].Options.SequenceEqual(GraphicsHelper.ShadowQualityOptions),
    "Shadow names and indices come directly from the native game options.");
Check(menu.Status.Contains("depends") && menu.Notes.Any(note => note.Contains("pop-in")),
    "The page describes scene-dependent benefit and visible tradeoffs.");

menu.Scalars["Tree distance"].Write(.8f);
Check(PlayerSettings.DetailSettings.TreeRangeMultiplier == .8f && NativeCalls.Events.Count == 2 && NativeCalls.Events[^1] == "save",
    "Tree callback uses the native setter and saves only after assignment.");
NativeCalls.Events.Clear();
menu.Scalars["Cloud detail"].Write(.4f);
Check(PlayerSettings.graphics.CloudDetail == .4f && NativeCalls.Events.Count == 2 && NativeCalls.Events[^1] == "save",
    "Cloud callback uses the native setter and saves only after assignment.");
NativeCalls.Events.Clear();
menu.Choices["Shadow quality"].Write(1);
Check(PlayerSettings.graphics.ShadowQuality == 1 && NativeCalls.Events.SequenceEqual(new[] { "shadow:1", "save" }),
    "Shadow callback delegates its apply/persistence behavior to the native property.");
Check(PlayerSettings.graphics.AntiAliasing == 2 && PlayerSettings.graphics.ShadowDistance == 4500 &&
      PlayerSettings.graphics.Vsync && PlayerSettings.DetailSettings.GrassEnabled,
    "These controls never touch unrelated game or runtime quality settings.");

NativeCalls.Events.Clear();
menu.Refresh();
menu.Scalars["Tree distance"].Write(.8f);
menu.Scalars["Cloud detail"].Write(.4f);
menu.Choices["Shadow quality"].Write(1);
Check(NativeCalls.Events.Count == 0, "Unchanged values and refreshes do not repeat native writes or saves.");
foreach (var invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    menu.Scalars["Tree distance"].Write(invalid);
    menu.Scalars["Cloud detail"].Write(invalid);
}
foreach (var invalid in new[] { int.MinValue, -1, GraphicsHelper.ShadowQualityOptions.Count, int.MaxValue })
    menu.Choices["Shadow quality"].Write(invalid);
Check(NativeCalls.Events.Count == 0, "Malformed scalar values and unknown enum indices cannot change native graphics.");
GameGraphicsSettings.SetTreeDistance(-5);
Check(PlayerSettings.DetailSettings.TreeRangeMultiplier == GameGraphicsSettings.TreeMinimum, "Tree distance clamps to native minimum.");
GameGraphicsSettings.SetTreeDistance(5);
Check(PlayerSettings.DetailSettings.TreeRangeMultiplier == GameGraphicsSettings.TreeMaximum, "Tree distance clamps to native maximum.");
GameGraphicsSettings.SetCloudDetail(-5);
Check(PlayerSettings.graphics.CloudDetail == 0, "Cloud detail preserves the native zero endpoint.");
GameGraphicsSettings.SetCloudDetail(5);
Check(PlayerSettings.graphics.CloudDetail == 1, "Cloud detail clamps to native maximum.");

foreach (var absent in new[] { "graphics", "details", "both" })
{
    NativeCalls.Reset();
    if (absent != "details")
        PlayerSettings.graphics = null!;
    if (absent != "graphics")
        PlayerSettings.DetailSettings = null!;
    menu.Refresh();
    Check(!GameGraphicsSettings.IsAvailable && menu.Scalars.Values.All(s => !s.Slider.interactable) &&
          menu.Choices["Shadow quality"].Options.Select((_, i) => i).All(i => !menu.Choices["Shadow quality"].CanSelect(i)),
        "Uninitialized settings disable every control: " + absent);
    Check(menu.Status.Contains("Waiting"), "Uninitialized settings show a clear waiting state.");
    menu.Scalars["Tree distance"].Write(1);
    menu.Scalars["Cloud detail"].Write(.5f);
    menu.Choices["Shadow quality"].Write(0);
    Check(NativeCalls.Events.Count == 0, "Late input cannot write missing native settings.");
}
NativeCalls.Reset();
menu.Refresh();
Check(GameGraphicsSettings.IsAvailable && menu.Scalars.Values.All(s => s.Slider.interactable) &&
      menu.Scalars["Tree distance"].Read() == 1.37f && menu.Choices["Shadow quality"].Read() == 3 && NativeCalls.Events.Count == 0,
    "Settings initialized or replaced after page creation are read live without overwriting them.");
FoveationMenuChecks.Run(Check);
Console.WriteLine($"PASS {checks} source-linked graphics/foveation menu checks (settings, page placement and uGUI boundaries).");
