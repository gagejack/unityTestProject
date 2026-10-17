# Feel spec

The single home for numeric feel targets. Values come from the baseline harness (**Tools > Baseline > Run All Sequences (Play Mode)**): fixed action sequences in `Assets/Game/Diagnostics/Sequences/`, played from the test-area start markers with a fixed timestep of 1/30, 1/60 and 1/144 s. Raw per-frame logs and frame captures go to `Logs/Baseline/latest/` (not committed). Metric code: `BaselineMetrics`.

**Baseline** = the migrated, unchanged code (recorded 2026-10-16). Jump apex baselines were corrected after the metric was fixed to measure from the frame before take-off. An arrow shows the value after a rebuild. **Targets** were approved by the owner on 2026-10-16; Play-mode checks will enforce them as each system is rebuilt. "n/a" means the run lost window focus on all three attempts.

How to read the columns: values are listed as 30 / 60 / 144 fps.

## Movement (start pad MC-Lane, MC-Open)
| Metric | Definition | Baseline 30 / 60 / 144 | Target |
|---|---|---|---|
| Input to motion | Frames from the first frame a move key is down to the first frame the body moves | 0 / 0 / 0 | Keep 0 |
| Walk / run top speed | Planar speed at the end of a hold (m/s) | 4.5 / 4.5 / 4.5 and 6.75 / 6.75 / 6.75 | Keep |
| Walk time to top speed | Seconds until speed stays within 5% of the final value | 0.10 / 0.10 / 0.10 | Keep |
| Walk stopping distance | Meters travelled after releasing W | 0.11 / 0.14 / 0.17 → **0.18 / 0.18 / 0.18** after the movement rebuild | Same at all frame rates (±0.02); checked by `MovementFeelTests` (0.18 ±0.02) |
| Run stopping distance | Same, from a run | 0.30 / 0.35 / 0.38 → **0.405 / 0.405 / 0.405** | Same at all frame rates (±0.02) |
| Airborne frames going down | Frames not grounded while walking or running down from the top pads (no jump pressed). Owner-reported bounce and floating pose | Walk 20°: 0 / 0 / 0. Run 20°: 15 / 40 / n/a. Walk 40°: 16 / 36 / 90. Steps: 16 / 40 / 109 → **0 everywhere** after ground snapping | 0; checked by `MovementFeelTests` (walking) |
| Reverse time | Seconds from S pressed (while walking) until settled backwards | 0.03 / 0.02 / 0.01 | Keep |
| Jump apex | Highest point minus the height just before take-off (m); Jump Height is set to 1.6 | 1.46 / 1.53 / 1.57 → **1.60 / 1.60 / 1.60** | 1.6 ±0.03 at all frame rates; checked by `MovementFeelTests` |
| Dive (jump, Caps Lock 0.2 s later) | Seconds from jump to dive start / slide duration (s) / total distance (m) | 0.30, 0.83, 12.0 / 0.30, 0.80, 12.0 / 0.30, 0.80, 12.0 → after rebuild distance 12.5 / 12.24 / 12.11 | Keep; the landing frame adds up to one frame of dive travel (known limit) |

## Animation (same sequences)
| Metric | Definition | Baseline 30 / 60 / 144 | Target |
|---|---|---|---|
| Walk foot slide (ankle) | While a foot is planted, its ankle's travel as % of body travel (0 = foot stays put); the original measure | 24 / 27 / 28 % → **15 / 16 / 17 %** with the jog and sprint clips | ≤ 10 % |
| Run foot slide (ankle) | Same, running | 51 / 59 / 60 % → **21 / 23 / 25 %** | ≤ 15 % |
| Strafe foot slide (ankle) | Same, strafing right | 27 / 30 / 29 % → **15 / 17 / 18 %** | ≤ 10 % |
| Walk / run / strafe foot slide (visible) | Same, measured at the lowest corner of each lower-leg block (what touches the floor on screen) | Walk 14 / 17 / 19 %, run 19 / 27 / 31 %, strafe right 52 / 51 / 50 % (first recorded with the jog and sprint clips) | Diagnostic only; the owner kept the ankle targets |
| Walk arm angle (unarmed) | Upper arm angle out from the body, front view (degrees) | 16 / 16 / 16 → 7.2 after the avatar rebuild → **0 / 2 / 1** with the jog clip | 5-9 (arms clear the chest block; proposed, confirm by playtest) |
| Run torso lean | Side-view angle of the line from hips to neck, forward from vertical (degrees); standing reads about 6 | Jog 22 / 22 / 22, sprint clip 45 → 38 / 38 / 38 with Running Lean Correction 15 → **35 / 35 / 35** with 23.3 | 35 (Running Lean Correction 23.3, set by the owner's playtest) |
| Run arm angle (unarmed) | Same measure as the walk arm angle, while sprinting | **38.5 / 38.5 / 38.5** with Running Arm Down Angle 51.5 | 38.5 (set by the owner's playtest) |
| Input to walk pose | Frames from key down to the walk blend starting | 0 / 0 / 0 | Keep 0 |

## Weapons (start pad GR-Firing, GR-Strafe, GR-Rocket)
| Metric | Definition | Baseline 30 / 60 / 144 | Target |
|---|---|---|---|
| Assault rifle fire rate | Rounds per minute while held; the asset says 0.1 s = 600 | 600 / 563 / 576 | 600 ±1 % at all frame rates |
| Pistol fire interval | Seconds between shots when clicking every 0.2 s (fire rate 0.24) | 0.40 / 0.40 / 0.40 | Investigate: clicks during cooldown are dropped |
| Click to shot | Frames from button down to the shot | 0 / 0 / 0 | Keep 0 |
| Shot to camera kick | Frames from the shot to the camera moving | 1 / 1 / 3 | 0 (kick on the shot frame) |
| Gun aim error (aiming) | Angle between the barrel and the line from muzzle to the crosshair point (degrees) | AR 1.6, pistol 0.6, launcher 1.5 at every rate | ≤ 0.5 |
| Support hand gap, walking with rifle | Distance from left hand to its grip point (cm) | 0 / 0 / 0 | Keep 0 |
| Support hand gap, running with rifle | Same, running | 132 / n/a / 131 → **0 / 0 / 0** with the sprint hold pose (rifle, shotgun and bazooka also 0) | ≤ 5 |
| Speed while strafing and firing | m/s (rifle movement multiplier and fire slowdown) | 3.04 / n/a / 3.04 | Keep |

## Unarmed (start pad GR-Melee)
| Metric | Definition | Baseline 30 / 60 / 144 | Target |
|---|---|---|---|
| Click to attack | Frames from click to the attack clip starting | 0 / 0 / 0 | Keep 0 |
| Attacks started from 8 clicks | Clicks 0.35 s apart | 8 / 8 / 8 | Keep |
| Hits on the dummy from 8 attacks | Damage events (each run starts at a different attack in the combo) | 6 / 6 / 5 | Same at all frame rates |
| Attacks that reach the dummy | Of the 12 attacks in the combo, at 1.2 m | 9 of 12 (left jab, two kicks miss) | All attacks that are meant to connect at this range |
| Click to hit, per attack | Seconds from click to the hit, compared attack by attack | Within one frame across frame rates (e.g. left hook 0.20 / 0.18 / 0.17) | Keep |
| Straight right contact | Seconds from click to the hit registering | 0.07 at every rate (before the punch visibly extends) | Contact when the fist visibly arrives |
| Speed while attacking and walking | m/s | 2.16 / 2.24 / 2.38 | Same at all frame rates |

## Combat feedback
Covered by the weapon and unarmed rows above (camera kick, hits). No separate targets yet.

## Mobs (start pad TA-Start, player standing still)
| Mob | Chase speed (m/s) | Foot slide 30 / 60 / 144 | Time to first attack (s) |
|---|---|---|---|
| Dwarf | 3.11 | 33 / 39 / 40 % | 2.0 |
| Giant | 2.64 | 206 / 188 / 131 % | 1.9 |
| Skeleton | 1.90 | 56 / 56 / 57 % | 3.3 |
| Soldier | stands and shoots | n/a | 0.0 (in range at spawn) |
| Warrior | 3.09 | 39 / 36 / 35 % | 2.0 |
| Zombie | 1.90 | 98 / 98 / 98 % | 3.3 |

Re-measured after the mob AI moved to `MobBrain`: every value above is identical at all three frame rates.

Target for mobs: foot slide ≤ 20 %. Over 100 % means the planted foot moves faster than the body: the clip's stride does not match the chase speed.

## Fixed view aiming (proposed)
From the [fixed view aiming spec](spec/FixedViewAiming/SPEC.md); checkpoints 1 (facing) and 2 (shooting, sprint facing) are built. Targets are proposed and confirmed by playtest. Start pad: GR-Ring (F8). Checked by `FixedViewAimingTests`.

| Metric | Definition | Baseline 30 / 60 / 144 | Target |
|---|---|---|---|
| Turn to cursor | Seconds for the body to turn 180° toward the cursor | Within ±1 frame of 0.25 at all three (Play-mode check) | 0.25 ±1 frame at all frame rates |
| Fixed-view aim error | Angle between the barrel and the line from muzzle to the aim point, facing settled (degrees) | 60 fps, standing: rifle and pistol 0.00 hip and aimed; dual handguns 0.09 hip, 0.00 aimed (Play-mode check) | ≤ 0.5 |
| Shift sprint directions | Screen directions (of 8) in which Shift + movement sprints, travelling that way | 8 of 8 at 60 (Play-mode check) | 8 of 8 |
| Dead-zone stability | Facing change while the cursor moves inside the dead zone (degrees) | 0 at 60 (Play-mode check) | 0 |
| Look-ahead | Largest camera shift toward the cursor (m) | Not built | ≤ 3 |

## Notes on the measures
- Hand jitter (in the raw logs) is a frame-to-frame second difference and shrinks with frame rate; compare it only at the same frame rate.
- Ankle foot slide: a foot counts as planted while its ankle is within 3 cm of that ankle's lowest point in the window, so very short contacts at 30 fps are partly missed. Mob foot slide uses this measure.
- Visible foot slide: a foot counts as planted while the bottom corner of its lower-leg block is within 2 cm of the lowest contact of either foot. It rises with frame rate because finer frames catch the brief scrape as a foot lands and lifts; 144 fps is the closest to what is seen. Strafing reads high because the strafe clip's crossing foot reaches the floor while still moving sideways (the ankle measure does not see this).
