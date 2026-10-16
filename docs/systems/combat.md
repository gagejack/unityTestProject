# Combat (hits, damage and feedback)

## Purpose
What happens when a shot or a strike lands: the physics query that finds the hit, the damage receiver, and the feedback (sounds, hitmarker, muzzle flash, tracers, impact effects). Shared by guns and fists.

## Files
- Folder: `Assets/Game/Combat/`
- `CombatPhysics`: nearest-hit ray and sphere casts that ignore the attacker and triggers.
- `TargetHealth` (on targets and mobs): takes damage, flashes white, and destroys its object at zero. Implements `IDamageable` from Core.
- `CombatFeedback` (on Player): shot, impact, melee and hitmarker feedback; it also triggers the queued shot after the final weapon pose (see the update order in [architecture](../ARCHITECTURE.md#update-order)).
- `CombatTransient`: moves and shrinks short-lived effects. `AudioVolumeSettings` and `GunAudioVolume`: master and gun volume, saved on this computer only.
- Sounds: `Assets/Game/Combat/Audio/` (melee swing, impact, 8 hitmarkers).

## How to tune by feel
- Player > Combat Feedback: overall feedback volume, which hitmarker and punch sound is used, and their volumes (also in F1 > Audio).
- A target's or mob's **Hit Points** and **Hit Flash Duration** are on its Target Health component.

## How to test
Hit the Gun Range targets, posts, melee dummy and stationary mobs with guns and fists; each hit shows a floating damage number and the target's remaining hit points. Listen for the hitmarker and watch the flash.

## Known limits
- No hit reactions: a hit only flashes the target. Mobs and targets vanish instantly at zero health.
- Mobs have one capsule collider, so there is no head or body distinction on them.
- The player has no health, so mob attacks cannot hurt the player.
