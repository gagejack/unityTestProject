# Status

**Checkpoint 10 (plan step 6a): foot slide reduced with the jog and sprint clips.**

## Current state
- Normal movement plays the Run01 jog clip, running plays the Sprint01 clip (Player > Male Walk Retarget; Reference Walk Speed 3.1, Reference Run Speed 4.8).
- Foot slide on the original ankle measure, 30 / 60 / 144 fps: walk 15 / 16 / 17 % (was 24 / 27 / 28), run 21 / 23 / 25 % (was 51 / 59 / 60), strafe 15 / 17 / 18 % (was 27 / 30 / 29). Still above the ≤ 10 / 15 % targets. The new visible-contact measure is in the [feel spec](FEEL_SPEC.md#animation).
- Body height (`BlockHumanoidRig`): clip poses use a fixed offset instead of pressing swinging feet onto the floor (the old cause of most visible skating); crouch, roll, dive and slide still snap to the floor; and on the ground no block may sink below the floor. That last rule fixes a foot sinking up to 8 cm into the floor for about 0.1 s when a roll or slide ends (measured).
- Mob foot slide keeps its original ankle measure; all mob numbers are unchanged.
- Running no longer stops when the Game view loses focus (`CapsuleMovement`); the Input System already releases keys then.
- All 20 Play-mode tests pass; the full baseline runs through at 30, 60 and 144 fps. Movement, jump, dive, weapon and mob numbers are unchanged.

## Measured, not yet decided
- Walk arm angle reads 0-2° with the jog clip (target 5-9°, set so the arms clear the chest block). In the captured frames the arms look clear; check in Play mode.
- Standing idle, the feet hover about 2 cm above the floor.
- Running leans forward as much as the Sprint01 clip does.

## Decision needed
1. Foot slide targets: keep the ankle targets, or switch to the visible-contact measure (walk 19 %, run 31 %, strafe 50 % at 144 fps today)?
2. Running posture: should running be more upright (see the next step)?

## Next step
Owner playtest of jogging and sprinting at the Movement Course (F6). Then step 6b: one pose order with built-in Humanoid IK (gun standing and aiming pose, running arms without the code tuck), which is also where a running-posture adjustment would go.
