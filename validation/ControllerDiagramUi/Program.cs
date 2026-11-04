using BepInEx.Configuration;
using NOVR.MotionControls;
using NOVR.VrUi;
using UnityEngine;
using UnityEngine.UI;

var checks = 0;
var directory = Path.Combine(Path.GetTempPath(), "novr-diagram-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
try
{
    var config = new ConfigFile(Path.Combine(directory,"bindings.cfg"), false) { SaveOnConfigSet = false };
    var bindings = new ControllerBindings(config); bindings.Profile.Value = ControllerBindingProfile.Frame;
    ModConfiguration.Instance.ControllerBindings = bindings;
    var host = new GameObject("Native settings host"); var closes = 0;
    var panel = new ControllerBindingPanel(host.transform, new Font(), () => closes++); panel.Refresh();
    Check(Page("CONTROLLER DIAGRAM").activeInHierarchy, "Shared panel opens directly on a diagram without a cockpit/camera");
    Check(Control("Left controller", "Grip").activeInHierarchy && Control("Right controller", "Grip").activeInHierarchy,
        "Both physical controller diagrams are present");
    Check(Control("Left controller","Top").activeInHierarchy && Control("Right controller","Shoulder").activeInHierarchy,
        "Frame exposes D-pad/extra face buttons and bumpers");
    var controls = GameObject.All.Where(g => Within(g,Page("CONTROLLER DIAGRAM")) && g.activeInHierarchy && g.Has<Button>()).ToArray();
    var bounds = controls.Select(g => Bounds(g, Page("CONTROLLER DIAGRAM"))).ToArray();
    Check(bounds.All(b => b.left >= -260 && b.right <= 260 && b.bottom >= -155 && b.top <= 155),
        "Both controller hit targets fit the diagram area above its instructions and navigation");
    for(var i=0;i<bounds.Length;i++) for(var j=i+1;j<bounds.Length;j++)
        Check(bounds[i].right <= bounds[j].left || bounds[j].right <= bounds[i].left || bounds[i].top <= bounds[j].bottom || bounds[j].top <= bounds[i].bottom,
            "Physical controller hit targets do not overlap");
    var navigation = GameObject.All.Where(g => Within(g,host) && g.Has<Button>() && g.transform.parent.gameObject.name == "Visual controller bindings").ToArray();
    Check(navigation.Length == 3 && navigation.All(g => ((RectTransform)g.transform).anchoredPosition.y - ((RectTransform)g.transform).sizeDelta.y*.5f > -254.5f),
        "Shared navigation stays above the cockpit host footer without overlap");
    ClickControl("Right controller","Primary");
    Check(Page("CONTROLLER BUTTON").activeInHierarchy && Texts("CONTROLLER BUTTON").Any(t => t.Contains("Missile / weapon release · gripped")),
        "Selecting physical A displays its real gripped assignment");
    Click("Edit · Missile / weapon release");
    Check(Page("EDIT BINDING").activeInHierarchy && Texts("EDIT BINDING").Any(t => t.Contains("R · A")), "Physical selection prefills existing shared edit page");
    Click("Cancel");
    Check(Page("CONTROLLER BUTTON").activeInHierarchy && bindings.Get(ControllerActionId.WeaponRelease).Hand == ControllerHandRole.Stick,
        "Cancel returns to button details without changing role or bindings");
    Click("Back"); ClickControl("Left controller","Primary");
    Click("Assign · Missile / weapon release"); Click("Save binding");
    Check(Page("SHARED BUTTON").activeInHierarchy && Texts("SHARED BUTTON").Any(t => t.Contains("Countermeasures")),
        "Assigning occupied physical button enters existing conflict review");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == ControllerHandRole.Stick && bindings.Get(ControllerActionId.Countermeasures).Button != ControllerButton.None,
        "Conflict review does not mutate either action before approval");
    Click("Replace assignments");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == ControllerHandRole.Left && bindings.Get(ControllerActionId.WeaponRelease).Button == ControllerButton.Primary &&
        bindings.Get(ControllerActionId.Countermeasures).Button == ControllerButton.None, "Replace commits clicked physical hand and clears conflicting assignment through real binding service");
    Click("Bindings"); Click("Undo last change");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == ControllerHandRole.Stick && bindings.Get(ControllerActionId.Countermeasures).Button == ControllerButton.Primary,
        "Shared Undo restores both the moved action and displaced assignment");
    Click("Visual controller diagram"); ClickControl("Right controller","Primary"); Click("Edit · Missile / weapon release");
    var before = bindings.Get(ControllerActionId.WeaponRelease); bindings.Profile.Value = ControllerBindingProfile.Touch;
    FindButton("Save binding").onClick.Invoke(); panel.Refresh();
    Check(!Page("EDIT BINDING").activeInHierarchy && !Page("CONTROLLER BUTTON").activeInHierarchy,
        "A profile change cancels stale physical-button and edit contexts");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == before.Hand && bindings.Get(ControllerActionId.WeaponRelease).Button == before.Button,
        "Stale save cannot write the newly active controller preset");
    Check(!Control("Left controller","Top").activeInHierarchy && !Control("Right controller","Shoulder").activeInHierarchy,
        "Touch hides unsupported Frame controls");
    Check(!Control("Right controller","Menu").GetComponent<Button>().interactable &&
        Control("Right controller","Menu").transform.children.Any(t => t.gameObject.Has<Text>() && t.gameObject.GetComponent<Text>().text == "System"),
        "Touch right system button is clearly unavailable");
    ModConfiguration.Instance.MotionControls.LeftHanded.Value = true; panel.Refresh();
    ClickControl("Left controller","Primary");
    Check(Texts("CONTROLLER BUTTON").Any(t => t.Contains("LEFT · X") && t.Contains("Missile / weapon release")),
        "Changing handedness moves stick actions to physical left X in the diagram");
    Click("Edit · Missile / weapon release"); Click("Save binding");
    Check(bindings.Get(ControllerActionId.WeaponRelease).Hand == ControllerHandRole.Stick, "Saving an existing diagram binding preserves handedness-relative Stick semantics");
    Click("Back"); ClickControl("Left controller","StickClick"); Click("More actions");
    Check(!FindButton("Assign · Map / respawn").interactable && !FindButton("Assign · View menu").interactable,
        "Free thumbstick click remains reserved for wheel rather than map/View rebinding");
    Check(Texts("CONTROLLER BUTTON").Any(t => t.Contains("Held click is editable; free click stays the wheel")), "Click detail explains fixed free versus editable held behavior");
    Click("Previous actions");
    Check(FindButton("Edit · Select target").interactable, "Held stick-click target selection remains editable");
    Click("Back"); ClickControl("Left controller","Grip");
    Check(Texts("CONTROLLER BUTTON").Any(t => t.Contains("HOLD / TOGGLE")) &&
        !GameObject.All.Any(g => Within(g,Page("CONTROLLER BUTTON")) && g.activeInHierarchy && g.Has<Button>()),
        "Grip detail explains its fixed function and offers no rebind actions");
    Click("Bindings"); Click("Restore defaults…");
    Check(Page("RESTORE BINDINGS").activeInHierarchy, "Both hosts retain the shared reset confirmation");
    Click("Keep my bindings"); Click("Done");
    Check(closes == 1, "Native Settings host has an explicit Done callback independent of cockpit state");

    // Create the same panel under a second host, as the View menu does.
    host.SetActive(false);
    host = new GameObject("Cockpit View host"); var viewCloses = 0;
    panel = new ControllerBindingPanel(host.transform,new Font(),() => viewCloses++); panel.Refresh();
    Check(Page("CONTROLLER DIAGRAM").activeInHierarchy, "Cockpit host uses the identical diagram/editor implementation");
    Click("Bindings"); Click("Menu & map"); Click("View menu\nL · Menu");
    Check(Page("EDIT BINDING").activeInHierarchy, "Action-list entry still opens the same editor after refactoring");
    Click("Leave unassigned"); Click("Save binding");
    Check(bindings.Get(ControllerActionId.WristMenu).Button == ControllerButton.None, "Existing action editor can still unassign via real binding service");
    Click("Bindings"); Click("Undo last change");
    Check(bindings.Get(ControllerActionId.WristMenu).Button == ControllerButton.Menu, "Undo restores View menu after unassignment");
    Click("Done"); Check(viewCloses == 1 && closes == 1, "Hosts keep their own navigation and close callbacks");
    Console.WriteLine($"Controller diagram UI checks passed: {checks} assertions (actual shared UI callbacks and BepInEx binding persistence; Unity UI doubles, no rendering).");

    GameObject Page(string name) => GameObject.All.Single(g => g.name == name && Within(g,host));
    GameObject Control(string hand,string name) => GameObject.All.Single(g => g.name == name && g.transform.parent?.gameObject.name == hand && Within(g,host));
    IEnumerable<string> Texts(string page) => GameObject.All.Where(g => Within(g,Page(page)) && g.activeInHierarchy && g.Has<Text>()).Select(g => g.GetComponent<Text>().text);
    Button FindButton(string text) => GameObject.All.Where(g => Within(g,host) && g.activeInHierarchy && g.Has<Button>()).Single(g =>
        g.transform.children.Any(t => t.gameObject.Has<Text>() && t.gameObject.GetComponent<Text>().text == text)).GetComponent<Button>();
    void Click(string text) { panel.Refresh(); var button=FindButton(text); if(button.interactable) button.onClick.Invoke(); panel.Refresh(); }
    void ClickControl(string hand,string name) { var button=Control(hand,name).GetComponent<Button>(); if(button.interactable) button.onClick.Invoke(); panel.Refresh(); }
    bool Within(GameObject value,GameObject ancestor) { for(var t=value.transform;t!=null;t=t.parent) if(t.gameObject==ancestor) return true; return false; }
    (float left,float right,float bottom,float top) Bounds(GameObject value,GameObject ancestor)
    {
        var x=0f;var y=0f;var size=((RectTransform)value.transform).sizeDelta;
        for(var t=value.transform;t!=ancestor.transform;t=t.parent) { x+=((RectTransform)t).anchoredPosition.x;y+=((RectTransform)t).anchoredPosition.y; }
        return(x-size.x*.5f,x+size.x*.5f,y-size.y*.5f,y+size.y*.5f);
    }
    void Check(bool pass,string message) { if(!pass) throw new Exception(message); checks++; }
}
finally { Directory.Delete(directory,true); }
