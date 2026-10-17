# Status

**Checkpoint 9 (plan step 6a): avatar rebuilt; walking arms fixed.**

## Current state
- Player avatar rebuilt by Tools > Animation > Rebuild Player Avatar: root at the feet (human scale 0.92, was -0.04), so the clips' hip bob comes through (walk 2.0 cm, was 0.5); arm down range 76 degrees (Unity default 60).
- Walking unarmed: arms 7.2 degrees out from the body (was 16), hanging beside the torso. Crouch, roll, dive and grounding still work; all 20 Play-mode tests pass.
- Foot slide unchanged (walk about 26 %, run about 45-60 %). Cause measured: on this rig the walk clip's planted foot moves at 1.45 m/s and the run clip's at 3.13 m/s, while the character walks at 4.5 and runs at 6.75 m/s.

## Decision needed
How to remove foot slide: (A) calibrate to the clips (no slide, very fast legs), (B) jog clip for walking and sprint clip for running (no slide, natural cadence, looks like jogging), (C) keep the compromise, (D) slow the walk.

## Last feedback
"Walking down slopes/stairs does not work, the character bounces and does a floating animation." Fixed here.

## Next step
Apply the chosen foot-slide option, then step 6b: one pose order with built-in Humanoid IK (gun standing and aiming pose, running arms without the code tuck).
