# Status

## Debug menu keys checkpoint
- F1 now opens the pixel-style menu (previously F2). The original menu is deprecated and moved to F2; its sidebar says "DEPRECATED". Play mode starts with no menu open (Main Camera > View Mode Debug Menu > Show Menu is off).
- Validation: compiles cleanly; all 8 debug menu Play-mode checks pass, including a new one that Play mode starts with both menus closed.
- Try: press Play (no menu), click the Game view and move; F1 opens/closes the new menu; F2 opens the deprecated one.

## Weapon name checkpoint
- All 46 active weapon entries now show only the gun name. Removed confidence, creator, Classic, dual/scoped and Generic labels; duplicate models and loadouts can share a name. Both menus, the weapon HUD and pickup prompts read these names.
- Validation: all 46 saved labels are free of qualifiers; all seven menu Play-mode checks pass.
- Try F1 > Weapons, equip a weapon, then drop it to check its pickup name.

**Checkpoint 21 (holstering, step 2 of 2): the draw and holster motion.** Agreed in [the holstering spec](spec/Holstering/SPEC.md).

## Current state
- Holstering, the right hand (both hands for dual handguns) reaches the holster; at the contact the gun leaves the hand and swings onto its spot. Drawing is the reverse. A swap is a holster then a draw. Back guns play the Kevin Iglesias UnsheatheBack01_R / SheatheBack01_R clips on the upper body (copied from the original project), with IK pulling the hand onto the gun near the right shoulder; hip guns reach by IK alone, because the pack's hip clip is a cross-draw from the *left* hip (a change from "clips for both", flagged for the owner).
- Measured at 30, 60 and 144 fps (rifle and pistol): draw and holster 0.500 s each; hand within 1-2.5 cm of the gun when it changes hands; no jump of the gun (largest one-frame step at 144 fps 12.4 cm). The back holster's arm swing is fast (gun up to 15-18 m/s, the same at every frame rate); Draw Time and Holster Time are on Player > Weapon Holster.
- During a draw, holster or swap you can walk but not fire, aim, reload, run or punch.
- The motion's per-frame work runs only while switching (a few vector operations; not profiled separately). All holster checks pass.

## Checkpoint 20 (holstering): two guns, H swaps
- **Owner change:** the holster is a second slot. With a gun holstered the hands can carry another; **H** swaps them (holsters when the holster is empty, draws when the hands are empty). Each gun keeps its own magazine and reserve. Right mouse with empty hands draws into aim. The HUD shows the holstered gun after the one in hand ("H: <gun>").
- **Q** drops only the gun in the hands; **F** picks up whenever the hands are empty. F9 refills both guns.
- With empty hands the player is unarmed (punches, kicks, unarmed walk, no crosshair). A swap cancels a reload and aiming; a shot queued on that frame is refunded.

## Checkpoint 19 (holstering, step 1 of 2): stowing, controls and placement
- Placement from the category (`WeaponHolster` on the Player): pistols and SMGs on the right hip, muzzle down; dual handguns on both hips; all other categories across the back, muzzle up over the right shoulder. Every active gun was measured on its spot; per-gun settings fix the outliers: the four long SMGs (about 1.3 m with stocks) ride on the back (**Holster On Back**, a small departure from "SMGs on the hip"), the three rocket launchers sit higher, and the M2 .50 cal is rolled so its tripod faces away (one leg still pokes forward).
- Unarmed left click no longer needs the Unity window in front (same reason as the gun at checkpoint 18).
- Play-mode checks (`HolsterTests`): every active gun on its category's spot and clear of the floor; empty-handed punch; Q leaves a holstered gun; right mouse draws into aim; H swaps two guns, each keeping its magazine.

## Waiting on the owner's playtest
- Checkpoints 17-18 (fixed view aiming): facing the cursor, shooting at it, Shift sprint in any direction, the sprint facing rule. F1 > Camera > Fixed, then F8 (aim ring). Details on the [camera](systems/camera.md), [movement](systems/movement.md) and [weapons](systems/weapons.md) pages.
- The backward-running lean (about 22° back).

## Backlog
- **Fixed view aiming checkpoint 3 (moved to the backlog by the owner, 2026-10-17):** camera look-ahead toward the cursor ([spec](spec/FixedViewAiming/SPEC.md)). Until then anything more than about 7 m behind the character is below the screen.
- **Shooting while diving (owner request, 2026-10-17):** lightweight gun categories can fire during an air dive. Today every gun is blocked while rolling, diving or sliding. To decide: which categories (proposed: Pistols & Duals and SMGs), whether the landing slide and rolls count, and where the shot aims.

## Flaky check
- The aim ring shooting check failed 2 times in about 10 runs (once the body stopped following the cursor partway through; both failed runs were unusually short). Its failure message now reports whether the game was following the cursor and whether the window had focus, to pin down the cause next time.

## Measured, not yet decided
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor (fix deferred by the owner).
- Foot slide is still above the ankle targets for walk, run and strafe.

## To do
- **Debug menu:** the pixel-style menu is now on F1 and the old menu is deprecated (F2). Before removing the old menu, the new one needs weapon search, which only the old menu has. Built from `docs/spec/UI/DebugMenu/DESIGN_SPEC.md`.
- **HUD:** cash bag symbol, weapon-sheet sprite beside the ammo, Doppio One with outline and shadow. Owner to check readability at the Gun Range (F5), then try equipping, reloading and dropping a gun. Settings are in [weapons](systems/weapons.md).
- The debug menus' controls lists do not mention H yet (those files are being edited in the debug menu work).

## Last feedback
- "If a gun is holstered allow the user to carry another weapon in their hands, pressing H should swap weapons", then "continue with step 2" (both built: checkpoints 20 and 21).
- Earlier: moved fixed view aiming checkpoint 3 to the backlog; chose the holster controls (H, right mouse draws), key-only holstering, sheathe clips with IK for the motion, and Q only for a gun in hand.

## Next step
Owner playtest of holstering: equip a rifle (F1 > Weapons), press H and watch the hand put it on the back; equip a pistol, then H swaps the two (watch the draw over the shoulder); try dual handguns (both hips). Judge the back holster speed and the IK-only hip draw. Then the owner picks the next item from the backlog.
