# Status

**Checkpoint 5 (plan step 4): mob AI moved to `MobBrain`. No behaviour change.**

## Current state
- SpiderDroneBrain became `MobBrain` (same asset ID, so all six prefabs and the stationary mobs keep their saved values). Spider-only code removed: pounce (rear, leap, recover), shop leftovers, status overlay, the unused hit event.
- Mobs have their own assembly referencing Core only: attacks damage any `IDamageable`, the default target comes from `PlayerPresence`.
- Re-measured all six mobs at 30/60/144 fps: identical to the baseline. Mob code 676 to 534 lines (brain 382 to 236).
- Decisions 0002 (built-in Humanoid IK) and 0003 (keep the custom camera) accepted; armed sprint approach approved.

## Last feedback
- FEEL_SPEC targets approved. Holster: H toggles; right-click draws; stays holstered until H or right-click. Sprinting does not holster; armed sprint animations needed.

## Next step
Plan step 5: movement rebuild: split the long movement method, make jump height and stopping distance frame-rate independent (targets in FEEL_SPEC), own assembly.
