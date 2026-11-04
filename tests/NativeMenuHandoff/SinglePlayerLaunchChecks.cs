using Cysharp.Threading.Tasks;
using NuclearOption.Networking;
using NuclearOption.SavedMission;
using NOVR.VrUi.Native;
using UnityEngine.SceneManagement;

internal static class SinglePlayerLaunchChecks
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
        void CheckFinished(NativeSinglePlayerMissionPanel panel)
        {
            Check(!panel.Busy, "Every terminal single-player launch path releases the gate");
            Check(panel.Destroyed || panel.Restores == 1, "A live panel restores its controls exactly once");
        }

        var run = Create();
        var custom = new Mission { Name = "Customized in native picker" };
        run.panel.SetPicker(run.key, custom);
        var task = run.panel.ClickStart(run.key);
        await run.panel.ClickStart(new MissionKey("Another row"));
        Check(run.panel.Busy && run.panel.Locks == 1 && UniTask.Frames == 1 && run.manager.Starts.Count == 0,
            "The actual Start handler locks duplicate requests before its one-frame yield");
        run.panel.Visible = false;
        UniTask.PendingFrame!.SetResult(true);
        Check(ReferenceEquals(MissionManager.CurrentMission, custom) && run.manager.Starts.Count == 1,
            "An accepted hidden-panel request still uses the captured key and native customized mission");
        Check(run.manager.Starts[0].Socket == SocketType.Offline && run.manager.Starts[0].State == GameState.SinglePlayer,
            "Single-player preserves its native offline launch options");
        run.manager.StartCompletion!.SetResult(true);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.panel.Status == "Mission ready.", "An active native server reports mission readiness");
        CheckFinished(run.panel);

        foreach (var missingServer in new[] { true, false })
        {
            run = Create();
            if (missingServer)
                run.manager.Server = null!;
            else
                run.manager.Client = null!;
            task = run.panel.ClickStart(run.key);
            UniTask.PendingFrame!.SetResult(true);
            await task.WaitAsync(TimeSpan.FromSeconds(2));
            Check(run.manager.Starts.Count == 0 && ReferenceEquals(MissionManager.CurrentMission, run.original),
                "A missing serialized transport neither throws in the initial session query nor starts a mission");
            CheckFinished(run.panel);
        }

        var invalidations = new (string name, Action<NativeSinglePlayerMissionPanel, NetworkManagerNuclearOption> change)[]
        {
            ("manager replaced with an idle instance", (_, _) => NetworkManagerNuclearOption.i = new()),
            ("captured manager destroyed", (_, manager) => manager.Destroyed = true),
            ("different scene", (_, _) => SceneManager.SetActive(new Scene(2))),
            ("different game state", (_, _) => GameManager.SetGameState(GameState.SinglePlayer)),
            ("different mission", (_, _) => MissionManager.SetMission(new Mission { Name = "Other" }, false)),
            ("active server", (_, manager) => manager.Server.Active = true),
            ("active client", (_, manager) => manager.Client.Active = true),
            ("destroyed panel", (panel, _) => panel.Destroyed = true)
        };
        foreach (var (name, change) in invalidations)
        {
            run = Create();
            task = run.panel.ClickStart(run.key);
            change(run.panel, run.manager);
            var currentMission = MissionManager.CurrentMission;
            UniTask.PendingFrame!.SetResult(true);
            await task.WaitAsync(TimeSpan.FromSeconds(2));
            Check(run.manager.Starts.Count == 0 && ReferenceEquals(MissionManager.CurrentMission, currentMission),
                name + " during the yielded frame cannot start the old manager or replace the current mission");
            CheckFinished(run.panel);
        }

        // MissionManager.SetMission invokes listeners synchronously. The exact
        // operation must check context again after those listeners return.
        foreach (var (name, change) in invalidations.Take(7))
        {
            run = Create();
            var current = run;
            MissionManager.AfterSet = () =>
            {
                MissionManager.AfterSet = null;
                change(current.panel, current.manager);
            };
            task = run.panel.ClickStart(run.key);
            UniTask.PendingFrame!.SetResult(true);
            await task.WaitAsync(TimeSpan.FromSeconds(2));
            Check(run.manager.Starts.Count == 0, name + " inside a native mission listener prevents stale network startup");
            CheckFinished(run.panel);
        }

        run = Create();
        task = run.panel.ClickStart(run.key);
        BlueprinterContentReadiness.CanLaunch = false;
        UniTask.PendingFrame!.SetResult(true);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.manager.Starts.Count == 0 && ReferenceEquals(MissionManager.CurrentMission, run.original),
            "Content preload beginning during the yield still blocks offline launch");
        CheckFinished(run.panel);

        run = Create();
        task = run.panel.ClickStart(new MissionKey("Missing mission"));
        UniTask.PendingFrame!.SetResult(true);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.manager.Starts.Count == 0 && run.panel.Status.Contains("Couldn't open"),
            "Disk load failure reports an actionable error without starting a session");
        CheckFinished(run.panel);
        task = run.panel.ClickStart(run.key);
        UniTask.PendingFrame!.SetResult(true);
        run.manager.Server.Active = false;
        run.manager.StartCompletion!.SetResult(true);
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(!run.panel.Busy && run.panel.Restores == 2 && run.panel.Status.Contains("map couldn't load"),
            "A retry remains available and native normal-return map failure is not reported as success");

        run = Create();
        task = run.panel.ClickStart(run.key);
        UniTask.PendingFrame!.SetCanceled();
        await task.WaitAsync(TimeSpan.FromSeconds(2));
        Check(run.manager.Starts.Count == 0, "Canceled frame wait does not invoke native startup");
        CheckFinished(run.panel);
        return checks;
    }

    private static (NativeSinglePlayerMissionPanel panel, NetworkManagerNuclearOption manager, Mission original, MissionKey key) Create()
    {
        var manager = new NetworkManagerNuclearOption();
        NetworkManagerNuclearOption.i = manager;
        GameManager.gameState = GameState.Menu;
        SceneManager.Active = new Scene(1);
        var original = new Mission { Name = "Original" };
        MissionManager.CurrentMission = original;
        MissionManager.AfterSet = null;
        MissionKey.Loads.Clear();
        var key = new MissionKey("Requested");
        MissionKey.Loads[key.Name] = new Mission { Name = key.Name };
        UniTask.Frames = 0;
        UniTask.PendingFrame = null;
        BlueprinterContentReadiness.CanLaunch = true;
        return (new(), manager, original, key);
    }
}
