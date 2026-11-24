using NOVR;
using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;

internal static class CandidateChecks
{
    internal static HUDUnitMarker Marker(Vector3 position)
    {
        var image = new GameObject("Native unit marker").AddComponent<Image>();
        image.transform.position = position;
        return new HUDUnitMarker { unit = new Unit(), image = image };
    }
    internal static CombatHUD Hud(params HUDUnitMarker[] markers)
    {
        var hud = new CombatHUD { targetDesignator = new GameObject("Native designator").AddComponent<Image>() };
        hud.Add(markers);
        return hud;
    }
    internal static void ResetContext()
    {
        Time.unscaledTime = 1;
        ModConfiguration.Instance.LookCue.VerticalDegrees.Value = 0;
        // The former stock priority: for plain units it is the nearest angle,
        // which is what the checks below were written against. The game's own
        // ranking, now the default, has its checks in GamePriorityChecks.
        ModConfiguration.Instance.LookCue.LockPriority.Value = GazeLockPriority.UnitsBeforeMissiles;
        ModConfiguration.Instance.LookCue.FriendlyUnits.Value = false;
        DynamicMap.FriendlyFactions.Clear();
        SceneSingleton<TargetListSelector>.i = new TargetListSelector();
        APIBus.CockpitHudCamera = new GameObject("Actual HUD camera").AddComponent<Camera>();
        UnityEngine.XR.XRSettings.enabled = true;
        CameraStateManager.cameraMode = CameraMode.cockpit;
        DynamicMap.mapMaximized = false;
        NOVR.VrUi.NOUIManager.I = new();
    }
    internal static void Run(Action<bool, string> check)
    {
        CombatHUD Fresh(out HUDUnitMarker marker)
        {
            ResetContext();
            marker = Marker(new Vector3(0, 0, 1000));
            return Hud(marker);
        }
        var hud = Fresh(out var marker);
        check(TargetCueCandidates.Pick(hud) == marker && TargetCueCandidates.Nearby(hud), "centred unlocked native marker enables cue");
        hud.aircraft!.weaponManager!.Targets.Add(marker.unit!);
        check(!marker.selected && !TargetCueCandidates.Nearby(hud), "authoritative lock excludes target before presentation catches up");
        for (var index = 0; index < 4; index++)
        {
            hud = Fresh(out marker);
            var targets = hud.aircraft!.weaponManager!.Targets;
            for (var i = 0; i < 4; i++)
                targets.Add(i == index ? marker.unit! : new Unit());
            check(!TargetCueCandidates.Nearby(hud), "every target-list position is excluded, not just primary lock");
        }

        hud = Fresh(out marker);
        var alternative = Marker(new Vector3(40, 0, 1000));
        hud.Add(alternative);
        check(TargetCueCandidates.Pick(hud) == marker, "nearest marker is selected before an off-centre alternative");
        hud.aircraft!.weaponManager!.Targets.Add(marker.unit!);
        check(TargetCueCandidates.Pick(hud) == alternative, "newly locked marker yields to nearby unlocked alternative immediately");
        hud.aircraft.weaponManager.Targets.Add(alternative.unit!);
        check(!TargetCueCandidates.Nearby(hud), "all nearby multi-lock entries suppress the cue");
        hud.aircraft.weaponManager.Targets.Remove(marker.unit!);
        check(TargetCueCandidates.Pick(hud) == marker, "unlock returns immediately without waiting for a negative cache");
        hud = Fresh(out marker);
        hud.Targets.Add(marker.unit!);
        check(TargetCueCandidates.Nearby(hud), "live weapon-manager list takes precedence over stale HUD list");
        hud.aircraft!.weaponManager = null;
        check(!TargetCueCandidates.Nearby(hud), "HUD list fallback still excludes selected unit without weapon manager");
        hud.Targets.Clear();
        check(TargetCueCandidates.Nearby(hud), "HUD fallback unlock resumes immediately");

        foreach (var invalidate in new Action<CombatHUD, HUDUnitMarker>[]
        {
            (_, m) => m.selected = true,
            (_, m) => m.unit!.disabled = true,
            (_, m) => m.image!.enabled = false,
            (_, m) => m.image!.gameObject.SetActive(false),
            (_, m) => m.image!.canvasRenderer.Alpha = .01f,
            (_, m) => m.image!.canvasRenderer.Alpha = float.NaN,
            (_, m) => SceneSingleton<TargetListSelector>.i!.Excluded.Add(m.unit!),
            (_, m) => m.image = null,
            (_, m) => m.unit = null
        })
        {
            hud = Fresh(out marker);
            check(TargetCueCandidates.Nearby(hud), "candidate exists before native eligibility changes");
            invalidate(hud, marker);
            check(!TargetCueCandidates.Nearby(hud), "selection, visibility and exclusions apply on every scan");
        }

        foreach (var scale in new[] { .01f, 1f, 100f })
            foreach (var (direction, expected) in new[]
            {
                (new Vector3(0, 0, 1), true),
                (new Vector3(.099f, 0, .9951f), true),
                (new Vector3(.101f, 0, .9949f), false),
                (new Vector3(0, 0, -1), false),
                (new Vector3(1, 0, 0), false),
                (Vector3.zero, false),
                (new Vector3(float.NaN, 0, 1), false),
                (new Vector3(0, float.PositiveInfinity, 1), false)
            })
            {
                hud = Fresh(out marker);
                marker.image!.transform.position = direction * scale;
                check(TargetCueCandidates.Nearby(hud) == expected, "angular cone rejects outside/behind/nonfinite points independently of marker depth");
            }

        // x² + z² is exactly 40000 at float precision, so z/length is
        // exactly the production .995f cutoff rather than an approximate angle.
        hud = Fresh(out marker);
        marker.image!.transform.position = new Vector3(MathF.Sqrt(399), 0, 199);
        check(!TargetCueCandidates.Nearby(hud), "exact .995 alignment remains outside the strict native angular cone");

        foreach (var reversed in new[] { false, true })
        {
            ResetContext();
            var centredFar = Marker(new Vector3(0, 0, 10000));
            var offsetNear = Marker(new Vector3(4, 0, 100));
            hud = reversed ? Hud(centredFar, offsetNear) : Hud(offsetNear, centredFar);
            check(TargetCueCandidates.Pick(hud) == centredFar, "nearest angle wins regardless of list order or world/radial distance");
            var tied = Marker(new Vector3(0, 0, 2000));
            hud = Hud(centredFar, tied);
            check(TargetCueCandidates.Pick(hud) == centredFar, "exact angular ties retain stable marker order");
        }

        foreach (var yaw in new[] { -145f, -45f, 0f, 85f, 175f })
        {
            ResetContext();
            var camera = APIBus.CockpitHudCamera!.transform;
            camera.position = new Vector3(1230, -450, 770);
            camera.rotation = Quaternion.Euler(15, yaw, 8);
            marker = Marker(camera.position + camera.rotation * new Vector3(0, 0, 1000));
            alternative = Marker(camera.position + camera.rotation * new Vector3(70, 0, 1000));
            hud = Hud(alternative, marker);
            hud.targetDesignator.transform.position = new Vector3(-5000, 4000, 20);
            check(TargetCueCandidates.Pick(hud) == marker, "translated/rotated camera chooses the rendered centre independent of stale designator");
            camera.rotation = Quaternion.Euler(15, yaw + 4, 8);
            check(TargetCueCandidates.Pick(hud) == alternative, "head turn immediately replaces a still-eligible prior candidate with the nearest one");
        }
        hud = Fresh(out marker);
        check(TargetCueCandidates.Nearby(hud) && !TargetCueCandidates.Nearby(Hud()), "switching HUD uses only its current marker set");
        var otherAircraft = new Aircraft();
        otherAircraft.weaponManager!.Targets.Add(marker.unit!);
        hud.aircraft = otherAircraft;
        check(!TargetCueCandidates.Nearby(hud), "switching aircraft immediately uses its authoritative target list");
        hud.aircraft = new Aircraft();
        check(TargetCueCandidates.Nearby(hud), "new aircraft cannot inherit a previous negative result");

        foreach (var invalidate in new Action<CombatHUD>[]
        {
            h => h.aircraft = null,
            h => h.aircraft!.disabled = true,
            h => h.targetDesignator = null!,
            h => h.targetDesignator!.gameObject.SetActive(false),
            _ => SceneSingleton<TargetListSelector>.i = null,
            _ => APIBus.CockpitHudCamera = null,
            _ => APIBus.CockpitHudCamera!.transform.position = new Vector3(float.NaN, 0, 0),
            _ => APIBus.CockpitHudCamera!.transform.rotation = new Quaternion(float.NaN, 0, 0, 1)
        })
        {
            hud = Fresh(out marker);
            check(TargetCueCandidates.Nearby(hud), "candidate exists before native context loss");
            invalidate(hud);
            check(!TargetCueCandidates.Nearby(hud), "lost or invalid native context fails closed");
        }
        check(!TargetCueCandidates.Nearby(null), "missing HUD fails closed");

        // Friendly units: no cue and no gaze lock unless the option allows them.
        hud = Fresh(out marker);
        var allies = new FactionHQ();
        DynamicMap.FriendlyFactions.Add(allies);
        marker.unit!.NetworkHQ = allies;
        check(!TargetCueCandidates.Nearby(hud), "friendly units get no cue by default");
        ModConfiguration.Instance.LookCue.FriendlyUnits.Value = true;
        check(TargetCueCandidates.Pick(hud) == marker, "the friendly-units option restores the cue for allies");
        ModConfiguration.Instance.LookCue.FriendlyUnits.Value = false;
        var hostile = Marker(new Vector3(60, 0, 1000));
        hostile.unit!.NetworkHQ = new FactionHQ();
        hud.Add(hostile);
        check(TargetCueCandidates.Pick(hud) == hostile, "an off-centre hostile wins over a centred friendly");
        marker.unit.NetworkHQ = null;
        check(TargetCueCandidates.Pick(hud) == marker, "units without a faction remain eligible");

        // Lock priority: the configured class tier wins inside the cone and the
        // nearest angle decides within a tier, so an aircraft is not lost to the
        // missile it just launched.
        HUDUnitMarker Typed(Unit unit, Vector3 position)
        {
            var typed = Marker(position);
            typed.unit = unit;
            return typed;
        }
        foreach (var priority in Enum.GetValues<GazeLockPriority>().Where(p => p != GazeLockPriority.Game))
        {
            ResetContext();
            ModConfiguration.Instance.LookCue.LockPriority.Value = priority;
            var missile = Typed(new Missile(), new Vector3(0, 0, 1000));
            var ground = Typed(new Unit(), new Vector3(30, 0, 1000));
            var aircraft = Typed(new Aircraft(), new Vector3(60, 0, 1000));
            hud = Hud(missile, ground, aircraft);
            var expected = priority switch
            {
                GazeLockPriority.Nearest => missile,
                GazeLockPriority.UnitsBeforeMissiles => ground,
                _ => aircraft
            };
            check(TargetCueCandidates.Pick(hud) == expected, priority + " picks its preferred class inside the cone before the nearest angle");
            hud = Hud(aircraft, ground, missile);
            check(TargetCueCandidates.Pick(hud) == expected, priority + " does not depend on marker order");
            hud = Hud(missile, Typed(new Aircraft(), new Vector3(200, 0, 1000)));
            check(TargetCueCandidates.Pick(hud) == missile, priority + " still cues a lone missile when no preferred unit is inside the cone");
            hud = Hud(Typed(new Missile(), new Vector3(0, 0, 1000)), Typed(new Missile(), new Vector3(20, 0, 1000)));
            check(TargetCueCandidates.Pick(hud)!.image!.transform.position.x == 0, priority + " uses the nearest angle within one class");
        }
        ResetContext();
        ModConfiguration.Instance.LookCue.LockPriority.Value = (GazeLockPriority)99;
        check(ModConfiguration.Instance.LookCue.CurrentLockPriority == GazeLockPriority.Game &&
            TargetCuePriority.Normalize((GazeLockPriority)99) == GazeLockPriority.Game && TargetCuePriority.Normalize((GazeLockPriority)(-1)) == GazeLockPriority.Game,
            "an invalid saved priority behaves as the default, the game's ranking, not as nearest");
        check(TargetCuePriority.Tier(GazeLockPriority.AircraftFirst, true, false) == 0 && TargetCuePriority.Tier(GazeLockPriority.AircraftFirst, false, false) == 1 &&
            TargetCuePriority.Tier(GazeLockPriority.AircraftFirst, false, true) == 2 && TargetCuePriority.Tier(GazeLockPriority.Nearest, false, true) == 0,
            "tiers order aircraft, other units and missiles as documented");
        check(!TargetCuePriority.Eligible(false, true) && TargetCuePriority.Eligible(true, true) && TargetCuePriority.Eligible(false, false),
            "friendly eligibility follows the option without touching other factions");
        GamePriorityChecks(check);
    }

    // 0.4.8.35: with the Game priority the game's own score ranks what is in the cone.
    private static void GamePriorityChecks(Action<bool, string> check)
    {
        void Game()
        {
            ResetContext();
            ModConfiguration.Instance.LookCue.LockPriority.Value = GazeLockPriority.Game;
        }
        check(TargetCuePriority.Default == GazeLockPriority.Game && (int)GazeLockPriority.Game == 3 && (int)GazeLockPriority.Nearest == 0 &&
            (int)GazeLockPriority.UnitsBeforeMissiles == 1 && (int)GazeLockPriority.AircraftFirst == 2,
            "the game's ranking is the default and is appended, so stored priorities keep their values");
        check(TargetCuePriority.MenuLabels.SequenceEqual(new[] { "Game", "Nearest", "Units first", "Aircraft first" }) &&
            TargetCuePriority.MenuOrder.Length == TargetCuePriority.MenuLabels.Length &&
            TargetCuePriority.MenuOrder.Distinct().Count() == Enum.GetValues<GazeLockPriority>().Length, "the menus list every priority once, the default first");
        foreach (var priority in Enum.GetValues<GazeLockPriority>())
            check(TargetCuePriority.FromMenu(TargetCuePriority.MenuIndex(priority)) == priority, priority + " survives the menu's own ordering");
        check(TargetCuePriority.MenuIndex((GazeLockPriority)99) == 0 && TargetCuePriority.FromMenu(-1) == GazeLockPriority.Game &&
            TargetCuePriority.FromMenu(4) == GazeLockPriority.Game, "an invalid stored value or menu index shows and gives the default");
        check(TargetCuePriority.Tier(GazeLockPriority.Game, false, true) == 0 && TargetCuePriority.Tier(GazeLockPriority.Game, true, false) == 0,
            "the game's ranking has no class tiers of its own");

        foreach (var reversed in new[] { false, true })
        {
            Game();
            var centred = Marker(new Vector3(0, 0, 1000));
            var threat = Marker(new Vector3(60, 0, 1000));
            threat.Priority = 5;
            var hud = reversed ? Hud(threat, centred) : Hud(centred, threat);
            check(TargetCueCandidates.Pick(hud) == threat, "the game's higher score wins over a marker nearer the cue, in either list order");
            check(centred.Assessed.Count == 1 && threat.Assessed.Count == 1, "each marker in the cone is scored once per pick");
            var asked = threat.Assessed[0];
            check(asked.aircraft == hud.aircraft && asked.station == hud.aircraft!.weaponManager!.currentWeaponStation &&
                asked.designator == hud.targetDesignator.transform.position,
                "the score is asked with the player's aircraft, its selected weapon and the HUD's own designator");
            check(TargetCueCandidates.Nearby(hud) && centred.Assessed.Count == 1 && threat.Assessed.Count == 1,
                "the per-frame cue never runs the game's scoring");
            ModConfiguration.Instance.LookCue.LockPriority.Value = GazeLockPriority.Nearest;
            check(TargetCueCandidates.Pick(hud) == centred && threat.Assessed.Count == 1, "the other priorities never ask the game for a score");
        }

        Game();
        var first = Marker(new Vector3(40, 0, 1000));
        var second = Marker(new Vector3(0, 0, 1000));
        var tiedHud = Hud(first, second);
        check(TargetCueCandidates.Pick(tiedHud) == first, "an exact tie in score keeps the earlier marker, not the nearer one");

        Game();
        var outside = Marker(new Vector3(200, 0, 1000));
        outside.Priority = 1000;
        var inside = Marker(new Vector3(30, 0, 1000));
        check(TargetCueCandidates.Pick(Hud(outside, inside)) == inside && outside.Assessed.Count == 0,
            "a high score outside the cue's cone is neither picked nor scored");

        // Scores that are not numbers: a marker exactly on the designator divides by zero.
        Game();
        var centre = Marker(new Vector3(0, 0, 1000));
        centre.Priority = float.PositiveInfinity;
        var strong = Marker(new Vector3(20, 0, 1000));
        strong.Priority = 1e30f;
        check(TargetCueCandidates.Pick(Hud(strong, centre)) == centre, "an infinite score, a marker dead centre, wins");
        centre.Priority = float.NaN;
        check(TargetCueCandidates.Pick(Hud(centre, strong)) == strong && TargetCueCandidates.Pick(Hud(strong, centre)) == strong,
            "a score that is not a number never wins over a real one");
        strong.Priority = float.NaN;
        check(TargetCueCandidates.Pick(Hud(strong, centre)) == centre, "when no score is a number the nearest angle still gives the tap a target");
        strong.Priority = float.NegativeInfinity;
        centre.Priority = float.NegativeInfinity;
        check(TargetCueCandidates.Pick(Hud(strong, centre)) == centre, "nor does an all-negative-infinity cone lose the tap");

        // The cue's own rules still apply before the game's score.
        Game();
        var ally = Marker(new Vector3(0, 0, 1000));
        var allies = new FactionHQ();
        DynamicMap.FriendlyFactions.Add(allies);
        ally.unit!.NetworkHQ = allies;
        ally.Priority = 100;
        var hostile = Marker(new Vector3(60, 0, 1000));
        hostile.unit!.NetworkHQ = new FactionHQ();
        var mixed = Hud(ally, hostile);
        check(TargetCueCandidates.Pick(mixed) == hostile && ally.Assessed.Count == 0, "a friendly unit is not offered to the game's ranking by default");
        ModConfiguration.Instance.LookCue.FriendlyUnits.Value = true;
        check(TargetCueCandidates.Pick(mixed) == ally, "with cue friendly units on, the game's score decides between ally and hostile");
        ModConfiguration.Instance.LookCue.FriendlyUnits.Value = false;

        Game();
        var locked = Marker(new Vector3(0, 0, 1000));
        locked.Priority = 50;
        var free = Marker(new Vector3(50, 0, 1000));
        var lockedHud = Hud(locked, free);
        lockedHud.aircraft!.weaponManager!.Targets.Add(locked.unit!);
        check(TargetCueCandidates.Pick(lockedHud) == free && locked.Assessed.Count == 0, "an already selected target is skipped before scoring");
        var excluded = Marker(new Vector3(10, 0, 1000));
        excluded.Priority = 50;
        lockedHud.Add(excluded);
        SceneSingleton<TargetListSelector>.i!.Excluded.Add(excluded.unit!);
        check(TargetCueCandidates.Pick(lockedHud) == free && excluded.Assessed.Count == 0, "the game's own target filters still apply first");

        Game();
        var lone = Marker(new Vector3(0, 0, 1000));
        var noManager = Hud(lone);
        noManager.aircraft!.weaponManager = null;
        check(TargetCueCandidates.Pick(noManager) == lone && lone.Assessed.Single().station == null,
            "without a weapon manager the game scores as it does with no weapon selected");
        noManager.aircraft = new Aircraft();
        noManager.aircraft.weaponManager!.currentWeaponStation = null;
        check(TargetCueCandidates.Pick(noManager) == lone && lone.Assessed.Last().station == null, "no selected weapon is passed on as none");
        check(TargetCueCandidates.Pick(Hud()) == null && TargetCueCandidates.Pick(null) == null, "an empty cue or a missing HUD picks nothing");
    }
}
