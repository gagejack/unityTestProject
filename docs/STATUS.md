# Status

## Weapon name checkpoint
- All 46 active weapon entries now show only the gun name. Removed confidence, creator, Classic, dual/scoped and Generic labels; duplicate models and loadouts can share a name. Both menus, the weapon HUD and pickup prompts read these names.
- Validation: all 46 saved labels are free of qualifiers; all seven menu Play-mode checks pass.
- Try F2 > Weapons or F1 > Weapons, equip a weapon, then drop it to check its pickup name.

**Checkpoint 18 (fixed view aiming, step 2 of 3): shooting and sprint facing.**

## Current state
- **Shooting (fixed view):** the gun aims along the body's facing at the cursor aim point's distance and height; shots leave the muzzle along that line with the gun's normal spread, so they land at the cursor once the body faces it and spray across a turn. Between aimed shots the wrist turns the barrel onto the aim line (hip pose kept; checked in a close-up). Right mouse is the same precise aim as in third person without the zoom. Barrel error 0.00° for a rifle and pistol, 0.09° worst for dual handguns (target ≤ 0.5°).
- **Sprint facing (owner agreed):** sprinting keeps facing the cursor within 100° of the run (Player > Capsule Movement > Sprint Facing Limit); further round it faces the run, and turns back once the cursor is 20° inside the limit. A click while facing the run stops the sprint, turns, and fires once the gun is within 45° of the cursor (Weapon Combat Controller > Fire Turn Limit), about 0.2 s.
- Over a mob or target the aim point is now 0.2 m inside it along the cursor ray: aiming exactly at a corner the camera saw made the gun's shot graze past (found by the ring test).
- The aim ring is now 5 m across instead of 8 m: at 8 m the target behind the pad is below the bottom of the screen. The ring label moved behind the far target so it no longer covers the near ones.
- The gun no longer ignores the mouse while the Unity window is in the background; like movement, it relies on the Input System releasing buttons on focus loss. (That check made the shooting tests depend on which window was in front.)
- New Play-mode checks: barrel aim (3 guns, hip and aimed), aimed shots hitting all 8 ring targets including the one behind, and the sprint facing rule with turn-then-fire. All 39 Play-mode checks pass.

## Checkpoint 17 (fixed view aiming, step 1): aim point, cursor and facing
- In the fixed view the body turns toward the mouse cursor at **Cursor Turn Speed** (Player > Capsule Movement, 720°/s), armed or not. WASD stays screen-relative and the legs use the existing strafe and backpedal clips. Rolls, dives and slides still face their travel direction.
- The camera works out the aim point under the cursor (Main Camera > Follow Camera > Fixed view aiming): chest height (1.2 m) above plain floor, box tops and ledges; the exact point on mobs, targets and walls. Inside the 0.75 m dead zone, or with the cursor off the screen, the facing holds still. Movement reads it through `IViewHeading` in Core.
- The system pointer is hidden in the fixed view; the crosshair is drawn at the mouse (a small dot when unarmed). Escape or an open menu brings the pointer back and the character stops following it; clicking the Game view takes it again.
- New test station: the aim ring on the Gun Range (F8, pad GR-Ring), 8 targets in 45° steps, two on 1.5 m boxes.
- **Owner change:** Shift now sprints in any direction, in every view (the spec's 45° screen-up cone is dropped). Double-tap W still sprints forward only. Measured with the body facing up the screen: full sprint speed (6.75 m/s) in all 8 directions; upper body leans 31-35° forward running forward, 21-33° forward sideways, and 21-24° back running backward (the run clips' own posture plus the forward-run lean correction).
- New Play-mode checks (`FixedViewAimingTests`): 180° turn time at 30, 60 and 144 fps, dead-zone stability, the floor rule (floor, box top, target) and Shift sprint in all 8 directions. The cursor ray costs about 1 µs a frame with no allocations.

## Not yet built (next checkpoint)
- Camera look-ahead toward the cursor (checkpoint 3). Until then anything more than about 7 m behind the character is below the screen and cannot be aimed at.

## Measured, not yet decided
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor (fix deferred by the owner).
- Foot slide is still above the ankle targets for walk, run and strafe.

## To do
- **Shooting while diving (owner request, 2026-10-17):** lightweight gun categories can fire during an air dive. Today every gun is blocked while rolling, diving or sliding (`WeaponCombatController` refuses and refunds the shot). To decide when it is built: which categories count as lightweight (proposed: Pistols & Duals and SMGs), whether the landing slide and rolls are included, and where the shot aims (the dive pose points the gun along the dive).
- **Debug menu:** playtest the pixel-style F2 menu and the reworked F1 menu, then decide whether F2 replaces F1. F2 has no weapon search yet. Latest F2 change: slightly rounded button corners (press F2 and check the navigation, Equip / Spawn and arrow buttons). Built from `docs/spec/UI/DebugMenu/DESIGN_SPEC.md`.
- **HUD:** the cash total uses the cash bag symbol; larger ammo text has the matching weapon-sheet sprite to its right, with a larger weapon name below. Doppio One text has a thin dark outline and offset shadow; icons have a thin dark outline. Unarmed hides ammo and icon; reloads keep numeric ammo. All 46 selectable weapons switch to their assigned sprite; all 34 Play-mode checks pass. Owner to check readability at the Gun Range (F5), then try equipping, reloading and dropping a gun. Settings are documented in [weapons](systems/weapons.md).
- **F2 design spec:** rewritten from the current layout, stylesheet, controller and saved panel settings. It documents all six pages, button states, scaling and verification; the obsolete image reference and unbuilt sprite-button plan have been removed.

## Last feedback
- Agreed the sprint facing rule (face the cursor within 100° of the run, else face the run; a click turns back before firing). Shift sprinting in any direction was built at checkpoint 17.

## Next step
Owner playtest of fixed view shooting and sprint facing at the aim ring (F1 > Camera > Fixed, then F8). Then checkpoint 3 of the [fixed view aiming spec](spec/FixedViewAiming/SPEC.md): the camera look-ahead toward the cursor.
