# Status

**Checkpoint 6 (plan step 5): movement rebuild.**

## Current state
- `CapsuleMovement` rewritten: each frame is a short sequence of named steps (longest method 51 lines, was 272). Every Inspector field has a tooltip.
- Movement has its own assembly (Core only). Its links to other systems go through new Core contracts: `IMovementModifier` (guns, unarmed attacks), `IViewHeading` (camera), `IControlBlocker` and `PlayerControl` (debug menu, flying camera).
- Frame-rate independent: jump apex 1.60 m and walk stop 0.18 m at 30/60/144 fps (were 1.46-1.57 m and 0.11-0.17 m). Speeds, settle times and armed or strafe values unchanged.
- Play-mode checks `MovementFeelTests` (6) pass. The baseline jump metric was fixed (it measured from after the first frame's rise).
- Known limit: dive distance varies by up to one frame of dive travel (12.1-12.5 m).
- Lines: movement script 657 to 693 (tooltips, named steps, exact integration); Core contracts +71; tests +79.

## Last feedback
- FEEL_SPEC targets approved. Holster: H toggles; right-click draws; stays holstered until H or right-click. Sprinting does not holster; armed sprint animations needed.

## Next step
Plan step 6: animation and pose fixes (skeleton proportions and avatar root, one pose order with built-in Humanoid IK, armed sprint from Sprint01 legs and the WeaponHold upper body).
