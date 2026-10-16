# Status

**Checkpoint 2b: project structure (no gameplay changes).**

## Current state
- Migrated baseline reorganised into one folder per system under `Assets/Game/` (Core, Movement, Camera, Animation, Unarmed, Weapons, Combat, Mobs, DebugMenu, World, Scenes, Editor). Third-party pack files trimmed to what is used, in `Assets/ThirdParty/`. Template leftovers removed. Scene renamed to `Assets/Game/Scenes/Main.unity`.
- New: Core assembly with `IDamageable` (implemented by `TargetHealth`); editor tools assembly with **Tools > Project > Check Docs**.
- Docs replaced with the layout in `AGENTS.md`. Systems still reference each other directly; assemblies per system come as each is rebuilt ([decision 0001](decisions/0001-project-structure.md)).
- Verified: compiles, 0 missing scripts, only the pre-existing broken weapon texture, all 6 mobs spawn/chase/attack/take damage, all 46 guns equip, docs check clean.

## Baseline size (C# lines per system)
Movement 807, Camera 289, Animation 1,106, Unarmed 168, Weapons 1,435, Combat 585, Mobs 676, Debug menu 374, Core 11, Editor tools 77.

## Last feedback
"I did not mean copy the entire project over. Please follow the instructions in handoff_prompt.md to create a new project structure."

## Next step (proposed)
1. Build the gun range and movement course with labeled start markers.
2. Baseline harness: per-frame log and fixed action sequences at 30/60/144 fps, recorded in `docs/FEEL_SPEC.md`.
