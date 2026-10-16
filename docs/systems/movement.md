# Movement

## Purpose
Moves the player's collision capsule and decides the movement state: walking, running, jumping, wall jumping, crouching, rolling, air dashing, diving and sliding. Also owns stamina. It does not animate the body (see [animation](animation.md)).

## Files
- Folder: `Assets/Game/Movement/`
- `CapsuleMovement` (on Player): reads WASD, jump, crouch and roll input, moves the `CharacterController`, and sets facing. All movement modes are in one long method (SimulateMovement).
- `PlayerStamina` (on Player): spends and recovers stamina. Infinite stamina switches on at the start of every Play session; turn it off in F1 > Player.
- `StaminaBar` and `RoundedStaminaGraphic` (on Canvas > Stamina Bar and its child Stamina Fill): draw the stamina bar.

## How to tune by feel
All on Player > Capsule Movement unless noted.
- **Move Speed / Run Speed Multiplier:** overall pace. Higher feels faster but makes stopping on a mark harder.
- **Acceleration Time / Braking Time:** how weighty starts and stops feel. Lower is snappier.
- **Jump Height / Gravity:** height and hang time. Stronger gravity feels less floaty at the same height.
- **Air Control:** how much you can steer in the air. Low keeps takeoff momentum (committed jumps); high allows mid-air corrections.
- **Wall Jump** group: push-off strength, height and how much your WASD input bends it.
- **Dash Roll / Air Dash / Air Dive** groups: speed, duration and cooldown of each committed move.
- Player > Player Stamina: capacity, recovery rate and the pause before recovery.
- Gun aim and fire slow movement down; those settings are on `WeaponCombatController` (see [weapons](weapons.md)).

## How to test
Use the Movement Course (F6): start/stop on the meter lane, slopes, steps, ledges, gaps, the tunnel and wall jumps. Turn off Infinite Stamina in F1 > Player to test costs. Measured by the movement and jump sequences of the baseline harness ([feel spec](../FEEL_SPEC.md#movement)).

## Known limits
- Character facing snaps to the camera heading every frame in third person.
- Jump height and stopping distance change with frame rate: jumps are lower at low frame rates (measured; numbers in [feel spec](../FEEL_SPEC.md#movement)).
- Movement reads the debug menu and weapon and animation state directly (see [architecture](../ARCHITECTURE.md#dependencies-today)).
- Falling below the world returns the player to the start; there is no health or death.
