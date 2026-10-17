# 0002: Built-in Humanoid IK instead of the Animation Rigging package

**Status:** accepted (owner approved 2026-10-16). How it is applied alongside the running lean and arm tuck is in [0004](0004-humanoid-ik-for-gun-hands.md).

## Context
The support hand, aiming and the armed sprint need hand placement (IK). Today it is done by custom code that rotates arm bones after the animation has been evaluated (`WeaponCombatController` solves both arms, `MaleWalkRetarget` tucks the running arms and tilts the chest, `BlockHumanoidRig` runs it all again after grounding). Up to five pieces of code rotate the same arm bone each frame.

The Animation Rigging package (com.unity.animation.rigging 6.6.0) would replace this with constraint components (two-bone IK, multi-aim). Unity's Humanoid avatar also has a built-in IK solver (hand and foot goals, look-at with spine weight) that needs no package.

## Evidence
A probe on a copy of the block character played a clip through a manually evaluated PlayableGraph, the same way the player's animation graph works, with Humanoid IK requested. The IK callback ran every frame and the left hand reached its target exactly (0.000 m error, against 0.684 m without IK). The solve happens inside the animation evaluation, before bone transforms are written, so it is one writer instead of several.

## Decision
Use the built-in Humanoid IK (hand goals for the support hand and holster reach, look-at for aiming) inside the animation graph. Do not add Animation Rigging now.

## Consequences
- No new package; the solver respects the avatar's muscle limits.
- Fewer constraint types than Animation Rigging (no damped or chain constraints). Revisit if a later feature needs them.
- Needs the block avatar's proportions fixed first (see the animation page), because Humanoid IK works in the avatar's muscle space.
