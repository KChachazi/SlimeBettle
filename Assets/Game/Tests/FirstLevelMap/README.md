# First Level map fixture

`PF_FirstLevelMapData_TEST` binds `MAP_FirstLevel` to a scene so `MapManager` builds the same read-only map snapshot used by `FirstLevel`. The fixture is for inspecting the map and its debug geometry; it is not the playable level.

## Coordinates and image

- The map image and target canvas are 5760 × 5760 pixels. The origin is at the lower-left, with positive Y upward.
- The source coordinate files use a 5670 × 5670 canvas. As requested, all source positions and dimensions are uniformly scaled by `5760 / 5670` (equal to `64 / 63`) to fit the image canvas.
- `designPixelsPerUnityUnit` is 100. A source coordinate `(x, y)` becomes Unity position `(x × 64 / 63 / 100, y × 64 / 63 / 100)`; map bounds are `(0, 0)` through `(57.6, 57.6)`.
- The map image is imported as a Sprite at 100 PPU. Its center is `(28.8, 28.8, 0)`, so the canvas aligns with the map bounds when the prefab root is at the world origin.
- Actor move speeds remain authored in sprite pixels per second and are converted by `MovementSystem` using the actor sprite's PPU.

## Authored map facts

`MAP_FirstLevel.asset` is built from the supplied files under `Desktop/数据`:

- Fifteen polygon areas define walkable road. The source rectangles are treated as road regardless of their labels; their road kind is `Unclassified` because the source does not authoritatively distinguish main and branch road.
- Two polygon areas are swamps. Each has a `0.5` movement multiplier.
- Three enemy spawn points are loaded. `FirstLevelCompositionRoot` currently creates ordinary enemies at all three points using the available `EnemyConfig`.
- One trap map point and its source interaction radius are recorded. It is a map marker; it does not create a live trap or apply an effect.
- `route_escort` contains ten ordered checkpoints. The first checkpoint is used for the initial Slime and escort weapon; the last checkpoint is the route goal.
- No placement anchors, canyon polygons, or a separate goal-area polygon are included because the current source data does not define them. The last Escort waypoint is marked as the route goal.

The current background is `IMG_FirstLevel_AnningTown.png`, a 5760 × 5760 image from the supplied `素材2` folder. It is visual art only and does not define collision or walkability.

## Runtime behavior

- `MapLayoutAuthoring` draws road polygons, swamp polygons, the route, spawn points, and the trap point as Scene View Gizmos for debugging.
- `MapRoot` has no physical `Collider2D` for the map. `StoneDetails` and `WallOcclusion` use trigger colliders marked with `MapObstacleAuthoring`, so their large art outlines are evaluated by `MapManager` without physical collision resolution.
- `MapManager.CanOccupy` checks map bounds, road-area membership, and movement blockers. Authored roads override map-art blockers, while runtime-registered mechanisms remain solid inside roads. Movement requests can opt out of the Road-area requirement while retaining map-bound checks and map-art blockers.
- `MovementSystem` checks the next physics-step position before setting the dynamic `Rigidbody2D` velocity. Slimes opt into 0.5-unit A* grid paths that follow authored roads and avoid runtime-placed blockers; diagonal corner cutting is disallowed and clear segments are simplified. Guards may move off Road to hold their weapon-relative formation and use A* around map-art and runtime blockers. If a configured guard post is blocked at spawn, it moves to the nearest passable grid point and follows with that corrected weapon offset. A 0.2-unit road tolerance bridges small source measurement seams.
- The escort weapon follows `route_escort`. Free Slimes respond to active guidance sources; without one, they choose random destinations within 1.5 Unity units of their current position. Enemies spawn at the three authored points and their random-walk destinations are filtered through map occupancy.
- Placement preview and final mechanism placement both validate against walkable roads and runtime-placed blockers.

## Current limits and verification

- The trap point is not connected to trigger/effect runtime. No status effect is configured for swamp entry; swamp currently changes movement speed only.
- The road outlines are the authored polygons from the supplied data and have not been auto-derived from image pixels.
- Previous manual Unity PlayMode inspection from Bootstrap reached `FirstLevel`; that observation predates the current collision-policy change and has not been repeated.
- Current targeted Unity EditMode verification passed 7/7: guard priority and moving-post follow tests pass; all 15 authored road centers return a valid food-placement preview; placed blockers remain solid inside roads; actors allowed off Road can move across it despite map-art blockers; the Escort weapon advances along its authored route; and all five guard offsets can follow the full route around blockers.
- A PlayMode test attempt did not produce a pass: the existing Bootstrap test could not find `MainMenuLevelEntry`. Runtime visual verification after this collision-policy change remains outstanding.
