# Status

**Checkpoint 14 (plan step 6b, part 2): the gun arms move into the pose step.**

## Current state
- Aiming the gun and holding its support grip now happen inside the animation graph, in its last step (`BodyPoseJob`), after the running lean, arm tuck and chest aim pitch. `WeaponCombatController` keeps the recoil and grip state and says where the hands go (`GunHands`); it no longer moves the arm bones after the animation. Nothing should look different.
- Humanoid IK was tried first and dropped: it reached its target exactly but erased the running lean and arm edits made in the same step ([decision 0004](decisions/0004-arm-solver-inside-the-graph.md), needs your approval). The existing arm solver moved into the step instead.
- Measured before → after (armed walk, aimed and strafing rifle fire, pistol, rocket, run and armed run at 30 / 60 / 144 fps): aim error rifle 1.62 → 1.48-1.62°, pistol 0.62 → 0.62°, rocket 1.50 → 1.50°; support hand gap walking 0 → 0 cm; armed walk arm angle 5.7 → 5.7°; sprint lean 34.7°, run arms 38.5° and run foot slide unchanged.
- New code paths checked once (no earlier numbers to compare): dual handguns aim within 0.62° (right gun; the left gun looked level with it in the captured frames), and the shotgun's pump stroke pulls the support hand 10 cm, as designed.
- Cost: the gun arms add about 0.01 ms per frame inside the graph, replacing the same solve after it.
- All Play-mode tests pass.

## Measured, not yet decided
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor (fix deferred by the owner).
- Foot slide is still above the ankle targets for walk, run and strafe.
- Running with a gun still drops the support hand (about 160 cm from its grip); that is the armed sprint step.

## Next step
Owner playtest at the gun range (F5): equip a rifle, a pistol, dual handguns and a shotgun from F1, aim (right mouse) and fire; nothing should look different. Then the armed sprint: Sprint01 legs, Soldier WeaponHold upper body and the support hand on the gun while running.
