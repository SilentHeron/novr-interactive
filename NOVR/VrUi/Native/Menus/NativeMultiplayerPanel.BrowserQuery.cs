using System;
using NuclearOption.Networking.Lobbies;
using UnityEngine;

namespace NOVR.VrUi.Native;

public partial class NativeMultiplayerPanel
{
    // Filter/sort state has no UI references. The view passes the current
    // search text explicitly; native lobby discovery remains with the panel.
    private sealed class LobbyBrowserQuery
    {
        private static readonly int?[] MaxPingFilters = { null, 75, 150, 250, 400 };

        private static readonly PingDistanceFilter[] DistanceFilters =
        {
            PingDistanceFilter.Any,
            PingDistanceFilter.Nearby,
            PingDistanceFilter.Regional,
            PingDistanceFilter.Worldwide
        };

        public LobbySortColumn SortColumn = LobbySortColumn.Lobby;

        public bool SortAscending = true;

        public bool ShowPve = true;

        public bool ShowPvp = true;

        public bool ShowOpenLobbies = true;

        public bool ShowPasswordedLobbies = true;

        public bool ShowVanillaLobbies = true;

        public bool ShowModdedLobbies = true;

        public bool ShowDedicatedServers = true;

        public bool ShowPlayerHostedServers = true;

        public bool HideFullLobbies;

        public bool HideEmptyLobbies;

        public int MaxPingFilterIndex;

        public PingDistanceFilter DistanceFilter = PingDistanceFilter.Any;

        public bool Matches(NativeLobbyEntry lobby, string searchText)
        {
            var missionMatches = lobby.MissionType switch
            {
                MissionPvpType.Pve => ShowPve,
                MissionPvpType.Pvp => ShowPvp,
                _ => ShowPve || ShowPvp
            };
            if (!missionMatches)
                return false;

            if (lobby.HasPassword && !ShowPasswordedLobbies)
                return false;
            if (!lobby.HasPassword && !ShowOpenLobbies)
                return false;

            if (lobby.IsModded && !ShowModdedLobbies)
                return false;
            if (!lobby.IsModded && !ShowVanillaLobbies)
                return false;

            if (lobby.IsDedicated && !ShowDedicatedServers)
                return false;
            if (!lobby.IsDedicated && !ShowPlayerHostedServers)
                return false;
            if (HideFullLobbies && lobby.IsFull)
                return false;
            if (HideEmptyLobbies && lobby.CurrentPlayers <= 0)
                return false;
            if (!MatchesSearch(lobby, searchText))
                return false;

            var pingLimit = GetActivePingLimit();
            if (pingLimit.HasValue && (!lobby.Ping.HasValue || lobby.Ping.Value > pingLimit.Value))
            {
                return false;
            }

            return true;
        }

        private static bool MatchesSearch(NativeLobbyEntry lobby, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            var terms = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < terms.Length; index++)
            {
                var term = terms[index];
                if (!ContainsSearchTerm(lobby.LobbyName, term) &&
                    !ContainsSearchTerm(lobby.MissionName, term) &&
                    !ContainsSearchTerm(lobby.MapName, term) &&
                    !ContainsSearchTerm(lobby.Description, term) &&
                    !ContainsSearchTerm(lobby.ServerTypeText, term) &&
                    !ContainsSearchTerm(lobby.StatusText, term))
                {
                    return false;
                }
            }

            return true;
        }

        public int Compare(NativeLobbyEntry left, NativeLobbyEntry right)
        {
            var comparison = SortColumn switch
            {
                LobbySortColumn.Type => CompareText(left.MissionTypeText, right.MissionTypeText),
                LobbySortColumn.Password => CompareText(left.AccessText, right.AccessText),
                LobbySortColumn.Mods => CompareText(left.ModsText, right.ModsText),
                LobbySortColumn.Server => CompareText(left.ServerColumnText, right.ServerColumnText),
                LobbySortColumn.Lobby => CompareText(left.LobbyName, right.LobbyName),
                LobbySortColumn.Mission => CompareText(left.MissionName, right.MissionName),
                LobbySortColumn.Players => left.CurrentPlayers.CompareTo(right.CurrentPlayers),
                LobbySortColumn.Ping => NullableIntSortKey(left.Ping).CompareTo(NullableIntSortKey(right.Ping)),
                _ => CompareText(left.LobbyName, right.LobbyName)
            };

            if (!SortAscending)
            {
                comparison = -comparison;
            }

            return comparison != 0 ? comparison : CompareText(left.LobbyName, right.LobbyName);
        }

        public void SetSortColumn(LobbySortColumn column)
        {
            if (SortColumn == column)
            {
                SortAscending = !SortAscending;
            }
            else
            {
                SortColumn = column;
                SortAscending = column != LobbySortColumn.Players;
            }

        }

        public void CycleMaxPingFilter(int direction)
        {
            MaxPingFilterIndex = WrapIndex(MaxPingFilterIndex + direction, MaxPingFilters.Length);
        }

        public void CycleDistanceFilter(int direction)
        {
            var currentIndex = Array.IndexOf(DistanceFilters, DistanceFilter);
            if (currentIndex < 0)
                currentIndex = 0;

            DistanceFilter = DistanceFilters[WrapIndex(currentIndex + direction, DistanceFilters.Length)];
        }

        private int? GetActivePingLimit()
        {
            var maxPingLimit = MaxPingFilters[Mathf.Clamp(MaxPingFilterIndex, 0, MaxPingFilters.Length - 1)];
            var distanceLimit = GetDistancePingLimit(DistanceFilter);
            if (!maxPingLimit.HasValue)
                return distanceLimit;
            if (!distanceLimit.HasValue)
                return maxPingLimit;

            return Math.Min(maxPingLimit.Value, distanceLimit.Value);
        }

        public string GetMaxPingFilterLabel()
        {
            var limit = MaxPingFilters[Mathf.Clamp(MaxPingFilterIndex, 0, MaxPingFilters.Length - 1)];
            return limit.HasValue ? $"{limit.Value} ms" : "ANY";
        }

        private static int? GetDistancePingLimit(PingDistanceFilter filter)
        {
            return filter switch
            {
                PingDistanceFilter.Nearby => 75,
                PingDistanceFilter.Regional => 150,
                PingDistanceFilter.Worldwide => 300,
                _ => null
            };
        }

        public string GetDistanceFilterLabel()
        {
            return DistanceFilter switch
            {
                PingDistanceFilter.Nearby => "NEAR",
                PingDistanceFilter.Regional => "REGION",
                PingDistanceFilter.Worldwide => "WORLD",
                _ => "ANY"
            };
        }

        private static int WrapIndex(int index, int count)
        {
            if (count <= 0)
                return 0;

            var wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        private static int CompareText(string left, string right)
        {
            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsSearchTerm(string value, string term)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int NullableIntSortKey(int? value)
        {
            return value.GetValueOrDefault(int.MaxValue);
        }

    }

    private enum LobbySortColumn
    {
        Type,
        Password,
        Mods,
        Server,
        Lobby,
        Mission,
        Players,
        Ping
    }

    private enum PingDistanceFilter
    {
        Any,
        Nearby,
        Regional,
        Worldwide
    }
}
