# Status

**Checkpoint 16 (plan step 6c): armed sprint.**

## Current state
- Sprinting with a two-handed gun keeps the sprint legs and torso and puts that gun family's WeaponHold pose on the arms (assault rifle, rifle, bazooka; brought over from the original project's Kevin Iglesias pack with their import settings). The support hand stays on the gun. Pistols and dual handguns keep a one-handed carry with swinging arms.
- Each WeaponSettings asset has a new **Hold** clip in its Soldier clips; all 37 two-handed guns have one.
- Measured (30 / 60 / 144 fps): support hand gap sprinting with an assault rifle 160 → 0 cm (rifle, shotgun and bazooka also 0); sprint lean 34.6-34.7° armed and unarmed; unarmed run arms 38.5°; foot slide, armed walk, aiming and support grip unchanged. Side view of the rifle sprint: `Logs/Baseline/armed_sprint_side/` (not committed).
- New Play-mode check: the support hand stays within 5 cm of an assault rifle's grip while sprinting, at 30, 60 and 144 fps. All 28 Play-mode tests pass.

## Measured, not yet decided
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor (fix deferred by the owner).
- Foot slide is still above the ankle targets for walk, run and strafe.

## Next step
Owner playtest: equip an assault rifle, a rifle or shotgun, a bazooka and a pistol from F1 and sprint (Shift + W) on the Movement Course (F6). Then the next plan step: holstering (H toggles; guns stowed on the hip or back by category, with draw and holster moves).
