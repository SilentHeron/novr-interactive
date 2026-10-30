using System;
using System.Globalization;

namespace NOVR.MotionControls;

internal enum MfdContextAction
{
    None, ZoomIn, ZoomOut, Map, Sensor, Weapons, Flight, EngineFuel, Systems, Tactical,
    RecenterMap, ToggleMapOrientation, HeadAim, TrackSelected, ClearTargets,
    PreviousWeapon, NextWeapon, NextCountermeasure, PageMenu, SwapDisplays,
    PickerPrevious, PickerNext, PickerOpen, PickerBack
}

// Snapshot of verified runtime capability, not an optimistic request to native UI.
// HeadFeed means a fresh verified HEAD frame, not merely an enabled optical HUD.
internal struct MfdContextState
{
    internal int Available;
    internal bool MapReady, MapNorthUp;
    internal float MapZoom;
    // SensorReady means an available camera/assigned texture; lazy allocation
    // must not prevent enabling HEAD. HeadFeed separately proves a fresh image.
    internal bool SensorReady, OpticalEnabled, Tracking, HeadFeed, CanLookToAim, CanTrackSelected;
    internal float SensorZoom;
    internal bool HasTargets, LocalHudReady, WeaponActionsReady, CountermeasureActionsReady;
    internal bool PickerOpen, CanSwap;
    internal MfdPage PickerSelection;
}

internal readonly struct MfdContextKey
{
    internal readonly string label, state, hint;
    internal readonly MfdContextAction action;
    internal readonly bool enabled;
    internal MfdContextKey(string label, MfdContextAction action, bool enabled, string state, string hint)
    { this.label = label; this.action = action; this.enabled = enabled; this.state = state; this.hint = hint; }
}

// Four physical softkeys per display. The caller re-evaluates this matrix at
// activation, then uses existing page selection, native action or optical APIs.
internal static class MfdContextControls
{
    internal const int KeyCount = 4;
    internal static MfdContextKey Key(MfdPage page, int key, MfdContextState context)
    {
        if (key < 0 || key >= KeyCount || (int)page < 0 || (int)page >= MfdPages.Count)
            return new MfdContextKey("", MfdContextAction.None, false, "", "INVALID KEY");
        if (context.PickerOpen)
        {
            var selection = MfdPagePicker.Fitted(context.PickerSelection, context.Available);
            var canMove = MfdPagePicker.Count(context.Available) > 1;
            return key switch
            {
                0 => new MfdContextKey("PREV", MfdContextAction.PickerPrevious, canMove, "", canMove ? "PREVIOUS FITTED PAGE" : "ONLY FITTED PAGE"),
                1 => new MfdContextKey("NEXT", MfdContextAction.PickerNext, canMove, "", canMove ? "NEXT FITTED PAGE" : "ONLY FITTED PAGE"),
                2 => new MfdContextKey("OPEN", MfdContextAction.PickerOpen, selection, "", selection ? "OPEN " + MfdPages.Title(context.PickerSelection) : "NO FITTED PAGE"),
                _ => new MfdContextKey("BACK", MfdContextAction.PickerBack, true, "", "CLOSE WITHOUT CHANGING PAGE")
            };
        }
        if (page == MfdPage.Map)
        {
            var zoom = MapZoom(context.MapZoom, 0);
            if (key < 2) return Zoom(key, context.MapReady, zoom, 12, "MAP NOT READY");
            if (key == 2) return new MfdContextKey("CENTER", MfdContextAction.RecenterMap, context.MapReady, "",
                context.MapReady ? "CENTER MAP ON AIRCRAFT" : "MAP NOT READY");
            return new MfdContextKey(context.MapNorthUp ? "NORTH UP" : "HDG UP", MfdContextAction.ToggleMapOrientation,
                context.MapReady, "", !context.MapReady ? "MAP NOT READY" : context.MapNorthUp ? "CHANGE TO HEADING UP" : "CHANGE TO NORTH UP");
        }
        if (page == MfdPage.Sensor)
        {
            if (key < 2)
            {
                var unavailable = !context.SensorReady ? "SENSOR NOT READY" : context.Tracking ? "TRACK USES AUTO ZOOM" : "HEAD FEED REQUIRED";
                return Zoom(key, context.SensorReady && context.HeadFeed && context.OpticalEnabled && !context.Tracking,
                    SensorZoom(context.SensorZoom, 0), 6, unavailable);
            }
            if (key == 2)
            {
                var head = context.Tracking || !context.OpticalEnabled;
                var enabled = context.SensorReady && (head ? context.CanLookToAim : context.CanTrackSelected && context.HasTargets);
                return new MfdContextKey(head ? "HEAD" : "TRACK", head ? MfdContextAction.HeadAim : MfdContextAction.TrackSelected,
                    enabled, !context.OpticalEnabled ? "OFF" : context.Tracking ? "TRACK" : "HEAD",
                    !context.SensorReady ? "SENSOR NOT READY" : enabled ? head ? "AIM SENSOR WITH HEAD" : "TRACK SELECTED TARGET" :
                    !head && !context.HasTargets ? "SELECT A TARGET FIRST" : "OPTICAL MODE UNAVAILABLE");
            }
            return new MfdContextKey("CLEAR", MfdContextAction.ClearTargets, context.LocalHudReady && context.HasTargets,
                "", !context.LocalHudReady ? "TARGET CONTROLS NOT READY" : !context.HasTargets ? "NO SELECTED TARGETS" : "CLEAR ALL TARGETS");
        }
        if (page == MfdPage.Weapons)
        {
            if (key < 2) return new MfdContextKey(key == 0 ? "WPN −" : "WPN +",
                key == 0 ? MfdContextAction.PreviousWeapon : MfdContextAction.NextWeapon, context.WeaponActionsReady,
                "", context.WeaponActionsReady ? key == 0 ? "PREVIOUS WEAPON" : "NEXT WEAPON" : "WEAPON SELECTION UNAVAILABLE");
            // Native safety is derived from gear/ground state, not a toggle.
            if (key == 2) return Page(MfdPage.Map, context.Available);
            return new MfdContextKey("CM NEXT", MfdContextAction.NextCountermeasure, context.CountermeasureActionsReady,
                "", context.CountermeasureActionsReady ? "NEXT COUNTERMEASURE TYPE" : "COUNTERMEASURES UNAVAILABLE");
        }
        if (key >= 2)
        {
            var menu = key == 2; var enabled = menu ? MfdPagePicker.Count(context.Available) > 0 : context.CanSwap;
            return new MfdContextKey(menu ? "MENU" : "SWAP", menu ? MfdContextAction.PageMenu : MfdContextAction.SwapDisplays,
                enabled, "", menu ? enabled ? "CHOOSE A DISPLAY PAGE" : "NO FITTED PAGE" : enabled ? "EXCHANGE LEFT AND RIGHT DISPLAYS" : "DISPLAY SWAP UNAVAILABLE");
        }
        var next = page switch
        {
            MfdPage.Flight => key == 0 ? MfdPage.Map : MfdPage.EngineFuel,
            MfdPage.EngineFuel => key == 0 ? MfdPage.Flight : MfdPage.Systems,
            MfdPage.Systems => key == 0 ? MfdPage.Flight : MfdPage.Tactical,
            _ => key == 0 ? MfdPage.Map : MfdPage.Sensor
        };
        return Page(next, context.Available);
    }

    private static MfdContextKey Zoom(int key, bool ready, float zoom, float max, string unavailable)
    {
        var up = key == 0;
        var enabled = ready && (up ? zoom < max : zoom > 1);
        return new MfdContextKey(up ? "ZOOM +" : "ZOOM −", up ? MfdContextAction.ZoomIn : MfdContextAction.ZoomOut,
            enabled, zoom.ToString("0.#", CultureInfo.InvariantCulture) + "×", !ready ? unavailable : !enabled ? "ZOOM LIMIT" : up ? "INCREASE ZOOM" : "DECREASE ZOOM");
    }
    private static MfdContextKey Page(MfdPage page, int available)
    {
        var enabled = (available & (1 << (int)page)) != 0;
        var label = page switch { MfdPage.EngineFuel => "ENG/FUEL", MfdPage.Tactical => "TACTICAL", _ => MfdPages.ShortTitle(page) };
        var action = page switch
        {
            MfdPage.Map => MfdContextAction.Map, MfdPage.Sensor => MfdContextAction.Sensor,
            MfdPage.Weapons => MfdContextAction.Weapons, MfdPage.Flight => MfdContextAction.Flight,
            MfdPage.EngineFuel => MfdContextAction.EngineFuel, MfdPage.Systems => MfdContextAction.Systems,
            _ => MfdContextAction.Tactical
        };
        return new MfdContextKey(label, action, enabled, "", enabled ? "SHOW " + MfdPages.Title(page) : "PAGE NOT FITTED");
    }
    internal static float MapZoom(float value, int direction) => Clamp(Clamp(value, 1, 12) *
        (direction > 0 ? 1.35f : direction < 0 ? 1 / 1.35f : 1), 1, 12);
    internal static float SensorZoom(float value, int direction) => Clamp(Clamp(value, 1, 6) +
        (direction > 0 ? .5f : direction < 0 ? -.5f : 0), 1, 6);
    private static float Clamp(float value, float min, float max) => float.IsNaN(value) || float.IsInfinity(value)
        ? min : Math.Max(min, Math.Min(max, value));
}

// Pure picker state shared by the physical and legacy page menu presentation.
// Generation changes invalidate touches whenever OPEN would mean a new action.
internal sealed class MfdPagePicker
{
    internal bool Open { get; private set; }
    internal MfdPage Selection { get; private set; } = (MfdPage)(-1);
    internal int Available { get; private set; }
    internal int Generation { get; private set; }
    internal static bool Fitted(MfdPage page, int available) => (int)page >= 0 && (int)page < MfdPages.Count && (available & (1 << (int)page)) != 0;
    internal static int Count(int available)
    {
        var count = 0;
        for (var page = 0; page < MfdPages.Count; page++) if (Fitted((MfdPage)page, available)) count++;
        return count;
    }
    internal bool Show(MfdPage current, int available)
    {
        if (Count(available) == 0) return false;
        Available = available; Selection = Fitted(current, available) ? current : Next((MfdPage)(-1), 1, available);
        Open = true; Generation++; return true;
    }
    internal void Refresh(int available)
    {
        if (Available == available) return;
        Available = available;
        if (!Open) return;
        if (!Fitted(Selection, available)) Selection = Next(Selection, 1, available);
        Generation++;
    }
    internal bool Move(int direction)
    {
        if (!Open || direction == 0 || Count(Available) < 2) return false;
        var next = Next(Selection, direction, Available);
        if (next == Selection) return false;
        Selection = next; Generation++; return true;
    }
    internal bool Confirm(out MfdPage page)
    {
        page = Selection;
        if (!Open || !Fitted(page, Available)) return false;
        Close(); return true;
    }
    internal void Close() { if (Open) { Open = false; Generation++; } }
    private static MfdPage Next(MfdPage start, int direction, int available)
    {
        var index = (int)start;
        if (index < -1 || index >= MfdPages.Count) index = direction > 0 ? -1 : 0;
        for (var step = 1; step <= MfdPages.Count; step++)
        {
            var page = (MfdPage)((index + (direction > 0 ? step : -step) + MfdPages.Count * 2) % MfdPages.Count);
            if (Fitted(page, available)) return page;
        }
        return (MfdPage)(-1);
    }
}
