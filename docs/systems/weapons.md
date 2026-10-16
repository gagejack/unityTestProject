# Weapons

## Purpose
The gun the player holds: equipping, aiming, firing (automatic, semi-automatic, pump), reloading, ammo, dropping (Q) and picking up (F), dual handguns, rockets, the weapon arm pose and the ammo HUD. Each gun's behaviour is data in a WeaponSettings asset.

## Files
- Folder: `Assets/Game/Weapons/`
- `WeaponCombatController` (on Player): input, ammo, reload, the shot, aim and support-hand arm pose, HUD built at runtime, pickups, debug equip and spawn, points.
- `WeaponSettings`: one asset per gun in `Assets/Game/Weapons/Settings/` (48 assets; the 46 active ones are listed in the controller's Offers). Its Category field (pistols, SMGs, assault rifles, shotguns, rifles and snipers, machine guns, launchers) groups the debug menu.
- `RocketProjectile` (launchers), `DroppedWeapon` (pickups on the ground).
- `MysteryWeaponBox`: kept only because the controller still references it; no box is in the scene.
- Fitted gun prefabs: `Assets/Game/Weapons/Prefabs/`. Icons and materials: `Assets/Game/Weapons/Art/`. Gun sounds: `Assets/Game/Weapons/Audio/` plus `Assets/ThirdParty/FreeWeaponSFX_v1.1/`.

## How to tune by feel
- On a WeaponSettings asset: fire rate and trigger mode (pace and rhythm), damage and pellets, spread and bloom (how much accuracy falls off when spraying), recoil (camera and gun kick), movement multiplier (how heavy the gun feels to carry), grip and support-hand offsets (how it sits in the hands), sounds and pitch.
- Player > Weapon Combat Controller: **Aiming Move Speed Multiplier / Shooting Move Speed Multiplier** (how much aiming and firing slow you), **Aim Blend Time** (how quickly aim settles), **Support Hand Weight**.

## How to test
F1 > Weapons > Equip any gun, then use the Combat Practice Lane: fire at 5, 15 and 30 m, fire past the Shoulder cover, and fire while strafing. Spawn mobs (F1 > Mobs) to hit characters. No Play-mode checks exist yet.

## Known limits
- The controller mixes many jobs in one file.
- Each Soldier animation family takes its clips from the first gun of that family in Offers, so reordering Offers changes animations.
- Fire rate is tied to frame timing (hypothesis: slower at low frame rates; to be measured).
- 21 pack guns share a material whose texture file was already missing in the original project.
- There is no holster yet: an unequipped gun is not shown on the body.
