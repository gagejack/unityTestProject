# 0004: Humanoid IK for the gun hands; lean, arm tuck and aim pitch stay bone rotations

**Status:** accepted (owner chose Humanoid IK, 2026-10-17). Says how [0002](0002-built-in-humanoid-ik.md) is applied.

## Context
[0002](0002-built-in-humanoid-ik.md) chose built-in Humanoid IK for the support hand and aiming. The pose step inside the animation graph (`BodyPoseJob`) also applies the running lean, the running-arm tuck and the chest aim pitch, which the owner tuned by eye (sprint lean 35°, running arms 38.5°).

## Evidence
Probes inside `BodyPoseJob`:
- Humanoid IK puts a hand exactly on its goal (0.000 m).
- Solving Humanoid IK rebuilds the pose from the avatar's muscle values, so bone rotations made before it in the same step are lost (lean 30° set, torso read 4.9°, the unleaned value).
- The lean cannot be made with muscles instead: bending the Spine and Chest Front-Back muscles back by 20° changed the torso by +1.6°, because Unity keeps the body's centre of mass fixed and tips the hips (and legs) the other way.
- The arm tuck does not map onto the Arm Down-Up muscle: a setting of 0 (horizontal) measured 41.5° and 51.5 measured -4° on the running-arm measure.
- A hand goal left unset keeps its goal from an earlier frame, so every solve sets both hands (weight 0 for a hand that should follow the animation).

## Decision
In `BodyPoseJob`, Humanoid IK places the gun hands first (right hand aims; left hand holds the support grip or aims a second handgun). Then the running lean, running-arm tuck and chest aim pitch rotate the bones, unchanged. The hand goals are given in the pose before those rotations (mapped back through the spine and chest turns), so the hands land on target after them. A look-at for aiming was not used: the chest aim pitch already does that job and stays exact.

## Consequences
- The owner's lean and arm values stay exact, and the hands get Humanoid IK's joint limits.
- Measured against the previous two-bone solver at 30/60/144 fps: aim error, support-hand gap, lean, arm angle and foot slide unchanged within run-to-run noise; armed walk arm angle 5.7° → 5.1°.
- The arm tuck still rotates the upper arms after IK; while blending between running and aiming (both partly on), the hands can be briefly off target.
