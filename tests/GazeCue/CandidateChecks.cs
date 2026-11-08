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
        ModConfiguration.Instance.LookCue.LockPriority.Value = TargetCuePriority.Default;
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
        check(TargetCueCandidates.Nearest(hud) == marker && TargetCueCandidates.Nearby(hud), "centred unlocked native marker enables cue");
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
        check(TargetCueCandidates.Nearest(hud) == marker, "nearest marker is selected before an off-centre alternative");
        hud.aircraft!.weaponManager!.Targets.Add(marker.unit!);
        check(TargetCueCandidates.Nearest(hud) == alternative, "newly locked marker yields to nearby unlocked alternative immediately");
        hud.aircraft.weaponManager.Targets.Add(alternative.unit!);
        check(!TargetCueCandidates.Nearby(hud), "all nearby multi-lock entries suppress the cue");
        hud.aircraft.weaponManager.Targets.Remove(marker.unit!);
        check(TargetCueCandidates.Nearest(hud) == marker, "unlock returns immediately without waiting for a negative cache");
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
            check(TargetCueCandidates.Nearest(hud) == centredFar, "nearest angle wins regardless of list order or world/radial distance");
            var tied = Marker(new Vector3(0, 0, 2000));
            hud = Hud(centredFar, tied);
            check(TargetCueCandidates.Nearest(hud) == centredFar, "exact angular ties retain stable marker order");
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
            check(TargetCueCandidates.Nearest(hud) == marker, "translated/rotated camera chooses the rendered centre independent of stale designator");
            camera.rotation = Quaternion.Euler(15, yaw + 4, 8);
            check(TargetCueCandidates.Nearest(hud) == alternative, "head turn immediately replaces a still-eligible prior candidate with the nearest one");
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
        check(TargetCueCandidates.Nearest(hud) == marker, "the friendly-units option restores the cue for allies");
        ModConfiguration.Instance.LookCue.FriendlyUnits.Value = false;
        var hostile = Marker(new Vector3(60, 0, 1000));
        hostile.unit!.NetworkHQ = new FactionHQ();
        hud.Add(hostile);
        check(TargetCueCandidates.Nearest(hud) == hostile, "an off-centre hostile wins over a centred friendly");
        marker.unit.NetworkHQ = null;
        check(TargetCueCandidates.Nearest(hud) == marker, "units without a faction remain eligible");

        // Lock priority: the configured class tier wins inside the cone and the
        // nearest angle decides within a tier, so an aircraft is not lost to the
        // missile it just launched.
        HUDUnitMarker Typed(Unit unit, Vector3 position)
        {
            var typed = Marker(position);
            typed.unit = unit;
            return typed;
        }
        foreach (var priority in Enum.GetValues<GazeLockPriority>())
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
            check(TargetCueCandidates.Nearest(hud) == expected, priority + " picks its preferred class inside the cone before the nearest angle");
            hud = Hud(aircraft, ground, missile);
            check(TargetCueCandidates.Nearest(hud) == expected, priority + " does not depend on marker order");
            hud = Hud(missile, Typed(new Aircraft(), new Vector3(200, 0, 1000)));
            check(TargetCueCandidates.Nearest(hud) == missile, priority + " still cues a lone missile when no preferred unit is inside the cone");
            hud = Hud(Typed(new Missile(), new Vector3(0, 0, 1000)), Typed(new Missile(), new Vector3(20, 0, 1000)));
            check(TargetCueCandidates.Nearest(hud)!.image!.transform.position.x == 0, priority + " uses the nearest angle within one class");
        }
        ResetContext();
        ModConfiguration.Instance.LookCue.LockPriority.Value = (GazeLockPriority)99;
        check(ModConfiguration.Instance.LookCue.CurrentLockPriority == TargetCuePriority.Default &&
            TargetCuePriority.Tier((GazeLockPriority)99, false, true) == 1, "an invalid saved priority behaves as the default, not as nearest");
        check(TargetCuePriority.Tier(GazeLockPriority.AircraftFirst, true, false) == 0 && TargetCuePriority.Tier(GazeLockPriority.AircraftFirst, false, false) == 1 &&
            TargetCuePriority.Tier(GazeLockPriority.AircraftFirst, false, true) == 2 && TargetCuePriority.Tier(GazeLockPriority.Nearest, false, true) == 0,
            "tiers order aircraft, other units and missiles as documented");
        check(!TargetCuePriority.Eligible(false, true) && TargetCuePriority.Eligible(true, true) && TargetCuePriority.Eligible(false, false),
            "friendly eligibility follows the option without touching other factions");
    }
}
