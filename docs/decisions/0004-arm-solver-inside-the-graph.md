# 0004: Solve the gun hands in the pose step instead of with Humanoid IK

**Status:** proposed (needs the owner's approval). Replaces the hand-goal part of [0002](0002-built-in-humanoid-ik.md).

## Context
[0002](0002-built-in-humanoid-ik.md) chose built-in Humanoid IK for the support hand and aiming so that one step inside the animation graph writes the arms. Checkpoint 13 added that step (`BodyPoseJob`), which also applies the running lean, the running-arm tuck and the chest aim pitch by editing bone rotations directly.

## Evidence
A probe inside `BodyPoseJob` (standing pose, running lean set to 30°) asked Humanoid IK to move the left hand to a point 0.4 m away:
- The hand reached the point exactly (0.000 m), as in 0002.
- The torso lean went from -8.5° (lean applied) back to 4.9° (no lean): solving Humanoid IK rebuilds the pose from the avatar's muscle values, which do not include bone edits made earlier in the same step. Its hand goals are in world space and are read from the pose before those edits.

Using Humanoid IK would mean rewriting the lean, arm tuck and aim pitch as muscle values, which would only approximate the owner's tuned values (35° lean, 38.5° arms).

## Decision
Move the existing two-bone arm solver and aim correction from `WeaponCombatController` into `BodyPoseJob`, after the lean, tuck and aim pitch. `WeaponCombatController` keeps the recoil and grip state and says where the hands should go (`GunHands`); the job moves the bones. Humanoid IK stays available for later work that does not follow bone edits (for example a look-at).

## Consequences
- The arms are written once, inside the graph, and the tuned lean and arm angles stay exact.
- No muscle limits on the solved arms (the same as before this change).
- The solve happens before grounding, so the aim point is moved by the expected grounding shift (last frame's).
