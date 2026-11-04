using NuclearOption.Networking.Lobbies;

namespace NuclearOption.Networking.Lobbies
{
    public enum MissionPvpType
    {
        Any, Pve, Pvp
    }

    public sealed partial class LobbyInstance
    {
        internal MissionPvpType QueryMissionType = MissionPvpType.Pve;
        internal bool QueryModded, QueryDedicated;
        internal int QueryPlayers = 1;
        internal int? QueryPing = 40;
        internal string QueryMissionName = "Mission", QueryMapName = "Map", QueryStatus = "Ready", QueryDescription = "Description";
    }
}

namespace NOVR.VrUi.Native
{
    public partial class NativeMultiplayerPanel
    {
        // The query is the production implementation. Only native lobby
        // metadata is doubled; no UI, Steam, or network operations are needed.
        internal static int RunBrowserQueryChecks()
        {
            var checks = 0;
            void Check(bool value, string message)
            {
                if (!value)
                    throw new Exception(message);
                checks++;
            }
            var native = new LobbyInstance(1, "Alpha Squadron", 0);
            var entry = new NativeLobbyEntry(native);
            var query = new LobbyBrowserQuery();
            Check(query.Matches(entry, ""), "Default browser filters include an ordinary open lobby");
            native.QueryPing = null;
            Check(query.Matches(entry, ""), "Unknown ping remains visible when neither ping filter is set");

            var toggles = new (Action<bool> metadata, Action<LobbyBrowserQuery, bool> filter, string name)[]
            {
                (value => native.Password = value, (q, value) => q.ShowPasswordedLobbies = value, "passworded"),
                (value => native.Password = !value, (q, value) => q.ShowOpenLobbies = value, "open"),
                (value => native.QueryModded = value, (q, value) => q.ShowModdedLobbies = value, "modded"),
                (value => native.QueryModded = !value, (q, value) => q.ShowVanillaLobbies = value, "vanilla"),
                (value => native.QueryDedicated = value, (q, value) => q.ShowDedicatedServers = value, "dedicated"),
                (value => native.QueryDedicated = !value, (q, value) => q.ShowPlayerHostedServers = value, "player hosted")
            };
            foreach (var (metadata, filter, name) in toggles)
            {
                query = new();
                metadata(true);
                filter(query, false);
                Check(!query.Matches(entry, ""), "Disabled " + name + " category is excluded");
                metadata(false);
                Check(query.Matches(entry, ""), "The complementary " + name + " category remains visible");
            }

            query = new();
            native.QueryMissionType = MissionPvpType.Pve;
            query.ShowPve = false;
            Check(!query.Matches(entry, ""), "PvE can be hidden independently");
            native.QueryMissionType = MissionPvpType.Pvp;
            Check(query.Matches(entry, ""), "PvP remains visible while PvE is hidden");
            native.QueryMissionType = MissionPvpType.Any;
            Check(query.Matches(entry, ""), "Unspecified mission type is visible when either type is enabled");
            query.ShowPvp = false;
            Check(!query.Matches(entry, ""), "Unspecified mission type is hidden when both types are disabled");

            query = new()
            {
                HideFullLobbies = true,
                HideEmptyLobbies = true
            };
            native.Full = true;
            Check(!query.Matches(entry, ""), "Full lobbies are hidden by availability filter");
            native.Full = false;
            native.QueryPlayers = 0;
            Check(!query.Matches(entry, ""), "Zero players counts as empty");
            native.QueryPlayers = -1;
            Check(!query.Matches(entry, ""), "Unresolved negative player count retains previous empty behavior");
            native.QueryPlayers = 1;
            Check(query.Matches(entry, ""), "An available populated lobby passes availability filters");

            query = new();
            native.QueryMissionName = "Desert Patrol";
            native.QueryMapName = "Ridge";
            native.QueryDescription = "Night operations";
            native.QueryDedicated = true;
            native.QueryStatus = "Password Ready";
            Check(query.Matches(entry, "  ALPHA  ridge night dedicated ready "),
                "Search matches all terms case-insensitively across name, map, description, server type and status");
            Check(query.Matches(entry, "patrol") && query.Matches(entry, "   "), "Mission search and blank search remain supported");
            Check(!query.Matches(entry, "alpha absent"), "Every nonblank search term must match some metadata field");
            Check(!query.Matches(entry, "alpha\tridge"), "Search preserves its existing space-only token separator");

            query.CycleMaxPingFilter(1);
            Check(query.GetMaxPingFilterLabel() == "75 ms" && !query.Matches(entry, ""), "A configured ping cap excludes unknown ping");
            native.QueryPing = 75;
            Check(query.Matches(entry, ""), "Ping equal to the limit remains included");
            native.QueryPing = 76;
            Check(!query.Matches(entry, ""), "Ping above the limit is excluded");
            query.CycleMaxPingFilter(3);
            query.CycleDistanceFilter(-1);
            Check(query.GetMaxPingFilterLabel() == "400 ms" && query.GetDistanceFilterLabel() == "WORLD", "Both filter cycles retain their labels and reverse wrapping");
            native.QueryPing = 301;
            Check(!query.Matches(entry, ""), "Distance and explicit ping filters use their stricter limit");
            native.QueryPing = 300;
            Check(query.Matches(entry, ""), "Worldwide distance includes its exact 300ms boundary");
            query.CycleMaxPingFilter(1);
            query.CycleDistanceFilter(1);
            native.QueryPing = null;
            Check(query.GetMaxPingFilterLabel() == "ANY" && query.GetDistanceFilterLabel() == "ANY" && query.Matches(entry, ""),
                "Forward wrapping clears both caps and restores unknown-ping lobbies");

            var left = new LobbyInstance(2, "Zulu", 0)
            {
                QueryMissionType = MissionPvpType.Pvp,
                Password = true,
                QueryModded = false,
                QueryDedicated = false,
                QueryMissionName = "Zulu Mission",
                QueryPlayers = 5,
                QueryPing = null
            };
            var right = new LobbyInstance(3, "Alpha", 0)
            {
                QueryMissionType = MissionPvpType.Pve,
                Password = false,
                QueryModded = true,
                QueryDedicated = true,
                QueryMissionName = "Alpha Mission",
                QueryPlayers = 2,
                QueryPing = 40
            };
            foreach (LobbySortColumn column in Enum.GetValues(typeof(LobbySortColumn)))
            {
                query = new()
                {
                    SortColumn = (LobbySortColumn)(-1)
                };
                query.SetSortColumn(column);
                var expected = column == LobbySortColumn.Players ? -1 : 1;
                Check(Math.Sign(query.Compare(new(left), new(right))) == expected,
                    column + " retains its primary comparison and default direction");
                query.SetSortColumn(column);
                Check(Math.Sign(query.Compare(new(left), new(right))) == -expected,
                    column + " reverses when its selected header is clicked again");
            }
            right.QueryPlayers = left.QueryPlayers;
            query = new()
            {
                SortColumn = LobbySortColumn.Players,
                SortAscending = false
            };
            Check(query.Compare(new(left), new(right)) > 0, "Equal primary values retain ascending lobby-name tie-breaking even under descending sort");
            return checks;
        }

        private readonly partial struct NativeLobbyEntry
        {
            internal MissionPvpType MissionType => Lobby.QueryMissionType;
            internal bool IsModded => Lobby.QueryModded;
            internal bool IsDedicated => Lobby.QueryDedicated;
            internal int CurrentPlayers => Lobby.QueryPlayers;
            internal int? Ping => Lobby.QueryPing;
            internal string MissionTypeText => MissionType == MissionPvpType.Pve ? "PvE" : MissionType == MissionPvpType.Pvp ? "PvP" : "Any";
            internal string AccessText => HasPassword ? "PWD" : "OPEN";
            internal string ModsText => IsModded ? "MOD" : "VANILLA";
            internal string ServerColumnText => IsDedicated ? "DED" : "PLAYER";
            internal string ServerTypeText => IsDedicated ? "Dedicated" : "Player Host";
        }
    }
}
