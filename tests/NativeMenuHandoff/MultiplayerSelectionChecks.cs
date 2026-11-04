using NuclearOption.Networking.Lobbies;
using NOVR.VrUi.Native;
using Steamworks;

internal static class MultiplayerSelectionChecks
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

        var first = new LobbyInstance(101, "Same name", 0);
        var selected = new LobbyInstance(202, "Same name", 1);
        var panel = new NativeMultiplayerPanel();
        panel.Replace(first, selected);
        Check(panel.Selected == 0 && panel.JoinEnabled, "Initial discovery retains the first-row default");
        panel.Select(1);
        var refreshed = new LobbyInstance(202, "Renamed selected lobby", -1);
        panel.Replace(first, refreshed, new LobbyInstance(303, "Same name", -2));
        Check(panel.Selected == 1 && panel.Title == refreshed.Name,
            "Insertion/re-sort preserves selected Steam identity despite replaced objects and duplicate names");
        panel.Join();
        Check(ReferenceEquals(panel.LastJoined, refreshed), "JOIN uses the refreshed instance of the intended server");

        refreshed.Password = true;
        panel.Replace(first, refreshed);
        panel.Join();
        Check(ReferenceEquals(panel.PasswordLobby, refreshed), "Password prompting is bound to the retained server identity");
        Check(panel.JoinCalls == 1, "Password prompting does not dispatch an unpassworded join");

        panel.Replace(first);
        Check(panel.Selected == -1 && !panel.JoinEnabled && panel.Title == "",
            "Disappeared selection clears details and disables JOIN instead of selecting its old row");
        panel.Join();
        Check(panel.JoinCalls == 1, "No join is dispatched for a missing server");
        panel.Replace(first, new LobbyInstance(404, "Another", 1));
        Check(panel.Selected == -1 && !panel.JoinEnabled, "Later partial refreshes cannot silently pick a substitute");
        panel.Replace();
        Check(panel.Selected == -1, "Empty refresh keeps the intended selection unavailable");
        var returned = new LobbyInstance(202, "Back online", 2) { Full = true };
        panel.Replace(first, returned);
        Check(panel.Selected == 1 && panel.Title == returned.Name && !panel.JoinEnabled,
            "Returning identity restores its latest details and full-server availability");
        returned.Full = false;
        panel.Replace(returned, first);
        Check(panel.JoinEnabled, "A later vacancy is reflected without replacing the selection");
        panel.Select(0);
        panel.Replace(first, returned);
        Check(panel.Selected == 0 && panel.Title == first.Name, "Explicit row selection replaces the retained identity");

        panel.Select(1);
        returned.Hidden = true;
        panel.Replace(first, returned);
        Check(panel.Selected == -1, "A selected lobby excluded by refreshed filters cannot redirect JOIN");
        panel.ResetFilters();
        Check(panel.Selected == 0, "Explicit filter changes retain the existing first-result behavior");
        panel.Reopen();
        panel.Replace();
        panel.Replace(returned, first);
        Check(panel.Selected == 0 && panel.Title == first.Name, "Reopening starts a fresh browser selection");

        var page = new NativeMultiplayerPanel();
        var many = Enumerable.Range(0, 12).Select(index => new LobbyInstance((ulong)(1000 + index), "Lobby " + index, index)).ToArray();
        page.Replace(many);
        page.Select(11);
        page.Replace(many);
        Check(page.Selected == 11 && page.Page == 1, "A retained selected row stays visible on its page");
        many[11].Order = -1;
        page.Replace(many);
        Check(page.Selected == 0 && page.Page == 0 && page.Title == many[11].Name,
            "Metadata reordering across pages keeps the correct selected row visible");

        var unknown = new NativeMultiplayerPanel();
        var unresolved = new LobbyInstance(0, "Resolving", 1);
        unknown.Replace(first, unresolved);
        unknown.Select(1);
        unknown.Replace(new LobbyInstance(0, "Different unresolved server", 0), unresolved);
        Check(unknown.Selected == 1, "Unresolved zero IDs match only the original instance");
        unknown.Replace(new LobbyInstance(0, "Different unresolved server", 0));
        Check(unknown.Selected == -1 && !unknown.JoinEnabled, "Different zero-ID servers cannot become the join destination");

        foreach (var password in new[] { " leading", "trailing ", " both ", "   ", "ordinary" })
        {
            var protectedLobby = new LobbyInstance(505, "Protected", 0) { Password = true, PasswordValue = password };
            var join = new NativeMultiplayerPanel();
            join.Replace(protectedLobby);
            join.Join();
            join.SubmitPassword(password);
            Check(join.JoinCalls == 1 && ReferenceEquals(join.LastJoined, protectedLobby) && join.LastPassword == password,
                "Password submission forwards the exact entered password, including whitespace");
            Check(!join.PasswordPending, "A successful password submission closes its prompt");
        }

        var passwordPanel = new NativeMultiplayerPanel();
        var passwordLobby = new LobbyInstance(606, "Exact password", 0) { Password = true, PasswordValue = " protected " };
        passwordPanel.Replace(passwordLobby);
        passwordPanel.Join();
        foreach (var empty in new string?[] { "", null })
        {
            passwordPanel.SubmitPassword(empty);
            Check(passwordPanel.JoinCalls == 0 && passwordPanel.PasswordPending && passwordPanel.PasswordStatus == "Enter the server password.",
                "Empty input cannot join or discard the pending server");
        }
        passwordPanel.SubmitPassword("protected");
        Check(passwordPanel.JoinCalls == 0 && passwordPanel.PasswordPending && passwordPanel.PasswordStatus == "Password incorrect.",
            "Whitespace-sensitive passwords reject an incorrect trimmed variant");
        passwordPanel.Replace(first);
        passwordPanel.SubmitPassword(" protected ");
        Check(passwordPanel.JoinCalls == 1 && ReferenceEquals(passwordPanel.LastJoined, passwordLobby) && passwordPanel.LastPassword == " protected ",
            "An open password prompt keeps its original server while the browser refreshes");
        passwordPanel.SubmitPassword(" protected ");
        Check(passwordPanel.JoinCalls == 1, "A closed password prompt cannot submit twice");
        return checks;
    }
}

namespace Steamworks
{
    public readonly record struct CSteamID(ulong m_SteamID);
}

namespace NuclearOption.Networking.Lobbies
{
    // Native metadata/Steam boundary only. The refresh, identity matching,
    // selection, detail updates and join routing are the production partial.
    public sealed partial class LobbyInstance
    {
        public readonly CSteamID LobbyId;
        public string Name;
        public int Order;
        public bool Full, Password, Hidden;
        public string PasswordValue = "";
        public LobbyInstance(ulong id, string name, int order)
        {
            LobbyId = new CSteamID(id);
            Name = name;
            Order = order;
        }
    }

    public static partial class LobbyPassword
    {
        // Native SHA-256/Steam metadata stays outside this fixture. Its exact
        // string comparison contract must not normalize the submitted password.
        public static bool TestShortPassword(LobbyInstance lobby, string password) =>
            !lobby.Password || lobby.PasswordValue == password;
    }
}

namespace NOVR.VrUi.Native
{
    public partial class NativeGameActionAdapter
    {
        internal readonly List<LobbyInstance> Joins = new();
        internal readonly List<string?> Passwords = new();
        public bool TryJoinLobby(LobbyInstance lobby, string? password, bool promptIfPasswordNeeded)
        {
            Joins.Add(lobby);
            Passwords.Add(password);
            return true;
        }
    }

    public partial class NativeMultiplayerPanel : UnityEngine.MonoBehaviour
    {
        private const int PageSize = 10;
        private readonly List<NativeLobbyEntry> _lobbies = new(), _visibleLobbies = new();
        private readonly NativeGameActionAdapter _actions = new();
        private int _selectedIndex = -1, _page;
        private readonly TextProbe _titleText = new(), _summaryText = new(), _descriptionText = new(), _statusText = new();
        private readonly ButtonProbe _joinButton = new();
        private NativeLobbyEntry? _pendingPasswordLobby;
        private readonly InputProbe _joinPasswordInput = new();
        private readonly TextProbe _passwordStatusText = new();
        internal int Selected => _selectedIndex;
        internal int Page => _page;
        internal string Title => _titleText.text;
        internal bool JoinEnabled => _joinButton.interactable;
        internal int JoinCalls => _actions.Joins.Count;
        internal LobbyInstance? LastJoined => _actions.Joins.LastOrDefault();
        internal string? LastPassword => _actions.Passwords.LastOrDefault();
        internal bool PasswordPending => _pendingPasswordLobby.HasValue;
        internal string PasswordStatus => _passwordStatusText.text;
        internal LobbyInstance? PasswordLobby;
        internal void Select(int index) => SelectLobby(index);
        internal void Join() => JoinSelectedLobby();
        internal void SubmitPassword(string? password)
        {
            _joinPasswordInput.text = password;
            SubmitJoinPassword();
        }
        internal void ResetFilters() => ReapplyFiltersFromFirstPage();
        internal void Reopen() => ResetLobbySelection();
        internal void Replace(params LobbyInstance[] lobbies)
        {
            _lobbies.Clear();
            _lobbies.AddRange(lobbies.Select(lobby => new NativeLobbyEntry(lobby)));
            ApplyFiltersAndSort();
        }
        private static bool MatchesFilters(NativeLobbyEntry entry) => !entry.Lobby.Hidden;
        private static int CompareLobbies(NativeLobbyEntry a, NativeLobbyEntry b) => a.Lobby.Order.CompareTo(b.Lobby.Order);
        private int GetLastPage() => Math.Max(0, (_visibleLobbies.Count - 1) / PageSize);
        private void UpdateFilterLabels()
        {
        }
        private void RefreshList()
        {
        }
        private void ShowPasswordPanel(NativeLobbyEntry entry)
        {
            PasswordLobby = entry.Lobby;
            _pendingPasswordLobby = entry;
            _joinPasswordInput.text = "";
        }
        private void HidePasswordPanel()
        {
            PasswordLobby = null;
            _pendingPasswordLobby = null;
        }
        private void SetStatus(string value) => _statusText.text = value;
        private sealed class TextProbe
        {
            internal string text = "";
        }
        private sealed class ButtonProbe
        {
            internal bool interactable;
        }
        private sealed class InputProbe
        {
            internal string? text = "";
        }
        private readonly partial struct NativeLobbyEntry
        {
            internal readonly LobbyInstance Lobby;
            internal NativeLobbyEntry(LobbyInstance lobby) => Lobby = lobby;
            internal string LobbyName => Lobby.Name;
            internal string MissionName => Lobby.QueryMissionName;
            internal string MapName => Lobby.QueryMapName;
            internal string PlayerCountText => "1/8";
            internal string PingText => "40 ms";
            internal string StatusText => Lobby.QueryStatus;
            internal string Description => Lobby.QueryDescription;
            internal bool IsFull => Lobby.Full;
            internal bool HasPassword => Lobby.Password;
        }
    }
}
