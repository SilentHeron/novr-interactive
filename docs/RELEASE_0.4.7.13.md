# 0.4.7.13

- Fixes cockpit widgets appearing tens of metres away: pilot viewpoint bones inherit 100× model scale on FS-20 and KR-67. Widget positions and hand contact now use metres without bone scale.
- Waits for the current aircraft's tracked camera before placing the layout.
- Removes empty legacy HUD backplates and gives the weapon widget a compact safety indication instead of the large grey overlay.

**View → Edit layout:** grip to move any widget or flight control, or grab opposite widget corners with both hands to resize. Reopen View to save or cancel.
