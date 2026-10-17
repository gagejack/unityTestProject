# Status

**Checkpoint 13 (plan step 6b, part 1): one pose step inside the animation graph.**

## Current state
- The running lean, the running-arm tuck and the chest aim pitch now happen inside the animation graph, in its last step (`BodyPoseJob`), instead of being written onto the bones after the animation. Same math, same settings; nothing should look different.
- Measured (walk, run, armed run, aimed rifle fire at 30 / 60 / 144 fps), before → after: sprint lean 34.7 → 34.6-34.7°, run arms 38.5 → 38.5°, run speed and foot slide unchanged within run-to-run noise, rifle aim error 1.6 → 1.5-1.6°. The walk arm angle varies 0-4° between two runs of the same code, so it is too noisy to compare.
- Cost of the new step: about 0.01 ms per frame while running or aiming.
- New Play-mode check `RunningPoseTests`: sprint lean and running arm angle stay on their feel-spec targets at 30, 60 and 144 fps. All 23 Play-mode tests pass.

## Proposed pose order (rest of step 6b)
1. Clips are mixed (locomotion, unarmed attacks, Soldier gun poses).
2. `BodyPoseJob`: running lean, running arms, chest aim pitch (done), then the gun hands with Humanoid IK goals: right hand aims the gun, left hand on the gun's support grip (next).
3. After the graph: special-move poses and grounding (`BlockHumanoidRig`), punch contact. The weapon arm solve in `WeaponCombatController` is removed once step 2 covers it.

## Measured, not yet decided
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor (fix deferred by the owner).
- Foot slide is still above the ankle targets for walk, run and strafe.

## Later
- The pixel button sheet in `docs/spec/UI/DebugMenu/` is a style reference for future main and pause menus.

## Next step
Owner playtest: run, sprint and aim at the Movement Course (F6) and gun range (F5); nothing should look different. Then checkpoint 14: the gun hands move into the pose step with Humanoid IK.
