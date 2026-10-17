# Status

**Checkpoint 7: F1 debug menu presentation.**

## Current state
- The F1 menu now uses UI Toolkit, with the supplied UIExample-inspired cream grid panel, colorful beveled buttons and Barlow Condensed headings.
- Weapons has search, type/creator/pack filters, a preview, Equip Weapon and Spawn Pickup. Mobs, Player, Camera, Audio and Controls retain their existing actions in the new layout.
- Existing player-control gating, cursor capture and camera modes remain connected. The menu does not pause world simulation.
- Validation: all 5 menu interaction tests and the 6 existing movement tests passed (the F1 check was rerun with Game view focus). Unity compiles without errors or warnings. Layout reviewed at 1280x720 and 1920x1080, plus all tabs.
- Editor UI profiling captured 178 frames in Logs/DebugMenu/profile.json. These markers include editor rendering and are not an isolated menu/build benchmark. Screenshots and the pre-change unsaved scene backup are also in Logs/DebugMenu.
- Layout/styles and setup instructions: [architecture](ARCHITECTURE.md#debug-menu-presentation). Playtest route: [test areas](TEST_AREAS.md#debug-menu).

## Next checkpoint
Review the menu's visual style, scale and browsing in Play mode; refine from feedback. The earlier animation/pose work remains pending (plan step 6).
