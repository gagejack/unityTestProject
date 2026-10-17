# Animation (block player character)

## Purpose
Makes the block character move: plays walk, run, strafe, idle and fall clips; blends in unarmed attack clips and the Soldier gun poses; poses special moves (crouch, roll, dive, slide); and places the visible blocks on the skeleton. It reads movement and weapon state; it does not move the character.

## Files
- Folder: `Assets/Game/Animation/`
- `MaleWalkRetarget` (on Player): builds the animation graph in code, picks walk/run/strafe blends from measured motion, plays unarmed attacks (selection and timing), layers the Soldier weapon poses, straightens the upper body and tucks the arms while running, and tilts the chest toward the aim.
- `BlockHumanoidRig` (on Player): special-move poses, copies the skeleton onto the visible blocks, sets the body's height (a fixed offset for clip poses, a snap to the floor for special poses, and never below the floor while grounded), and runs punch contact and the weapon arm pose at the right moment.
- `PlayerBlockRounding` (on Player): rounds the block corners.
- `Assets/Game/Animation/Avatars/BlockHumanoidAvatar.asset`: the Humanoid skeleton description, rebuilt by **Tools > Animation > Rebuild Player Avatar** (`PlayerAvatarSetup`). Its root sits at the feet (rig root 0.96 m below the player's centre), so the clips' hip bob and sway reach the body, and its arm down range is longer than Unity's default so the arms hang close to the wide block torso. Materials for the body are in `Assets/Game/Animation/Materials/`.
- Clips come from `Assets/ThirdParty/Kevin Iglesias/` (movement, Soldier poses) and `Assets/ThirdParty/FightingMotionsVolume1/` (punches and kicks).

## How to tune by feel
- Player > Male Walk Retarget: **Reference Walk Speed / Reference Run Speed** match the clips' stride to actual travel (wrong values make feet slide); **Gait Blend Time / Direction Blend Time** soften walk-to-run and turning; **Running Arm Down Angle** sets how close to the body the arms are pulled while running; **Running Lean Correction** tilts the upper body back toward upright while running (0 keeps the sprint clip's strong forward lean, higher is more upright; the legs are not affected).
- Player > Male Walk Retarget: the walk slots hold the Run01 jog clip and the run slots the Sprint01 clip (the backward run slots keep Run01), so the feet keep up with the travel speed.
- Player > Block Humanoid Rig: crouch depth and shuffle, and how fast special poses blend in. **Animated Pose Height** raises or lowers the walking and running body so the soles meet the floor; **Snap Lowest Block To Floor** brings back the old always-snap (feet pressed onto the floor while swinging, which looked like skating).
- Resize body blocks under Player > Block Body outside Play mode; this does not change the collision capsule.
- How far the arms hang from the body: the arm down range in `PlayerAvatarSetup` (a longer range brings the arms in). Run **Tools > Animation > Rebuild Player Avatar** after changing it.

## How to test
Walk, run and strafe on the Movement Course (F6) lane and open area; crouch in the tunnel; equip a gun from F1 and run and aim. Foot slide and arm angles are measured by the baseline harness ([feel spec](../FEEL_SPEC.md#animation)); frame captures land in Logs/Baseline/latest/frames.

## Known limits (measured)
- Normal movement looks like jogging and running looks like sprinting: the clips were picked to match the travel speeds (walk 4.5, run 6.75 m/s); the original walk clip's planted foot only moves at 1.45 m/s on this rig.
- Feet still slide somewhat, most visibly when strafing ([feel spec](../FEEL_SPEC.md#animation)).
- Running Lean Correction is applied by code after the animation, on the spine and chest, so it adds to the bones that several pieces of code already adjust.
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor and the other 3-4.5 cm: the one fixed height offset is set so the lowest point of a walking or running stride touches the floor, and the idle clip holds its feet higher than that (measured).
- Running arms are still pulled down toward the body by code after the animation (Running Arm Down Angle).
- While running with a gun, the gun pose and support-hand grip are switched off, so the arms swing freely around the gun (support hand measured over a meter from its grip).
- Several pieces of code rotate the same arm bones in one frame (graph, run tuck, special poses, punch contact, weapon pose).
