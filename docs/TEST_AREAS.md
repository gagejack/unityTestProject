# Test areas

## Debug menu

Open Main and press Play. F1 opens/closes the menu. At the Gun Range (F5), search for M1911, select a result, click Equip Weapon, then Spawn Pickup. Close the menu and try firing/picking up. Search for nonsense and confirm both actions disable; Clear restores the list. Try By Type and By Pack, then switch to another tab and back: the search and selection should remain. Type in Search and press F1; movement and mouse look should resume only after the menu closes. Click inside the panel and outside its border to check cursor capture.

Visit Mobs, Player, Camera, Audio and Controls. Check mob selection/spawn/clear at the mob test spot (F7), infinite stamina, the three camera modes, volume sliders and sound previews. Try 1280×720 and 1920×1080 Game view sizes and scroll the catalogue and longer settings pages.

Automated checks: Window > General > Test Runner > PlayMode > Game.DebugMenu.Tests. Also run the movement checks to catch regressions in the shared control gate.

All in `Assets/Game/Scenes/Main.unity`. Surfaces use the grid shader (`Assets/Game/World/Shaders/WhiteBlockGrid.shader`), which draws 1 m cells, so distances can be read off the floor. Every station has an orange start pad with an id; the small tab on the pad points the way the player will face.

The Gun Range and Movement Course are built by **Tools > Test Areas > Rebuild Gun Range and Movement Course** (`TestAreaBuilder`). Running it again replaces only those areas. Their runtime parts are in `Assets/Game/TestAreas/`.

## Shortcuts (Play mode)
| Key | Action |
|---|---|
| F5 | Go to the gun range firing line (GR-Firing) |
| F6 | Go to the movement course straight lane (MC-Lane) |
| F7 | Go to the mob test spot in the original Test Area (TA-Start) |
| F9 | Reset every target, post and stationary mob, and refill the held gun's ammo |

Any gun can be equipped from F1 > Weapons. Shortcuts are handled by `TestAreaControls` on the scene object "Test Area Tools".

## Gun Range (west of the Test Area)
| Station | Start pad | What it is for |
|---|---|---|
| Firing line with targets at 5, 15 and 30 m | GR-Firing | Accuracy, recoil, hit feedback at range (rebuilt Combat Practice Lane, same target hit points) |
| Low cover (1.1 m high) | GR-Cover | Standing muzzles clear it; crouch to test shots blocked by cover |
| Strafe lane, 10 m with a tick per meter | GR-Strafe | Firing while moving sideways past the target row |
| Moving targets at 12 m (2 m/s) and 22 m (4 m/s) | GR-Firing | Tracking moving targets |
| Stationary mobs (Dwarf, Zombie, Giant) | GR-Firing | Hits on real characters; their AI and navigation are switched off |
| Melee dummy | GR-Melee | Punches and kicks at reach distance |
| Rocket blast wall with posts at 1, 2, 3, 4 and 6 m | GR-Rocket | Blast radius and falloff (fire at the wall from 24 m) |

Every target shows its remaining hit points above it, and each hit shows a floating damage number (red with "KO" on a kill), from `HealthReadout` and `DamagePopups`. Targets, posts and stationary mobs are children of a `ResettableGroup`, which is what F9 restores.

## Movement Course (east of the Test Area)
| Station | Start pad | What it is for |
|---|---|---|
| Straight lane, 40 m, a line per meter, numbered every 5 m | MC-Lane | Start, stop, top speed, stopping distance |
| Open area, 20 x 20 m outline | MC-Open | Turning, reversing, strafing |
| Slopes of 20° and 40°, both rising 2.5 m | MC-Slope20, MC-Slope40 (bottom); MC-Slope20-Top, MC-Slope40-Top (top, facing down) | Walking and running up and down slopes |
| Steps, ten 0.25 m rises | MC-Steps (bottom); MC-Steps-Top (top, facing down) | Climbing up (up to 0.3 m without jumping) and walking down without leaving the ground |
| Ledges of 0.5, 1, 1.5, 2 and 2.5 m | MC-Ledges | Jump height and landing |
| Gaps of 1 to 5 m between 1 m platforms | MC-Gaps | Jump distance and dashes |
| Low tunnel, 1.5 m clear height | MC-Tunnel | Crouching (standing needs 2 m) |
| Two 8 m walls 3 m apart | MC-Wall | Wall jumps (the same wall cannot be used twice in a row) |

## Original Test Area
The scene object "Test Area" holds the ground, the 50 m corner wall, the parkour course and the `NavMeshSurface` mobs walk on (data: `Assets/Game/World/Navigation/MainNavMesh.asset`). Spawn mobs here from F1 > Mobs. The new areas are not children of Test Area, so they are not part of the mob NavMesh and do not change mob navigation. After changing geometry under Test Area, rebake: select Test Area and click **Bake** on its NavMesh Surface.

## Adding a test station
Add a new scene object (not inside Test Area, Gun Range or Movement Course) with a labeled `TestStartMarker` whose id starts with the system's short name. Put destructible targets under a `ResettableGroup` so F9 restores them. Fixed action sequences can then start from the new marker.
