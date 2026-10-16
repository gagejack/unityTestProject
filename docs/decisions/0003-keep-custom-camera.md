# 0003: Keep the custom third-person camera instead of Cinemachine

**Status:** proposed (waiting for the owner's approval)

## Context
`FollowCamera` (289 lines) does mouse look, shoulder aim, follow smoothing, collision pull-in, recoil, a landing dip, a fixed view and free roam. Cinemachine (com.unity.cinemachine 6.6.0) could provide follow, collision (deoccluder), impulse shake and aim blending.

## Evidence
The baseline found two camera problems, both small and local:
- Recoil shows one frame after the shot, because the camera is placed before the shot is resolved in the update order.
- The collision check allocates memory every frame.
No measurement showed a problem that Cinemachine would solve better. Weapons read the aim direction (including recoil) from the camera; with Cinemachine, recoil as impulse shake would move the view without moving the aim, so aiming would have to be rebuilt.

## Decision
Keep `FollowCamera`. Fix the update order and the allocation in place when the camera is touched.

## Consequences
- No new package and no re-tuning of saved camera values.
- If camera shake, cut scenes or multiple camera rigs become goals, revisit Cinemachine.
