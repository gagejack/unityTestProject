Create a separate Unity 6000.6.5f1 URP project in `C:\Users\gagej\Projects\Unity\2nd Project` (this folder; it currently holds only this prompt, so create the project here and move this file to `docs/HANDOFF_PROMPT.md` afterwards), derived from my current connected project at `C:\Users\gagej\My project`. Preserve the original project.

Carry over the following, plus the assets and supporting systems they need to work:
- the player movement and the third-person camera
- the weapons
- the unarmed combat (punches and kicks)
- the block player character (the block humanoid rig, its rounding and its art)
- the grid look (`WhiteBlockGrid.shader` and the ground, platform and boundary materials that use it)
- the six block mobs (Dwarf, Giant, Skeleton, Soldier, Warrior, Zombie), with their prefabs, Animator controllers, animation sets, and the source animation clips they depend on
- what the mobs need to function: NavMesh, health, attack driver, and a way to spawn them for testing

Leave behind the spider drone and systems these don't depend on, such as the block-character showcase and the debug-menu extras. If one of them turns out to be required, list it and the reason before bringing it over.

Carry over data as the source of truth: WeaponSettings assets, animation clips, models, materials, sounds, and the tuning values saved in scenes, prefabs and assets. Saved values override C# defaults, so use the saved values. Code may be copied to reproduce the baseline, but the improved systems should be rebuilt to fit the findings, not patched line by line. Keep the scope focused on making these systems feel better and making the new project understandable and maintainable over time.

**This is a prototype and the work is iterative.** Work in small checkpoints. After each one, stop and give me a short summary of what changed, what I should try in Play mode, and what you propose next. Wait for my feedback before continuing. Don't bundle many improvements into one step.

**Treat this as a controlled migration followed by targeted improvement.** First inspect the actual implementation and running behavior:
- source files, scenes, prefabs and Inspector values
- input, physics and navigation settings
- animation clips, the hand-built animation graph, and any Animator layers and transitions
- weapon configuration, mob configuration, and asset dependencies

Verify the existing documentation against these sources. Identify what must transfer, and explain supporting dependencies that would otherwise be easy to miss.

**Known problems to verify, not assume.** A first look at the current project found the following. Confirm each one, measure it, or rule it out:
1. `WeaponCombatController.cs` (~1,030 lines) mixes firing, reloading, HUD building, pickups, point spending, debug spawning, arm-bone lookups and hand-pose application.
2. `MaleWalkRetarget.cs` (~600 lines) builds the animation graph by hand in code. It also moves arm bones with code (for example, tucking the arms while running). Unarmed attack selection and timing live here too. The weapon pose is then layered on top by code in a separate file.
3. Movement, animation and shooting depend on running in a set order, spread across files (`DefaultExecutionOrder` -50/-25, and shots resolved after the displayed pose). This is a likely source of competing transform writes and pose jitter.
4. `CapsuleMovement.cs` has one ~270-line movement method that holds walking, running, jumping, dashing, crouching and wall-jumping.
5. Documentation overlaps. About 20 top-level feature guides in `docs/`, written as change diaries, repeat content from `docs/systems/`, and some facts appear in more than one place.
6. The mobs' sight, pursuit, search and pounce AI lives in `SpiderDroneBrain.cs`, despite the name. Since the spider drone is not coming over, move the logic the humanoid mobs actually use into a clearly named mob AI component. Drop the parts that only the spider uses, and confirm the mobs behave the same as before.

**Establish a small, repeatable baseline** for movement, weapon, unarmed and mob behavior before making improvements. Use fixed action sequences in the test areas (below), frame-by-frame captures, and a lightweight per-frame log (input, position, velocity, animation state and phase, foot positions, camera, shot and feedback events). Where it helps, turn findings into numeric feel targets (for example, time to top speed, stopping distance, foot slide per step, frames from input to visible response). Each later change can then be checked against those numbers. Reproduce the baseline in the new project, runnable on its own, with asset references and required settings preserved. Keep migration changes separate from intentional gameplay changes, using clear version-control checkpoints.

**Then analyze the complete interaction** from input → movement → character animation → gun animation → camera and shot feedback. Focus on:
- starting, stopping, reversing, and changing movement speed
- firing while stationary and while moving
- unarmed attacks and how they flow into and out of movement
- drawing and holstering weapons
- hitting mobs with guns and fists: hit feedback, hit reactions and damage

Examine other existing actions only where they reveal a problem in those interactions. For the mobs, the goal is a faithful migration and solid hit interaction, not new AI behavior. Report any obvious mob animation problems as findings for a later iteration.

**Pose problems I can see and want fixed:**
1. When simply walking unarmed, the arms stick out from the body instead of hanging and swinging naturally.
2. Holding a gun while standing and aiming looks awkward.
3. Running with a gun looks awkward. The arms, the support hand and the weapon don't move together with the run.
4. Movement, unarmed attacks and weapon poses don't blend into each other naturally.

Find the actual cause of each one, such as clip retargeting or muscle limits, blend weights, code overwriting arm bones, support-hand IK fighting locomotion, or update order. Fix the cause rather than layering corrections on top.

**Holstering (new behavior):**
- When the character is not actively holding the weapon, it should be visibly stowed on the body.
- Pistols and SMGs go on the side of the hip. Dual handguns go on both hips.
- Assault rifles, shotguns, rifles and snipers, machine guns and launchers go on the back.
- Drive the placement from the existing `WeaponSettings.category` field, with an optional per-weapon offset where a model needs adjusting. Don't hand-place 48 weapons.
- Include a draw and a holster transition, so the hand moves to the holster and the weapon follows the hand. Don't just teleport the model.
- Unarmed attacks should work with the weapon holstered.
- Propose the controls for holstering and drawing. Keep the existing drop (Q) and pickup (F) behavior unless I approve a change.

**Test areas (new, kept simple):** Build two small, clearly separated test areas in the main scene. Use the grid materials so distances are easy to judge, and give each area a labeled start marker.

1. **Gun range:**
   - Rebuild the existing Combat Practice Lane: a firing line, plus targets at about 5, 15 and 30 meters.
   - Add a low cover block so the muzzle-blocked-by-cover check can be tested.
   - Add a short strafe lane parallel to the targets, for firing while moving.
   - Add one or two moving targets, a few stationary mobs to test hits on characters, and an open area where a rocket's blast can be tested.
   - Show damage and hits clearly.
   - Provide a simple way to reset targets and refill ammo without restarting Play mode.
   - Make every weapon quickly available here, through a weapon rack or the existing debug equip.
2. **Movement course:**
   - a flat straight lane with distance markings every meter, for start, stop, top speed and stopping distance
   - an open area for turning, reversing and strafing
   - a slope and a set of steps
   - a few ledges and gaps of increasing height and width, for jumping, dashing and landing
   - a low tunnel for crouching
   - a wall for wall-jumping

Make the fixed baseline action sequences start from these markers so measurements are repeatable. Build the areas from simple primitives and prefabs. No new art or packages. Document each area's layout and purpose on one short page, and don't let test geometry interfere with mob navigation outside its area.

**Methods and tools:**
- Use Unity MCP, source and dependency inspection, frame-by-frame captures, animation tools, lightweight timing measurements, and the Unity Profiler where available.
- Look for competing transform writes, unnecessary input delay, frame-rate-dependent behavior, mismatches between animation and actual movement (foot sliding), abrupt transitions, and poorly synchronized weapon feedback.
- Separate demonstrated problems from hypotheses and subjective preferences.
- If a Unity package would replace a meaningful amount of custom code (for example, Animation Rigging for support-hand IK and aim, or Cinemachine for camera and camera shake), explain the trade-off and get my approval before adding it.

**Implement the smallest changes that address the highest-impact findings.** Aim for:
- responsive controls
- readable movement
- coherent character and weapon poses
- satisfying firing and hit feedback, for both guns and fists

Preserve intentional weight and existing mechanics unless evidence supports changing them. Don't add unrelated features.

**Give movement, animation, weapons, unarmed combat, mobs and camera effects clear responsibilities.** Movement should calculate the character's state once; animation and weapon pose should read that state, not recalculate or overwrite it. Use one explicit, documented update order. Reduce duplicated state and overlapping logic. Favor simple components, and data-driven weapon differences where they remove real duplication. Judge efficiency by clarity, correctness and measured runtime cost. Report line counts before and after for each system. Explain any case where more code makes the system easier to maintain.

**Organize the new project professionally without unnecessary architecture.** One folder per system under `Assets/Game/`, with an assembly definition each if it helps keep the boundaries clear. Replace the overlapping feature diaries from the old project with current-state pages. Use exactly this documentation layout, and don't create other doc files without asking:

```
README.md              how to open, play, and where to go next (one screen)
AGENTS.md              the only agent instructions: ownership, update order, validation, doc rules,
                       and the "Adding a new system" workflow below
CLAUDE.md              one line: "Follow AGENTS.md" (plus the beginner note)
docs/
  STATUS.md            overwritten each checkpoint, kept to one screen: current state,
                       my last feedback, next step
  ARCHITECTURE.md      diagram, which system owns what, dependency rules, update order, glossary
  FEEL_SPEC.md         the single home for numeric feel targets, one section per system;
                       tests read or cite these
  TEST_AREAS.md        gun range, movement course, and any test stations: layout and how to use
  systems/_TEMPLATE.md the template every system page follows
  systems/<system>.md  one per system (movement, camera, animation, weapons, holster, unarmed,
                       mobs): purpose, files, how to tune by feel, how to test, known limits
  decisions/NNNN-<title>.md   short records of why a real choice was made
```

Documentation rules:
- Each fact has one home. Other pages link to it.
- Inspector field meanings and units go in `[Tooltip]` attributes in the code. System pages explain what a setting feels like and point to the component, without repeating values.
- Feel numbers live only in `FEEL_SPEC.md` and the tests that check them.
- System pages describe what's built now, not plans. Plans go in `STATUS.md`.
- Git commits are the change history. Don't create dated history files.
- Any change to a system updates its page, its tests and `STATUS.md` in the same commit.
- Provide an editor menu command (for example **Tools > Project > Check Docs**) that confirms every file path and component named in the docs still exists. Run it at each checkpoint.

**Make the project easy to extend:**
- Create a small `Core` folder and assembly for shared contracts only. Examples: a damage interface any target can implement, hit and damage events, input action references, and shared enums. No gameplay logic goes in Core. Keep it small.
- Dependency direction: every system may depend on Core. Systems don't reference each other directly unless a decision record says why, and assembly references must never form a loop. Systems communicate through Core interfaces and C# events, not by finding or reaching into each other's components.
- Keep the update order in one place (one documented list, or one coordinating component that only calls systems in order and holds no gameplay logic), so a new system has an obvious slot.
- New content (new weapons, mob types, pickups) should be added as data (ScriptableObject assets) wherever possible, not new code.
- Design the test areas so a new system can add its own small test station alongside them, without changing the gun range or movement course.

**In `AGENTS.md`, include this "Adding a new system" workflow.** First decide whether it really is a new system. It is one only if it owns its own state and responsibility. Otherwise extend the existing system and follow the normal change rules.
1. **Read only what's needed:** `README.md` → `STATUS.md` → `ARCHITECTURE.md` → the pages of the systems it will touch.
2. **Propose:** in `STATUS.md`, write its purpose, which systems it interacts with and through which Core contracts, its feel targets, and its test station. Stop for my approval if it changes the game concept or needs a package.
3. **Contracts first:** add or extend Core interfaces and events if needed.
4. **Scaffold:** create the folder and assembly (referencing Core only), plus a page copied from `systems/_TEMPLATE.md`.
5. **Test first:** build the test station and write Play-mode checks for its behavior and feel targets.
6. **Build the smallest playable slice:** data in ScriptableObjects, with tooltips on every Inspector field.
7. **Wire it in:** place it in the update order, and edit scenes and prefabs through Unity editor tools.
8. **Verify:** it compiles, the Console is clean, all tests pass (new and existing), it's tried in Play mode at its test station, and it's profiled if it runs every frame.
9. **Document:** complete its page, then update the `ARCHITECTURE.md` diagram and ownership and its `FEEL_SPEC.md` section. Add a decision record only for a real choice. Change `AGENTS.md` only if a rule changes. Run the docs check.
10. **Checkpoint:** update `STATUS.md`, make one commit with code, tests and docs together, and stop for my playtest.

**Validate:**
- The new project opens, compiles and runs on its own, with no missing references (including mob prefabs, materials, the grid shader and animation clips) and no dependencies on the original project's folders.
- All six mobs spawn, navigate, attack and take damage.
- Both test areas load, reset correctly, and work with every weapon category and every movement action listed above.
- The original baseline and the final behavior are compared using the same action sequences.
- Report evidence for responsiveness, animation continuity, movement/weapon alignment, holster placement for every weapon category, and consistency across frame rates (for example 30, 60 and 144 fps).
- Turn the key measurements into automated Play-mode checks so later changes can't silently break them.
- Profile any claimed performance improvement.
- Name the feel changes that still need my playtesting, and give me a short checklist for it.

**Finish with a concise, beginner-friendly handoff:**
- what transferred
- what changed and why
- where to edit common movement, weapon, holster, unarmed-combat and mob settings
- how to use the gun range and movement course, and how to run the checks
- remaining limitations
- the proposed next iteration

Make sure another agent can continue from the project's files and documentation without needing this conversation.
