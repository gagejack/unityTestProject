# Status

**Checkpoint 15 (plan step 6b, part 3): the gun hands use Humanoid IK (owner's choice).**

## Current state
- In the pose step inside the animation graph (`BodyPoseJob`), Humanoid IK now places the gun hands (right hand aims, left hand on the support grip or aiming a second handgun). The running lean, arm tuck and chest aim pitch are applied after it, unchanged, and the hand goals allow for them ([decision 0004](decisions/0004-humanoid-ik-for-gun-hands.md)).
- Tried and dropped: making the lean and arm tuck out of Humanoid muscles. A 20° spine bend moved the torso only 1.6° (Unity tips the hips to keep the centre of mass), and the arm muscle did not match the tuned arm angle.
- Fixed while measuring: a hand without a goal kept its aiming goal from earlier frames, which pulled the left hand 40-60 cm off the grip after aiming. Both hands now get a goal on every solve.
- Measured against the previous solver (30 / 60 / 144 fps): aim error rifle 1.50-1.62°, pistol 0.62°, rocket 1.50°, dual handguns 0.62°; support hand on the grip (0 cm) walking, strafing and aiming, shotgun 1.7-2.2 cm (pump stroke); sprint lean 34.6-34.7°, run arms 38.5°; foot slide within the old runs' spread; armed walk arm angle 5.7 → 5.1°.
- Cost: Humanoid IK adds about 0.014 ms per frame while a gun is held.
- Player > Weapon Combat Controller > **Run Block After Shot** is 0.1 s (owner's choice; it was a fixed 0.27 s).
- All Play-mode tests pass.

## Measured, not yet decided
- Standing idle, the lower foot hovers 1.5-2.5 cm above the floor (fix deferred by the owner).
- Foot slide is still above the ankle targets for walk, run and strafe.
- Running with a gun still drops the support hand (about 160 cm from its grip); that is the armed sprint step.

## Next step
Owner playtest at the gun range (F5): equip a rifle, a pistol, dual handguns and a shotgun from F1, walk, aim (right mouse) and fire; the hands should stay on the guns. Then the armed sprint: Sprint01 legs, Soldier WeaponHold upper body and the support hand on the gun while running.
