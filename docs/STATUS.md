# Status

**Checkpoint 3: test areas and baseline measurements (no gameplay changes).**

## Current state
- Gun Range (F5) and Movement Course (F6) built from primitives with labeled start pads; F7 goes to the mob test spot, F9 resets targets and refills ammo. Layout: [TEST_AREAS](TEST_AREAS.md). The old Combat Practice Lane was rebuilt inside the Gun Range.
- New Core contracts: `CombatEvents` and `PlayerRequests`; movement, camera and weapons gained small listeners (teleport, re-frame, refill). No feel changes.
- Baseline harness (`Assets/Game/Diagnostics/`): 19 fixed sequences at 30/60/144 fps with a virtual keyboard and mouse, per-frame logs and frame captures. Numbers and proposed targets: [FEEL_SPEC](FEEL_SPEC.md).

## Demonstrated by the baseline
- Jump apex 1.21 / 1.40 / 1.51 m at 30 / 60 / 144 fps (set to 1.6); stopping distance also varies.
- Assault rifle fires 600 / 563 / 576 rpm (set to 600).
- Camera kick lands one frame after the shot.
- Support hand is over a meter off the rifle while running; aimed guns are 0.6–1.6° off the crosshair point.
- Feet slide: walking ~25 %, running ~55 %; Giant and Zombie mobs ~100–200 %.
- Click-to-first-hit for punches differs by frame rate (cause unknown).

## Last feedback
"Add a UI element for meters / s above the bottom right UI." Added: Speed HUD shows horizontal speed (m/s) above the points text.
Earlier: H toggles holster/draw, and starting a sprint also holsters.

## Next step (proposed)
Approve or adjust the proposed targets in FEEL_SPEC, then: interaction analysis and package trade-offs (Animation Rigging, Cinemachine), the mob AI rename, and the first fixes (frame-rate-independent jump and fire rate).
