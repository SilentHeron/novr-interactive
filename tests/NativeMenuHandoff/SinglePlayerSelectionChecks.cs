using NuclearOption.SavedMission;
using NOVR.VrUi.Native;

internal static class SinglePlayerSelectionChecks
{
    internal static int Run()
    {
        var checks = 0;
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
            checks++;
        }
        var revision = 0;
        void Catalog(params string[] names)
        {
            revision++;
            NativeMissionHeaders.Entries = names.Select(name =>
            {
                var mission = new MissionQuickLoad();
                mission.missionSettings.Tags.Add(new MissionTag($"Tag {name} v{revision}"));
                mission.missionSettings.description = $"{name} briefing v{revision}";
                return (new MissionKey(name), mission);
            }).ToArray();
        }

        var panel = new NativeSinglePlayerMissionPanel();
        void Details(string name, int expectedRevision)
        {
            Check(panel.DetailTitle == name, "Selected mission title is populated for " + name);
            Check(panel.DetailTags == $"Single Player   Tag {name} v{expectedRevision}", "Selected mission tags are current for " + name);
            Check(panel.DetailDescription == $"{name} briefing v{expectedRevision}", "Selected mission description is current for " + name);
        }
        var nativeSelection = new MissionSelectPanel();
        var nativePicker = new MissionPicker(nativeSelection);
        panel.SelectionAdapter.NativePicker = nativePicker;
        Catalog("First", "Customized", "Third");
        panel.LoadCatalog();
        Check(panel.SelectedKey == new MissionKey("First") && nativePicker.Loads == 1,
            "Opening a fresh catalog retains the existing first-row selection");
        Details("First", 1);
        panel.ChooseRow(1);
        Details("Customized", 1);
        var customized = nativePicker.Mission!;
        customized.Description = "Unsaved customization";
        var loads = nativePicker.Loads;
        panel.SelectionAdapter.SelectionHistory.Clear();

        Catalog("New First", "Third", "Customized", "First");
        panel.RefreshCatalog();
        Check(panel.SelectedKey == new MissionKey("Customized") && panel.SelectionIndex == 2,
            "Refreshing resolves the saved key against reordered rows");
        Details("Customized", 2);
        Check(panel.SelectionAdapter.SelectionHistory.SequenceEqual(new[] { new MissionKey("Customized") }),
            "The native callback receives only the final preserved key, without a row-zero detour");
        Check(ReferenceEquals(customized, nativePicker.Mission) && nativePicker.Mission!.Description == "Unsaved customization" && nativePicker.Loads == loads,
            "Refresh preserves the actual in-memory customized mission without reloading it");

        panel.SelectionAdapter.SelectionHistory.Clear();
        Catalog(Enumerable.Range(0, 14).Select(i => "Row " + i).Append("Customized").ToArray());
        panel.RefreshCatalog();
        Check(panel.SelectionIndex == 14 && panel.SelectionPage == 1,
            "The preserved mission remains visible if content additions move it to another page");
        Details("Customized", 3);
        Check(panel.SelectionAdapter.SelectionHistory.Count == 1 && ReferenceEquals(customized, nativePicker.Mission),
            "Pagination relocation still synchronizes once and retains customization");

        panel.SelectionAdapter.SelectionHistory.Clear();
        panel.GroupFilter = key => key.Name != "Excluded group";
        panel.TagFilter = key => key.Name != "Excluded tag";
        Catalog("Excluded group", "Excluded tag", "Fallback", "Another");
        panel.RefreshCatalog();
        Check(panel.SelectedKey == new MissionKey("Fallback") && panel.SelectionPage == 0,
            "A removed selection falls back to the first remaining mission inside the existing filters");
        Details("Fallback", 4);
        Check(panel.SelectionAdapter.SelectionHistory.SequenceEqual(new[] { new MissionKey("Fallback") }) && nativePicker.Loads == loads + 1,
            "Fallback has one intentional native selection and no excluded/intermediate row");

        panel.SelectionAdapter.SelectionHistory.Clear();
        panel.ChooseRow(1);
        Check(panel.SelectedKey == new MissionKey("Another") && panel.SelectionAdapter.SelectionHistory.SequenceEqual(new[] { new MissionKey("Another") }),
            "An explicit user row change still updates the native picker immediately");
        Details("Another", 4);

        panel.SelectionAdapter.SelectionHistory.Clear();
        Catalog("Excluded group", "Excluded tag");
        panel.RefreshCatalog();
        Check(panel.SelectionIndex == -1 && !panel.SelectionStartEnabled && !panel.SelectionCustomizeEnabled,
            "An empty filtered catalog has no launchable or customizable selection");
        Check(panel.SelectionAdapter.SelectionHistory.Count == 0,
            "An empty refresh sends no hidden selection to the native picker");
        Check(panel.DetailTitle == "", "Empty filtered selection clears the old mission title");
        Check(panel.DetailTags == "", "Empty filtered selection clears the old mission tags");
        Check(panel.DetailDescription == "", "Empty filtered selection clears the old mission description");
        return checks;
    }
}

namespace NuclearOption.SavedMission
{
    public sealed class MissionQuickLoad
    {
        public readonly Settings missionSettings = new();
        public sealed class Settings
        {
            public List<MissionTag> Tags = new() { MissionTag.SinglePlayer };
            public string description = "Mission description";
        }
    }
    public sealed class MissionGroup
    {
        public static readonly MissionGroup All = new();
        public static void Init()
        {
        }
        public IEnumerable<MissionKey> GetMissions() => NativeMissionHeaders.Entries.Select(entry => entry.key);
    }
}

namespace NOVR.VrUi.Native
{
    // Catalog IO and UI rendering are boundaries; the loading, filtering and
    // native selection order below come from the actual panel partial.
    internal static class NativeMissionHeaders
    {
        internal static (MissionKey key, MissionQuickLoad mission)[] Entries = Array.Empty<(MissionKey, MissionQuickLoad)>();
        internal static IEnumerable<(MissionKey key, MissionQuickLoad mission)> QuickLoadMany(IEnumerable<MissionKey> keys) => Entries;
    }

    public partial class NativeGameActionAdapter
    {
        internal MissionPicker? NativePicker;
        internal readonly List<MissionKey> SelectionHistory = new();
        public bool TrySelectOriginalMission(MissionKey key)
        {
            SelectionHistory.Add(key);
            if (NativePicker == null)
                return false;
            NativeMissionSelection.Select(NativePicker, key);
            return true;
        }
    }

    public partial class NativeSinglePlayerMissionPanel
    {
        private const int PageSize = 13;
        private readonly List<MissionEntry> _missions = new();
        private int _page;
        private readonly SelectionText _titleText = new(), _tagsText = new(), _descriptionText = new();
        private readonly SelectionButton _startButton = new(), _customizeButton = new();
        internal Predicate<MissionKey> GroupFilter = _ => true, TagFilter = _ => true;
        internal NativeGameActionAdapter SelectionAdapter => _actions;
        internal MissionKey? SelectedKey => _selectedIndex >= 0 && _selectedIndex < _filteredMissions.Count ? _filteredMissions[_selectedIndex].Key : null;
        internal int SelectionIndex => _selectedIndex;
        internal int SelectionPage => _page;
        internal string DetailTitle => _titleText.text;
        internal string DetailTags => _tagsText.text;
        internal string DetailDescription => _descriptionText.text;
        internal bool SelectionStartEnabled => _startButton.interactable;
        internal bool SelectionCustomizeEnabled => _customizeButton.interactable;
        internal void LoadCatalog() => LoadMissions();
        internal void RefreshCatalog() => RefreshContentMissions();
        internal void ChooseRow(int index) => SelectMission(index);
        private bool MatchesGroupFilter(MissionEntry entry) => GroupFilter(entry.Key);
        private bool MatchesTagFilter(MissionEntry entry) => TagFilter(entry.Key);
        private static int ClampInt(int value, int min, int max) => Math.Clamp(value, min, max);
        private int GetLastPage() => Math.Max(0, (_filteredMissions.Count - 1) / PageSize);
        private void RefreshList()
        {
        }
        private void RefreshFilterButtons()
        {
        }
        private sealed class SelectionText
        {
            public string text = "";
        }
        private sealed class SelectionButton
        {
            public bool interactable = true;
        }
    }
}
