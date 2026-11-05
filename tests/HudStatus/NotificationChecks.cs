using System.Text;
using NOVR.VrUi.HarmonyPatches;
using NOVR.VrUi.SpecialBehavior;
using TMPro;
using UnityEngine;

internal static class NotificationChecks
{
    internal static int Run()
    {
        var passed = 0;
        void Check(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException(reason);
            passed++;
        }
        TextMeshProUGUI Text(string name) =>
            new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        var native = Text("Native unit deaths");
        var builder = new StringBuilder("Unit destroyed");
        native.SetText(builder);
        Check(native.text == "Unit destroyed" && native.text == "", "Fixture reproduces the installed TMP one-shot backing-string getter");

        var panel = new GameObject("Notifications", typeof(RectTransform));
        var feed = new CockpitNotificationMirror(native, panel.transform, Vector2.zero, new Vector2(480, 96), 24, true);
        void Publish(TMP_Text source, string value)
        {
            builder.Clear().Append(value);
            source.SetText(builder);
            NativeMessageFeedPatch.Postfix(source, builder, value.Length > 0);
        }
        Publish(native, "Old\n<color=red>Enemy destroyed</color>\nFriendly destroyed\nNeutral destroyed");
        var reads = native.Reads;
        for (var frame = 0; frame < 90; frame++) feed.Refresh(true, true, true, 1, true);
        var copies = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        Check(feed.Visible && copies.Select(c => c.text).SequenceEqual(new[] { "Enemy destroyed", "Friendly destroyed", "Neutral destroyed" }),
            "Ordinary kills persist for repeated frames and retain the newest three entries");
        Check(native.Reads == reads, "Native feed mirroring never consumes the mutable TMP getter");
        Check(native.enabled && native.gameObject.activeSelf && native.canvasRenderer.cull, "Live native feed logic remains enabled while its drawing is suppressed");
        Check(copies.All(c => !c.richText && c.fontSize == 24 && c.maxVisibleLines == 1), "Native markup cannot enlarge the compact kill feed");

        var missionNative = Text("Native delayed HQ messages");
        missionNative.color = new Color(.9f, .8f, .7f, .8f);
        var missionPanel = new GameObject("Mission slot", typeof(RectTransform));
        var mission = new CockpitNotificationMirror(missionNative, missionPanel.transform, Vector2.zero, new Vector2(480, 84), 22);
        Publish(missionNative, "HQ: Proceed to runway");
        // Another consumer may read the source before NOVR; the snapshot is
        // independent of render order, culling and the native canvas location.
        _ = missionNative.text;
        _ = missionNative.text;
        mission.Refresh(true, true, false, .5f, true);
        var missionCopy = missionPanel.GetComponentInChildren<TextMeshProUGUI>(true);
        Check(mission.Visible && missionCopy.text == "HQ: Proceed to runway", "Delayed radio/HQ text survives other consumers reading TMP first");
        Check(MathF.Abs(missionCopy.color.a - .4f) < .0001f, "Mission color/opacity is retained");
        Publish(missionNative, "HQ: Proceed to runway\nHQ: Clearance granted");
        mission.Refresh(true, true, false, 1, true);
        Check(missionCopy.text == "HQ: Proceed to runway\nHQ: Clearance granted", "Delayed subsequent lines reach the movable mission feed");

        // Clear/last-entry expiry does not clear MessageFeed._sb. Its native
        // _hasContent flag is authoritative and must discard that stale buffer.
        NativeMessageFeedPatch.Postfix(missionNative, builder, false);
        mission.Refresh(true, true, false, 1, true);
        Check(!mission.Visible && !missionCopy.enabled, "Native ClearMessages/expiry hides the last message despite a stale builder");
        Publish(missionNative, "Fresh after clear");
        mission.Refresh(true, true, false, 1, true);
        Check(mission.Visible && missionCopy.text == "Fresh after clear", "Messages can resume after native expiry");

        foreach (var transition in new[] { "radial", "pause", "full map", "bindings" })
        {
            Check(CockpitFeedPresentation.Choose(true, true, false, false, true, false) == CockpitFeedMode.Native,
                transition + " may suspend compact presentation");
            feed.Refresh(false, true, true, 1, CockpitFeedPresentation.SuppressNative(true, true, true));
            Canvas.Render();
            Check(!feed.Visible && native.canvasRenderer.cull, transition + " cannot reveal the old upper-left feed");
        }
        feed.Refresh(true, false, true, 1, true);
        Check(!feed.Visible && native.canvasRenderer.cull, "Hiding the widget or kill-feed setting cannot reveal the native duplicate");
        mission.Refresh(false, true, false, 1, false);
        Check(!missionNative.canvasRenderer.cull, "Opening native chat restores its original message history");
        Publish(native, "Received while menu open");
        feed.Refresh(true, true, true, 1, true);
        Check(feed.Visible && copies[0].text == "Received while menu open", "Closing a menu reveals the latest live message rather than stale content");
        foreach (var state in new[] { (false, true, true), (true, false, true), (true, true, false) })
            Check(!CockpitFeedPresentation.SuppressNative(state.Item1, state.Item2, state.Item3), "Outside owned VR gameplay native notifications are restored");

        var rankNative = Text("Native reward");
        rankNative.text = "Rank increased";
        rankNative.color = new Color(1, .2f, .1f);
        var rankPanel = new GameObject("Reward slot", typeof(RectTransform));
        var rank = new CockpitNotificationMirror(rankNative, rankPanel.transform, Vector2.zero, new Vector2(480, 32), 24);
        rank.Refresh(true, true, false, 1, true);
        Check(rank.Visible && rankNative.Reads == 1 && rankPanel.GetComponentInChildren<TextMeshProUGUI>(true).text == "Rank increased",
            "Text-property rewards still work and are sampled only once per frame");

        // Many messages refresh only on enqueue/expiry. Per-frame presentation
        // should not allocate a string or force a TMP mesh rebuild to read them.
        // Exclude the test double's allocating hierarchy traversal for culling.
        for (var i = 0; i < 100; i++) feed.Refresh(true, true, true, 1, false);
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++) feed.Refresh(true, true, true, 1, false);
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "A thousand unchanged feed frames allocate no managed memory");

        feed.Release(); mission.Release(); rank.Release();
        Check(!native.canvasRenderer.cull && !missionNative.canvasRenderer.cull && !rankNative.canvasRenderer.cull,
            "Teardown restores all borrowed native text drawings");
        Check(copies.All(c => c.gameObject.Destroyed) && missionCopy.gameObject.Destroyed, "Teardown removes only NOVR's copied labels");
        var replacement = Text("New mission scene");
        Check(NativeNotificationText.Read(replacement) == "", "New scene/source identity cannot inherit the previous scene's text");
        Check(NativeNotificationText.Read(native) == "Received while menu open", "Rebinding does not consume or discard the still-live native message");
        return passed;
    }
}
