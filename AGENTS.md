# Agent instructions

These are the only instructions for agents (and people) changing this project. The owner is new to Unity: explain each change in plain language, give exact menu paths and Inspector field names, and say what to try in Play mode.

## Working style
- Work in small checkpoints. After each one, stop: summarise what changed, what to try in Play mode, and what you propose next. Wait for feedback.
- Separate demonstrated problems from hypotheses and preferences. Measure before claiming a feel or performance improvement.
- Ask before adding a package, changing the game concept, or upgrading Unity (6000.6.5f1).

## Ownership
Each system lives in one folder under `Assets/Game/` and owns its state. The ownership table and dependency rules are in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md); each system's page is in `docs/systems/`.
- `Assets/Game/Core/` holds shared contracts only (interfaces, events, enums, input references). No gameplay logic.
- Systems may depend on Core. They do not reference each other directly unless a decision record in `docs/decisions/` says why. Assembly references must never form a loop.
- New content (weapons, mob types, pickups) is added as data (ScriptableObject assets) wherever possible.
- Third-party pack files live in `Assets/ThirdParty/`; only files the game uses are kept.

## Update order
There is one documented update order, in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#update-order). Do not add an execution-order attribute or rely on script order anywhere else; give a new system a slot in that list.

## Validation (every checkpoint)
1. The project compiles and the Console has no errors.
2. All Play-mode checks pass (Window > General > Test Runner).
3. The change has been tried in Play mode at its test station (see [docs/TEST_AREAS.md](docs/TEST_AREAS.md)).
4. Anything that runs every frame is profiled when it changes.
5. **Tools > Project > Check Docs** reports no problems.

The full baseline (**Tools > Baseline > Run All Sequences**, about 5 minutes) is not part of every checkpoint. To check a change, run only the sequences it can affect. Ask the owner before running the full baseline.

## Documentation rules
- Layout: `README.md`, this file, `CLAUDE.md`, and in `docs/`: `STATUS.md`, `ARCHITECTURE.md`, `FEEL_SPEC.md`, `TEST_AREAS.md`, `systems/_TEMPLATE.md`, `systems/<system>.md`, `decisions/NNNN-<title>.md`, and `flow/` (the visual game map, `game-map.drawio`, and how to edit it), `spec/<Feature>/` for specs agreed with the owner before building (a spec is a plan: once built, the system pages describe the result and the spec is kept as the agreed brief). Do not create other doc files without asking. (`docs/HANDOFF_PROMPT.md` is the original brief and is kept for reference.)
- Each fact has one home; other pages link to it.
- Inspector field meanings and units go in `[Tooltip]` attributes in code. System pages describe what a setting feels like and point to the component, without repeating values.
- Feel numbers live only in [docs/FEEL_SPEC.md](docs/FEEL_SPEC.md) and the tests that check them.
- System pages describe what is built now. Plans go in [docs/STATUS.md](docs/STATUS.md), which is overwritten each checkpoint and kept to one screen.
- Git commits are the change history. No dated history files.
- A change to a system updates its page, its tests and `docs/STATUS.md` in the same commit.
- Docs check conventions: write project paths in backticks starting with `Assets/` or `docs/`, and write component or type names in backticks (for example `CapsuleMovement`). Any backticked CamelCase word must be a real type.

## Unity conventions
- Never lose a `.meta` file: move or rename assets inside Unity (or with their `.meta`). GUIDs are how scenes and prefabs find assets.
- Edit scenes, prefabs and settings through Unity editor tools, not by hand-editing YAML. Stop Play mode before saving scene changes.
- Saved scene, prefab and asset values override C# defaults. Check the saved value before documenting or changing tuning.
- Use the Input System package; do not add legacy `UnityEngine.Input` calls.

## Git
- Commit at each checkpoint with code, tests and docs together. Do not commit Library, Temp, Logs, UserSettings or IDE files.
- Keep migration or restructuring commits separate from gameplay changes.

## Adding a new system
First decide whether it really is a new system: it is one only if it owns its own state and responsibility. Otherwise extend the existing system and follow the normal change rules.
1. **Read only what's needed:** `README.md` → `docs/STATUS.md` → `docs/ARCHITECTURE.md` → the pages of the systems it will touch.
2. **Propose:** in `docs/STATUS.md`, write its purpose, which systems it interacts with and through which Core contracts, its feel targets, and its test station. Stop for the owner's approval if it changes the game concept or needs a package.
3. **Contracts first:** add or extend Core interfaces and events if needed.
4. **Scaffold:** create the folder and assembly (referencing Core only), plus a page copied from `docs/systems/_TEMPLATE.md`.
5. **Test first:** build the test station and write Play-mode checks for its behavior and feel targets.
6. **Build the smallest playable slice:** data in ScriptableObjects, with tooltips on every Inspector field.
7. **Wire it in:** place it in the update order, and edit scenes and prefabs through Unity editor tools.
8. **Verify:** it compiles, the Console is clean, all tests pass (new and existing), it's tried in Play mode at its test station, and it's profiled if it runs every frame.
9. **Document:** complete its page, then update the `docs/ARCHITECTURE.md` diagram and ownership and its `docs/FEEL_SPEC.md` section. Add a decision record only for a real choice. Change this file only if a rule changes. Run the docs check.
10. **Checkpoint:** update `docs/STATUS.md`, make one commit with code, tests and docs together, and stop for the owner's playtest.
