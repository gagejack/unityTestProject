# F1 Debug Menu: pixel button style spec

Reference: `docs/spec/UI/DebugMenu/sampleUI.jpg`, 626 × 575 px, JPEG (72 dpi tag, no real DPI). Unity 6000.6.5f1 (read from the open editor).

**Labels.** OBSERVED = measured from the image. INFERRED = assumed; not in the image. JPEG noise is about ±6 per channel, so colors are snapped to the median of flat areas.

**Inputs.** Found: `Assets/Game/UI/GameUI.uxml`, `Assets/Game/UI/DebugMenu.uss`, `Assets/Game/UI/DebugMenuTheme.tss` (imports the default theme only), `Assets/Game/UI/DebugMenuPanelSettings.asset`, `Assets/Game/UI/Fonts/BarlowCondensed-ExtraBold.ttf`. Not examined: `Assets/Game/UI/UIExample.png`, `Assets/Game/UI/background.png`. Missing: any pixel font (assumption below).

**Scope.** The image shows buttons only. Nothing else (panel, list rows, text field, toggle, slider, scrollbar, badge) is specified here; see section 5.

## 0. Text rule (applies to every screen)

Do not add unnecessary or redundant text.

- Text appears only to operate a control or to show a live value the user needs.
- Say a thing once. The selected nav button names the page, so there is no page title, badge, eyebrow, subtitle or section title repeating it.
- Remove: header eyebrow, subtitle, page badge, sidebar note, footer key hints, "click outside to resume", item-count labels, and status lines that restate what is already visible.
- Labels are one word where possible, caps, no articles, punctuation or instructions. At the standard button width, at most 8 characters (CONTINUE fits).
- Use an icon alone only for a glyph in the reference (play, plus, arrows, close, power, save, help, swap, menu, pause). Never an icon and a word that say the same thing. No tooltip that repeats the label.

## 1. Design tokens

All lengths are logical px at scale 1 (1 px = 1 image px = 1 texel). The in-game scale is an integer (section 3).

### 1.1 Color

Per hue: `outline`, `hi` (top highlight, 2 px), `face`, `mid` (highlight line above the lip, 2 px), `lip` (lower band, 6 px), `shade` (lowest lip row), `ink` (label and icon).

| Hue | face | hi | mid | lip | shade | ink |
|---|---|---|---|---|---|---|
| green | #9AE760 | #C9FA87 | #C8FF88 | #58C453 | #469A48 | #2B842E |
| blue | #02CCF8 | #2AE3F0 | #24EBFF | #0A97D8 | #076FA4 | #00619B |
| red | #F65660 | #F17881 | #FF7B82 | #E5343F | #B42C3A | #A81A24 |
| orange | #FEA319 | #FBC849 | #FFC035 | #EB7720 | #B2522A | #A43300 |

All OBSERVED. Hue meaning in the sheet: green = go/confirm, blue = data/navigation, red = leave/destructive, orange = info/settings.

| Token | Value | Basis |
|---|---|---|
| `--outline` | #0A0505 | OBSERVED range #000600 to #1F0000 (tinted near-black); one value INFERRED |
| `--canvas` | #FFFFFF | OBSERVED sheet background (#FCFEFB to #FFFFFF) |
| `--text-shadow` | the hue's `mid` | OBSERVED: 2 px light line under glyphs (blue #30E3FC, red #FF7A80, orange #FFB92A; green weak) |
| `--face-hover` | face lightened about 8% | INFERRED |
| `--face-disabled` | opacity 0.4 | INFERRED (matches the current `.bevel:disabled`) |

```css
:root {
  --outline: #0A0505; --canvas: #FFFFFF;
  --green-face: #9AE760; --green-hi: #C9FA87; --green-mid: #C8FF88; --green-lip: #58C453; --green-shade: #469A48; --green-ink: #2B842E;
  --blue-face:  #02CCF8; --blue-hi:  #2AE3F0; --blue-mid:  #24EBFF; --blue-lip:  #0A97D8; --blue-shade:  #076FA4; --blue-ink:  #00619B;
  --red-face:   #F65660; --red-hi:   #F17881; --red-mid:   #FF7B82; --red-lip:   #E5343F; --red-shade:   #B42C3A; --red-ink:   #A81A24;
  --orange-face:#FEA319; --orange-hi:#FBC849; --orange-mid:#FFC035; --orange-lip:#EB7720; --orange-shade:#B2522A; --orange-ink:#A43300;
}
```

The face, hi, mid, lip and shade colors are baked into the button sprites (section 4). USS uses `--*-ink` and `--*-mid` only.

### 1.2 Type

| Token | Value | Basis |
|---|---|---|
| Cap height | 21 px (21, 21, 21, 21 on P L A Y) | OBSERVED |
| Letter width | 13 px (11 for A) | OBSERVED |
| Letter gap | 5 to 6 px | OBSERVED |
| Stroke | about 4 px | OBSERVED |
| Cell size | about 4.15 px, so glyphs are 3 × 5 cells (PLAY = 4 × 13 + 3 × 5 = 67; measured 63 to 67) | INFERRED from the numbers above |
| Case | upper | OBSERVED |
| Alignment | centered horizontally; vertically centered in the top 44 px (outline to `mid`), so 5 px above the button center | OBSERVED (glyph y 12 to 32 of 54) |

Font: a blocky 3 × 5 pixel font. I cannot identify the exact typeface from a JPEG. INFERRED candidates, all OFL: Silkscreen, Tiny5. Neither is in the project; adding one is a new third-party file under `Assets/ThirdParty/`, so confirm the choice and licence first. Fallback: `Assets/Game/UI/Fonts/BarlowCondensed-ExtraBold.ttf`, already in the project, which will not look pixel-art.

```css
:root { --label-size: 32px; /* tune until cap height measures 21 ± 1 px; depends on the font */ --label-shadow-y: 2px; }
```

### 1.3 Spacing and size (OBSERVED)

| Token | Value |
|---|---|
| `--btn-h` | 54 px |
| `--btn-wide-w` | 177 px |
| `--btn-square-w` | 49 px (50 in the arrow row) |
| `--gap-x` | 15 px (arrow row 15 to 16) |
| `--gap-y` | 16 to 17 px (row pitch 70.5) |
| `--gap-group` | 30 px (between the two columns) |
| Sheet margin | 57 px left and right, 49 px top and bottom |
| Label inset | 14 px each side of the wide button (149 px usable) |

### 1.4 Borders, radii, elevation

| Token | Value | Basis |
|---|---|---|
| `--outline-w` | 2 px (1 px solid plus anti-aliasing in the image) | OBSERVED |
| Corner | stepped: the outline starts 5 px in on the first row, 3 px in on rows 3 to 4, 1 px in from row 5. This is a pixel-art corner, not a smooth radius | OBSERVED |
| Elevation | none: no drop shadow, no glow, no gradient, no blur | OBSERVED |
| Depth | comes only from the `hi` line, the `mid` line and the 6 px `lip` | OBSERVED |

Band rows inside the 54 px height (OBSERVED, from row-wise medians): 0 to 2 outline, 3 to 4 `hi`, 5 to 41 `face`, 42 to 43 `mid`, 44 to 50 `lip` (last row `shade`), 51 to 52 outline.

## 2. Component inventory

One component family: the button.

| Component | Size | Anatomy |
|---|---|---|
| Wide button | 177 × 54 | sprite frame + centered label |
| Square button | 49 × 54 | sprite frame + centered icon |
| Arrow cluster | four square buttons, 15 px gaps | same as square |

Label: ink color, text-shadow `0 2px 0 mid`. Icon: 17 × 17 px cell (11 × 17 for play, 13 × 17 for help and pause), ink color, centered horizontally, 5 px above center (OBSERVED, all 17 icons within ±1 px).

Wide labels in the image: PLAY, NEW GAME, CONTINUE, START (green); CLOSE, EXIT, BACK (red); LOAD, SAVE (blue); HELP, OPTIONS, MENU, PAUSE (orange).
Icons: play, plus, arrow right, play, close, power, arrow left (left column); up, save, then up/down/right/left, help, swap, menu, pause (right column).

| State | Look | Basis |
|---|---|---|
| Normal | as measured | OBSERVED |
| Hover | face and hi sprite lightened about 8% | INFERRED |
| Pressed | face, label and icon move down 6 px, lip collapses to 1 row | INFERRED |
| Disabled | opacity 0.4 | INFERRED |
| Focus | 2 px `--outline` frame 4 px outside the button | INFERRED |
| Selected (nav) | uses the pressed look | INFERRED |

Skeleton:

```xml
<ui:Button class="px-btn blue" text="LOAD" />
<ui:Button class="px-btn px-square green"><ui:VisualElement class="icon icon-play" /></ui:Button>
```

```css
.px-btn { height: 54px; min-width: 98px; padding: 0 14px 10px; border-width: 0; background-color: rgba(0,0,0,0); -unity-slice-left: 8; -unity-slice-right: 8; -unity-slice-top: 6; -unity-slice-bottom: 12; flex-shrink: 0; -unity-text-align: middle-center; font-size: var(--label-size); }
.px-square { width: 49px; min-width: 49px; padding: 0 0 10px; }
.px-btn.green { background-image: url("sprites/btn-green.png"); color: var(--green-ink); text-shadow: 0 2px 0 var(--green-mid); }
```

(The `url()` path is a placeholder; real paths are set when the sprites exist. USS `var()` in `text-shadow` is not verified in 6000.6; if it fails, write the hex value.)

## 3. Layout

PanelSettings assumed: `Assets/Game/UI/DebugMenuPanelSettings.asset` today uses scale with screen size, reference 1440 × 900, match 0.5 (read from the asset). INFERRED change: constant pixel size with an integer scale, so every art pixel is the same size on screen: `scale = max(1, floor(Screen.height / 480))`. Logical canvas: 720p → 1× (1280 × 720), 1080p → 2× (960 × 540), 1440p → 3× (853 × 480), 2160p → 4× (960 × 540). The shell must work down to 640 × 480 logical.

Structure:

```
overlay (full screen, centered)
  shell (background --canvas, padding 24, width 96%, max-width 960, max-height 94%)
    nav column (wide buttons, gap 16, flex-shrink 0)
    content column (flex-grow 1, min-width 0)
      page body (scrolls)
      action row (flex-direction row, buttons flex-grow 1, gap 15)
```

- Buttons never shrink (`flex-shrink: 0`); height is fixed at 54. Only the page body scrolls.
- Wide buttons grow in width; the 9-slice keeps the corners intact. A label longer than 8 characters widens the button (padding 14 on each side); do not shrink the font.
- Square buttons keep 49 × 54.
- Aspect ratios: ultrawide is capped by the shell max-width; tall or narrow screens scroll the page body. Nav stays one column down to 640 logical width.
- The shell color and padding are INFERRED: the image has only a white canvas. The outline gives non-text contrast (section 6).

## 4. UI Toolkit feasibility map

| Effect | Method | Why |
|---|---|---|
| 2 px outline with stepped corners | baked 9-slice sprite | `border-radius` is smooth and anti-aliased, so it cannot make a stepped corner |
| `hi`, `mid` and lip bands | baked into the same sprite | exact, one draw, no extra elements |
| Pixel-sharp scaling | texture import: Point filter, no compression, no mipmaps | otherwise the sprite blurs |
| Hover / pressed variants | extra baked sprites | USS cannot brighten a sprite; tint only multiplies |
| Text shadow | native USS `text-shadow` | offset, no blur needed |
| Pixel font | font file | needs a font; crisp only at the native size multiple, check in Play mode |
| Icons | one white sprite each, native `-unity-background-image-tint-color` | one sprite serves all four hues |
| Disabled | native `opacity` | |
| Glow, shadow, gradient, blur | not needed | the reference has none |
| Shader or render texture | not needed in the UI; used only by the verification harness | |

Sprite spec: 49 × 54, slice left 8, right 8, top 6, bottom 12; one per hue and state. The same sprite serves wide and square buttons.

## 5. Gap analysis against the current F1 menu

Current menu (`Assets/Game/UI/DebugMenu.uss`, built by `Assets/Game/DebugMenu/Scripts/ViewModeDebugMenu.Toolkit.cs`): soft rounded bevel buttons (`.bevel`: 3 px border, 7 px bottom border, 16 px radius, shine strip), bright palette, Barlow Condensed, off-white shell with a 30 px radius, five hues including purple.

| Part | Action |
|---|---|
| Buttons (`.bevel`, `.nav-button`, `.action-row` buttons) | rebuild with sprites; drop the shine strip and `border-radius` |
| Hues | keep green, orange, red; blue replaces cyan; purple has no counterpart, so reassign its three camera buttons to blue |
| Shell | restyle: remove the 30 px radius and 6 px white border; background `--canvas` |
| Header (eyebrow, title, subtitle), page title, badge, sidebar note, footer, count label | remove (text rule) |
| Resume | keep as a green square play button |
| Search field, filter and weapon rows, preview panel, toggle, slider, scrollbar, `.keycap`, `.small-button` | not in the reference. Keep working as is until a second reference is supplied. Do not invent styles for them |
| Panel scale | change as in section 3 |
| Behavior in `ViewModeDebugMenu.Toolkit.cs` (tabs, pages, actions) | keep unchanged |

Order, one checkpoint each: 1) token USS and sprites, font choice; 2) buttons and nav; 3) text removal; 4) shell and panel scale; 5) remaining controls once referenced.

The new menu is built alongside the current one and opens with F2 until it replaces it.

## 6. Verification plan

Harness: a test UXML that reproduces the sheet (14 wide, 14 square, same positions as section 1.3), rendered with a PanelSettings target texture at 626 × 575, scale 1, point filtering, then saved as PNG. Compare against `sampleUI.jpg` after a straight overlay (no resizing).

| Check | Method | Pass |
|---|---|---|
| Whole image | SSIM, gray, 7 × 7 gaussian window | at least 0.90 |
| Each button crop | SSIM | at least 0.85 |
| Face fill | median CIEDE2000 over a 20 × 20 px face patch | at most 5 |
| Bands (`hi`, `mid`, `lip`, `shade`) | median CIEDE2000 per band row | at most 8 |
| Ink | median CIEDE2000 of the darkest 8% of glyph pixels | at most 10 |
| Geometry | button bounding box, label bounding box, icon bounding box | within ±1 px (label ±2 px) |

Thresholds are INFERRED. The reference is a JPEG, so a perfect match is impossible and tighter limits would fail on noise. The tools are not in the project yet: `numpy` and `Pillow` are installed; SSIM needs `scikit-image` (`pip install scikit-image`), which I have not installed.

Contrast (WCAG 2, computed from the OBSERVED colors above):

| Pair | Ratio | Result |
|---|---|---|
| green ink on face | 3.15 | passes 3:1 (large text), fails 4.5:1 |
| blue ink on face | 3.45 | same |
| red ink on face | 2.26 | fails 3:1 |
| orange ink on face | 3.43 | passes 3:1, fails 4.5:1 |
| outline on `--canvas` | 20.3 | passes (this is what separates a button from the background; face on white is only 1.5 to 3.3) |

Labels are 21 px cap height (32 px type), so the 3:1 large-text threshold applies. Red fails as drawn. INFERRED accessible inks, to use only if you accept leaving the reference: red #6E0A14 (3.74), green #1A5E20 (5.24), blue #00426E (5.49), orange #6E2200 (5.53). Decision for you: match the image (red label 2.26) or darken the inks.
