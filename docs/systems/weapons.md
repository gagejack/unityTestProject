# Weapons

## Purpose
The gun the player holds: equipping, aiming, firing (automatic, semi-automatic, pump), reloading, ammo, dropping (Q) and picking up (F), dual handguns, rockets, the weapon arm pose and the ammo HUD. Each gun's behaviour is data in a WeaponSettings asset.

## Files
- Folder: `Assets/Game/Weapons/`
- `WeaponCombatController` (on Player): input, ammo, reload, the shot, aim, where the hands hold and aim the gun each frame (`GunHands`; the arms are moved by the animation's pose step), HUD built at runtime, pickups, debug equip and spawn, points.
- `WeaponSettings`: one asset per gun in `Assets/Game/Weapons/Settings/` (48 assets; the 46 active ones are listed in the controller's Offers). Its Category field (pistols, SMGs, assault rifles, shotguns, rifles and snipers, machine guns, launchers) groups the debug menu.
- `RocketProjectile` (launchers), `DroppedWeapon` (pickups on the ground).
- `MysteryWeaponBox`: kept only because the controller still references it; no box is in the scene.
- Fitted gun prefabs: `Assets/Game/Weapons/Prefabs/`. Icons and materials: `Assets/Game/Weapons/Art/`. Gun sounds: `Assets/Game/Weapons/Audio/` plus `Assets/ThirdParty/FreeWeaponSFX_v1.1/`.

## How to tune by feel
- Player > Weapon Combat Controller > **HUD Font** sets the points, ammo, weapon name and pickup prompt font. Main uses Doppio One from `Assets/Game/UI/Fonts/DoppioOne-Regular.ttf`.
- On a WeaponSettings asset: fire rate and trigger mode (pace and rhythm), damage and pellets, spread and bloom (how much accuracy falls off when spraying), recoil (camera and gun kick), movement multiplier (how heavy the gun feels to carry), grip and support-hand offsets (how it sits in the hands), the Soldier clips including **Hold** (the arm pose while sprinting; empty for a one-handed carry), sounds and pitch.
- Player > Weapon Combat Controller: **Aiming Move Speed Multiplier / Shooting Move Speed Multiplier** (how much aiming and firing slow you), **Aim Blend Time** (how quickly aim settles), **Support Hand Weight**; **Run Block After Shot** (how long after a shot before you can sprint again; 0 keeps you sprinting through the shot, slowed briefly by the shooting slowdown). Holding Shift + W resumes the sprint by itself when the block ends; a double-tap W sprint is cancelled by the block and needs another double-tap.

## How to test
F5 goes to the Gun Range; F1 > Weapons equips any gun; F9 resets targets and refills ammo. Fire at 5, 15 and 30 m, crouch behind the low cover, strafe-fire on the strafe lane, track the moving targets, shoot the stationary mobs, and fire a launcher at the blast wall. Measured by the weapon sequences of the baseline harness.

## Known limits
- The controller mixes many jobs in one file.
- Each Soldier animation family takes its clips from the first gun of that family in Offers, so reordering Offers changes animations.
- Fire rate depends on frame rate: automatic guns fire slower than their setting at some frame rates, and clicks during the cooldown are dropped (measured; [feel spec](../FEEL_SPEC.md#weapons)).
- Aimed guns point slightly off the crosshair point (measured; [feel spec](../FEEL_SPEC.md#weapons)).
- 21 pack guns share a material whose texture file was already missing in the original project.
- There is no holster yet: an unequipped gun is not shown on the body.
