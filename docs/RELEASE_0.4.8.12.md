# 0.4.8.12

- Single-target selection chooses the eligible marker nearest the look cue, without native threat/range weighting. Adjusting the cue's position also adjusts targeting alignment.
- Removed target-candidate caching and the native designator's exaggerated head rotation. Already-selected targets remain excluded.
- Fixed two-hand widget edit highlights and stale menu recenter requests.
- Restore borrowed menu input bindings when their owner closes or changes, preserving subsequent external rebindings.
- Apply native settings once when saving; separated settings access and menu lifetime responsibilities.
- Continued HUD, sleeve and native-menu cleanup, and prevented stale installer requests from downgrading a newer installation.

Saved layouts, bindings, fitted poses and SteamVR quality settings are unchanged.
