using System;
using System.Collections.Generic;
using NuclearOption.Networking.Lobbies;

namespace NOVR.VrUi.Native;

public partial class NativeMultiplayerPanel
{
    private readonly struct NativeLobbyEntry
    {
        public NativeLobbyEntry(LobbyListItem item)
        {
            Lobby = item.lobby;
            LobbyName = string.IsNullOrWhiteSpace(item.LobbyName) ? "Unnamed Lobby" : item.LobbyName;
            MissionName = string.IsNullOrWhiteSpace(item.MissionName) ? "Unknown Mission" : item.MissionName;
            MapName = string.IsNullOrWhiteSpace(item.MapName) ? "Unknown Map" : item.MapName;
            Ping = item.Ping;
            PingText = Ping.HasValue ? $"{Ping.Value} ms" : "--";
            IsFull = item.IsFull;
            IsDedicated = item.lobby.DedicatedServer || item.IsServer;
            ServerTypeText = IsDedicated ? "Dedicated" : "Player Host";
            ServerColumnText = IsDedicated ? "DED" : "PLAYER";
            IsModded = item.lobby.ModdedServer;
            ModsText = IsModded ? "MOD" : "VANILLA";
            HasPassword = item.lobby.IsPasswordProtected(out _);
            AccessText = HasPassword ? "PWD" : "OPEN";
            MissionType = item.lobby.MissionPvpType;
            MissionTypeText = MissionTypeLabel(MissionType);
            Description = string.IsNullOrWhiteSpace(item.lobby.MissionDescriptionSanitized)
                ? "No mission description available."
                : item.lobby.MissionDescriptionSanitized;

            if (item.lobby.GetPlayerCounts(out var currentPlayers, out var maxPlayers))
            {
                CurrentPlayers = currentPlayers;
                MaxPlayers = maxPlayers;
                PlayerCountText = $"{currentPlayers}/{maxPlayers}";
            }
            else
            {
                CurrentPlayers = item.PlayerCount;
                MaxPlayers = 0;
                PlayerCountText = $"{item.PlayerCount}";
            }

            FlagsText = BuildFlagsText(HasPassword, IsModded, IsFull, IsDedicated, MissionType);
            StatusText = $"{ServerTypeText}   {MissionTypeLabel(MissionType)}   {(HasPassword ? "Password" : "Open")}   {(IsModded ? "Modded" : "Vanilla")}";
            if (IsFull)
            {
                StatusText += "   Full";
            }
        }

        public LobbyInstance Lobby
        {
            get;
        }
        public string LobbyName
        {
            get;
        }
        public string MissionName
        {
            get;
        }
        public string MapName
        {
            get;
        }
        public string FlagsText
        {
            get;
        }
        public string PlayerCountText
        {
            get;
        }
        public int CurrentPlayers
        {
            get;
        }
        public int MaxPlayers
        {
            get;
        }
        public int? Ping
        {
            get;
        }
        public string PingText
        {
            get;
        }
        public bool IsFull
        {
            get;
        }
        public bool IsDedicated
        {
            get;
        }
        public bool IsModded
        {
            get;
        }
        public bool HasPassword
        {
            get;
        }
        public MissionPvpType MissionType
        {
            get;
        }
        public string MissionTypeText
        {
            get;
        }
        public string AccessText
        {
            get;
        }
        public string ModsText
        {
            get;
        }
        public string ServerColumnText
        {
            get;
        }
        public string ServerTypeText
        {
            get;
        }
        public string StatusText
        {
            get;
        }
        public string Description
        {
            get;
        }

        private static string BuildFlagsText(bool hasPassword, bool isModded, bool isFull, bool isDedicated, MissionPvpType missionType)
        {
            var flags = new List<string>();
            flags.Add(MissionTypeLabel(missionType));
            if (hasPassword)
                flags.Add("PWD");
            if (isModded)
                flags.Add("MOD");
            if (isFull)
                flags.Add("FULL");
            if (isDedicated)
                flags.Add("DED");

            return string.Join(" ", flags);
        }

        private static string MissionTypeLabel(MissionPvpType missionType)
        {
            return missionType switch
            {
                MissionPvpType.Pve => "PvE",
                MissionPvpType.Pvp => "PvP",
                _ => "Any"
            };
        }
    }
}
