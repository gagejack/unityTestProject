# Architecture

## Systems and ownership
| System | Folder | Owns | Page |
|---|---|---|---|
| Core | `Assets/Game/Core/` | Shared contracts only: `IDamageable`, `CombatEvents` (damage reports), `PlayerRequests` (teleport, refill ammo), `PlayerPresence` (where the player is), the project-wide input actions asset | (this page) |
| Movement | `Assets/Game/Movement/` | Character position, velocity, movement state (walk, run, jump, crouch, roll, dive, slide, wall jump), stamina | [movement](systems/movement.md) |
| Camera | `Assets/Game/Camera/` | View yaw/pitch, aim direction, follow and collision, aim zoom, camera recoil and landing dip, free roam | [camera](systems/camera.md) |
| Animation | `Assets/Game/Animation/` | The block player character: Humanoid rig, animation graph, special-move poses, block placement, rounding | [animation](systems/animation.md) |
| Unarmed | `Assets/Game/Unarmed/` | Limb contact for punches and kicks (attack selection and timing currently sit in Animation) | [unarmed](systems/unarmed.md) |
| Weapons | `Assets/Game/Weapons/` | Equipped gun, ammo, reload, firing, weapon pose, pickups, weapon data | [weapons](systems/weapons.md) |
| Combat | `Assets/Game/Combat/` | Hit queries, the damage receiver, shot and hit feedback, combat audio volume | [combat](systems/combat.md) |
| Mobs | `Assets/Game/Mobs/` | The six block mobs: AI, attacks, animation playback, prefabs, test spawner. Own assembly, references Core only | [mobs](systems/mobs.md) |
| Debug menu | `Assets/Game/DebugMenu/` | F1 menu (equip, spawn, stamina, camera, audio tabs) and the "menu is open" input gate | this page |
| World | `Assets/Game/World/` | Grid shader and level materials, NavMesh data | [test areas](TEST_AREAS.md) |
| Test areas | `Assets/Game/TestAreas/` | Start markers, target reset, moving targets, damage numbers, shortcuts, and the area builder. Own assembly, references Core only | [test areas](TEST_AREAS.md) |
| Diagnostics | `Assets/Game/Diagnostics/` | Baseline harness: action sequences, per-frame recorder, metrics. A measuring tool that reads the baseline components directly | [feel spec](FEEL_SPEC.md) |

Third-party files used by these systems are in `Assets/ThirdParty/`. Render pipeline settings are in `Assets/Settings/`.

## Dependencies today
Apart from Core, Mobs, Test areas and the editor tools, the gameplay code is the migrated baseline: it compiles into the default assembly and systems still call each other directly (arrows read "calls or reads"):

```mermaid
flowchart LR
    Menu[DebugMenu] --> Camera & Movement & Weapons & Mobs & Combat
    Camera --> Movement & Weapons & Menu
    Movement --> Camera & Animation & Weapons & Menu
    Animation --> Movement & Weapons & Unarmed & Menu
    Unarmed --> Animation & Weapons & Movement
    Weapons --> Movement & Camera & Animation & Combat & Menu
    Combat --> Weapons
    Mobs --> Core
    Combat --> Core
    Movement & Camera & Weapons --> Core
    Diagnostics -.-> Mobs
    TestAreas --> Core
```

Test areas reach the game only through Core: they raise `PlayerRequests` (movement teleports, the camera re-frames, weapons refill) and listen to `CombatEvents`. Diagnostics is a measuring tool outside the dependency rules; it will read Core contracts instead once the systems it measures are rebuilt.

These loops are why systems cannot have their own assemblies yet. See [decision 0001](decisions/0001-project-structure.md).

## Dependency rules (apply to new and rebuilt code)
- Every system may depend on Core. Core depends on nothing in the game.
- Systems do not reference each other directly unless a decision record says why. They talk through Core interfaces and C# events, not by finding or reaching into each other's components.
- Assembly references never form a loop. A rebuilt system gets its own assembly that references Core only.

## Update order
The one list of what runs when. A lower number runs earlier within the same phase; all Update work happens before any LateUpdate work.

| Order | Component | Update | LateUpdate |
|---:|---|---|---|
| -120 | `ViewModeDebugMenu` | Menu and view shortcuts | |
| -100 | `FollowCamera` | Mouse look, free-roam flight | Place the camera (before the character pose is final) |
| -50 | `MaleWalkRetarget` | Read the unarmed attack click | Evaluate the animation graph, then run-arm tuck, chest aim pitch |
| -25 | `WeaponCombatController` | Aim, fire input (queues the shot), reload, pickups, HUD | |
| 0 | `CapsuleMovement` and all scripts without an order | Move the character and set its facing; `TestAreaControls` shortcuts | `MobAttackDriver` animation choice, `StaminaBar`, `SpeedReadout`; `BaselineRunner` queues the next frame's test input |
| 160 | `BlockHumanoidRig` | | Special-move poses, copy bones to blocks, ground the body, punch contact, weapon arm pose |
| 200 | `PlayerBlockRounding` | | Refresh rounded block meshes |
| 220 | `CombatFeedback` | | Resolve the queued shot from the final muzzle position, effects |
| 10000 | `BaselineRecorder` (only during a baseline run) | | Log the final state of the frame |

Known consequence: camera recoil from a shot is applied after the camera has been placed, so it shows one frame later.

## Glossary
- **Humanoid rig / avatar:** Unity's standard human skeleton description; lets clips made for one character play on another.
- **Animation graph (PlayableGraph):** clips mixed in code rather than in an Animator Controller window.
- **NavMesh:** the walkable-area map mobs use for pathfinding.
- **ScriptableObject:** a data asset (for example a WeaponSettings asset) edited in the Inspector.
- **GUID:** the ID in each `.meta` file that scenes and prefabs use to find an asset.
- **Test station:** a small, labeled area in the scene for trying one system.
