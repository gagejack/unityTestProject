# Status

**Checkpoint 12 (plan step 6a): running pose settled by playtest.**

## Current state
- The owner's playtest values are now the defaults: Player > Male Walk Retarget > **Running Lean Correction** 23.3 and **Running Arm Down Angle** 51.5.
- Measured on the run sequences only (unarmed and armed, 30 / 60 / 144 fps): sprint torso lean 35° (38° before), run arm angle 38.5°, run foot slide (ankle) 20 / 23 / 24 %, unchanged. Numbers and targets are in [docs/FEEL_SPEC.md](FEEL_SPEC.md#animation).
- Foot slide keeps the ankle targets; the visible-contact measure stays as a diagnostic.
- All Play-mode tests pass.

## Measured, not yet decided
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor. Fix deferred by the owner; proposed fix is to snap the feet to the floor while standing still.
- Walk arm angle reads 0-2° with the jog clip (target 5-9°).
- Foot slide is still above the ankle targets for walk, run and strafe.
- A baseline run can be thrown off by mouse or keyboard input in the editor while it runs. Not proven; rerun any run with odd numbers.

## Later
- The pixel button sheet in `docs/spec/UI/DebugMenu/` is a style reference for future game menus (main and pause), not for the F1 debug menu now.

## Next step
Step 6b: one pose order with built-in Humanoid IK (gun standing and aiming pose, running arms without the code tuck); the running lean moves into that order.
