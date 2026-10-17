# Status

## HUD font checkpoint
- Main's speed, points, ammo, weapon name and pickup prompt text now use the bundled Doppio One font. Player > Weapon Combat Controller > HUD Font controls the runtime text; Speed HUD > Speed Text > Text > Font controls speed.
- Validation: compilation succeeded; all 28 Play-mode tests passed; all five HUD text components use Doppio One in Play mode and the Console has no errors. No per-frame code changed.
- Docs check remains blocked by the existing missing reference image `docs/spec/UI/DebugMenu/sampleUI.jpg`.
- Next: owner checks HUD readability in Play mode at the Gun Range (F5), including ammo, points and pickup prompts.

**Checkpoint 16 (plan step 6c): armed sprint. Fixed view aiming specified (not built).**

## Current state
- Sprinting with a two-handed gun keeps the sprint legs and torso and puts that gun family's WeaponHold pose on the arms (assault rifle, rifle, bazooka; brought over from the original project's Kevin Iglesias pack with their import settings). The support hand stays on the gun. Pistols and dual handguns keep a one-handed carry with swinging arms.
- Each WeaponSettings asset has a new **Hold** clip in its Soldier clips; all 37 two-handed guns have one.
- Measured (30 / 60 / 144 fps): support hand gap sprinting with an assault rifle 160 → 0 cm (rifle, shotgun and bazooka also 0); sprint lean 34.6-34.7° armed and unarmed; unarmed run arms 38.5°; foot slide, armed walk, aiming and support grip unchanged. Side view of the rifle sprint: `Logs/Baseline/armed_sprint_side/` (not committed).
- New Play-mode check: the support hand stays within 5 cm of an assault rifle's grip while sprinting, at 30, 60 and 144 fps. All 28 Play-mode tests pass.

## Measured, not yet decided
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor (fix deferred by the owner).
- Foot slide is still above the ankle targets for walk, run and strafe.

## Later
- Pixel-style debug menu on F2, built from `docs/spec/UI/DebugMenu/DESIGN_SPEC.md`: all six pages work (weapon category and one row per weapon with Equip / Spawn, mobs, stamina, camera, volumes and feedback sounds, controls). Layout redesigned: tinted nav column with CLOSE at its foot, settings in a label column with aligned controls, weapon names before their buttons under a fixed category bar, grouped controls. It has no weapon search yet. The F1 menu got the same layout principles in its own style (sidebar with Resume at the foot, header and footer text removed, actions beside the weapon preview, setting rows). Next: owner playtest of both menus, then decide whether F2 replaces F1.

## Last feedback
- Armed sprint: looks good (owner playtest).

## Next step
Fixed view aiming, agreed in [the spec](spec/FixedViewAiming/SPEC.md): the body faces the mouse cursor and shoots in any direction; sprint only within 45° of screen-up. Three checkpoints: aim point and facing, shooting, sprint rules and look-ahead. Holstering follows.
