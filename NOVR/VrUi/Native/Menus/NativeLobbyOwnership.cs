using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using NuclearOption.Networking;
using NuclearOption.Networking.Lobbies;
using Steamworks;

namespace NOVR.VrUi.Native;

// SteamLobby.LeaveLobby clears joined state and global rich presence as well as
// its hosted lobby. It is only safe for our failed request while all ownership
// checks below still match. Missing/changed native fields fail closed.
internal static class NativeLobbyOwnership
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo? Hosted = typeof(SteamLobby).GetField("_hostedLobby", Fields);
    private static readonly FieldInfo? Joined = typeof(SteamLobby).GetField("_joinedLobby", Fields);
    private static readonly FieldInfo? PendingJoin = typeof(SteamLobby).GetField("joinPendingLobby", Fields);
    private static readonly FieldInfo? PendingHost = typeof(SteamLobby).GetField("HostLobbyCompletion", Fields);
    private static readonly FieldInfo? Initialized = typeof(SteamLobby).GetField("_initialized", Fields);

    internal static bool CanBegin(SteamLobby? lobby, NetworkManagerNuclearOption? manager) =>
        IsIdleOwner(lobby, manager) && TryRead(lobby!, out var hosted) && !hosted.IsValid;

    internal static bool OwnsIdleLobby(SteamLobby? lobby, NetworkManagerNuclearOption? manager, HostedLobbyInstance owned) =>
        owned.IsValid && IsIdleOwner(lobby, manager) && TryRead(lobby!, out var hosted) && hosted.Id == owned.Id;

    internal static bool TryRelease(SteamLobby lobby, NetworkManagerNuclearOption manager, HostedLobbyInstance owned)
    {
        if (!OwnsIdleLobby(lobby, manager, owned))
            return false;
        lobby.LeaveLobby();
        return true;
    }

    private static bool IsIdleOwner(SteamLobby? lobby, NetworkManagerNuclearOption? manager) =>
        lobby != null && manager != null && ReferenceEquals(SteamLobby.instance, lobby) &&
        ReferenceEquals(NetworkManagerNuclearOption.i, manager) && ReferenceEquals(lobby.Manager, manager) &&
        manager.Server != null && manager.Client != null && !manager.Server.Active && !manager.Client.Active;

    private static bool TryRead(SteamLobby lobby, out HostedLobbyInstance hosted)
    {
        hosted = default;
        if (Hosted?.FieldType != typeof(HostedLobbyInstance) || Joined?.FieldType != typeof(LobbyInstance) ||
            PendingJoin?.FieldType != typeof(LobbyInstance) || Initialized?.FieldType != typeof(bool) ||
            PendingHost?.FieldType != typeof(UniTaskCompletionSource<LobbyCreated_t>))
            return false;
        try
        {
            if (!(Initialized.GetValue(lobby) is true) || Joined.GetValue(lobby) != null ||
                PendingJoin.GetValue(lobby) != null || PendingHost.GetValue(lobby) != null ||
                !(Hosted.GetValue(lobby) is HostedLobbyInstance current))
                return false;
            hosted = current;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
