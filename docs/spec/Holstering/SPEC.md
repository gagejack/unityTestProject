# Holstering

**Status:** agreed with the owner on 2026-10-17 (answers below). Checkpoint 1 (stowing, controls, placement) is being built; checkpoint 2 (draw and holster animation) follows.

**Request (original brief, [handoff](../../HANDOFF_PROMPT.md)):** when the character is not actively holding the weapon, it is visibly stowed on the body. Pistols and SMGs go on the side of the hip, dual handguns on both hips; assault rifles, shotguns, rifles and snipers, machine guns and launchers on the back. Placement comes from `WeaponSettings.category` with an optional per-weapon offset. Draw and holster are animated (the hand moves to the holster and the gun follows it). Unarmed attacks work with the gun holstered. Drop (Q) and pickup (F) keep working.

## Decisions

| Question | Owner's answer |
|---|---|
| Controls | **H** holsters or draws. While holstered, **right mouse** draws straight into aim. Left click while holstered punches. |
| Does the gun ever holster by itself? | No, only on the key. The armed sprint (checkpoint 16) stays. |
| Draw and holster motion | The Kevin Iglesias Sheathe / Unsheathe clips (back and hip), brought over from the original project, with Humanoid IK putting the hand exactly on the holster ([decision 0002](../../decisions/0002-built-in-humanoid-ik.md)). |
| Q while holstered | Does nothing: Q only drops a gun in hand. Draw first, then Q. |

Consequence: F only picks up a gun when you have none, in hand or holstered. To swap guns: draw, Q, F.

## What the player experiences
- Holstered, the character is unarmed: unarmed walk and run, punches and kicks, no crosshair (the fixed view shows its small dot). The gun keeps its magazine and reserve ammo, and the HUD still shows it.
- Holstering cancels a reload in progress and stops aiming. A shot queued on the same frame is not lost.
- Placement: the gun's grip sits on a holster spot that moves with the body.
  - Pistols and SMGs: right hip, muzzle down along the thigh. Dual handguns: both hips, mirror images.
  - All other categories: across the back, muzzle up over the right shoulder.
  - Each WeaponSettings asset has a **Holster Offset** and **Holster Rotation** (zero by default) for models that need nudging.

## Build order (each a checkpoint with owner playtest)
1. **Stowing:** the holster state, controls (H, right mouse, Q rule), placement for every category, HUD. The swap between hand and holster is instant at this checkpoint. Checks: every category lands on its spot; holstered punches work; right mouse draws; Q leaves a holstered gun alone; ammo is kept.
2. **Draw and holster motion:** the sheathe clips on the arms, the hand reaching the holster by IK, the gun changing hands at the moment of contact. Checks: draw and holster times, the hand within a few centimetres of the grip at the change, the same result at 30, 60 and 144 fps.

## How it fits the architecture
- **Weapons** owns it: a holstered gun is still the equipped gun. `WeaponHolster` (on the Player, in `Assets/Game/Weapons/`) owns the holster state, the H and right-mouse input and the placement; `WeaponCombatController` treats a holstered gun as owned but not in hand (its Has Weapon is false), so animation, unarmed attacks and the HUD crosshair follow without changes.
- Placement parents the gun to a Humanoid bone (Hips or Chest). The rig's bones rest aligned with the character, so spots are written in plain character axes.
- Update order: `WeaponHolster` runs in the default slot (0), after the weapon controller; a change shows from the next frame.

## Test station
The Gun Range (F5) and the aim ring (F8), with every category from the debug menu.
