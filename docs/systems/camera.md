# Camera

## Purpose
Owns the view: mouse look, the third-person follow position, pulling in when scenery is in the way, aim zoom with a shoulder offset, camera recoil, a small landing dip, and free roam (K). The aim direction for guns comes from here.

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

## How to test
Walk and turn on the Test Area ground; aim and fire at the Combat Practice Lane; walk the camera into the Concave Corner Wall to check it pulls in. No Play-mode checks exist yet.

## Known limits
- Shot recoil appears one frame after the shot (see the update order in [architecture](../ARCHITECTURE.md#update-order)).
- The collision check allocates memory every frame.
- The camera reads the debug menu, movement and weapons directly.
