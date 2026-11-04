using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

internal static class LeaseChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var own = new HandOptionWheelOwnership();
        check(!own.Owns(0, 0) && !own.Owns(1, 0), "No hand owned before open");
        foreach (var side in new[] { 0, 1 })
        {
            var frame = 30 + side * 10;
            own.Open(side, frame);
            check(own.Owns(side, frame) && !own.Owns(1 - side, frame), "Opening frame blocks only wheel hand");
            check(own.Owns(side, frame + 4), "Open owner blocks across later frames");
            own.Close(frame + 5);
            check(own.Owner == -1 && own.Owns(side, frame + 5) && !own.Owns(1 - side, frame + 5), "Commit/cancel frame cannot leak button or grip to flight input");
            check(!own.Owns(side, frame + 6), "Closing-frame block expires next frame");
        }
        var f = new Fixture(); var original = f.Snapshot();
        using (var lease = new NativeRadialMenuLease(f.Native, f.Host))
        {
            check(f.Native.Opens == 1 && lease.Count == 1 && !lease.WeaponsPage, "Opens native setup and starts real main page");
            check(f.Menu.transform.parent == f.Host && f.Menu.activeSelf && f.Group.alpha == 1, "Native menu borrowed and visible");
            check(f.Menu.layer == 30 && f.Caption.gameObject.layer == 30 && !f.Caption.raycastTarget, "Native graphics use hand canvas without pointer capture");
            check(ReferenceEquals(lease.ActionAt(0), f.MainAction), "Native action object is reused, not a replacement list");
            check(lease.Hover(0) && f.MainAction.Hovers == 1 && f.Caption.text == f.MainAction.DisplayName, "Hover uses original icon/name callbacks");
            check(!lease.Hover(0) && f.MainAction.Hovers == 1, "Steady hover does not replay callback");
            lease.ChangePage();
            check(lease.WeaponsPage && f.Native.Refreshes == 1 && f.MainAction.Unhovers == 1 && f.Native.selectedAction == null, "Native weapon page refreshes ammo and clears main highlight");
            check(ReferenceEquals(lease.ActionAt(0), f.WeaponAction), "Weapon page uses native station action");
            check(lease.ActionAt(-1) == null && lease.ActionAt(99) == null, "Native index bounds guarded");
            f.Parent.SetActive(false);
            check(f.Menu.activeInHierarchy && f.Caption.gameObject.activeInHierarchy, "Caption stays visible when original native parent hides");
            f.Parent.SetActive(true);
            lease.Hover(0);
        }
        check(f.Snapshot().Equals(original), "Parent/sibling/anchors/pivot/size/position/rotation/scale/layers/raycasts/alpha restore");
        check(!f.Menu.activeSelf && f.Native.currentState == RadialMenuMain.RadialMenuType.Closed && f.Native.selectedAction == null,
            "Release is immediately closed, not a native commit-on-release");
        check(f.MainAction.Activations == 0 && f.WeaponAction.Activations == 0, "Lifecycle/page switching never activates an action");
        foreach (var phase in new[] { "setup", "reparented", "invalid caption" })
        {
            f = new Fixture(); original = f.Snapshot();
            if (phase == "setup") f.Native.FailOpen = true;
            if (phase == "reparented") f.Main.FailActivation = true;
            if (phase == "invalid caption") f.Caption.transform.SetParent(f.Parent.transform, false);
            var threw = false;
            try { using var lease = new NativeRadialMenuLease(f.Native, f.Host); }
            catch (InvalidOperationException) { threw = true; }
            check(threw, "Injected " + phase + " failure observed");
            if (phase == "invalid caption")
                check(f.Native.Opens == 0 && f.Menu.transform.parent == f.Parent.transform, "Ancestry rejected before any native setup or borrowing");
            else
            {
                check(f.Snapshot().Equals(original), "Failed " + phase + " acquisition rolls back complete presentation");
                check(!f.Menu.activeSelf && f.Native.Closes == 1 && f.Native.currentState == RadialMenuMain.RadialMenuType.Closed,
                    "Failed " + phase + " acquisition closes native state without owner disposal");
                check(f.MainAction.Activations == 0 && f.WeaponAction.Activations == 0, "Failed acquisition cannot activate a native action");
            }
        }
    }
    private sealed class Fixture
    {
        internal readonly GameObject Parent = new("RadialMenu"), Menu = new("MenuObject"), Main = new("actionsMain"), Weapons = new("actionsWeapons");
        internal readonly RectTransform Host = new GameObject("Hand canvas") { layer = 30 }.transform;
        internal readonly RadialMenuMain Native;
        internal readonly TMP_Text Caption;
        internal readonly CanvasGroup Group;
        internal readonly RadialMenuAction MainAction = new() { DisplayName = "Native gear" }, WeaponAction = new() { DisplayName = "Native missile" };
        internal Fixture()
        {
            Menu.transform.SetParent(Parent.transform, false); Menu.layer = 5; Menu.SetActive(false);
            Menu.transform.localPosition = new(6, 8, 2); Menu.transform.localScale = new(.5f, .5f, .5f);
            Main.transform.SetParent(Menu.transform, false); Weapons.transform.SetParent(Menu.transform, false); Weapons.SetActive(false);
            var caption = new GameObject("selectedAction") { layer = 7 }; caption.transform.SetParent(Menu.transform, false); Caption = caption.AddComponent<TMP_Text>();
            Group = Parent.AddComponent<CanvasGroup>(); Group.alpha = .24f;
            Native = Parent.AddComponent<RadialMenuMain>(); Native.menuObject = Menu; Native.actionsMainContainer = Main; Native.actionsWeaponsContainer = Weapons;
            Native.selectedActionText = Caption; Native.canvasGroup = Group;
            Native.allowedActionsMain.Add(MainAction); Native.allowedActionsWeapons.Add(WeaponAction);
        }
        internal object Snapshot() => (Menu.transform.parent, Menu.transform.GetSiblingIndex(), Menu.transform.anchorMin, Menu.transform.anchorMax,
            Menu.transform.pivot, Menu.transform.sizeDelta, Menu.transform.localPosition, Menu.transform.localRotation, Menu.transform.localScale,
            Menu.layer, Caption.gameObject.layer, Caption.raycastTarget, Group.alpha, Main.activeSelf, Weapons.activeSelf);
    }
}
