using System.Runtime.CompilerServices;
using Cysharp.Threading.Tasks;
using NuclearOption.Networking;
using NuclearOption.Networking.Lobbies;
using NuclearOption.SavedMission;
using NuclearOption.SceneLoading;
using Steamworks;

namespace Cysharp.Threading.Tasks
{
    public sealed class UniTaskCompletionSource<T> { }
    // Real Task continuations control timing; the production host method and
    // its exception/finally paths compile unchanged, without Steam or Unity.
    [AsyncMethodBuilder(typeof(HostTaskBuilder))]
    public readonly struct UniTaskVoid(Task task)
    {
        internal static readonly List<Task> Started = new();
        public void Forget() => Started.Add(task);
    }
    public struct HostTaskBuilder
    {
        private AsyncTaskMethodBuilder _builder;
        public static HostTaskBuilder Create() => new() { _builder = AsyncTaskMethodBuilder.Create() };
        public UniTaskVoid Task => new(_builder.Task);
        public void SetResult() => _builder.SetResult();
        public void SetException(Exception error) => _builder.SetException(error);
        public void SetStateMachine(IAsyncStateMachine machine) => _builder.SetStateMachine(machine);
        public void Start<T>(ref T machine) where T : IAsyncStateMachine => _builder.Start(ref machine);
        public void AwaitOnCompleted<TA, TS>(ref TA awaiter, ref TS machine)
            where TA : INotifyCompletion where TS : IAsyncStateMachine => _builder.AwaitOnCompleted(ref awaiter, ref machine);
        public void AwaitUnsafeOnCompleted<TA, TS>(ref TA awaiter, ref TS machine)
            where TA : ICriticalNotifyCompletion where TS : IAsyncStateMachine => _builder.AwaitUnsafeOnCompleted(ref awaiter, ref machine);
    }
}

namespace UnityEngine
{
    public static class Debug
    {
        public static readonly List<string> Messages = new();
        public static void Log(string text) => Messages.Add(text);
        public static void LogWarning(string text) => Messages.Add(text);
        public static void LogError(string text) => Messages.Add(text);
    }
}

public enum GameState { Menu, SinglePlayer, Multiplayer }
public static class GameManager
{
    public static GameState gameState;
    public static readonly ChangeEvent OnGameStateChanged = new();
    public static void SetGameState(GameState state)
    {
        gameState = state;
        OnGameStateChanged.Invoke();
    }
    public sealed class ChangeEvent
    {
        private event Action? Changed;
        public bool HasInvoked = true;
        public int Count => Changed?.GetInvocationList().Length ?? 0;
        public void AddListener(Action action)
        {
            if (HasInvoked)
                action();
            Changed += action;
        }
        public void RemoveListener(Action action) => Changed -= action;
        public void Invoke() => Changed?.Invoke();
    }
}
public static class MissionManager
{
    public static Mission? CurrentMission;
    public static event Action<Mission>? onMissionLoad;
    public static Action? AfterSet;
    public static int Listeners => onMissionLoad?.GetInvocationList().Length ?? 0;
    public static void SetMission(Mission mission, bool checkIfSame)
    {
        CurrentMission = mission;
        onMissionLoad?.Invoke(mission);
        AfterSet?.Invoke();
    }
}
public static class PlayerSettings { public static string playerName_Unsanitized = "Pilot"; }
public readonly record struct MissionTag(string Tag)
{
    public static readonly MissionTag SinglePlayer = new("Single Player");
    public static string GetPvpTypeLobbyString(Mission mission) => "0";
}
public static class RichTextBoundary { public static string SanitizeRichText(this string value, int maximum) => value; }

namespace NuclearOption.SceneLoading
{
    public readonly record struct MapKey(string Name);
    public sealed class MapLoader : UnityEngine.Object
    {
        public bool TryGetMapName(MapKey key, out string name) { name = key.Name; return true; }
    }
}
namespace NuclearOption.SavedMission
{
    public readonly partial record struct MissionKey
    {
        public static readonly Dictionary<string, Mission> Loads = new();
        public PublishedFileId_t? WorkshopId => null;
        public bool TryLoad(out Mission mission, out string error)
        {
            var loaded = Loads.TryGetValue(Name, out mission!);
            error = loaded ? "" : "Missing mission";
            return loaded;
        }
    }
    public sealed partial class Mission
    {
        public MapKey MapKey = new("Map");
        public MissionKey? LoadKey;
        public readonly Settings missionSettings = new();
        public sealed class Settings { public string description = "Description"; public bool allowEventContent; }
    }
}
namespace Steamworks
{
    public readonly struct LobbyCreated_t { }
    public enum ELobbyType { k_ELobbyTypePublic, k_ELobbyTypeFriendsOnly, k_ELobbyTypePrivate }
    public readonly record struct PublishedFileId_t(ulong m_PublishedFileId);
}
namespace NuclearOption.Networking
{
    public enum SocketType { Steam, Offline }
    public sealed class HostOptions(SocketType socket, GameState state, MapKey map)
    {
        public readonly SocketType Socket = socket;
        public readonly GameState State = state;
        public readonly MapKey Map = map;
        public int? MaxConnections;
        public string? Password;
    }
    public sealed class NetworkManagerNuclearOption : UnityEngine.Object
    {
        public static NetworkManagerNuclearOption i = null!;
        public static bool? ModdedServer;
        public Transport Server = new(), Client = new();
        public readonly List<HostOptions> Starts = new();
        public bool FailBeforeActivation;
        public TaskCompletionSource<bool>? StartCompletion;
        public Task StartHostAsync(HostOptions options)
        {
            Starts.Add(options);
            if (FailBeforeActivation)
                throw new InvalidOperationException("Simulated start failure");
            Server.Active = true;
            GameManager.SetGameState(options.State);
            StartCompletion = new();
            return StartCompletion.Task;
        }
        public sealed class Transport : UnityEngine.Object { public bool Active; }
    }
}
namespace NuclearOption.Networking.Lobbies
{
    public readonly struct HostedLobbyInstance(CSteamID id)
    {
        public readonly CSteamID Id = id;
        public bool IsValid => Id.m_SteamID != 0;
        public static readonly List<(CSteamID id, string key, string value)> Writes = new();
        public void SetData(string key, string value) => Writes.Add((Id, key, value));
    }
    public sealed partial class LobbyInstance
    {
        public static string BoolToTag(bool value) => value ? "1" : "0";
    }
    public static partial class LobbyPassword
    {
        public static string GetShortPassword(string? password) => password ?? "";
    }
    public sealed class SteamLobby : UnityEngine.Object
    {
        public static SteamLobby instance = null!;
        public NetworkManagerNuclearOption Manager = null!;
        private HostedLobbyInstance _hostedLobby;
        private LobbyInstance? _joinedLobby, joinPendingLobby;
        private UniTaskCompletionSource<LobbyCreated_t>? HostLobbyCompletion;
        private bool _initialized = true;
        public string CurrentLobbyName = "";
        public readonly List<Request> Requests = new();
        public readonly List<CSteamID> Leaves = new();
        public HostedLobbyInstance Hosted { get => _hostedLobby; set => _hostedLobby = value; }
        public LobbyInstance? Joined { get => _joinedLobby; set => _joinedLobby = value; }
        public LobbyInstance? PendingJoin { get => joinPendingLobby; set => joinPendingLobby = value; }
        public UniTaskCompletionSource<LobbyCreated_t>? PendingHost { get => HostLobbyCompletion; set => HostLobbyCompletion = value; }
        public bool Initialized { get => _initialized; set => _initialized = value; }
        public void CheckRelayLocationTask() { }
        public Task<HostedLobbyInstance?> HostLobby(int players, ELobbyType visibility)
        {
            var request = new Request(this);
            Requests.Add(request);
            HostLobbyCompletion = new();
            return request.Completion.Task;
        }
        public void LeaveLobby()
        {
            if (!_initialized)
                return;
            Leaves.Add(_hostedLobby.Id);
            _hostedLobby = default;
            _joinedLobby = null;
        }
        public sealed class Request(SteamLobby owner)
        {
            internal readonly TaskCompletionSource<HostedLobbyInstance?> Completion = new();
            public Action? BeforeComplete;
            public void Finish(ulong id)
            {
                owner.HostLobbyCompletion = null;
                HostedLobbyInstance? result = id == 0 ? null : new HostedLobbyInstance(new CSteamID(id));
                if (result.HasValue)
                    owner._hostedLobby = result.Value;
                BeforeComplete?.Invoke();
                Completion.SetResult(result);
            }
            public void Fail()
            {
                owner.HostLobbyCompletion = null;
                Completion.SetException(new InvalidOperationException("Simulated Steam failure"));
            }
        }
    }
}
namespace NOVR.VrUi.Native
{
    public partial class NativeMultiplayerPanel
    {
        private readonly List<HostMissionEntry> _hostMissions = new();
        private int _hostSelectedMissionIndex;
        private int _hostMaxPlayers = 8;
        private HostLobbyVisibility _hostVisibility = HostLobbyVisibility.Public;
        private bool _hostPasswordEnabled;
        private readonly InputProbe _hostNameInput = new(), _hostPasswordInput = new();
        private readonly TextProbe _hostStatusText = new();
        private bool _wasVisible = true;
        internal bool HostingBusy => _isHosting;
        internal string HostStatus => _hostStatusText.text;
        internal void HideHostView() => _wasVisible = false;
        internal Task ClickHost(Mission mission, string? password = null)
        {
            var key = new MissionKey(mission.Name);
            MissionKey.Loads[key.Name] = mission;
            _hostMissions.Clear();
            _hostMissions.Add(new HostMissionEntry(key));
            _hostSelectedMissionIndex = 0;
            _hostPasswordEnabled = password != null;
            _hostPasswordInput.text = password;
            var before = UniTaskVoid.Started.Count;
            HostLobbyClicked();
            return UniTaskVoid.Started.Count == before ? Task.CompletedTask : UniTaskVoid.Started[^1];
        }
        private readonly record struct HostMissionEntry(MissionKey Key);
        private enum HostLobbyVisibility { Public, FriendsOnly, Private }
    }
}
