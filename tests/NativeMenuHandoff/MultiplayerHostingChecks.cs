using NuclearOption.Networking;
using NuclearOption.Networking.Lobbies;
using NuclearOption.SavedMission;
using NOVR.VrUi.Native;
using Steamworks;
using UnityEngine.SceneManagement;

internal static class MultiplayerHostingChecks
{
    internal static async Task<int> Run()
    {
        var checks = 0;
        void Check(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
            checks++;
        }
        void CheckReleased(NativeMultiplayerPanel panel)
        {
            Check(!panel.HostingBusy, "Every terminal host path releases its duplicate-click gate");
            Check(GameManager.OnGameStateChanged.Count == 0 && MissionManager.Listeners == 0 && SceneManager.Subscribers == 0,
                "Every terminal host path unsubscribes all context observers");
        }

        var run = Create();
        var task = run.panel.ClickHost(run.requested, " raw password ");
        Check(run.panel.HostingBusy && ReferenceEquals(MissionManager.CurrentMission, run.original),
            "Creating a Steam lobby does not unload or replace the current mission");
        await run.panel.ClickHost(run.requested).WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.service.Requests.Count == 1, "Duplicate host clicks cannot create concurrent Steam requests");
        var status = run.panel.HostStatus;
        run.panel.HideHostView();
        run.service.Requests[0].Finish(100);
        Check(run.manager.Starts.Count == 1 && ReferenceEquals(MissionManager.CurrentMission, run.requested),
            "Hiding presentation alone does not cancel an accepted host operation");
        Check(run.manager.Starts[0].Password == " raw password " && run.manager.Starts[0].MaxConnections == 7,
            "Captured launch options retain the requested password and capacity");
        Check(run.panel.HostStatus == status, "Hidden presentation receives no stale hosting feedback");
        run.manager.StartCompletion!.SetResult(true);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.service.Leaves.Count == 0, "Successful active hosting is not cleaned up as an abandoned request");
        CheckReleased(run.panel);

        var invalidations = new (string name, Action<NativeMultiplayerPanel, SteamLobby, NetworkManagerNuclearOption, Mission> change, bool release)[]
        {
            ("mission changed away and back", (_, _, _, original) =>
            {
                MissionManager.SetMission(new Mission { Name = "Other" }, false);
                MissionManager.SetMission(original, false);
            }, true),
            ("game state changed away and back", (_, _, _, _) =>
            {
                GameManager.SetGameState(GameState.SinglePlayer);
                GameManager.SetGameState(GameState.Menu);
            }, true),
            ("scene changed away and back", (_, _, _, _) =>
            {
                SceneManager.SetActive(new Scene(2));
                SceneManager.SetActive(new Scene(1));
            }, true),
            ("destroyed presentation", (panel, _, _, _) => panel.Destroyed = true, true),
            ("replaced Steam service", (_, _, manager, _) => SteamLobby.instance = new SteamLobby { Manager = manager }, false),
            ("replaced network manager", (_, _, _, _) => NetworkManagerNuclearOption.i = new(), false),
            ("changed Steam manager ownership", (_, service, _, _) => service.Manager = new(), false),
            ("destroyed Steam service", (_, service, _, _) => service.Destroyed = true, false),
            ("active server", (_, _, manager, _) => manager.Server.Active = true, false),
            ("active client", (_, _, manager, _) => manager.Client.Active = true, false),
            ("another joined lobby", (_, service, _, _) => service.Joined = new LobbyInstance(999, "Other", 0), false),
            ("another pending join", (_, service, _, _) => service.PendingJoin = new LobbyInstance(999, "Other", 0), false),
            ("another pending host", (_, service, _, _) => service.PendingHost = new(), false),
            ("newer hosted lobby", (_, service, _, _) => service.Hosted = new HostedLobbyInstance(new CSteamID(999)), false),
            ("uninitialized ownership", (_, service, _, _) => service.Initialized = false, false)
        };
        foreach (var (name, change, release) in invalidations)
        {
            run = Create();
            task = run.panel.ClickHost(run.requested);
            // Native HostLobby assigns its result before returning. Exercise a
            // changed owner/session at that exact continuation boundary too.
            var current = run;
            run.service.Requests[0].BeforeComplete = () => change(current.panel, current.service, current.manager, current.original);
            run.service.Requests[0].Finish(200);
            await task.WaitAsync(TimeSpan.FromSeconds(2));
            Check(run.manager.Starts.Count == 0 && !ReferenceEquals(MissionManager.CurrentMission, run.requested),
                name + " prevents a stale callback from starting or replacing the mission");
            Check(release ? run.service.Leaves.SequenceEqual(new[] { new CSteamID(200) }) : run.service.Leaves.Count == 0,
                name + " cleans only provably owned idle state, never a newer/joined/active session");
            CheckReleased(run.panel);
        }

        run = Create();
        task = run.panel.ClickHost(run.requested);
        run.service.Requests[0].Finish(0);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(ReferenceEquals(MissionManager.CurrentMission, run.original) && run.service.Leaves.Count == 0,
            "Steam creation failure leaves the previous mission and lobby untouched");
        CheckReleased(run.panel);
        task = run.panel.ClickHost(run.requested);
        Check(run.service.Requests.Count == 2, "A failed request permits a fresh explicit retry");
        run.service.Requests[1].Fail();
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.manager.Starts.Count == 0, "Steam exceptions do not reach network startup");
        CheckReleased(run.panel);

        run = Create();
        run.manager.FailBeforeActivation = true;
        task = run.panel.ClickHost(run.requested);
        run.service.Requests[0].Finish(300);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.service.Leaves.SequenceEqual(new[] { new CSteamID(300) }),
            "Network startup failure before activation releases the exact idle lobby it owns");
        CheckReleased(run.panel);

        run = Create();
        task = run.panel.ClickHost(run.requested);
        run.service.Requests[0].Finish(350);
        run.manager.Server.Active = false;
        run.manager.StartCompletion!.SetResult(true);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.service.Leaves.SequenceEqual(new[] { new CSteamID(350) }),
            "Native map-load failure returning normally after Server.Stop still releases its idle lobby");
        CheckReleased(run.panel);

        run = Create();
        task = run.panel.ClickHost(run.requested);
        run.service.Requests[0].Finish(400);
        run.manager.StartCompletion!.SetException(new InvalidOperationException("Failure after native activation"));
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.manager.Server.Active && run.service.Leaves.Count == 0,
            "A fault after native activation cannot tear down the active session");
        CheckReleased(run.panel);

        run = Create();
        var replacement = new Mission { Name = "Listener replacement" };
        MissionManager.AfterSet = () =>
        {
            MissionManager.AfterSet = null;
            MissionManager.SetMission(replacement, false);
        };
        task = run.panel.ClickHost(run.requested);
        run.service.Requests[0].Finish(500);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.manager.Starts.Count == 0 && ReferenceEquals(MissionManager.CurrentMission, replacement),
            "A synchronous native mission listener can supersede the request before StartHostAsync");
        CheckReleased(run.panel);

        foreach (var (_, change, _) in invalidations.Skip(5))
        {
            run = Create();
            change(run.panel, run.service, run.manager, run.original);
            await run.panel.ClickHost(run.requested).WaitAsync(TimeSpan.FromSeconds(2));
            Check(run.service.Requests.Count == 0, "Unavailable or occupied services reject hosting before any Steam creation");
            CheckReleased(run.panel);
        }
        return checks;
    }

    private static (NativeMultiplayerPanel panel, SteamLobby service, NetworkManagerNuclearOption manager, Mission original, Mission requested) Create()
    {
        var manager = new NetworkManagerNuclearOption();
        NetworkManagerNuclearOption.i = manager;
        var service = new SteamLobby { Manager = manager };
        SteamLobby.instance = service;
        GameManager.gameState = GameState.Menu;
        SceneManager.Active = new Scene(1);
        var original = new Mission { Name = "Original" };
        MissionManager.CurrentMission = original;
        MissionManager.AfterSet = null;
        return (new NativeMultiplayerPanel(), service, manager, original, new Mission { Name = "Requested" });
    }
}
