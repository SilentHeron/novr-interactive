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
        var feed = new CockpitNotificationMirror(native, panel.transform, Vector2.zero, new Vector2(480, 96), CockpitTextPresentation.FeedFontSize, true);
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
        Check(copies.All(c => !c.richText && c.fontSize == 22 && c.maxVisibleLines == 1), "Kill feed defaults slightly smaller and native markup cannot enlarge it");
        feed.Refresh(true, true, true, 1, true, 1.5f);
        Check(copies.All(c => c.fontSize == 33) && copies.Count(c => c.enabled) == 2 &&
            copies.Where(c => c.enabled).Select(c => c.text).SequenceEqual(new[] { "Friendly destroyed", "Neutral destroyed" }),
            "Changing the slider immediately grows unchanged feed text and keeps its newest two entries within the same frame");
        feed.Refresh(true, true, true, 1, true, .65f);
        Check(copies.All(c => c.enabled && Math.Abs(c.fontSize - 14.3f) < .001f) && copies[0].text == "Enemy destroyed",
            "Shrinking text restores the third available entry without rebinding or losing the native feed snapshot");
        for (var step = 65; step <= 150; step++)
        {
            feed.Refresh(true, true, true, 1, true, step / 100f);
            var enabled = copies.Where(c => c.enabled).ToArray();
            Check(enabled.All(c => c.rectTransform.anchoredPosition.y + c.rectTransform.sizeDelta.y * .5f <= 48.001f &&
                    c.rectTransform.anchoredPosition.y - c.rectTransform.sizeDelta.y * .5f >= -48.001f &&
                    CockpitTextPresentation.LineHeight(c.fontSize) <= c.rectTransform.sizeDelta.y),
                "Feed rows fit the fixed panel throughout slider travel " + step);
            Check(enabled.Zip(enabled.Skip(1), (a, b) =>
                    a.rectTransform.anchoredPosition.y - a.rectTransform.sizeDelta.y * .5f >=
                    b.rectTransform.anchoredPosition.y + b.rectTransform.sizeDelta.y * .5f - .001f).All(x => x),
                "Growing rows never overlap " + step);
        }
        foreach (var invalid in new[] { float.NaN, float.NegativeInfinity, float.PositiveInfinity })
        {
            feed.Refresh(true, true, true, 1, true, invalid);
            Check(copies.All(c => c.fontSize == 22), "Non-finite feed settings fall back to the readable default");
        }
        feed.Refresh(true, true, true, 1, true, 999);
        Check(copies.All(c => c.fontSize == 33), "Out-of-range feed settings are capped");
        feed.Refresh(true, true, true, 1, true);

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
        mission.Refresh(true, true, false, 1, true, 1.5f);
        Check(missionCopy.fontSize == 33 && missionCopy.maxVisibleLines == 2 &&
            missionCopy.text == "HQ: Proceed to runway\nHQ: Clearance granted" &&
            missionCopy.rectTransform.sizeDelta.y >= CockpitTextPresentation.LineHeight(missionCopy.fontSize) * missionCopy.maxVisibleLines,
            "Notifications size includes mission lines and keeps their native text/color independent of scaling");

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

        // A spectator can join with the full map already open and no cockpit
        // history. Hiding its replacement must not reveal the central source.
        var spectatorSuppression = CockpitFeedPresentation.SuppressNative(true, true, false, true);
        feed.Refresh(false, true, true, 1, spectatorSuppression);
        Canvas.Render();
        Check(!feed.Visible && native.canvasRenderer.cull,
            "Initial spectator map suppresses the native duplicate without a prior cockpit");
        feed.Refresh(true, true, true, .8f, spectatorSuppression, 1.2f);
        Check(feed.Visible && native.canvasRenderer.cull && copies.All(c => MathF.Abs(c.fontSize - 26.4f) < .001f),
            "Closing spectator map shows a single feed and retains the chosen notification text size");
        feed.Refresh(false, true, true, 1, spectatorSuppression);
        Canvas.Render();
        Check(!feed.Visible && native.canvasRenderer.cull,
            "Reopening spectator map cannot restore the old floating duplicate");

        var rankNative = Text("Native reward");
        rankNative.text = "Rank increased";
        rankNative.color = new Color(1, .2f, .1f);
        var rankPanel = new GameObject("Reward slot", typeof(RectTransform));
        var rank = new CockpitNotificationMirror(rankNative, rankPanel.transform, Vector2.zero, new Vector2(480, 48), 24);
        rank.Refresh(true, true, false, 1, true);
        Check(rank.Visible && rankNative.Reads == 1 && rankPanel.GetComponentInChildren<TextMeshProUGUI>(true).text == "Rank increased",
            "Text-property rewards still work and are sampled only once per frame");
        var rankCopy = rankPanel.GetComponentInChildren<TextMeshProUGUI>(true);
        rank.Refresh(true, true, false, .75f, true, 1.5f);
        Check(rankCopy.fontSize == 36 && rankCopy.maxVisibleLines == 1 && rankCopy.color.Equals(new Color(1, .2f, .1f, .75f)),
            "Reward rows respond live to the same Notifications setting while preserving native color and widget opacity");
        Check(CockpitTextPresentation.LineHeight(rankCopy.fontSize) <= rankCopy.rectTransform.sizeDelta.y,
            "Reward text fits its reserved band at the maximum setting");

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
