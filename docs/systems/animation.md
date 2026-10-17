# Animation (block player character)

## Purpose
Makes the block character move: plays walk, run, strafe, idle and fall clips; blends in unarmed attack clips and the Soldier gun poses; poses special moves (crouch, roll, dive, slide); and places the visible blocks on the skeleton. It reads movement and weapon state; it does not move the character.

## Files
- Folder: `Assets/Game/Animation/`
- `MaleWalkRetarget` (on Player): builds the animation graph in code, picks walk/run/strafe blends from measured motion, plays unarmed attacks (selection and timing), layers the Soldier weapon poses, tucks the arms while running, and tilts the chest toward the aim.
- `BlockHumanoidRig` (on Player): special-move poses, copies the skeleton onto the visible blocks, lowers the body so the lowest block touches the floor, and runs punch contact and the weapon arm pose at the right moment.
- `PlayerBlockRounding` (on Player): rounds the block corners.
- `Assets/Game/Animation/Avatars/BlockHumanoidAvatar.asset`: the Humanoid skeleton description, rebuilt by **Tools > Animation > Rebuild Player Avatar** (`PlayerAvatarSetup`). Its root sits at the feet (rig root 0.96 m below the player's centre), so the clips' hip bob and sway reach the body, and its arm down range is longer than Unity's default so the arms hang close to the wide block torso. Materials for the body are in `Assets/Game/Animation/Materials/`.
- Clips come from `Assets/ThirdParty/Kevin Iglesias/` (movement, Soldier poses) and `Assets/ThirdParty/FightingMotionsVolume1/` (punches and kicks).

## How to tune by feel
- Player > Male Walk Retarget: **Reference Walk Speed / Reference Run Speed** match the clips' stride to actual travel (wrong values make feet slide); **Gait Blend Time / Direction Blend Time** soften walk-to-run and turning; **Running Arm Down Angle** sets how close to the body the arms are pulled while running.
- Player > Block Humanoid Rig: crouch depth and shuffle, and how fast special poses blend in.
- Resize body blocks under Player > Block Body outside Play mode; this does not change the collision capsule.
- How far the arms hang from the body: the arm down range in `PlayerAvatarSetup` (a longer range brings the arms in). Run **Tools > Animation > Rebuild Player Avatar** after changing it.

## How to test
Walk, run and strafe on the Movement Course (F6) lane and open area; crouch in the tunnel; equip a gun from F1 and run and aim. Foot slide and arm angles are measured by the baseline harness ([feel spec](../FEEL_SPEC.md#animation)); frame captures land in Logs/Baseline/latest/frames.

## Known limits (measured)
- Feet slide because the clips' natural speed on this rig (walk 1.45 m/s, run 3.13 m/s with the planted foot moving at normal playback) is far below the travel speed (walk 4.5, run 6.75); the reference speeds are a compromise between sliding and a very fast leg cadence ([feel spec](../FEEL_SPEC.md#animation)).
- Running arms are still pulled straight down by code after the animation (Running Arm Down Angle).
- While running with a gun, the gun pose and support-hand grip are switched off, so the arms swing freely around the gun (support hand measured over a meter from its grip).
- Feet slide noticeably while walking and a lot while running (measured; [feel spec](../FEEL_SPEC.md#animation)).
- Several pieces of code rotate the same arm bones in one frame (graph, run tuck, special poses, punch contact, weapon pose).
