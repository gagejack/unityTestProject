# Mobs

## Purpose
Six block mobs (Dwarf, Giant, Skeleton, Soldier, Warrior, Zombie) that see the player, chase, investigate where they last saw them, search, return home, and attack. Spawned for testing from F1 > Mobs.

## Files
- Folder: `Assets/Game/Mobs/`
- Prefabs: `Assets/Game/Mobs/Prefabs/` (the six mobs) are variants of the base appearance prefabs in `Assets/Game/Mobs/Prefabs/Base/`.
- `SpiderDroneBrain` (on each mob): sight, pursuit, investigate, search and return home. The name comes from the original project, where the spider drone shared it; its pounce states are not used by these mobs.
- `MobAttackDriver` (on each mob): picks attack, idle, walk and run clips and checks range and facing when an attack lands.
- `BlockCharacterAnimations` with a `BlockCharacterAnimationSet` asset: plays the mob's clips through its Animator Controller (`Assets/Game/Mobs/Animation/`).
- `TargetHealth` (see [combat](combat.md)), a `NavMeshAgent`, a capsule collider and a kinematic rigidbody.
- `DebugMobSpawner` (on Main Camera): spawns a selected mob at a clear, reachable spot near the player and clears spawned mobs.
- Clips: `Assets/ThirdParty/Kevin Iglesias/`.

## How to tune by feel
- Mob prefab > Spider Drone Brain: **Sight Range / Field Of View** (how easily they notice you), **Chase Speed**, **Memory Seconds** and **Search Seconds** (how long they keep hunting).
- Mob prefab > Mob Attack Driver: **Range**, **Cooldown** (aggression), **Contact Time** (when in the swing the hit counts), **Damage**.
- Mob prefab > Target Health: **Hit Points** (how many hits they take).

## How to test
F7 goes to the mob test spot in the Test Area; F1 > Mobs spawns a mob there (mobs need the baked NavMesh on Test Area). Break line of sight behind the Concave Corner Wall and attack it. Three stationary mobs stand on the Gun Range for hit testing. Measured by the mob sequences of the baseline harness.

## Known limits
- Mob attacks do not hurt the player (the player has no health).
- Feet slide while chasing, badly for the Giant and Zombie (measured; [feel spec](../FEEL_SPEC.md#mobs)). Likely cause: walk or run is chosen by whether the mob can see you, not by its speed (hypothesis).
- The Soldier mob plays a rifle animation at range but fires no bullets.
- No hit reactions or death animation.
