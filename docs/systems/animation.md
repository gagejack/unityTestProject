# Animation (block player character)

## Purpose
Makes the block character move: plays walk, run, strafe, idle and fall clips; blends in unarmed attack clips and the Soldier gun poses; poses special moves (crouch, roll, dive, slide); and places the visible blocks on the skeleton. It reads movement and weapon state; it does not move the character.

## Files
- Folder: `Assets/Game/Animation/`
- `MaleWalkRetarget` (on Player): builds the animation graph in code, picks walk/run/strafe blends from measured motion, plays unarmed attacks (selection and timing), layers the Soldier weapon poses, tucks the arms while running, and tilts the chest toward the aim.
- `BlockHumanoidRig` (on Player): special-move poses, copies the skeleton onto the visible blocks, lowers the body so the lowest block touches the floor, and runs punch contact and the weapon arm pose at the right moment.
- `PlayerBlockRounding` (on Player): rounds the block corners.
- `Assets/Game/Animation/Avatars/BlockHumanoidAvatar.asset`: the Humanoid skeleton description. Materials for the body are in `Assets/Game/Animation/Materials/`.
- Clips come from `Assets/ThirdParty/Kevin Iglesias/` (movement, Soldier poses) and `Assets/ThirdParty/FightingMotionsVolume1/` (punches and kicks).

## How to tune by feel
- Player > Male Walk Retarget: **Reference Walk Speed / Reference Run Speed** match the clips' stride to actual travel (wrong values make feet slide); **Gait Blend Time / Direction Blend Time** soften walk-to-run and turning; **Running Arm Down Angle** sets how close to the body the arms are pulled while running.
- Player > Block Humanoid Rig: crouch depth and shuffle, and how fast special poses blend in.
- Resize body blocks under Player > Block Body outside Play mode; this does not change the collision capsule.

## How to test
Walk, run, strafe and turn on the Test Area ground; crouch, roll and dive on the Parkour Course; equip a gun from F1 and run and aim. No Play-mode checks exist yet.

## Known limits (measured during inspection)
- The block skeleton's shoulders are about twice as far apart as the source character's, so arms hang away from the torso.
- The avatar's root sits at the body's centre, so hip bob and sway from the clips are almost lost; the rig corrects height by moving the body each frame.
- While running with a gun, the gun pose and support-hand grip are switched off, so the arms swing freely around the gun.
- Several pieces of code rotate the same arm bones in one frame (graph, run tuck, special poses, punch contact, weapon pose).
