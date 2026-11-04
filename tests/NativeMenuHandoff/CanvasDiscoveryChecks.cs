using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class CanvasDiscoveryChecks
{
    internal static int Run()
    {
        var passed = 0;
        void Check(bool value, string description)
        {
            if (!value)
                throw new Exception(description);
            passed++;
        }
        Canvas Add(string name, Scene scene, bool active = true)
        {
            var item = new Canvas { gameObject = new GameObject { name = name, scene = scene, activeSelf = active } };
            Resources.Loaded.Add(item);
            return item;
        }

        var firstScene = new Scene(1);
        var secondScene = new Scene(2);
        SceneManager.SetActive(firstScene);
        var discovery = new NativeMainCanvasDiscovery();
        Check(discovery.Find(0) == null && Resources.Searches == 0, "Disabled discovery must not enumerate.");
        discovery.Enable();
        discovery.Enable();
        Check(SceneManager.Subscribers == 3, "Enable must register scene events exactly once.");
        Add("MainCanvas", default); // Prefab asset, not a scene instance.
        Add("MainCanvas", new Scene(3, false));
        Add("OtherCanvas", firstScene);
        var original = Add("MainCanvas", firstScene, false);
        Check(discovery.Find(0) == original.gameObject, "Find an inactive native menu, ignoring assets/unloaded scenes.");
        Check(Resources.Searches == 1, "Initial discovery performs one Canvas enumeration.");
        for (var frame = 1; frame <= 9000; frame++)
            discovery.Find(frame / 90f);
        Check(Resources.Searches == 1, "A cached flight/menu canvas must not rescan, even after minutes.");
        original.gameObject.activeSelf = true;
        Check(discovery.Find(101) == original.gameObject && Resources.Searches == 1, "Visibility changes keep the same cached menu.");

        var replacement = Add("MainCanvas", secondScene, false);
        SceneManager.Load(secondScene);
        Check(discovery.Find(102) == original.gameObject && Resources.Searches == 2, "Loading a scene invalidates discovery without discarding the active scene menu.");
        SceneManager.SetActive(secondScene);
        Check(discovery.Find(102.01f) == replacement.gameObject && Resources.Searches == 3, "The replacement scene's inactive menu wins over the previous active menu.");
        replacement.gameObject.scene = new Scene(2, false);
        SceneManager.Unload(secondScene);
        Check(discovery.Find(102.02f) == original.gameObject && Resources.Searches == 4, "Scene unload immediately recovers a surviving native menu.");

        original.Destroyed = true;
        var recreated = Add("MainCanvas", firstScene, false);
        Check(discovery.Find(102.03f) == recreated.gameObject && Resources.Searches == 5, "Unity-destroyed Canvas recovers immediately inside the old scan interval.");
        recreated.gameObject.Destroyed = true;
        Check(discovery.Find(102.04f) == null && Resources.Searches == 6, "A destroyed GameObject is not returned even if its component wrapper remains.");
        Check(discovery.Find(102.5f) == null && Resources.Searches == 6, "Missing canvas retries start at half a second.");
        discovery.Find(102.55f);
        Check(Resources.Searches == 7, "First missing retry runs after half a second.");
        discovery.Find(103.5f);
        Check(Resources.Searches == 7, "Second retry backs off to one second.");
        discovery.Find(103.6f);
        discovery.Find(105f);
        Check(Resources.Searches == 8, "Repeated missing menus back off to the two-second cap.");
        var delayed = Add("MainCanvas", firstScene, false);
        Check(discovery.Find(105.61f) == delayed.gameObject && Resources.Searches == 9, "A late-created inactive menu is discovered without a scene event.");
        Check(discovery.Find(106) == delayed.gameObject && Resources.Searches == 9, "Successful retry resumes caching.");

        delayed.gameObject.scene = new Scene(1, false);
        Check(discovery.Find(106.01f) == null && Resources.Searches == 10, "Scene validity also protects against a missed unload event.");
        var immediate = Add("MainCanvas", firstScene);
        SceneManager.Load(firstScene);
        Check(discovery.Find(106.02f) == immediate.gameObject && Resources.Searches == 11, "Scene readiness bypasses a missing-canvas delay.");
        discovery.Invalidate();
        Check(discovery.Find(106.03f) == immediate.gameObject && Resources.Searches == 12, "Startup camera replacement can explicitly invalidate discovery.");

        discovery.Disable();
        discovery.Disable();
        Check(SceneManager.Subscribers == 0, "Disable/destroy removes every scene subscription without duplicates.");
        SceneManager.Load(secondScene);
        SceneManager.Unload(firstScene);
        SceneManager.SetActive(secondScene);
        Check(discovery.Find(200) == null && Resources.Searches == 12, "Disabled discovery ignores scene activity.");
        replacement.gameObject.scene = secondScene;
        discovery.Enable();
        Check(discovery.Find(200) == replacement.gameObject && Resources.Searches == 13, "Re-enable discovers scenes changed while VR UI was disabled.");
        discovery.Disable();
        Check(SceneManager.Subscribers == 0, "Final cleanup releases scene handlers.");
        return passed;
    }
}
