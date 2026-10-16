# Test areas

Everything here is in `Assets/Game/Scenes/Main.unity`. The floors and walls use the grid materials in `Assets/Game/World/Materials/` (shader: `Assets/Game/World/Shaders/WhiteBlockGrid.shader`), so distances are easy to judge.

## Test Area (scene object "Test Area")
Holds the `NavMeshSurface` that mobs walk on (data: `Assets/Game/World/Navigation/MainNavMesh.asset`).
- **Ground:** the large floor. The player starts here.
- **Concave Corner Wall (50m):** two long walls meeting at a corner. Good for wall jumps.
- **Parkour Course:** five numbered sections: stepping stones, balance beam, ascending platforms, an optional wall-jump route, and a dive-and-slide finish.

After moving or adding geometry under Test Area, rebake navigation: select Test Area, then in the Inspector's NavMesh Surface click **Bake**.

## Combat Practice Lane (scene object "Combat Practice Lane")
- Two standing markers (the firing line) and three targets labeled 5 m, 15 m and 30 m. Each target has a body and a head and is destroyed when its health runs out.
- **Shoulder cover:** a block near the firing line for testing shots blocked by cover.
- Targets do not come back during Play mode; stop and start Play mode to reset them.

## Adding a test station
A new system adds its own small station as a separate scene object with a labeled start marker, placed away from the existing areas. It must not change the areas above. If the station is outside Test Area, it does not affect mob navigation.
