# Status

**Checkpoint 4 (plan step 3): interaction analysis and package trade-offs. No gameplay changes.**

## Findings (input to movement to animation to gun to camera)
- Start/stop/reverse: input reaches motion and the walk pose on the same frame; top speed in 0.1 s. Facing snaps to the camera each frame. Stopping distance and jump height vary with frame rate.
- Firing: the shot is queued in Update and resolved after the final pose (good); camera recoil lands a frame later. Firing cancels running and slows to 75 %.
- Running armed: the gun pose and support-hand grip are switched off (hand over 1 m from the grip). No armed run clips exist; the packs have upper-body WeaponHold poses for rifle, assault rifle and bazooka, and Sprint01 leg clips, none used yet.
- Unarmed: attack starts on the click frame; the combo never resets; 3 of 12 attacks miss at 1.2 m; the straight right counts a hit before it visibly lands. The earlier "frame-rate" timing difference was the combo start, not frame rate.
- Draw / drop: the gun appears in the hand instantly and the pose blends in over 0.16 s; dropping removes it instantly. No holster.
- Hits: a 0.09 s white flash, sparks and a hitmarker; no hit reaction; targets and mobs vanish at zero health. Every shot creates and destroys effect objects (to profile).
- Five pieces of code rotate arm bones each frame (structural; visible jitter not demonstrated).

## Proposed decisions (need approval)
- [0002](decisions/0002-built-in-humanoid-ik.md): use built-in Humanoid IK (probe: hand on target, 0.000 m) instead of Animation Rigging.
- [0003](decisions/0003-keep-custom-camera.md): keep the custom camera instead of Cinemachine.
- Armed sprint: Sprint01 legs + upper-body WeaponHold pose + support-hand IK (pistols: one-handed carry, no hold clip exists).

## Last feedback
- FEEL_SPEC targets approved. Holster: H toggles; right-click draws; stays holstered until H or right-click. Sprinting does not holster; armed sprint animations needed.

## Next step
Plan step 4: rename the mob AI out of SpiderDroneBrain, drop spider-only code, re-measure mobs.
