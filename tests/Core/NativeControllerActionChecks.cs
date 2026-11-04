using NOVR.MotionControls;

internal static class NativeControllerActionChecks
{
    internal static void Run(Action<string, Action> check, Action<bool> truth)
    {
        check("Native action names preserve game input spelling and distinct weapon intents", () =>
        {
            var names = new (string Name, NativeControllerAction Action)[]
            {
                ("Brake", NativeControllerAction.Brake), ("Fire", NativeControllerAction.Fire),
                ("Guns", NativeControllerAction.Guns), ("Weapon Release", NativeControllerAction.WeaponRelease),
                ("Countermeasures", NativeControllerAction.Countermeasures), ("Select", NativeControllerAction.Select),
                ("Next Weapon", NativeControllerAction.NextWeapon), ("Previous Weapon", NativeControllerAction.PreviousWeapon),
                ("Next Countermeasure", NativeControllerAction.NextCountermeasure), ("Gear", NativeControllerAction.Gear),
                ("Radar", NativeControllerAction.Radar), ("Toggle Engine", NativeControllerAction.ToggleEngine),
                ("Flight Assist", NativeControllerAction.FlightAssist), ("Auto Hover", NativeControllerAction.AutoHover),
                ("Eject", NativeControllerAction.Eject), ("Nav Lights", NativeControllerAction.NavLights),
                ("Link Guns", NativeControllerAction.LinkGuns)
            };
            foreach (var (name, action) in names)
                truth(NativeControllerActionNames.Parse(name) == action);
            foreach (var unknown in new[] { null, "", "gear", "NextWeapon", "Weapon Release ", "Unrelated game action" })
                truth(NativeControllerActionNames.Parse(unknown) == NativeControllerAction.Unknown);
        });
        check("Only previously supported one-shot native intents can enter the queue", () =>
        {
            var allowed = new[]
            {
                NativeControllerAction.Gear, NativeControllerAction.Radar, NativeControllerAction.ToggleEngine,
                NativeControllerAction.FlightAssist, NativeControllerAction.NextWeapon, NativeControllerAction.PreviousWeapon,
                NativeControllerAction.NextCountermeasure, NativeControllerAction.Eject, NativeControllerAction.AutoHover,
                NativeControllerAction.NavLights, NativeControllerAction.LinkGuns
            };
            foreach (var action in Enum.GetValues<NativeControllerAction>())
                truth(NativeControllerActionNames.CanQueue(action) == allowed.Contains(action));
            truth(!NativeControllerActionNames.CanQueue((NativeControllerAction)999));
        });
    }
}
