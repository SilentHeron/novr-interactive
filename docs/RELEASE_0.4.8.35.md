# 0.4.8.35 — the game's own select, unselect and fire; restart without VR

Two requests from players
([issue 1](https://github.com/SilentHeron/novr-interactive/issues/1),
[issue 2](https://github.com/SilentHeron/novr-interactive/issues/2)).

## Controller actions closer to the game

- **Hold Select target to select a group.** A tap still selects one target.
  Holding the button past the game's press delay selects every hostile unit
  inside the look-direction cue that the selected weapon can engage, which is
  the game's own group selection. Until now a hold never reached the game from
  a VR controller.
- **Hold Unselect to clear all targets.** A tap unselects the most recently
  selected target; holding the same button clears them all. One button now
  does both, as in the game. Unselecting also resets the target camera and
  clearing also clears the map's highlighted icons, which the mod's direct
  calls used to skip. **Clear all targets** remains as a separate, optional
  binding and the View menu's two buttons use the same complete paths.
- Both use the game's own **Button Click Delay** and **Button Hold Delay**
  (Settings → Controls; 0.25 s and 0.15 s by default). A release after the
  click delay is no longer a tap. A press between the two delays does both,
  exactly as on a keyboard. On the default **B** that means a slow tap of
  0.15 to 0.25 s clears every target, not only the latest; raise Button Hold
  Delay if that catches you out.
- **A tap takes what the game ranks highest.** Inside the cue's cone the game's
  own priority score now decides: threat and opportunity for the selected
  weapon, divided by range and by the squared distance from the cue. Since
  0.4.8.12 a tap took the marker nearest the centre of the cue instead, and
  0.4.8.33 added class tiers on top. Those remain as choices under **View →
  Look direction cue → Targeting** (also Settings → HUD): **Game**, **Nearest**,
  **Units first**, **Aircraft first**. The former stock choice, Units first, is
  moved to Game once; Nearest and Aircraft first are kept, and so is Units
  first if you pick it again. **Cue friendly units** still applies.
- **New binding: Fire selected weapon.** The game's single fire button, for
  guns and for missile or bomb release alike. It starts unassigned and the
  split layout (guns on the trigger, release on A) stays the default. To free
  a button, assign it under **View → Controller bindings → Weapons & targets**
  and leave **Guns** and **Missile / weapon release** unassigned.

Cycling weapons and countermeasures still acts on any release, and the other
bindings are unchanged.

## Switching VR off and on

- **Restart without VR.** In the headset, open the main menu's **VR UI
  SETTINGS** and click **RESTART WITHOUT VR**, then **CONFIRM RESTART**. The
  game closes and starts again on the desktop with NOVR switched off.
- **Restart in VR.** In that flat session, the main menu shows a small button
  in the bottom-left corner of the window; it or **F11** restarts in VR. It is
  offered at the main menu only, so it cannot end a flight. The switch lasts
  for that session: starting the game again from Steam also returns to VR.
- **When VR did not start.** The desktop notice now has a **Restart without
  VR** button, for a session at the desk when the headset is not connected.
- Both restarts keep your other launch options and the Steam or Proton
  environment. They work for a game started by Steam; otherwise the game stays
  open and the message names the launch option to use instead.
- `-novr-flat` in the launch options still starts without VR every time. It has
  existed since 0.4.8.13 and was only mentioned under troubleshooting.

VR cannot be switched within a running session: the renderer, the headset
session and the mod's changes to the game are all set up as the game starts.

## What has been checked

The maintainer tried this build briefly in a Steam Frame headset on Linux with
Proton before release: it starts, every adjustment to the game installs, saved
settings carry over and the target priority moves to Game. The automatic checks
cover the tap and hold timing, the binding rules, the ranking and its one-time
move, and the restart's arguments and refusals.

Not yet confirmed:

- **The restart with or without VR has not been run**, on Windows or on Linux
  with Proton. On Proton this is the first real use of the relaunch it is
  built on. If it misbehaves, starting the game again from Steam returns to VR,
  and `-novr-flat` in the launch options starts without it.
- The Touch layout, including Pico controllers through Virtual Desktop.
- Whether slow taps on **B** clear all targets more often than is comfortable
  with the game's default delays.
- Whether the game's ranking brings back the case the class tiers were added
  for, a lock going to a missile instead of the aircraft that fired it.

One package for Windows and Linux/Proton. Saved layouts, bindings and settings
are preserved.
