# Mobs

## Purpose
Six block mobs (Dwarf, Giant, Skeleton, Soldier, Warrior, Zombie) that see the player, chase, investigate where they last saw them, search, return home, and attack. Spawned for testing from F1 > Mobs.

## Files
- Folder: `Assets/Game/Mobs/`, its own assembly (Game.Mobs) that references only Core.
- Prefabs: `Assets/Game/Mobs/Prefabs/` (the six mobs) are variants of the base appearance prefabs in `Assets/Game/Mobs/Prefabs/Base/`.
- `MobBrain` (on each mob): sight, chase (leading the target), attack in range, investigate the last sighting, search, return home. Without a target it hunts the player (`PlayerPresence` in Core).
- `MobAttackDriver` (on each mob): picks attack, idle, walk and run clips and checks range and facing when an attack lands.
- `BlockCharacterAnimations` with a `BlockCharacterAnimationSet` asset: plays the mob's clips through its Animator Controller (`Assets/Game/Mobs/Animation/`).
- `TargetHealth` (see [combat](combat.md)), a `NavMeshAgent`, a capsule collider and a kinematic rigidbody. Mob attacks damage anything that implements `IDamageable`.
- `DebugMobSpawner` (on Main Camera): spawns a selected mob at a clear, reachable spot near the player and clears spawned mobs.
- Clips: `Assets/ThirdParty/Kevin Iglesias/`.

## How to tune by feel
- Mob prefab > Mob Brain: **Sight Range / Field Of View** (how easily they notice you), **Chase Speed**, **Memory Seconds** and **Search Seconds** (how long they keep hunting).
- Mob prefab > Mob Attack Driver: **Range**, **Cooldown** (aggression), **Contact Time** (when in the swing the hit counts), **Damage**.
- Mob prefab > Target Health: **Hit Points** (how many hits they take).

## How to test
F7 goes to the mob test spot in the Test Area; F1 > Mobs spawns a mob there (mobs need the baked NavMesh on Test Area). Break line of sight behind the Concave Corner Wall and attack it. Three stationary mobs stand on the Gun Range for hit testing. Measured by the mob sequences of the baseline harness.

## Known limits (mob animation findings for a later iteration)
- Mob attacks do not hurt the player (the player has no health).
- Feet slide while chasing, badly for the Giant and Zombie (measured; [feel spec](../FEEL_SPEC.md#mobs)). Likely cause: walk or run is chosen by whether the mob can see you, not by its speed, and clip playback speed is never matched to travel speed (hypothesis).
- Mobs turn at a fixed rate with no turning animation, so they can pivot on the spot.
- Every animation change uses the same short crossfade, including attack back to locomotion.
- The Soldier mob plays a rifle animation at up to 10 m but shows no shot (no flash, tracer or sound), yet its hit can still land.
- No hit reactions or death animation; mobs vanish at zero health.
