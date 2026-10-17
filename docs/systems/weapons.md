# Weapons

## Purpose
The gun the player holds: equipping, aiming, firing (automatic, semi-automatic, pump), reloading, ammo, dropping (Q) and picking up (F), dual handguns, rockets, the weapon arm pose and the ammo HUD. The crosshair sits in the screen centre in third person and at the hidden mouse pointer in the fixed view; unarmed in the fixed view, a small dot marks the pointer instead. Each gun's behaviour is data in a WeaponSettings asset.

Where shots go: in third person, toward what the crosshair's camera ray touches. In the fixed view, along the gun's aim line: the body's facing, at the cursor aim point's distance and height (the aim point comes from the camera). Once the body faces the cursor the barrel points exactly at the aim point; during a turn the gun turns with the body, so shots fired mid-turn spray across it. In the fixed view the wrist also turns the barrel onto the aim line between aimed shots (not while running or reloading). Right mouse gives the same precise aim as in third person (raised gun, tighter spread, slower walk, no sprint) without the camera zoom.

## Files
- Folder: `Assets/Game/Weapons/`
- `WeaponCombatController` (on Player): input, ammo, reload, the shot, aim, where the hands hold and aim the gun each frame (`GunHands`; the arms are moved by the animation's pose step), HUD built at runtime, pickups, debug equip and spawn, points.
- `WeaponSettings`: one asset per gun in `Assets/Game/Weapons/Settings/` (48 assets; the 46 active ones are listed in the controller's Offers). Its Category field (pistols, SMGs, assault rifles, shotguns, rifles and snipers, machine guns, launchers) groups the debug menu.
- `RocketProjectile` (launchers), `DroppedWeapon` (pickups on the ground).
- `MysteryWeaponBox`: kept only because the controller still references it; no box is in the scene.
- The bottom-right HUD uses Doppio One with a fine dark outline and offset shadow. Cash is a dollar amount beside a transparent cash bag; the larger ammo count has the equipped weapon's sprite on its right, with the weapon name below. Unarmed hides ammo and the gun icon; reloading keeps the numeric ammo count. Pickup prompts retain the same text styling.
- HUD art: `Assets/Game/UI/Icons/`. The supplied weapon sheet is sliced into named sprites with point filtering; every weapon settings asset has a HUD Sprite assignment. Duplicate models and simplified Soldier guns share the closest matching sprite. The original menu preview Icon stays independent. The cash bag's transparent copy preserves the supplied JPEG as its source.
- Fitted gun prefabs: `Assets/Game/Weapons/Prefabs/`. Icons and materials: `Assets/Game/Weapons/Art/`. Gun sounds: `Assets/Game/Weapons/Audio/` plus `Assets/ThirdParty/FreeWeaponSFX_v1.1/`.

## Weapon names
The menus, equipped-weapon HUD and pickup prompts show only the gun name, so separate assets and loadouts can share a display name. Lettered CASTLE BRAVO guns follow the publisher's [weapon reference spreadsheet](https://74abae92-d3c7-4671-ada0-b786f4d5dc28.filesusr.com/ugd/aa33f4_cd6e9d5452d042228a76e1fb04b80aee.xlsx). The Classic minigun, pistol, revolver, rifle, scoped rifle, shotgun and grenade launcher remain visual identifications without publisher confirmation. The four simplified Soldier models use weapon-type names.

## How to tune by feel
- Player > Weapon Combat Controller > **HUD Font** sets the points, ammo, weapon name and pickup prompt font. Main uses Doppio One from `Assets/Game/UI/Fonts/DoppioOne-Regular.ttf`.
- Player > Weapon Combat Controller > **Cash Bag Sprite** sets the cash symbol. Each weapon settings asset's **Hud Sprite** selects its ammo-row icon.
- On a WeaponSettings asset: fire rate and trigger mode (pace and rhythm), damage and pellets, spread and bloom (how much accuracy falls off when spraying), recoil (camera and gun kick), movement multiplier (how heavy the gun feels to carry), grip and support-hand offsets (how it sits in the hands), the Soldier clips including **Hold** (the arm pose while sprinting; empty for a one-handed carry), sounds and pitch.
- Player > Weapon Combat Controller: **Aiming Move Speed Multiplier / Shooting Move Speed Multiplier** (how much aiming and firing slow you), **Aim Blend Time** (how quickly aim settles), **Support Hand Weight**; **Run Block After Shot** (how long after a shot before you can sprint again; 0 keeps you sprinting through the shot, slowed briefly by the shooting slowdown). Holding Shift while moving resumes the sprint by itself when the block ends; a double-tap W sprint is cancelled by the block and needs another double-tap. **Fire Turn Limit** (fixed view): sprinting away from the cursor, a click stops the sprint, the body turns back, and the shot fires once the gun is within this angle of the cursor. Lower waits longer for a straighter first shot.

## How to test
F5 goes to the Gun Range; F1 > Weapons equips any gun; F9 resets targets and refills ammo. Fire at 5, 15 and 30 m, crouch behind the low cover, strafe-fire on the strafe lane, track the moving targets, shoot the stationary mobs, and fire a launcher at the blast wall. Measured by the weapon sequences of the baseline harness. Fixed view shooting: the aim ring (F8); `FixedViewAimingTests` (Game.Movement.Tests) checks the barrel aim error for a rifle, a pistol and dual handguns, aimed shots hitting all eight ring targets, and the turn before a shot after sprinting away.

## Known limits
- The controller mixes many jobs in one file.
- Each Soldier animation family takes its clips from the first gun of that family in Offers, so reordering Offers changes animations.
- Fire rate depends on frame rate: automatic guns fire slower than their setting at some frame rates, and clicks during the cooldown are dropped (measured; [feel spec](../FEEL_SPEC.md#weapons)).
- Aimed guns point slightly off the crosshair point (measured; [feel spec](../FEEL_SPEC.md#weapons)).
- 21 pack guns share a material whose texture file was already missing in the original project.
- There is no holster yet: an unequipped gun is not shown on the body.
