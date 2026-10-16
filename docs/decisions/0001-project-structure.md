# 0001: Project structure for the migrated baseline

**Status:** accepted

## Context
The prototype was migrated from an earlier project. Its gameplay scripts call each other directly in loops (for example movement reads weapons, weapons read movement). Unity assemblies cannot reference each other in a loop, so giving every system its own assembly would not compile until those loops are broken.

## Decision
- One folder per system under `Assets/Game/`, holding that system's scripts, data, prefabs, art and audio.
- `Assets/Game/Core/` is its own assembly (Game.Core) with shared contracts only. Editor tools are in their own editor-only assembly (Game.Editor).
- Until a system is rebuilt, its migrated scripts stay in the default assembly. When a system is rebuilt it gets its own assembly that references Core only, and its loops are replaced by Core interfaces and events.
- Third-party pack files are kept in `Assets/ThirdParty/`, one folder per pack, trimmed to the files the game uses, with their original folder layout so pack updates stay recognisable.
- Unity's render pipeline settings stay in `Assets/Settings/`, where URP expects them.

## Consequences
- The folder layout already shows ownership; the assembly boundaries arrive system by system.
- Asset GUIDs were preserved by moving assets inside Unity, so no references broke.
