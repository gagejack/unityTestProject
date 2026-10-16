# 2nd Project

A third-person movement and combat prototype with a block-built character, 46 guns, punches and kicks, and six block mobs. It is a focused rebuild of an earlier prototype.

## Open and play
1. Open Unity Hub, choose **Add > Add project from disk**, and pick this folder. Use Unity **6000.6.5f1**.
2. Open the scene `Assets/Game/Scenes/Main.unity` (Project window > Assets > Game > Scenes > Main).
3. Press **Play**. The debug menu (F1) opens at the start; click in the Game view to take mouse control.

## Controls
| Action | Input |
|---|---|
| Move / look | WASD / mouse |
| Run | Double-tap W and hold, or Shift + W |
| Jump / wall jump | Space (wall jump: Space while touching a wall in the air) |
| Crouch | Hold Ctrl |
| Roll / air dash / dive | Caps Lock on the ground / Caps Lock in the air / Alt + Caps Lock in the air |
| Attack (unarmed) or fire (armed) | Left mouse |
| Aim | Right mouse |
| Reload / drop gun / pick up | R / Q / F |
| Debug menu (equip guns, spawn mobs) | F1 |
| Go to gun range / movement course / mob test spot | F5 / F6 / F7 |
| Reset targets and refill ammo | F9 |
| Free camera | K |
| Release the mouse | Escape |

## Where to go next
- What is happening right now and what comes next: [docs/STATUS.md](docs/STATUS.md)
- How the systems fit together: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
- One page per system: [docs/systems/](docs/systems/)
- The test areas in the scene: [docs/TEST_AREAS.md](docs/TEST_AREAS.md)
- Rules for anyone (person or AI agent) changing the project: [AGENTS.md](AGENTS.md)
