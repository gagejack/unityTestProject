# Movement

## Purpose
Moves the player's collision capsule and decides the movement state: walking, running, jumping, wall jumping, crouching, rolling, diving and sliding. Also owns stamina. It does not animate the body (see [animation](animation.md)).

## Files
- Folder: `Assets/Game/Movement/`, its own assembly (Game.Movement) that references only Core.
- `CapsuleMovement` (on Player): reads WASD, jump, crouch and roll input, decides the movement state once per frame and moves the `CharacterController`. Each frame is a short sequence of named steps (timers, crouch, facing, committed moves, travel, gravity, collisions). Other systems change movement only through `IMovementModifier` components on the Player (guns and unarmed attacks), take control away through `IControlBlocker` (debug menu, flying camera), and supply the steering heading through `IViewHeading` (the camera); all three are in Core.
- `PlayerStamina` (on Player): spends and recovers stamina. Infinite stamina switches on at the start of every Play session; turn it off in F1 > Player.
- `StaminaBar` and `RoundedStaminaGraphic` (on Canvas > Stamina Bar and its child Stamina Fill): draw the stamina bar.
- `SpeedReadout` (on the scene object Speed HUD): shows horizontal speed in m/s above the points and ammo text, bottom right. Speed HUD has its own canvas scaled like the gun HUD so the two stay aligned.

## How to tune by feel
All on Player > Capsule Movement unless noted.
- **Move Speed / Run Speed Multiplier:** overall pace. Higher feels faster but makes stopping on a mark harder. Holding Shift runs in any direction (sideways and backward too, using those directions' run clips); a double-tap W run is forward only.
- **Acceleration Time / Braking Time:** how weighty starts and stops feel. Lower is snappier.
- **Jump Height / Gravity:** height and hang time. Stronger gravity feels less floaty at the same height.
- **Air Control:** how much you can steer in the air. Low keeps takeoff momentum (committed jumps); high allows mid-air corrections.
- **Wall Jump** group: push-off strength, height and how much your WASD input bends it.
- **Dash Roll / Air Dive / Dive Landing Slide** groups: speed, duration and cooldown of the roll, the dive (Caps Lock in the air; hold S to dive backward) and the slide it turns into on landing.
- Player > Player Stamina: capacity, recovery rate and the pause before recovery.
- **Cursor Turn Speed:** in the fixed view the body turns toward the mouse cursor at this speed, armed or not, whichever way it walks (WASD stays screen-relative and the legs strafe or backpedal). Higher snaps round faster; lower feels heavier. Rolls, dives and slides still face their travel direction.
- **Sprint Facing Limit:** in the fixed view, sprinting keeps facing the cursor while it is within this angle of the run; further round, the body faces the run (and uses the forward sprint clip) until the cursor comes 20° back inside the limit. Lower makes sprints commit to facing the run sooner; higher keeps you aiming while sprinting sideways and backward. A click while facing the run turns you back first (see Fire Turn Limit in [weapons](weapons.md)).
- **Ground Snap Distance:** how big a drop the character follows while walking or running down slopes and steps instead of falling. Too small brings back the bounce and floating pose going down stairs; too large makes walking off a ledge feel sticky.
- Speed HUD > Speed Readout: **Smoothing** steadies the number (0 shows every frame's raw speed); **Decimals** sets precision.
- Speed HUD > Speed Text > Text > **Font** uses Doppio One to match the weapon HUD.
- Speed Text sits above the cash row and uses the matching dark Outline and offset Shadow components for readability.
- Gun aim and fire slow movement down; those settings are on `WeaponCombatController` (see [weapons](weapons.md)). Attack slowdowns are on `MaleWalkRetarget` attack timings (see [unarmed](unarmed.md)).

## How to test
Use the Movement Course (F6): start/stop on the meter lane, slopes, steps, ledges, gaps, the tunnel and wall jumps. Turn off Infinite Stamina in F1 > Player to test costs. Measured by the movement and jump sequences of the baseline harness ([feel spec](../FEEL_SPEC.md#movement)). Play-mode checks in `MovementFeelTests` (Window > General > Test Runner > PlayMode > Game.Movement.Tests) assert jump height, walk stopping distance and staying on the ground while walking down both slopes and the stairs, at 30, 60 and 144 fps. Fixed view facing: the aim ring (F8); `FixedViewAimingTests` checks the turn time toward the cursor at 30, 60 and 144 fps, that the facing holds still with the cursor in the dead zone, that Shift sprints in all eight directions, and the sprint facing rule.

## Known limits
- Character facing snaps to the camera heading every frame in third person.
- A dive's landing is detected on the frame it touches the ground, which still carries that frame's full dive travel, so total dive distance varies by up to one frame of travel (about 0.4 m at 30 fps; [feel spec](../FEEL_SPEC.md#movement)).
- Falling below the world returns the player to the start; there is no health or death.
