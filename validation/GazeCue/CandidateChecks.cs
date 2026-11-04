using NOVR.MotionControls;
using UnityEngine;
using UnityEngine.UI;

internal static class CandidateChecks
{
    internal static void Run(Action<bool, string> check)
    {
        HUDUnitMarker Marker(float distance = 20)
        {
            var image = new GameObject("Native unit marker").AddComponent<Image>();
            image.transform.position = new Vector3(distance, 0, 0);
            return new HUDUnitMarker { unit = new Unit(), image = image };
        }
        CombatHUD Hud(params HUDUnitMarker[] markers)
        {
            var hud = new CombatHUD { targetDesignator = new GameObject("Native designator").AddComponent<Image>() };
            hud.Add(markers); return hud;
        }
        CombatHUD Fresh(out HUDUnitMarker marker)
        {
            TargetCueCandidates.Nearby(null); Time.unscaledTime = 1;
            SceneSingleton<TargetListSelector>.i = new TargetListSelector();
            marker = Marker(); return Hud(marker);
        }

        var hud = Fresh(out var marker);
        check(TargetCueCandidates.Nearby(hud), "nearby unlocked native marker enables cue");
        hud.aircraft!.weaponManager!.Targets.Add(marker.unit!); Time.unscaledTime += .01f;
        check(!marker.selected && !TargetCueCandidates.Nearby(hud),
            "authoritative lock list invalidates cached positive before marker.selected catches up");
        for (var index = 0; index < 4; index++)
        {
            hud = Fresh(out marker);
            var targets = hud.aircraft!.weaponManager!.Targets;
            for (var i = 0; i < 4; i++) targets.Add(i == index ? marker.unit! : new Unit());
            check(!TargetCueCandidates.Nearby(hud), "every target-list position is excluded, not just primary lock");
        }

        hud = Fresh(out marker); var alternative = Marker(40); hud.Add(alternative);
        check(TargetCueCandidates.Nearby(hud), "first unlocked marker warms candidate cache");
        hud.aircraft!.weaponManager!.Targets.Add(marker.unit!); Time.unscaledTime += .01f;
        check(TargetCueCandidates.Nearby(hud), "newly locked cached marker yields to nearby unlocked alternative immediately");
        hud.aircraft.weaponManager.Targets.Add(alternative.unit!); Time.unscaledTime += .01f;
        check(!TargetCueCandidates.Nearby(hud), "all nearby multi-lock entries suppress the cue");
        hud.aircraft.weaponManager.Targets.Remove(marker.unit!); Time.unscaledTime += .01f;
        check(!TargetCueCandidates.Nearby(hud), "unlock respects existing bounded negative-cache interval");
        Time.unscaledTime += .09f;
        check(TargetCueCandidates.Nearby(hud), "unlocked candidate returns after negative cache expires");

        hud = Fresh(out marker); hud.Targets.Add(marker.unit!);
        check(TargetCueCandidates.Nearby(hud), "live weapon-manager list takes precedence over stale HUD list");
        hud.aircraft!.weaponManager = null; Time.unscaledTime += .01f;
        check(!TargetCueCandidates.Nearby(hud), "HUD target-list fallback still excludes selected unit without weapon manager");
        hud.Targets.Clear(); Time.unscaledTime += .09f;
        check(TargetCueCandidates.Nearby(hud), "HUD fallback unlock also resumes after cache expiry");

        foreach (var invalidate in new Action<CombatHUD, HUDUnitMarker>[]
        {
            (_, m) => m.selected = true,
            (_, m) => m.unit!.disabled = true,
            (_, m) => m.image!.enabled = false,
            (_, m) => m.image!.gameObject.SetActive(false),
            (_, m) => m.image!.canvasRenderer.Alpha = .01f,
            (_, m) => m.image!.transform.position = new Vector3(100, 0, 0),
            (_, m) => SceneSingleton<TargetListSelector>.i!.Excluded.Add(m.unit!),
            (_, m) => m.image = null,
            (_, m) => m.unit = null
        })
        {
            hud = Fresh(out marker); check(TargetCueCandidates.Nearby(hud), "valid candidate warms positive cache");
            invalidate(hud, marker); Time.unscaledTime += .01f;
            check(!TargetCueCandidates.Nearby(hud), "cached candidates retain native selection/visibility/distance/exclusion filters");
        }

        foreach (var (x, y, z, expected) in new[]
        {
            (99.99f, 0f, 0f, true), (100f, 0f, 0f, false), (100.01f, 0f, 0f, false),
            (60f, 80f, 0f, false), (60f, 79f, 0f, true), (0f, 0f, 100f, false)
        })
        {
            hud = Fresh(out marker);
            hud.targetDesignator!.transform.position = new Vector3(250, -30, 10);
            marker.image!.transform.position = new Vector3(250 + x, -30 + y, 10 + z);
            check(TargetCueCandidates.Nearby(hud) == expected,
                "native strict100-unit three-dimensional distance stays relative to designator");
        }

        hud = Fresh(out marker); check(TargetCueCandidates.Nearby(hud), "old HUD has cached positive");
        var otherHud = Hud(); Time.unscaledTime += .01f;
        check(!TargetCueCandidates.Nearby(otherHud), "switching HUD cannot reuse old HUD candidate");
        check(TargetCueCandidates.Nearby(hud), "switching back rescans actual HUD immediately");
        var otherAircraft = new Aircraft(); otherAircraft.weaponManager!.Targets.Add(marker.unit!);
        hud.aircraft = otherAircraft; Time.unscaledTime += .01f;
        check(!TargetCueCandidates.Nearby(hud), "new aircraft target list replaces cached aircraft ownership");
        hud.aircraft = new Aircraft(); Time.unscaledTime += .01f;
        check(TargetCueCandidates.Nearby(hud), "new aircraft is not held back by previous aircraft's negative cache");

        foreach (var invalidate in new Action<CombatHUD>[]
        {
            h => h.aircraft = null,
            h => h.aircraft!.disabled = true,
            h => h.targetDesignator = null!,
            h => h.targetDesignator!.gameObject.SetActive(false),
            _ => SceneSingleton<TargetListSelector>.i = null
        })
        {
            hud = Fresh(out marker); check(TargetCueCandidates.Nearby(hud), "candidate exists before native context loss");
            invalidate(hud); Time.unscaledTime += .01f;
            check(!TargetCueCandidates.Nearby(hud), "lost native context cannot leave cached cue active");
        }
        check(!TargetCueCandidates.Nearby(null), "missing HUD fails closed and clears cache");
        SceneSingleton<TargetListSelector>.i = null;
    }
}
