# Status

**Checkpoint 2: unchanged baseline migrated (no gameplay changes).**

## Current state
- 844 assets copied from the original with their `.meta` files (GUIDs preserved), at their original paths. Folder reorganisation comes later as its own step.
- Scene: `Assets/Game/Scenes/SampleScene.unity` (only build scene).
- Migration-only edits: Mystery Weapon Box removed from the scene (script kept so weapons compile); Spider Drone removed from the mob spawner list; gravity -10 and Run In Background copied from the original.
- Left behind: spider drone prefab/legs/camera/art, mystery box art, character showcase, all old editor setup/check scripts, unused AR Pack 1 and SMG Pack 3.
- Smoke test (Play mode): compiles with 0 errors; 0 missing scripts; all 6 mobs spawn, path to the player, attack and accept damage; all 46 catalogue weapons equip; console clean.

## Known issues carried over unchanged
- 21 pack weapons (AR_*, Pistol_*, Shotgun*, SMG_*) share a material whose texture slot points at a file missing in the original too.
- Import warnings from the source packs: old ModelImporter versions; Fighting Motions clips report "Transform 'Root' not found" on their avatar setup.
- NavMesh data still lives at `Assets/Game/Characters/SpiderDrone/Navigation/` (moved during reorganisation).

## Last feedback
Bring the debug menu unchanged; bring only the mystery box script; leave the original as it is; asked what Git LFS is (recommended: not needed, largest carried file is 5 MB).

## Next step
Checkpoint 3 (proposed): baseline measurement harness (per-frame log + fixed action sequences) run on this unchanged code.
