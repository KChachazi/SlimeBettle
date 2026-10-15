# First Level map fixture

`PF_FirstLevelMapData_TEST` binds `MAP_FirstLevel` to a scene so `MapManager` can build its normal read-only snapshots. Its `Map Art` child renders the supplied first-level map image as a visual background. This remains a reusable test fixture, not a playable or official level scene.

## Coordinate convention

- The design canvas and source image are both 5760 × 5760 pixels, with the design origin at the lower-left and positive Y upward.
- The provisional scale is **100 design pixels per Unity world unit**. The map bounds resolve to `(0, 0)` through `(57.6, 57.6)`.
- A design coordinate `(x, y)` resolves to Unity world position `(x / 100, y / 100)`.
- The source image is imported as a single Sprite at 100 PPU, with an 8192 maximum texture size. Its center is placed at `(28.8, 28.8, 0)`, aligning its canvas with the map bounds when the prefab root is at `(0, 0, 0)`.
- Actor move speeds remain authored in sprite pixels per second and are converted by `MovementSystem`. Keep map art at PPU 100 unless the map definition scale is updated with the art/physics scale.

## Authored facts

- Bounds, start/guard point, fork, three merges, five enemy points, five trap points, two supplies, tutorial points, and the goal cave are stored in `MAP_FirstLevel`.
- `route_branch_1`, `route_branch_2`, and `route_branch_3` terminate at their respective merge points. `route_main` is the shared main-line segment. The DOCX point order is preserved; actors still need a separate route-selection policy.
- The goal cave is a circular `MapAreaKind.Goal` with the documented 400px radius.
- Swamp T4 and cliff T5 remain provisional circular terrain masks from their documented local radii. The image is visual only: no area geometry or physical colliders have been traced from it. The T4 map area is a semantic terrain marker only (`1.0` terrain multiplier); its documented `0.5` speed effect for 3 seconds lives in `CFG_Trap_T4` so a future trap runtime does not apply the same slowdown twice through MapManager and StatusManager.
- ST3 is described as a group event, not a position trigger, so it has no map point.
- L1/L2/L3 reference their blocked branch routes. SL1/SL2/SL3 reference T1/T4/T3. Every anchor accepts Obstacles, allows replacement, and uses the documented 80px snap radius.

## Mechanism and hazard configurations

`Assets/Game/Configs/FirstLevelMechanisms/` contains `CFG_Food`, `CFG_Furnace`, `CFG_Obstacle`, `CFG_Trap_T1` through `CFG_Trap_T5`, and `CFG_Supply`. Point `configId` values match the hazard and supply assets. Add the required assets to the owning LevelManager's `levelConfigs` list when composing a runnable level; this fixture does not edit Bootstrap or Build Settings.

Food attraction is 400px. Furnace attraction remains at the existing 500px default because the latest level DOCX gives no first-level furnace radius. Obstacle stock, durability, placement cooldown, and circle radius are 10, 3, 1s, and 55px. Trap and supply assets preserve the DOCX trigger/effect values as static data; their trigger runtime and ConfigRegistry registration are still pending level composition.

## Use and verification

- Keep the fixture root at the world origin so the visual image and absolute map definition coordinates stay aligned.
- The `Map Art` SpriteRenderer has sorting order `-100` and no Collider2D. It does not define roads, terrain boundaries, walkability, or physical blocking.
- PlayMode capture: FirstLevelMap_TEST_PlayMode.png. The full image is visible and centered in the Game view; the scene uses an orthographic camera at (28.8, 28.8, -10), size 30.
- The image canvas matching the design canvas does not yet verify that every DOCX point, route, terrain contour, or cliff edge is visually aligned. Confirm those against an annotated map before replacing the provisional geometry.

## Runtime map query probe

A one-off Editor probe constructed MapManager against this active scene and read the bound data: bounds were present; route_branch_1, route_branch_2, route_branch_3, and route_main were found with 7/6/9/9 waypoints; all six anchors were found; the swamp area center resolved to (7.49, 30.52). The Console had no errors or warnings, and scene validation reported zero issues. This probe was not saved as a project script and is not an automated test.

The scene does not instantiate a full LevelContext, actors, traps, or StatusManager effects. No Unity Test Runner or complete gameplay test was run.
