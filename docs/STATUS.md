# Status

**Checkpoint 1: inspection of the original project (no assets copied yet).**
Source: `C:\Users\gagej\Projects\Unity\My project` (working tree; most of it is uncommitted there).

## Current state
- Empty URP project (Unity 6000.6.5f1) with the Unity Pipeline package for MCP. Nothing migrated.
- Original inspected through MCP and source reading only; it was not modified.

## Known problems (from the handoff prompt), verified
| # | Claim | Result |
|---|---|---|
| 1 | WeaponCombatController mixes many jobs | Confirmed: 1,033 lines; firing, reload, HUD building, pickups, points, debug spawn, arm IK, hand pose |
| 2 | MaleWalkRetarget hand-builds the graph and moves bones in code | Confirmed: 597 lines; graph + run-arm tuck + chest pitch + unarmed attack selection/timing |
| 3 | Order-dependent work spread across files | Confirmed: 7 explicit orders (-120 to 220); up to 5 writers touch an upper-arm bone per frame. Jitter itself not yet measured |
| 4 | One ~270-line movement method | Confirmed: `SimulateMovement` is 272 lines |
| 5 | Overlapping docs | Confirmed: 21 top-level files in `docs/` plus 7 system pages; at least one documented value is stale |
| 6 | Mob AI lives in `SpiderDroneBrain` | Confirmed: humanoid mobs use Watching/Tracking/Investigating/Searching/Attacking only; pounce states are spider-only |

## Last feedback
"continue" (after checkpoint 0). Open questions are listed in the checkpoint summary.

## Next step
Checkpoint 2 (proposed): copy assets with GUIDs preserved and get the unchanged baseline running in the new project.
