namespace NOVR.MotionControls;

// These are game input intents, not configurable controller bindings. AutoHover
// deliberately represents the native long-press branch of Flight Assist.
internal enum NativeControllerAction
{
    Unknown,
    Brake,
    Fire,
    Guns,
    WeaponRelease,
    Countermeasures,
    Select,
    NextWeapon,
    NextCountermeasure,
    Gear,
    Radar,
    ToggleEngine,
    FlightAssist,
    AutoHover,
    Eject,
    NavLights
}

internal static class NativeControllerActionNames
{
    // Translate strings only where NOVR joins the game's Rewired input API.
    internal static NativeControllerAction Parse(string? name) => name switch
    {
        "Brake" => NativeControllerAction.Brake,
        "Fire" => NativeControllerAction.Fire,
        "Guns" => NativeControllerAction.Guns,
        "Weapon Release" => NativeControllerAction.WeaponRelease,
        "Countermeasures" => NativeControllerAction.Countermeasures,
        "Select" => NativeControllerAction.Select,
        "Next Weapon" => NativeControllerAction.NextWeapon,
        "Next Countermeasure" => NativeControllerAction.NextCountermeasure,
        "Gear" => NativeControllerAction.Gear,
        "Radar" => NativeControllerAction.Radar,
        "Toggle Engine" => NativeControllerAction.ToggleEngine,
        "Flight Assist" => NativeControllerAction.FlightAssist,
        "Auto Hover" => NativeControllerAction.AutoHover,
        "Eject" => NativeControllerAction.Eject,
        "Nav Lights" => NativeControllerAction.NavLights,
        _ => NativeControllerAction.Unknown
    };

    internal static bool CanQueue(NativeControllerAction action) => action is
        NativeControllerAction.Gear or NativeControllerAction.Radar or NativeControllerAction.ToggleEngine or
        NativeControllerAction.FlightAssist or NativeControllerAction.NextWeapon or
        NativeControllerAction.NextCountermeasure or NativeControllerAction.Eject or NativeControllerAction.AutoHover or
        NativeControllerAction.NavLights;
}
