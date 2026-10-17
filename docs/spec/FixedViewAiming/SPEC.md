# Fixed view: 360° mouse aiming and screen-up sprint

**Status:** agreed with the owner on 2026-10-17 (questions and answers below). Not built yet. It comes before holstering in the plan ([status](../../STATUS.md)).

**Request (owner):** "For the fixed view camera, the character can shoot in any direction and turns smoothly toward where the mouse is: a 360° aiming system. The user can only sprint up, up-left and up-right in fixed view."

Third-person view, its controls and its tuning do not change. Everything below applies only while the view mode is Fixed (F1 or F2 menu > Camera).

## Decisions

| Question | Owner's answer |
|---|---|
| When does the character face the cursor? | Always, armed or not. Unarmed attacks go toward the cursor too. |
| Where do shots go? | The point in the world under the cursor. Over plain floor, 1.2 m above that floor point (chest height), so shots fly roughly level. Over a mob, target or wall, the exact point. |
| How does the turn feel? | Fast but smooth, at a set turn speed. |
| Firing before the turn finishes? | Allowed. The shot leaves where the gun points at that moment, not where the cursor is. |
| Which sprint directions are allowed? | Travel within 45° either side of screen-up (decided by travel angle, not by which keys are held). |
| Facing while sprinting | Faces the cursor while the cursor is within 90° of the sprint direction; otherwise faces the way it runs. |
| Leaving the sprint directions | Drops to a walk; the sprint comes back by itself on returning to an allowed direction while the sprint input is still held. |
| Walking speed by direction | The same in every direction (no backpedal slowdown). |
| Right mouse | Precise aim as in third person (gun raised, tighter spread, slower walk, no sprint), but no camera zoom. |
| Cursor | The system pointer is hidden; the game's crosshair is drawn at the mouse position. |
| Camera | Same height and angle, plus a small look-ahead toward the cursor. |
| Cursor on top of the character | Inside a small radius the facing holds still. |
| View switch key | None: switch in the debug menus as now. |

## What the player experiences

### Facing
- The body turns toward the cursor at the turn speed (default 720°/s, so a 180° turn takes 0.25 s). This happens standing, walking, crouching and unarmed.
- WASD moves in screen directions: W up the screen, S down, A left, D right, wherever the body faces. The legs play the strafe and backpedal clips that already exist for the eight directions. Walk speed is the same in every direction.
- Rolls, dives and slides keep their current facing rules (they face their travel direction). Facing the cursor resumes when they end.
- Within the dead-zone radius around the character (default 0.75 m, measured on the ground), the facing does not change, and the aim keeps its last direction at the dead-zone edge.

### Aim point
- A ray from the camera through the cursor finds the first thing it touches, ignoring the player.
- If that is plain floor (a surface facing up, within 45° of straight up, that is not a mob or a target), the aim point is 1.2 m above it. A ledge or box top counts as floor, so an enemy standing on a ledge is aimed at chest height.
- If it is a mob, a target or a wall (any other surface), the aim point is the exact point the ray touched.
- If the ray touches nothing within range, the aim point is where the ray meets the horizontal plane at the character's chest height.

### Shooting
- The gun aims along the body's facing, at the aim point's distance and height. When the body faces the cursor, the gun points exactly at the aim point.
- During a turn the gun turns with the body. A shot leaves the muzzle along the barrel, with the gun's normal spread, so a fast flip sprays across the turn.
- The chest tilts up or down toward the aim point, as it does in third person.
- Right mouse: precise aim (gun raised to the eye, tighter spread, slower walk, sprint blocked), with no camera zoom or shoulder shift.
- The existing rules stay: firing briefly blocks the sprint (Run Block After Shot), reloading blocks the sprint, and so on.

### Sprinting
- Sprint input is the same as now: hold Shift with movement, or double-tap W.
- The sprint only runs while the travel direction is within 45° of screen-up. This covers W, W+A and W+D on a keyboard.
- Outside that range the character walks. The sprint input is not cancelled: returning within range while Shift is still held, or with the double-tap W still held, sprints again on the next frame.
- While sprinting, the body faces the cursor if it is within 90° of the travel direction. Otherwise it faces the travel direction. The legs use the sprint clips for that direction (forward or diagonal).
- Sprinting with a two-handed gun uses the armed sprint pose from checkpoint 16.

### Camera and cursor
- The fixed camera keeps its height and angle and never rotates.
- It shifts toward the cursor by a fraction of the cursor's distance from the character (default 35 %), capped at 3 m, smoothed so it does not jerk.
- The system pointer is hidden while the game has control. The game's crosshair is drawn at the mouse position and grows with spread, as the third-person crosshair does. With no gun, a small dot is shown instead.
- When a debug menu opens, the normal pointer comes back and the character stops reacting to the cursor.

## New settings

Each gets a tooltip with its meaning and units. Defaults are starting values to tune by feel.

| Component | Setting | Default | Meaning |
|---|---|---|---|
| `FollowCamera` | Cursor Dead Zone | 0.75 m | Radius around the character where the cursor does not change the facing |
| `FollowCamera` | Floor Aim Height | 1.2 m | Height above plain floor that the aim point uses |
| `FollowCamera` | Floor Max Slope | 45° | Steepest surface still treated as floor for the rule above |
| `FollowCamera` | Look Ahead Fraction / Look Ahead Max / Look Ahead Smooth Time | 0.35 / 3 m / 0.2 s | How far and how softly the view shifts toward the cursor |
| `CapsuleMovement` | Cursor Turn Speed | 720°/s | How fast the body turns toward the cursor in fixed view |
| `CapsuleMovement` | Sprint Cone | 45° | Allowed sprint directions either side of screen-up in fixed view |
| `CapsuleMovement` | Sprint Facing Limit | 90° | While sprinting, face the cursor only within this angle of travel |

## How it fits the architecture

- **Camera owns the aim point.** It already owns the view and the aim direction. In fixed view, `FollowCamera` reads the mouse position in its Update (order -100, before weapons and movement), casts the cursor ray, and applies the floor rule and the dead zone. It publishes the result through a Core contract. That contract is an addition to the view-heading interface in `Assets/Game/Core/Scripts/MovementContracts.cs`: an aim point, and whether facing should follow it. Movement and Weapons read it through Core, so they still don't reference each other.
- **Movement** turns the body toward the aim point (Cursor Turn Speed), applies the dead zone's held facing, the sprint cone and the sprint facing limit. Screen-up is the camera's forward direction flattened onto the ground, not a fixed world axis.
- **Weapons** computes where the gun aims: along the body's facing, at the aim point's distance and height. It hands that to the existing gun-hands step (`GunHands` in `BodyPoseJob`). In fixed view the shot ray starts at the muzzle and follows the barrel; in third person it still starts at the camera. Precise aim keeps working, minus the camera zoom (the camera already zooms only in third person).
- **HUD crosshair:** `WeaponCombatController` already builds it. In fixed view it moves to the mouse position.
- **Unarmed:** attacks already go the way the body faces, so facing the cursor is enough.
- **Update order:** no new slots. The camera works out the aim point in Update before movement and weapons. Then the camera is placed in LateUpdate, and the shot is resolved after the final pose (unchanged). The aim point is computed from last frame's camera position. That is about 0.1 m at walking speed, well inside the dead zone and spread.
- Each fact keeps one home: settings meanings go in tooltips, numbers in [the feel spec](../../FEEL_SPEC.md#fixed-view-aiming-proposed), and built behaviour on the camera, movement and weapons system pages once it exists.

## Edge cases

- **Cursor outside the Game view or the window unfocused:** keep the last aim point, and don't turn.
- **Cursor over the character's own body:** the ray ignores the player, so it lands on the floor behind, which falls inside the dead zone.
- **Very close walls:** the exact-point rule applies, so the gun aims at the wall where the cursor is.
- **Dual handguns:** both guns aim at the same aim point, as in third person.
- **Rockets:** they fly along the barrel like bullets, with the existing blast.
- **Switching views mid-play:** going to fixed view keeps the current facing until the cursor moves out of the dead zone. Going back to third person restores mouse-look and recentres on the body's facing, as now.
- **Frame rate:** turning, look-ahead smoothing and the sprint rule must give the same results at 30, 60 and 144 fps.

## Feel targets (proposed)

These are listed in [the feel spec](../../FEEL_SPEC.md#fixed-view-aiming-proposed) and confirmed by playtest after building:
- A 180° turn toward the cursor takes 0.25 s ±1 frame at 30, 60 and 144 fps.
- With the facing settled, the barrel points at the aim point within 0.5°.
- No sprint frames while travelling more than 45° from screen-up. The sprint resumes on the first frame back inside the cone.
- The facing changes by 0° while the cursor moves inside the dead zone.
- The camera look-ahead never exceeds 3 m.

## Test station and checks (to build with the feature)

- **Gun Range ring:** a new start marker (planned id GR-Ring) on the Gun Range, with 8 targets in a circle 8 m away at 45° steps. Two of them sit on 1.5 m boxes to test the aim height. They go under the existing resettable group, so F9 restores them. The page for this is [test areas](../../TEST_AREAS.md), updated when it is built.
- **Baseline harness:** each sequence step gets an optional cursor position (a world point that the harness converts to a screen position each frame). The harness then drives the cursor for sequences that run in fixed view. Planned sequences:
  - face and shoot each ring target in turn;
  - sprint in a circle (checks the sprint cone and resume);
  - walk in all eight directions while aiming down the screen (checks facing and leg clips).
- **Play-mode checks:** turn time per frame rate; the sprint cone and resume; the dead zone; the floor-aim rule (cursor over floor, a box top, a target); a shot at a ring target behind the player hitting after the turn.
- **Profile:** the cursor ray and facing run every frame, so they are profiled when built.

## Build order (each a checkpoint with owner playtest)

1. **Aim point and cursor:** the Core contract, the camera's cursor ray (floor rule, dead zone), and the crosshair at the cursor with the pointer hidden. Then facing toward the cursor (turn speed, legs strafe), armed and unarmed. Then the ring test station and the turn and dead-zone checks.
2. **Shooting:** the gun aims along the facing; shots from the muzzle along the barrel; precise aim without zoom. Checks: aim error, shots hitting the ring targets.
3. **Sprint rules and look-ahead:** the sprint cone, resume, and sprint facing limit; the camera look-ahead. Checks: the sprint cone and look-ahead targets.

## Assumptions to confirm at the first playtest

- The unarmed cursor is a small dot rather than the full crosshair.
- Fixed view has no camera recoil (unchanged from today).
- Rolls, dives and slides keep facing their travel direction.
- The setting defaults in the table above.
