# Status

**Checkpoint 11 (plan step 6a): running lean slider.**

## Current state
- New setting Player > Male Walk Retarget > **Running Lean Correction** (0-30, default 15): tilts the spine and chest back while running, faded in with the run; legs and feet are unchanged.
- Measured torso lean (hips to neck, 0 = upright, standing reads about 6): jog 22°, sprint clip 45° → 38° at the default, the same at 30, 60 and 144 fps. Side-view captures at 0, 15 and 30 are in `Logs/Baseline/lean_side/` (not committed).
- The baseline now records torso lean (`BaselineRecorder`, `BaselineMetrics`).
- Full baseline: every other number is unchanged from checkpoint 10. All 20 Play-mode tests pass.
- Foot slide (ankle measure, 30 / 60 / 144 fps): walk 15 / 16 / 17 %, run 22 / 22 / 25 %, strafe 15 / 18 / 18 %; still above the targets.

## Measured, not yet decided
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor: one fixed height offset serves every clip and is set for the walking and running stride. Proposed fix: snap the feet to the floor while standing still (they are not moving, so this cannot cause skating).
- Walk arm angle reads 0-2° with the jog clip (target 5-9°); the arms look clear of the chest in the captured frames.
- A baseline run can be thrown off by mouse or keyboard input in the editor while it runs (one lean run started facing off the lane and ran off its edge). Not proven; rerun any run with odd numbers.

## Decision needed
1. Running lean: is 15 right? Try other values in Play mode.
2. Foot slide targets: keep the ankle targets, or switch to the visible-contact measure?
3. Fix the idle hover now?

## Next step
Owner playtest of the running lean. Then step 6b: one pose order with built-in Humanoid IK (gun standing and aiming pose, running arms without the code tuck); the running lean moves into that order.
