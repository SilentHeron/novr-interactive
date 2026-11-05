using NuclearOption.SavedMission;
using NOVR.VrUi.Native;
using UnityEngine;
using UnityEngine.UI;

internal static class SinglePlayerPreviewChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string message)
        {
            if (!value)
                throw new InvalidOperationException(message);
            checks++;
        }
        var first = new MissionKey("First preview");
        var second = new MissionKey("Second preview");
        NativeMissionHeaders.Entries = new[] { (first, new MissionQuickLoad()), (second, new MissionQuickLoad()) };
        var panel = new NativeSinglePlayerMissionPanel();
        panel.CreatePreviewBoundary();
        panel.LoadCatalog();
        var adapter = panel.SelectionAdapter;
        adapter.PreviewKey = first;
        panel.RefreshPreviewForCheck();
        Check(!panel.PreviewEnabled && panel.PreviewSprite == null && panel.PreviewStatus.Contains("Loading", StringComparison.OrdinalIgnoreCase),
            "Missing native picker shows loading rather than a black or stale image");
        Check(adapter.PreviewQueries.Count == 0 && adapter.SelectionHistory[^1] == first,
            "A missing picker is synchronized to the selected mission before any preview is borrowed");

        var firstSprite = new Sprite();
        var nativeImage = new Image { sprite = firstSprite, color = Color.white };
        adapter.NativePicker = new MissionPicker(new MissionSelectPanel());
        adapter.PreviewAvailable = true;
        adapter.PreviewSource = nativeImage;
        panel.RefreshPreviewForCheck();
        Check(panel.PreviewEnabled && ReferenceEquals(panel.PreviewSprite, firstSprite) && panel.PreviewColor == Color.white && panel.PreviewStatus == "",
            "A native picker appearing later supplies its cached preview without a second loader");
        Check(adapter.PreviewQueries[^1] == first && adapter.NativePicker.Loads == 1,
            "Late picker synchronization loads the selected mission once before reading its preview");
        panel.RefreshPreviewForCheck();
        Check(ReferenceEquals(panel.PreviewSprite, firstSprite) && ReferenceEquals(nativeImage.sprite, firstSprite) && !firstSprite.Destroyed,
            "Repeated refresh borrows the same cached sprite without replacing native ownership");
        Check(adapter.NativePicker.Loads == 1, "Repeated preview polling never reloads the native mission");

        var fallback = new Sprite();
        nativeImage.color = new(.25f, .25f, .25f, 1);
        adapter.PreviewFallback = fallback;
        panel.RefreshPreviewForCheck();
        Check(panel.PreviewEnabled && ReferenceEquals(panel.PreviewSprite, fallback) && panel.PreviewColor == Color.white,
            "Native loading tint rejects the previous mission sprite and displays the supplied fallback");
        Check(ReferenceEquals(nativeImage.sprite, firstSprite) && nativeImage.color == new Color(.25f, .25f, .25f, 1),
            "Fallback presentation does not recolor or replace the native loading image");
        adapter.PreviewFallback = null;
        panel.RefreshPreviewForCheck();
        Check(!panel.PreviewEnabled && panel.PreviewSprite == null && panel.PreviewStatus.Contains("Loading", StringComparison.OrdinalIgnoreCase),
            "A pending source without fallback clears the stale image and reports loading");
        nativeImage.color = Color.white;
        nativeImage.sprite = null;
        adapter.PreviewFallback = fallback;
        panel.RefreshPreviewForCheck();
        Check(ReferenceEquals(panel.PreviewSprite, fallback), "A white but empty native image still uses fallback");
        adapter.PreviewSource = null;
        panel.RefreshPreviewForCheck();
        Check(ReferenceEquals(panel.PreviewSprite, fallback), "An unavailable native image may still provide a usable fallback");

        adapter.PreviewSource = nativeImage;
        nativeImage.sprite = firstSprite;
        panel.RefreshPreviewForCheck();
        panel.ChooseRow(1);
        Check(panel.PreviewSprite == null && !panel.PreviewEnabled, "Changing mission immediately releases the previous borrowed preview");
        panel.RefreshPreviewForCheck();
        Check(panel.SelectedKey == second && adapter.PreviewQueries[^1] == second && panel.PreviewSprite == null,
            "A stale native selection cannot supply the old mission preview for the new row");
        adapter.PreviewKey = second;
        nativeImage.color = new(.5f, .5f, .5f, 1);
        panel.RefreshPreviewForCheck();
        Check(ReferenceEquals(panel.PreviewSprite, fallback), "The newly selected mission uses fallback while native loading retains its old sprite");
        var secondSprite = new Sprite();
        nativeImage.sprite = secondSprite;
        nativeImage.color = Color.white;
        panel.RefreshPreviewForCheck();
        Check(ReferenceEquals(panel.PreviewSprite, secondSprite) && panel.PreviewEnabled && panel.PreviewStatus == "",
            "Completing the native preview replaces fallback with the current mission image");

        var replacementPicker = new MissionPicker(new MissionSelectPanel());
        adapter.NativePicker = replacementPicker;
        nativeImage = new Image { sprite = firstSprite, color = new(.2f, .2f, .2f, 1) };
        adapter.PreviewSource = nativeImage;
        panel.RefreshPreviewForCheck();
        Check(replacementPicker.Loads == 1 && replacementPicker.Mission?.Name == second.Name && panel.SelectedKey == second,
            "Replacing the native picker while visible resynchronizes the retained mission exactly once");
        Check(ReferenceEquals(panel.PreviewSprite, fallback), "A replacement picker's loading image cannot show its stale previous sprite");
        panel.RefreshPreviewForCheck();
        Check(replacementPicker.Loads == 1, "Polling the replacement picker does not reload the selected mission");
        nativeImage.sprite = secondSprite;
        nativeImage.color = Color.white;
        panel.RefreshPreviewForCheck();
        Check(ReferenceEquals(panel.PreviewSprite, secondSprite), "The replacement picker's completed preview is adopted normally");

        panel.PreviewVisibleBoundary = false;
        panel.DisablePreviewForCheck();
        var queries = adapter.PreviewQueries.Count;
        panel.RefreshPreviewForCheck();
        Check(panel.PreviewSprite == null && !panel.PreviewEnabled && adapter.PreviewQueries.Count == queries,
            "Hidden panel clears its borrowed image and performs no native preview lookups");
        Check(panel.SelectedKey == second && !secondSprite.Destroyed && ReferenceEquals(nativeImage.sprite, secondSprite),
            "Hiding retains selection and leaves the native cached sprite alive");
        panel.PreviewVisibleBoundary = true;
        panel.EnablePreviewForCheck();
        Check(panel.SelectedKey == second && ReferenceEquals(panel.PreviewSprite, secondSprite),
            "Resuming the retained selection reacquires its existing cached preview");

        panel.ChooseRow(-1);
        panel.RefreshPreviewForCheck();
        Check(!panel.PreviewEnabled && panel.PreviewSprite == null && panel.PreviewStatus.Contains("Select a mission", StringComparison.OrdinalIgnoreCase),
            "Empty selection clears the preview and provides an explicit selection prompt");
        Check(adapter.PreviewQueries.Count == queries + 1, "An empty selection never queries a previous mission");
        panel.ClearPreviewForCheck();
        panel.ClearPreviewForCheck();
        panel.DestroyPreviewForCheck();
        Check(!firstSprite.Destroyed && !secondSprite.Destroyed && !fallback.Destroyed &&
            ReferenceEquals(nativeImage.sprite, secondSprite), "Repeated cleanup never destroys or mutates borrowed native/fallback sprites");
        return checks;
    }
}

namespace UnityEngine
{
    public sealed class Sprite : Object
    {
    }
    public readonly record struct Color(float r, float g, float b, float a = 1)
    {
        public static Color white => new(1, 1, 1, 1);
    }
}
namespace UnityEngine.UI
{
    public sealed class Image : MonoBehaviour
    {
        public Sprite? sprite;
        public Color color = Color.white;
        public bool enabled = true;
    }
    public sealed class Text : MonoBehaviour
    {
        public string text = "";
    }
}
namespace NOVR.VrUi.Native
{
    public partial class NativeGameActionAdapter
    {
        internal bool PreviewAvailable;
        internal MissionKey PreviewKey;
        internal Image? PreviewSource;
        internal Sprite? PreviewFallback;
        internal readonly List<MissionKey> PreviewQueries = new();
        internal MissionPicker? GetOriginalMissionPreviewPicker() => NativePicker;
        internal bool TryGetOriginalMissionPreview(MissionKey key, out Image? source, out Sprite? fallback)
        {
            PreviewQueries.Add(key);
            var available = PreviewAvailable && key == PreviewKey;
            source = available ? PreviewSource : null;
            fallback = available ? PreviewFallback : null;
            return available;
        }
    }
    public partial class NativeSinglePlayerMissionPanel
    {
        private bool _visible;
        internal bool PreviewVisibleBoundary
        {
            set => _visible = value;
        }
        internal bool PreviewEnabled => _previewImage!.enabled;
        internal Sprite? PreviewSprite => _previewImage!.sprite;
        internal Color PreviewColor => _previewImage!.color;
        internal string PreviewStatus => _previewStatusText!.text;
        internal void CreatePreviewBoundary()
        {
            _previewImage = new Image();
            _previewStatusText = new Text();
            _visible = true;
        }
        internal void RefreshPreviewForCheck() => RefreshMissionPreview();
        internal void ClearPreviewForCheck() => ClearMissionPreview();
        internal void DisablePreviewForCheck() => OnDisable();
        internal void EnablePreviewForCheck() => OnEnable();
        internal void DestroyPreviewForCheck() => OnDestroy();
        private void ResetMissionDescriptionScroll()
        {
        }
    }
}
