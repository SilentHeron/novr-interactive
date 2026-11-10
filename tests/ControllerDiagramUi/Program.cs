using BepInEx.Configuration;
using NOVR.MotionControls;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.UI;

var checks = 0;
var directory = Path.Combine(Path.GetTempPath(), "novr-diagram-ui-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var config = new ConfigFile(Path.Combine(directory, "bindings.cfg"), false) { SaveOnConfigSet = false };
    var bindings = new ControllerBindings(config);
    bindings.Profile.Value = ControllerBindingProfile.Frame;
    ModConfiguration.Instance.ControllerBindings = bindings;
    var host = new GameObject("Native settings host");
    var closes = 0;
    var panel = new ControllerBindingPanel(host.transform, new Font(), () => closes++);
    panel.Refresh();
    Check(Page("CONTROLLER DIAGRAM").activeInHierarchy, "Shared panel opens directly on a diagram without a cockpit/camera");
    Check(!Control("Left controller", "Grip").activeInHierarchy && Control("Right controller", "Grip").activeInHierarchy,
        "One physical controller is shown at a time, initially the flight-stick hand");
    Check(Tab(true).activeInHierarchy && Tab(false).activeInHierarchy, "Both physical hand tabs remain visible");
    Check(Control("Left controller", "Top").activeSelf && Control("Right controller", "Shoulder").activeSelf,
        "Frame exposes D-pad/extra face buttons and bumpers");
    CheckArtwork();
    var previews = new List<object>();
    foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
    {
        bindings.Profile.Value = profile;
        panel.Refresh();
        foreach (var left in new[] { true, false })
        {
            Tab(left).GetComponent<Button>().onClick.Invoke();
            panel.Refresh();
            CheckArtwork();
            previews.Add(Preview(profile, left));
        }
    }
    if (args.Length > 0)
        File.WriteAllText(args[0], System.Text.Json.JsonSerializer.Serialize(previews, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    bindings.Profile.Value = ControllerBindingProfile.Frame;
    panel.Refresh();
    ActivityChecks.Run(Check);
    Tab(false).GetComponent<Button>().onClick.Invoke();
    panel.Refresh();
    var baselineBinding = bindings.Get(ControllerActionId.WeaponRelease);
    var baselineColor = Control("Right controller", "Primary").GetComponent<Image>().color;
    TrackedControllers.RightSample = new ControllerSample { Valid = true, Primary = true };
    panel.Refresh();
    Check(Ring("Right controller", "Primary").enabled && Ring("Right controller", "Primary").color.g > .8f,
        "Physical press lights a green ring on the diagram without a click");
    Check(!Ring("Right controller", "Secondary").enabled && Page("CONTROLLER DIAGRAM").activeInHierarchy,
        "Activity never selects another button or opens the binding editor");
    Check(Control("Right controller", "Primary").GetComponent<Image>().color == baselineColor,
        "Live activity does not overwrite the assignment-selection background");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == baselineBinding.Hand && bindings.Get(ControllerActionId.WeaponRelease).Button == baselineBinding.Button,
        "Live input never mutates a binding");
    TrackedControllers.RightSample = new ControllerSample { Valid = true, SecondaryTouched = true };
    panel.Refresh();
    Check(!Ring("Right controller", "Primary").enabled && Ring("Right controller", "Secondary").enabled && Ring("Right controller", "Secondary").color.r == 1,
        "Release removes press ring while exact secondary touch shows amber");
    TrackedControllers.RightSample = new ControllerSample { Valid = false, Secondary = true };
    panel.Refresh();
    Check(!Ring("Right controller", "Secondary").enabled, "Invalid tracking clears a stale physical press");
    TrackedControllers.RightSample = new ControllerSample { Valid = true, Primary = true };
    panel.Refresh();
    var lifecycle = GameObject.All.Single(g => Within(g, host) && g.Has<ControllerDiagramActivityLifecycle>()).GetComponent<ControllerDiagramActivityLifecycle>();
    typeof(ControllerDiagramActivityLifecycle).GetMethod("OnDisable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(lifecycle, null);
    Check(!Ring("Right controller", "Primary").enabled, "Host/menu disable immediately clears hidden live highlights");
    TrackedControllers.RightSample = default;
    panel.Refresh();
    if (args.Length > 0)
    {
        var livePreviews = new List<object>();
        foreach (var profile in new[] { ControllerBindingProfile.Frame, ControllerBindingProfile.Touch })
        {
            bindings.Profile.Value = profile;
            Tab(false).GetComponent<Button>().onClick.Invoke();
            TrackedControllers.RightSample = new ControllerSample { Valid = true, Primary = true, TriggerTouched = true };
            panel.Refresh();
            livePreviews.Add(Preview(profile, false));
        }
        File.WriteAllText(args[0].Replace(".json", "-live.json"), System.Text.Json.JsonSerializer.Serialize(livePreviews, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        bindings.Profile.Value = ControllerBindingProfile.Frame;
        TrackedControllers.RightSample = default;
        panel.Refresh();
    }
    var navigation = GameObject.All.Where(g => Within(g, host) && g.Has<Button>() && g.transform.parent.gameObject.name == "Visual controller bindings").ToArray();
    Check(navigation.Length == 3 && navigation.All(g => ((RectTransform)g.transform).anchoredPosition.y - ((RectTransform)g.transform).sizeDelta.y * .5f > -254.5f),
        "Shared navigation stays above the cockpit host footer without overlap");
    ClickControl("Right controller", "Primary");
    Check(Page("CONTROLLER BUTTON").activeInHierarchy && Texts("CONTROLLER BUTTON").Any(t => t.Contains("Missile / weapon release · gripped")),
        "Selecting physical A displays its real gripped assignment");
    Click("Edit · Missile / weapon release");
    Check(Page("EDIT BINDING").activeInHierarchy && Texts("EDIT BINDING").Any(t => t.Contains("R · A")), "Physical selection prefills existing shared edit page");
    Click("Cancel");
    Check(Page("CONTROLLER BUTTON").activeInHierarchy && bindings.Get(ControllerActionId.WeaponRelease).Hand == ControllerHandRole.Stick,
        "Cancel returns to button details without changing role or bindings");
    Click("Back");
    ClickControl("Left controller", "Primary");
    Click("Assign · Missile / weapon release");
    Click("Save binding");
    Check(Page("SHARED BUTTON").activeInHierarchy && Texts("SHARED BUTTON").Any(t => t.Contains("Countermeasures")),
        "Assigning occupied physical button enters existing conflict review");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == ControllerHandRole.Stick && bindings.Get(ControllerActionId.Countermeasures).Button != ControllerButton.None,
        "Conflict review does not mutate either action before approval");
    Click("Replace assignments");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == ControllerHandRole.Left && bindings.Get(ControllerActionId.WeaponRelease).Button == ControllerButton.Primary &&
        bindings.Get(ControllerActionId.Countermeasures).Button == ControllerButton.None, "Replace commits clicked physical hand and clears conflicting assignment through real binding service");
    Click("Bindings");
    Click("Undo last change");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == ControllerHandRole.Stick && bindings.Get(ControllerActionId.Countermeasures).Button == ControllerButton.Primary,
        "Shared Undo restores both the moved action and displaced assignment");
    Click("Visual controller diagram");
    ClickControl("Right controller", "Primary");
    Click("Edit · Missile / weapon release");
    var before = bindings.Get(ControllerActionId.WeaponRelease);
    bindings.Profile.Value = ControllerBindingProfile.Touch;
    FindButton("Save binding").onClick.Invoke();
    panel.Refresh();
    Check(!Page("EDIT BINDING").activeInHierarchy && !Page("CONTROLLER BUTTON").activeInHierarchy,
        "A profile change cancels stale physical-button and edit contexts");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == before.Hand && bindings.Get(ControllerActionId.WeaponRelease).Button == before.Button,
        "Stale save cannot write the newly active controller preset");
    Check(!Control("Left controller", "Top").activeSelf && !Control("Right controller", "Shoulder").activeSelf,
        "Touch hides unsupported Frame controls");
    Check(!Control("Right controller", "Menu").GetComponent<Button>().interactable &&
        Control("Right controller", "Menu").transform.children.Any(t => t.gameObject.Has<Text>() && t.gameObject.GetComponent<Text>().text == "System"),
        "Touch right system button is clearly unavailable");
    ModConfiguration.Instance.MotionControls.LeftHanded.Value = true;
    panel.Refresh();
    ClickControl("Left controller", "Primary");
    Check(Texts("CONTROLLER BUTTON").Any(t => t.Contains("LEFT · X") && t.Contains("Missile / weapon release")),
        "Changing handedness moves stick actions to physical left X in the diagram");
    Click("Edit · Missile / weapon release");
    Click("Save binding");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == ControllerHandRole.Stick, "Saving an existing diagram binding preserves handedness-relative Stick semantics");
    Click("Back");
    ClickControl("Left controller", "StickClick");
    Click("More actions");
    Check(!FindButton("Assign · Map / respawn").interactable && !FindButton("Assign · View menu").interactable,
        "Free thumbstick click remains reserved for wheel rather than map/View rebinding");
    Check(Texts("CONTROLLER BUTTON").Any(t => t.Contains("Held click is editable; free click stays the wheel")), "Click detail explains fixed free versus editable held behavior");
    Click("Previous actions");
    Check(FindButton("Edit · Select target").interactable, "Held stick-click target selection remains editable");
    Click("Back");
    ClickControl("Left controller", "Grip");
    Check(Texts("CONTROLLER BUTTON").Any(t => t.Contains("HOLD / TOGGLE")) &&
        !GameObject.All.Any(g => Within(g, Page("CONTROLLER BUTTON")) && g.activeInHierarchy && g.Has<Button>()),
        "Grip detail explains its fixed function and offers no rebind actions");
    Click("Bindings");
    Click("Restore defaults…");
    Check(Page("RESTORE BINDINGS").activeInHierarchy, "Both hosts retain the shared reset confirmation");
    Click("Keep my bindings");
    Click("Done");
    Check(closes == 1, "Native Settings host has an explicit Done callback independent of cockpit state");

    // Create the same panel under a second host, as the View menu does.
    host.SetActive(false);
    host = new GameObject("Cockpit View host");
    var viewCloses = 0;
    panel = new ControllerBindingPanel(host.transform, new Font(), () => viewCloses++);
    panel.Refresh();
    Check(Page("CONTROLLER DIAGRAM").activeInHierarchy, "Cockpit host uses the identical diagram/editor implementation");
    Click("Bindings");
    Click("Menu, map & view");
    Click("View menu\nL · Menu");
    Check(Page("EDIT BINDING").activeInHierarchy, "Action-list entry still opens the same editor after refactoring");
    Click("Leave unassigned");
    Click("Save binding");
    Check(bindings.Get(ControllerActionId.WristMenu).Button == ControllerButton.None, "Existing action editor can still unassign via real binding service");
    Click("Bindings");
    Click("Undo last change");
    Check(bindings.Get(ControllerActionId.WristMenu).Button == ControllerButton.Menu, "Undo restores View menu after unassignment");
    Click("Done");
    Check(viewCloses == 1 && closes == 1, "Hosts keep their own navigation and close callbacks");
    Console.WriteLine($"Controller diagram UI checks passed: {checks} assertions (actual shared UI callbacks and BepInEx binding persistence; Unity UI doubles, no rendering).");

    GameObject Page(string name) => GameObject.All.Single(g => g.name == name && Within(g, host));
    GameObject Control(string hand, string name) => GameObject.All.Single(g => g.name == name && g.transform.parent?.gameObject.name == hand && Within(g, host));
    IEnumerable<string> Texts(string page) => GameObject.All.Where(g => Within(g, Page(page)) && g.activeInHierarchy && g.Has<Text>()).Select(g => g.GetComponent<Text>().text);
    Button FindButton(string text) => GameObject.All.Where(g => Within(g, host) && g.activeInHierarchy && g.Has<Button>()).Single(g =>
        g.transform.children.Any(t => t.gameObject.Has<Text>() && t.gameObject.GetComponent<Text>().text == text)).GetComponent<Button>();
    void Click(string text)
    {
        panel.Refresh();
        var button = FindButton(text);
        if (button.interactable)
            button.onClick.Invoke();
        panel.Refresh();
    }
    ControllerDiagramActivityRing Ring(string hand, string control) => GameObject.All.Single(g => Within(g, host) && g.name == control + " live ring" && g.transform.parent.gameObject.name == hand).GetComponent<ControllerDiagramActivityRing>();
    GameObject Tab(bool left) => GameObject.All.Single(g => g.name == (left ? "Left tab" : "Right tab") && Within(g, host));
    void ClickControl(string hand, string name)
    {
        Tab(hand == "Left controller").GetComponent<Button>().onClick.Invoke();
        panel.Refresh();
        var button = Control(hand, name).GetComponent<Button>();
        if (button.interactable)
            button.onClick.Invoke();
        panel.Refresh();
    }
    void CheckArtwork()
    {
        var page = Page("CONTROLLER DIAGRAM");
        var images = GameObject.All.Where(g => Within(g, page) && g.activeInHierarchy && g.Has<RawImage>()).ToArray();
        Check(images.Length == (bindings.EffectiveProfile == ControllerBindingProfile.Frame ? 2 : 1), "Selected hand uses original Frame views or supplied Touch crop");
        foreach (var image in images)
        {
            var raw = image.GetComponent<RawImage>();
            Check(raw.texture is Texture2D t && t.width > 200 && t.height > 200, "Real embedded PNG resource is present");
            var uv = raw.uvRect;
            Check(uv.x >= 0 && uv.y >= 0 && uv.x + uv.width <= 1.0001f && uv.y + uv.height <= 1.0001f, "Artwork crop stays within original image");
        }
        var controls = GameObject.All.Where(g => Within(g, page) && g.activeInHierarchy && g.Has<Button>() && g.transform.parent.gameObject.name.EndsWith("controller")).ToArray();
        var bounds = controls.Select(g => Bounds(g, page)).ToArray();
        Check(bounds.All(b => b.left >= -260 && b.right <= 260 && b.bottom >= -155 && b.top <= 155), "Callouts fit the area above instructions/navigation");
        for (var i = 0; i < bounds.Length; i++)
        {
            foreach (var image in images)
                Check(Separate(bounds[i], Bounds(image, page)), "All binding callouts remain outside controller artwork");
            for (var j = i + 1; j < bounds.Length; j++)
                Check(Separate(bounds[i], bounds[j]), "Callout hit targets do not overlap");
        }
        if (images.Length > 1)
            Check(Separate(Bounds(images[0], page), Bounds(images[1], page)), "Main and trigger views do not overlap");
        if (bindings.EffectiveProfile == ControllerBindingProfile.Touch)
            Check(GameObject.All.Any(g => Within(g, page) && g.activeInHierarchy && g.name == "Touch artwork light card" && g.GetComponent<Image>().color.r > .9f), "Black Touch line art has a plain light backing");
        var leaders = GameObject.All.Single(g => Within(g, page) && g.activeInHierarchy && g.Has<ControllerDiagramLeaders>()).GetComponent<ControllerDiagramLeaders>();
        var segments = (List<(Vector2 from, Vector2 to)>)typeof(ControllerDiagramLeaders).GetField("_segments", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(leaders)!;
        Check(segments.Count == controls.Length, "Each visible physical control has exactly one leader");
        for (var i = 0; i < segments.Count; i++)
            for (var j = i + 1; j < segments.Count; j++)
                Check(!Intersects(segments[i], segments[j]), "Actual rendered label leaders have no segment intersections across either bank");
        Check(Debug.Warnings.Count == 0, "No missing controller artwork resource warnings");
    }
    bool Intersects((Vector2 from, Vector2 to) first, (Vector2 from, Vector2 to) second)
    {
        static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        var a = Cross(first.from, first.to, second.from);
        var b = Cross(first.from, first.to, second.to);
        var c = Cross(second.from, second.to, first.from);
        var d = Cross(second.from, second.to, first.to);
        if (a * b < -.00001f && c * d < -.00001f)
            return true;
        // Also reject collinear overlap, not only transverse crossings.
        if (MathF.Abs(a) < .00001f && MathF.Abs(b) < .00001f)
            return Math.Max(Math.Min(first.from.x, first.to.x), Math.Min(second.from.x, second.to.x)) <
                Math.Min(Math.Max(first.from.x, first.to.x), Math.Max(second.from.x, second.to.x)) - .001f &&
                Math.Max(Math.Min(first.from.y, first.to.y), Math.Min(second.from.y, second.to.y)) <=
                Math.Min(Math.Max(first.from.y, first.to.y), Math.Max(second.from.y, second.to.y)) + .001f;
        return false;
    }
    bool Separate((float left, float right, float bottom, float top) a, (float left, float right, float bottom, float top) b) => a.right <= b.left || b.right <= a.left || a.top <= b.bottom || b.top <= a.bottom;
    object Preview(ControllerBindingProfile profile, bool left)
    {
        var page = Page("CONTROLLER DIAGRAM");
        var parts = new List<object>();
        foreach (var g in GameObject.All.Where(g => Within(g, page) && g.activeInHierarchy))
        {
            var b = Bounds(g, page);
            var rect = new[] { b.left, b.bottom, b.right - b.left, b.top - b.bottom };
            if (g.Has<RawImage>())
            {
                var image = g.GetComponent<RawImage>();
                var uv = image.uvRect;
                parts.Add(new
                {
                    kind = "art",
                    rect,
                    file = image.texture!.name,
                    uv = new[] { uv.x, uv.y, uv.width, uv.height }
                });
            }
            if (g.Has<Image>() && g.GetComponent<Image>().enabled)
            {
                var c = g.GetComponent<Image>().color;
                parts.Add(new
                {
                    kind = "box",
                    rect,
                    color = new[] { c.r, c.g, c.b, c.a }
                });
            }
            if (g.Has<ControllerDiagramActivityRing>() && g.GetComponent<ControllerDiagramActivityRing>().enabled)
            {
                var c = g.GetComponent<ControllerDiagramActivityRing>().color;
                parts.Add(new
                {
                    kind = "ring",
                    rect,
                    color = new[] { c.r, c.g, c.b, c.a }
                });
            }
            if (g.Has<Text>())
            {
                var t = g.GetComponent<Text>();
                parts.Add(new
                {
                    kind = "text",
                    rect,
                    text = t.text,
                    size = t.fontSize
                });
            }
            if (g.Has<ControllerDiagramLeaders>())
            {
                var segments = (List<(Vector2 from, Vector2 to)>)typeof(ControllerDiagramLeaders).GetField("_segments", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(g.GetComponent<ControllerDiagramLeaders>())!;
                foreach (var segment in segments)
                    parts.Add(new
                    {
                        kind = "line",
                        a = new[] { segment.from.x, segment.from.y },
                        b = new[] { segment.to.x, segment.to.y }
                    });
            }
        }
        return new
        {
            profile = profile.ToString(),
            left,
            parts
        };
    }
    bool Within(GameObject value, GameObject ancestor)
    {
        for (var t = value.transform; t != null; t = t.parent)
            if (t.gameObject == ancestor)
                return true;
        return false;
    }
    (float left, float right, float bottom, float top) Bounds(GameObject value, GameObject ancestor)
    {
        var x = 0f;
        var y = 0f;
        var size = ((RectTransform)value.transform).sizeDelta;
        for (var t = value.transform; t != ancestor.transform; t = t.parent)
        {
            x += ((RectTransform)t).anchoredPosition.x;
            y += ((RectTransform)t).anchoredPosition.y;
        }
        return (x - size.x * .5f, x + size.x * .5f, y - size.y * .5f, y + size.y * .5f);
    }
    void Check(bool pass, string message)
    {
        if (!pass)
            throw new Exception(message);
        checks++;
    }
}
finally { Directory.Delete(directory, true); }
