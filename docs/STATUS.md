# Status

**Checkpoint 8: walking down slopes and stairs fixed (movement).**

## Current state
- Ground snapping in `CapsuleMovement`: when grounded and not jumping, diving or wall jumping, the character follows walkable ground up to Ground Snap Distance (0.35 m) below instead of falling. Bigger drops are still falls.
- Before: running down the 20° slope, walking down the 40° slope and walking down the stairs spent 15-109 frames airborne (bounce plus floating pose). After: 0 airborne frames at 30/60/144 fps.
- New start pads at the top of both slopes and the stairs (facing down), new baseline sequences M07-M10, and Play-mode checks for descending (all 20 Play-mode tests pass).
- The F1 debug menu rebuild from the other session is committed separately (43960ec).

## Last feedback
"Walking down slopes/stairs does not work, the character bounces and does a floating animation." Fixed here.

## Next step
Plan step 6, first part: skeleton proportions and avatar root (walking arms, foot slide), with before/after captures.
