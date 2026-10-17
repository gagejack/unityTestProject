# Game map (flow chart)

`docs/flow/game-map.drawio` is the visual map of the game: which systems exist, who talks to whom, what is placed in the scene, and a template for planning a new feature. It shows structure only. The facts behind each box live on the pages in `docs/systems/` and in [the architecture page](../ARCHITECTURE.md), which this chart follows.

## Open and edit it
1. Go to https://app.diagrams.net in your browser.
2. Choose **Open Existing Diagram** (or File > Open from > Device) and pick `docs/flow/game-map.drawio`.
3. Edit, then **File > Save** (Ctrl+S). In Chrome or Edge this writes straight back to the file; in other browsers it downloads a copy, which you move over the original.
4. Commit it with git like any other file.

## The pages (tabs along the bottom)
- **1 Systems:** the game map. Blue boxes are built, purple is Core (shared contracts), grey is tools. Arrows follow the "Dependencies today" diagram in the architecture page.
- **2 Scene (Main):** what is in `Assets/Game/Scenes/Main.unity` and which scripts sit on each object.
- **3 Plan a feature:** a template for describing something new.

## Plan a feature
1. Right-click the **3 Plan a feature** tab, choose Duplicate, and rename the copy to your feature.
2. Keep the green dashed boxes you need, delete the rest, and copy boxes to add more. Type what each box does inside it.
3. Select a box and press **Ctrl+M** (Edit Data) to fill in `does`, `systems_touched`, `core_contract` and `test`. Leave a field empty if you do not know; Claude will propose it.
4. Draw arrows for what triggers what. Use amber dashed boxes for existing things that must change.
5. Save, then tell Claude: "implement the planned boxes on page <name> in `docs/flow/game-map.drawio`".

Claude turns the page into a proposal in `docs/STATUS.md`, then follows the steps in `AGENTS.md` (contracts, tests, smallest playable slice, docs) and stops for your playtest. When the feature is built, its boxes become blue and solid and move onto page 1 or 2 in the same commit as the code.

## Keeping it true
Do not copy numbers or settings into the chart. Name the box and point to the page or component that owns the fact. If a box no longer matches the project, tell Claude and it will compare the chart with the scene and assemblies and update it.
