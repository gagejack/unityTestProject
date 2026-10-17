# Animation (block player character)

## Purpose
Makes the block character move: plays walk, run, strafe, idle and fall clips; blends in unarmed attack clips and the Soldier gun poses; poses special moves (crouch, roll, dive, slide); and places the visible blocks on the skeleton. It reads movement and weapon state; it does not move the character.

## Files
- Folder: `Assets/Game/Animation/`
- `MaleWalkRetarget` (on Player): builds the animation graph in code, picks walk/run/strafe blends from measured motion, plays unarmed attacks (selection and timing), layers the Soldier weapon poses, and, in the graph's last step (`BodyPoseJob`), places the gun hands with Humanoid IK (aim and support grip), then straightens the upper body and tucks the arms while running and tilts the chest toward the aim.
- `BlockHumanoidRig` (on Player): special-move poses, copies the skeleton onto the visible blocks, sets the body's height (a fixed offset for clip poses, a snap to the floor for special poses, and never below the floor while grounded), and runs punch contact at the right moment.
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
Walk, run and strafe on the Movement Course (F6) lane and open area; crouch in the tunnel; equip a gun from F1 and run and aim. Foot slide and arm angles are measured by the baseline harness ([feel spec](../FEEL_SPEC.md#animation)); frame captures land in Logs/Baseline/latest/frames. Play-mode checks in `RunningPoseTests` (Window > General > Test Runner > PlayMode > Game.Animation.Tests) assert the sprint torso lean and running arm angle, and that the support hand stays on an assault rifle while sprinting, at 30, 60 and 144 fps.

## Known limits (measured)
- Normal movement looks like jogging and running looks like sprinting: the clips were picked to match the travel speeds (walk 4.5, run 6.75 m/s); the original walk clip's planted foot only moves at 1.45 m/s on this rig.
- Feet still slide somewhat, most visibly when strafing ([feel spec](../FEEL_SPEC.md#animation)).
- Running lean, the running-arm tuck, the chest aim pitch and the gun arms are code adjustments made inside the animation graph, in its last step (`BodyPoseJob`), so they are part of the pose that later code reads. The gun hands are placed first, with Humanoid IK, then the lean, tuck and aim pitch rotate the bones; the hand goals allow for those rotations ([decision 0004](../decisions/0004-humanoid-ik-for-gun-hands.md)).
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor and the other 3-4.5 cm: the one fixed height offset is set so the lowest point of a walking or running stride touches the floor, and the idle clip holds its feet higher than that (measured).
- Running arms are pulled down toward the body by code (Running Arm Down Angle), not by the clip.
- Drawing a gun from or holstering it to the back plays the Kevin Iglesias UnsheatheBack01_R / SheatheBack01_R clips on the upper body (the legs keep walking), at the time and weight `WeaponHolster` gives; the pose step's IK then pulls the hand onto the gun ([weapons](weapons.md)).
- Sprinting with a two-handed gun plays that gun family's WeaponHold pose on the arms only (the legs and torso keep the sprint, so the running lean is the same armed or not) and keeps the support hand on the gun. Pistols and dual handguns have no hold pose: their arms swing with the sprint.
- Several pieces of code still rotate the same arm bones after the graph in one frame (special poses, punch contact).
