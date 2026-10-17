# F2 Debug Menu: current design spec

This document describes the F2 debug menu currently implemented in Main. Its layout, stylesheet and controller are the design reference. Dimensions below are logical UI Toolkit pixels before panel scaling.

## Source files

| Responsibility | Source |
|---|---|
| Shell and document structure | `Assets/Game/UI/Prototype/PixelDebugMenu.uxml` |
| Layout, typography, colors and states | `Assets/Game/UI/Prototype/PixelDebugMenu.uss` |
| F2 page construction and controls | `Assets/Game/DebugMenu/Scripts/ViewModeDebugMenu.Pixel.cs` |
| Shared open state, input gate, catalogue and camera actions | `Assets/Game/DebugMenu/Scripts/ViewModeDebugMenu.cs` |
| Shared document initialization and F1 presentation | `Assets/Game/DebugMenu/Scripts/ViewModeDebugMenu.Toolkit.cs` |
| Panel scaling | `Assets/Game/UI/DebugMenuPanelSettings.asset` |
| Font | `Assets/Game/UI/Fonts/BarlowCondensed-ExtraBold.ttf` |
| Document setup command | `Assets/Game/DebugMenu/Editor/DebugMenuUISetup.cs` |
| Existing interaction tests | `Assets/Game/DebugMenu/Tests/DebugMenuTests.cs` |

The shared controller is `ViewModeDebugMenu`. System ownership and input dependencies are documented in [Architecture](../../../ARCHITECTURE.md#debug-menu-presentation).

## Opening and closing

- F2 toggles this menu. Opening it hides F1; F1 switches to the other menu. Only one presentation is visible at a time.
- Opening releases the cursor and blocks player control through the shared menu gate. The world simulation continues.
- CLOSE is fixed at the bottom of the sidebar. It closes the menu and returns cursor control to the camera. Clicking outside the shell also closes it through the camera's pointer handling.
- Escape releases the cursor; it is not the F2 close shortcut.
- Disabling the controller hides both documents and clears their focused element.
- Reopening F2 retains its selected tab and weapon category. The page is rebuilt to refresh its values, and its scroll position returns to the top. Switching tabs also resets scrolling.

## Text and visual hierarchy

Use text for an action, a setting name, a group label or a value the player needs. The selected sidebar tab already identifies the page. There is no repeated page title, menu title, subtitle, item count, footer instruction or action status message in the F2 panel.

Labels are uppercase. Long names and setting labels wrap where their style allows it; there is no eight-character limit. The font is Barlow Condensed ExtraBold throughout the F2 controls, labels and readouts. The HUD's Doppio One font is separate.

| Element | Font size |
|---|---:|
| Sidebar tab | 24 |
| Standard button, value box, weapon name | 20 |
| Setting label, control action | 18 |
| Section heading | 17, with 2 letter spacing |
| Compact list action, keycap | 16 |

Setting labels are dark gray; weapon names and ordinary values use the near-black outline color. Section headings are muted and separated from their content by a thin rule. Only the selected tab or selected camera mode uses the pressed deep-blue state.

## Shell and scaling

The scene's Pixel Debug Menu UI document uses the shared panel asset. Its settings are Scale With Screen Size, reference resolution 1440 × 900, Match Width Or Height with Match 0.5, sorting order 100. Scaling is continuous with the Game view; dimensions are not constrained to integer pixel multiples.

```text
overlay: full screen, centers the shell
  shell: horizontal layout
    sidebar: tabs at top, CLOSE at bottom
    content
      header: weapon category controls, when on Weapons
      body: vertical ScrollView
```

| Element | Current dimensions and styling |
|---|---|
| Overlay | Near-black #0A0505 at 55% opacity |
| Shell | Width 92%, maximum 980; height 84%, maximum 660; white background |
| Shell frame | 2 border, 6 bottom border, 4 corner radius; clips overflow |
| Sidebar | Width 200; padding 24 vertically and 16 horizontally; #EEF1F3 background; 2 right border |
| Content | Grows into remaining width; minimum width 0; padding 24 |
| Header | Fixed above body; right padding 12, bottom padding 16; bottom margin 8; 2 rule #E3E6E9 |
| Body | Fills remaining height; vertical scrolling; horizontal scroller hidden |

The sidebar and category controls stay in place while the weapon list scrolls. The header is hidden on the other five pages. Content rows use 8 spacing between controls; setting labels have a fixed width of 128 and a 16 gap before the control. Groups use 20 separation. Sidebar tabs have 10 spacing below each button.

The content does not switch to a separate mobile layout. Long weapon names wrap beside fixed-size action buttons. Check narrow Game views for crowding rather than assuming full support at every aspect ratio.

## Button construction and states

Buttons are native UI Toolkit elements styled in USS. Each frame has three non-pickable child bands: top highlight, middle highlight and bottom lip. Its text is a child Label so it can wrap and increase the button height. No button-frame sprites, shader effects or image comparison harness are required by this implementation.

| Component | Current geometry |
|---|---|
| Standard button | Minimum height 38; padding top 3, horizontal 20, bottom 8 |
| Frame | 2 near-black border; 6 corner radius; overflow clipped to frame |
| Top highlight | Height 2, at the top inside the frame |
| Middle highlight | Height 2, positioned 5 above the bottom |
| Bottom lip | Height 5, with 1 darker bottom border |
| Arrow button | Width 52; no horizontal padding |
| ON/OFF toggle | Width 96; no horizontal padding |
| Compact EQUIP / SPAWN | Minimum height 32, minimum width 84; padding top 2, horizontal 14, bottom 7 |

| State | Appearance |
|---|---|
| Normal | Hue palette below; text shadow offset down 2, with no blur |
| Hover | Whole button moves up 1; palette remains the same |
| Active | Whole button moves down 2 |
| Selected | Moves down 2; face #0A97D8, white text, shadow #076FA4; top and middle highlights hidden; lip reduced to 2 |
| Disabled | Opacity 0.4 |
| Focus | Uses the same border color; the stylesheet adds no separate focus ring |

Transitions have zero duration. Frames keep their size instead of shrinking to fit a crowded row. The slightly rounded silhouette and clipped bands are the current appearance.

## Palette

All button outlines use #0A0505. The table lists the normal state; selected buttons use the shared selected treatment above.

| Hue | Face | Top highlight | Middle highlight / text shadow | Lip | Lip bottom | Text |
|---|---|---|---|---|---|---|
| Green | #9AE760 | #C9FA87 | #C8FF88 | #58C453 | #469A48 | #2B842E |
| Blue | #02CCF8 | #2AE3F0 | #24EBFF | #0A97D8 | #076FA4 | #00619B |
| Red | #F65660 | #F17881 | #FF7B82 | #E5343F | #B42C3A | #A81A24 |
| Orange | #FEA319 | #FBC849 | #FFC035 | #EB7720 | #B2522A | #A43300 |
| Gray | #B9C0C6 | #D3D8DC | #DDE2E6 | #8D959C | #6E767D | #353B40 |

Green marks EQUIP and ON. Orange marks SPAWN, PLAY and the flight action. Red marks CLOSE, CLEAR and OFF. Gray marks unselected navigation and camera modes. Blue marks stepper arrows and adjacent value boxes.

## Readouts, rows and scrollbar

- Readouts are Labels, not clickable controls. Their base style is 168 wide, minimum height 38, horizontal padding 8, a 2 outline and 20 type; text stays on one line and uses ellipsis on overflow.
- Readouts inside rows use the blue face, blue text and light blue text shadow of their adjacent arrow buttons, with a 6 radius. The weapon category readout grows to fill the available header width.
- A setting row consists of its fixed label column followed by a toggle, camera choice or a left-arrow / value / right-arrow stepper. Volume values change in ten-percentage-point steps, clamped to 0–100%.
- Weapon rows have 6 vertical padding and a 1 #E3E6E9 separator. The name grows and wraps; compact action buttons align on the right with 8 gaps.
- The vertical scrollbar is 12 wide with an 8 left margin. Its tracker is #EEF1F3, its thumb is #B9C0C6 with a 2 near-black outline, and both have a 6 radius. Arrow buttons are hidden.
- Weapons always reserves its vertical scrollbar, including for short categories, to keep rows aligned with the header. Other pages show it automatically when needed.

## Pages and actions

| Page | Current content and behavior |
|---|---|
| WEAPONS | Fixed category stepper above an alphabetical list. Each row shows the weapon name followed by green EQUIP and orange SPAWN. Actions apply directly to that row's weapon and are disabled if its model is missing. Duplicate names can appear for different models or loadouts. |
| MOBS | MOB stepper, ALIVE value, then SPAWN and CLEAR actions. The alive count refreshes every 500 ms. |
| PLAYER | INFINITE STAMINA setting with a green ON / red OFF toggle. |
| CAMERA | VIEW row with FIXED, THIRD and FREE. The current mode is selected. FREE additionally shows FLIGHT with FLY or PARK, according to whether the free camera is flying. |
| AUDIO | VOLUME group with MASTER and WEAPONS steppers. HITMARKER and PUNCH groups appear when their sound collections exist. Each has ON CONTACT, SOUND selection, PLAY preview and VOLUME; PUNCH also has RANDOM. |
| CONTROLS | MOVEMENT, COMBAT and TOOLS groups, shown as wrapping two-column pairs of keycaps and action labels. |

Weapon categories cycle in both directions: ALL WEAPONS, PISTOLS & DUALS, SMGS, ASSAULT RIFLES, SHOTGUNS, RIFLES & SNIPERS, MACHINE GUNS, LAUNCHERS. The F2 category is independent of F1's browsing controls. The current F2 list has no search, pack filter, selection preview, weapon thumbnail or result message.

Control keycaps have width 96, minimum height 32 and a thicker bottom edge. The displayed mappings are defined by the F2 controller: WASD / SHIFT + WASD / SPACE / CTRL / CAPS / S + DIVE for movement; LMB / RMB / R / Q / F for combat; F5 through F9, K, F1 / F2 and ESC for tools. These labels describe the controls; they are not remapping widgets.

## Inspect and verify

Open `Assets/Game/Scenes/Main.unity`, press Play, then F2. To inspect the shell in UI Builder, use Window > UI Toolkit > UI Builder and open `Assets/Game/UI/Prototype/PixelDebugMenu.uxml`. The controller adds page contents at runtime, so inspect a running Game view for the complete menu.

For document wiring, stop Play mode, select the object containing View Mode Debug Menu and inspect its Pixel Menu Document field. The assigned UIDocument should use the F2 layout and shared panel listed above. Tools > Project > Set Up Debug Menu UI reconnects these references; save the scene after using it.

Manual review:

1. Open F2 from F1, switch through all six tabs and verify only one menu is visible. Check CLOSE, F2 toggle, clicking outside and returning player control.
2. At the Gun Range (F5), cycle weapon categories, scroll the list, EQUIP a row, SPAWN its pickup, close the menu and try it. Check duplicate names and wrapping of long names.
3. Check MOBS selection, SPAWN, CLEAR and the changing ALIVE count. Toggle infinite stamina on PLAYER.
4. Switch all CAMERA modes and try FLY / PARK in FREE. On AUDIO, change volumes, select and preview sounds, and check ON CONTACT and RANDOM toggles.
5. At 1280 × 720 and 1920 × 1080, check the fixed sidebar/category header, scrollable body, aligned setting controls, rounded frames and blue readouts. Inspect hover, pressed, selected and disabled states.
6. Confirm CONTROLS groups and keycaps remain readable while scrolling.

Existing automated checks cover opening F2 in place of F1, the shared control gate, pointer containment, content on all six pages, CLOSE and equipping from a weapon row. Run them through Window > General > Test Runner > PlayMode > Game.DebugMenu.Tests. The broader manual route is in [Test areas](../../../TEST_AREAS.md#debug-menu). Tools > Project > Check Docs validates the paths and component references in this document.
