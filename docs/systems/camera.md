# Camera

## Purpose
Owns the view: mouse look, the third-person follow position, pulling in when scenery is in the way, aim zoom with a shoulder offset, camera recoil, a small landing dip, and free roam (K). The aim direction for guns comes from here.

In the fixed view the camera also owns the **aim point**: the world point under the mouse cursor. Over plain floor (including box tops and ledges) the aim point is chest height above it; over a mob, target or wall it is the exact point. Inside a small dead zone around the character, or with the cursor off the screen, the aim keeps its last direction. Movement reads the aim point through `IViewHeading` (Core) to turn the body. The system pointer is hidden while the game has it (Escape or an open menu brings it back and the character stops following the cursor; click the Game view to take it again).

## Files
- Folder: `Assets/Game/Camera/`
- `FollowCamera` (on Main Camera).

## How to tune by feel
All on Main Camera > Follow Camera.
- **Third Person Offset / Look Height:** how far behind and above the character the camera sits.
- **Mouse Sensitivity / Invert Mouse Y:** look speed.
- **Aim Field Of View / Aim Camera Distance / Aim Shoulder Offset / Aim Height Offset:** how much aiming zooms in and moves over the shoulder.
- **Smooth Time / Aim Follow Smooth Time:** how loosely the camera trails the character. Higher feels floatier but can hide jitter.
- **Recoil Scale:** how much each shot kicks the view (each gun's own kick is on its WeaponSettings asset).
- **Maximum Landing Dip:** how much hard landings bob the view.
- **Fixed view aiming** group: **Cursor Dead Zone** (bigger keeps the facing calmer when the cursor is near the character, smaller lets you aim closer in), **Floor Aim Height** (how high shots over open floor fly), **Floor Max Slope** (which surfaces count as floor for that rule).

## How to test
Walk and turn in the Movement Course open area (F6); aim and fire on the Gun Range (F5); walk the camera into the Concave Corner Wall in the Test Area to check it pulls in. Camera kick timing is measured by the weapon sequences of the baseline harness ([feel spec](../FEEL_SPEC.md#weapons)). Fixed view aiming: the aim ring (F8); Play-mode checks in `FixedViewAimingTests` (Game.Movement.Tests) cover the floor rule and the dead zone.

## Known limits
- Shot recoil appears one frame after the shot (measured; cause in the update order in [architecture](../ARCHITECTURE.md#update-order)).
- The collision check allocates memory every frame.
- The fixed view aim point is worked out from last frame's camera position (about 0.1 m off at walking speed, inside the dead zone).
- Fixed view shots still follow the camera's view, not the aim point, until the shooting checkpoint of the [fixed view aiming spec](../spec/FixedViewAiming/SPEC.md).
- The camera reads the debug menu, movement and weapons directly.
