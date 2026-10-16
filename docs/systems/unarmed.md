# Unarmed combat

## Purpose
Punches and kicks with empty hands. Left click plays the next of 12 attacks; one follow-up click is buffered during an attack. Assigned straight punches and kicks use real limb contact: the fist or foot stops on the surface it hits.

## Files
- Folder: `Assets/Game/Unarmed/`
- `StraightPunchContact` (on Player): probes the striking limb, stops it on contact, applies a short impact pause, then releases.
- Attack selection, clip trimming, contact and recovery timing, damage and reach live in `MaleWalkRetarget` (Player > Male Walk Retarget > Attack Timings), see [animation](animation.md).
- Damage and hit feedback go through [combat](combat.md). Punch sounds are in `Assets/Game/Unarmed/Audio/`.

## How to tune by feel
- Player > Male Walk Retarget > Attack Timings (one entry per clip): **Contact** is when in the swing the hit lands; **Recovery** is when you can chain or dodge out; **Movement Multiplier** is how much the swing slows you.
- **Attack Input Buffer:** how forgiving a follow-up click is.
- Player > Straight Punch Contact: **Impact Pause** adds weight to a landed hit; **Compression** and **Release Blend Time** shape how the limb gives and recovers.

## How to test
Use the melee dummy (start pad GR-Melee on the Gun Range) or the stationary mobs, and attack while walking on the Movement Course. Measured by the unarmed sequences of the baseline harness ([feel spec](../FEEL_SPEC.md#unarmed)).

## Known limits
- Attacks without an assigned contact limb use a timed sphere check instead of real contact.
- Unarmed attacks are disabled while a gun is held (drop it with Q).
- The time from the first click to the first hit differs between frame rates (measured; cause not yet known).
