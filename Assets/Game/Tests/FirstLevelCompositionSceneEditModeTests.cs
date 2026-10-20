using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Actors.Guards;
using SlimeBattle.Actors.Spawning;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player.Placement;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using SlimeBattle.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Tests
{
    public sealed class FirstLevelCompositionSceneEditModeTests
    {
        [Test]
        public void FormalGuardConfigDefinesFiveEvenPostsAtRadiusTwo()
        {
            var guard = AssetDatabase.LoadAssetAtPath<GuardConfig>(
                "Assets/Game/Configs/Actors/CFG_Guard.asset");
            Assert.IsNotNull(guard);
            Assert.That(guard.InitialCount, Is.EqualTo(5));
            Assert.That(guard.SpawnOffsets.Count, Is.EqualTo(5));
            for (var i = 0; i < guard.SpawnOffsets.Count; i++)
            {
                Assert.That(guard.SpawnOffsets[i].magnitude, Is.EqualTo(2f).Within(.003f));
                var next = guard.SpawnOffsets[(i + 1) % guard.SpawnOffsets.Count];
                Assert.That(Vector2.Distance(guard.SpawnOffsets[i], next),
                    Is.EqualTo(2.351f).Within(.004f));
            }
        }

        [Test]
        public void FormalMapBlocksLargeStoneAndWallShapesButLeavesGroundArtWalkable()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Maps/PF_FirstLevelMapData.prefab");
            Assert.IsNotNull(prefab);
            var scene = EditorSceneManager.NewPreviewScene();
            MapManager map = null;
            try
            {
                var mapRoot = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
                Assert.IsNotNull(mapRoot);
                var ground = mapRoot.transform.Find("Map Art");
                var stones = mapRoot.transform.Find("StoneDetails");
                var walls = mapRoot.transform.Find("WallOcclusion");
                Assert.IsNotNull(ground);
                Assert.IsNotNull(stones);
                Assert.IsNotNull(walls);
                Assert.IsNull(ground.GetComponent<Collider2D>(),
                    "The ground background should remain walkable.");
                var stoneCollider = stones.GetComponent<PolygonCollider2D>();
                var wallCollider = walls.GetComponent<PolygonCollider2D>();
                Assert.IsNotNull(stones.GetComponent<MapObstacleAuthoring>());
                Assert.IsNotNull(walls.GetComponent<MapObstacleAuthoring>());
                Assert.IsNotNull(stoneCollider);
                Assert.IsNotNull(wallCollider);
                Assert.IsTrue(stoneCollider.isTrigger,
                    "Map art blockers must not physically stop actors inside authored roads.");
                Assert.IsTrue(wallCollider.isTrigger,
                    "Map art blockers must be checked through map occupancy instead of physics resolution.");
                Assert.That(stoneCollider.pathCount, Is.GreaterThan(0));
                Assert.That(wallCollider.pathCount, Is.GreaterThan(0));

                map = new MapManager(scene, null);
                Physics2D.SyncTransforms();
                var query = new OccupancyQuery(requireWithinBounds: false,
                    checkStaticBlockers: true, ignoreRoadArea: true);
                Assert.IsTrue(TryFindInteriorPoint(stoneCollider, out var stonePoint));
                Assert.IsFalse(map.CanOccupy(stonePoint, query),
                    "Large stone shapes must remain movement blockers outside authored roads.");
                Assert.IsTrue(TryFindInteriorPoint(wallCollider, out var wallPoint));
                Assert.IsFalse(map.CanOccupy(wallPoint, query),
                    "Wall and tree shapes must remain movement blockers outside authored roads.");
            }
            finally
            {
                map?.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void FormalRoadAreasRemainWalkableAcrossTheMapAndPlacedBlockersStillBlock()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Maps/PF_FirstLevelMapData.prefab");
            Assert.IsNotNull(prefab);
            var scene = EditorSceneManager.NewPreviewScene();
            MapManager map = null;
            GameObject placedBlocker = null;
            GameObject placementOwner = null;
            PlacementSystem placement = null;
            try
            {
                Assert.IsNotNull(PrefabUtility.InstantiatePrefab(prefab, scene));
                map = new MapManager(scene, null);
                Physics2D.SyncTransforms();
                var roadCenters = new[]
                {
                    new Vector2(26.509206f, 52.805080f),
                    new Vector2(37.521270f, 48.182858f),
                    new Vector2(39.070476f, 28.911746f),
                    new Vector2(53.592381f, 36.891430f),
                    new Vector2(49.092064f, 14.125714f),
                    new Vector2(3.301587f, 28.596825f),
                    new Vector2(9.528889f, 37.104762f),
                    new Vector2(18.585397f, 35.408253f),
                    new Vector2(21.389206f, 28.667937f),
                    new Vector2(28.744127f, 24.096508f),
                    new Vector2(16.040635f, 19.205079f),
                    new Vector2(31.095873f, 5.851429f),
                    new Vector2(23.05f, 4.7f),
                    new Vector2(38.5f, 10.1f),
                    new Vector2(38.5f, 5f)
                };
                var query = new OccupancyQuery(.2f, checkStaticBlockers: true,
                    roadTolerance: .2f);
                var guardQuery = new OccupancyQuery(.2f, checkStaticBlockers: true,
                    roadTolerance: .2f, ignoreRoadArea: true);
                foreach (var point in roadCenters)
                {
                    Assert.IsTrue(map.CanOccupy(point, query),
                        $"Configured road point {point} should stay walkable and placeable.");
                    Assert.IsTrue(map.CanOccupy(point, guardQuery),
                        $"Actors allowed off-road must still use the road's map-art clearance at {point}.");
                }

                placementOwner = new GameObject("TEST_ONLY_RoadPlacement");
                placementOwner.AddComponent<Grid>();
                var placementGrid = placementOwner.AddComponent<PlacementGridAuthoring>();
                placement = new PlacementSystem(placementGrid,
                    () => map.TryGetMapBounds(out var bounds) ? bounds : (MapBounds?)null,
                    () => LevelState.Running, new PreviewBackend(),
                    PlacementConfirmationMode.Single,
                    canOccupy: point => map.CanOccupy(point,
                        new OccupancyQuery(checkStaticBlockers: true)));
                foreach (var point in roadCenters)
                {
                    placement.BeginPlacement(PlacementItemId.Food);
                    var preview = placement.PreviewPlacement(point).Preview;
                    Assert.AreEqual(PlacementValidationStatus.Free, preview.Status,
                        $"Food placement should be valid at road point {point}: {preview.Message}");
                }

                placedBlocker = new GameObject("TEST_ONLY_PlacedObstacle");
                SceneManager.MoveGameObjectToScene(placedBlocker, scene);
                placedBlocker.transform.position = roadCenters[5];
                var collider = placedBlocker.AddComponent<BoxCollider2D>();
                Assert.IsTrue(map.RegisterMovementBlocker(collider));
                Assert.IsFalse(map.CanOccupy(roadCenters[5], query),
                    "A runtime placed blocker must remain solid inside a road area.");
            }
            finally
            {
                placement?.Dispose();
                map?.Dispose();
                if (placedBlocker != null) Object.DestroyImmediate(placedBlocker);
                if (placementOwner != null) Object.DestroyImmediate(placementOwner);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void ActorAllowedOffRoadCanMoveAlongAuthoredRoadDespiteMapArtBlockers()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Maps/PF_FirstLevelMapData.prefab");
            Assert.IsNotNull(prefab);
            var scene = EditorSceneManager.NewPreviewScene();
            MapManager map = null;
            MovementSystem movement = null;
            GameObject actorObject = null;
            try
            {
                Assert.IsNotNull(PrefabUtility.InstantiatePrefab(prefab, scene));
                map = new MapManager(scene, null);
                movement = new MovementSystem(map, null);
                actorObject = new GameObject("TEST_ONLY_OffRoadAllowedActor");
                SceneManager.MoveGameObjectToScene(actorObject, scene);
                actorObject.transform.position = new Vector2(3.301587f, 28.596825f);
                var body = actorObject.AddComponent<Rigidbody2D>();
                actorObject.AddComponent<CircleCollider2D>();
                var actorId = ActorId.NewId();
                Assert.IsTrue(movement.RegisterActor(actorId, body, 200f, 100f,
                    _ => { }, _ => true, requireRoadArea: false));
                Assert.IsTrue(movement.SubmitMovement(new MovementRequest(actorId,
                    new Vector2(4.301587f, 28.596825f))).IsValid);

                movement.FixedTick(.02f);

                Assert.IsTrue(movement.TryGetMovementState(actorId, out var state));
                Assert.AreEqual(MovementResult.InProgress, state.Result);
                Assert.Greater(state.Velocity.sqrMagnitude, 0f,
                    "A road-optional guard must not be stopped by decorative map colliders on Road.");
            }
            finally
            {
                movement?.Dispose();
                map?.Dispose();
                if (actorObject != null) Object.DestroyImmediate(actorObject);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void GuardFormationCanFollowTheFullEscortRouteAroundMapBlockers()
        {
            var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Maps/PF_FirstLevelMapData.prefab");
            var guardConfig = AssetDatabase.LoadAssetAtPath<GuardConfig>(
                "Assets/Game/Configs/Actors/CFG_Guard.asset");
            var guardPrefab = AssetDatabase.LoadAssetAtPath<GuardActor>(
                "Assets/Game/Prefabs/Actors/PF_Guard_FirstLevel.prefab");
            Assert.IsNotNull(mapPrefab);
            Assert.IsNotNull(guardConfig);
            Assert.IsNotNull(guardPrefab);
            var scene = EditorSceneManager.NewPreviewScene();
            MapManager map = null;
            try
            {
                Assert.IsNotNull(PrefabUtility.InstantiatePrefab(mapPrefab, scene));
                map = new MapManager(scene, null);
                Assert.IsTrue(map.TryGetMapBounds(out var bounds));
                Assert.IsTrue(map.TryGetRoute(new MapRouteId("route_escort"), out var route));

                var circle = guardPrefab.GetComponent<CircleCollider2D>();
                Assert.IsNotNull(circle);
                var scale = guardPrefab.transform.lossyScale;
                var radius = circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
                var occupancy = new OccupancyQuery(radius, checkStaticBlockers: true,
                    roadTolerance: .2f, ignoreRoadArea: true);
                System.Func<Vector2, bool> canOccupy =
                    position => map.CanOccupy(position, occupancy);
                var routeStart = route.Waypoints[0].Position;

                foreach (var offset in guardConfig.SpawnOffsets)
                {
                    var preferredSpawn = routeStart + offset;
                    var spawn = preferredSpawn;
                    if (!canOccupy(spawn))
                        Assert.IsTrue(AStarGridPathfinder.TryFindNearestPassablePoint(
                            preferredSpawn, bounds, canOccupy, out spawn),
                            $"Guard spawn near {preferredSpawn} should find a nearby free point.");
                    Assert.IsTrue(canOccupy(spawn));
                    var escortOffset = spawn - routeStart;
                    Assert.That(Vector2.Distance(escortOffset, offset),
                        Is.LessThanOrEqualTo(.5f),
                        "Only blocked initial posts should need a small clearance adjustment.");

                    var current = spawn;
                    for (var segment = 0; segment < route.Waypoints.Count - 1; segment++)
                    {
                        var start = route.Waypoints[segment].Position;
                        var end = route.Waypoints[segment + 1].Position;
                        var steps = Mathf.CeilToInt(Vector2.Distance(start, end) / .5f);
                        for (var step = 1; step <= steps; step++)
                        {
                            var weaponPosition = Vector2.Lerp(start, end,
                                step / (float)steps);
                            var target = weaponPosition + escortOffset;
                            Assert.IsTrue(AStarGridPathfinder.TryFindPath(current, target,
                                bounds, canOccupy, out var waypoints),
                                $"Guard post {escortOffset} cannot route to {target}.");
                            if (waypoints.Count > 0) current = waypoints[waypoints.Count - 1];
                        }
                    }
                }
            }
            finally
            {
                map?.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void EscortWeaponMovesAlongFormalRoadDespiteMapArtBlockers()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/Maps/PF_FirstLevelMapData.prefab");
            var config = AssetDatabase.LoadAssetAtPath<WeaponConfig>(
                "Assets/Game/Configs/Levels/LevelTest/CFG_EscortWeapon_Test.asset");
            Assert.IsNotNull(prefab);
            Assert.IsNotNull(config);
            var scene = EditorSceneManager.NewPreviewScene();
            MapManager map = null;
            EscortWeapon weapon = null;
            try
            {
                Assert.IsNotNull(PrefabUtility.InstantiatePrefab(prefab, scene));
                map = new MapManager(scene, null);
                Assert.IsTrue(map.TryGetRoute(new MapRouteId("route_escort"), out var route));

                var body = new GameObject("TEST_ONLY_EscortWeapon");
                SceneManager.MoveGameObjectToScene(body, scene);
                body.transform.position = route.Waypoints[0].Position;
                weapon = new EscortWeapon(MechanismId.NewId(), config, route,
                    body.transform, map);
                Assert.IsTrue(weapon.AddCarrier(ActorId.NewId()));
                var start = weapon.GetSnapshot().Position;
                for (var frame = 0; frame < 120; frame++) weapon.Tick(.02f);
                Assert.That(Vector2.Distance(start, weapon.GetSnapshot().Position),
                    Is.GreaterThan(.05f),
                    "The escort weapon should advance through its authored road despite map-art outlines.");
            }
            finally
            {
                weapon?.Dispose();
                map?.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private sealed class PreviewBackend : IPlacementBackend
        {
            public bool IsAvailable => true;
            public PlacementResult RequestPlacement(PlacementRequest request) =>
                new PlacementResult(false, default, "Preview only.");
            public System.Collections.Generic.IReadOnlyList<PlacementObjectSnapshot> ReadPlacedObjects() =>
                System.Array.Empty<PlacementObjectSnapshot>();
            public PlacementBudgetSnapshot GetBudgetSnapshot() =>
                new PlacementBudgetSnapshot(0, 20, 60, 3, 5);
        }

        [Test]
        public void SceneBindsProductionContextMapActorsAndCaveRules()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(
                "Assets/Game/Tests/FirstLevelComposition/FirstLevelComposition_TEST.unity",
                OpenSceneMode.Additive);
            LevelManager manager = null;
            try
            {
                Assert.IsTrue(SceneManager.SetActiveScene(scene));
                foreach (var root in scene.GetRootGameObjects())
                    if (manager == null) manager = root.GetComponentInChildren<LevelManager>();
                Assert.IsNotNull(manager);
                Assert.IsNotNull(manager.GetComponentInParent<FirstLevelCompositionRoot>());
                var guard = AssetDatabase.LoadAssetAtPath<GuardConfig>(
                    "Assets/Game/Tests/FirstLevelComposition/CFG_Guard_FirstLevelComposition_TEST.asset");
                Assert.IsNotNull(guard);
                Assert.That(guard.SpawnOffsets.Count, Is.EqualTo(guard.InitialCount),
                    $"Guard offsets={guard.SpawnOffsets.Count}, count={guard.InitialCount}");
                manager.Initialize();
                string loadFailure = null;
                manager.LevelLoadFailed += failure => loadFailure = failure.Reason;
                Assert.IsTrue(manager.StartCurrentSceneLevel(), loadFailure);
                var context = manager.GetCurrentContext();
                Assert.IsNotNull(context);
                Assert.That(context.MapManager.GetPlacementAnchors().Count, Is.EqualTo(6));
                var guardPoint = context.MapManager.GetMapPoints(MapPointKind.GuardSpawn)[0].Position;
                Assert.That(guardPoint.x, Is.EqualTo(6.91f).Within(0.001f));
                Assert.That(guardPoint.y, Is.EqualTo(8.06f).Within(0.001f));
                Assert.That(context.GuardManager.GetAliveSnapshots().Count, Is.EqualTo(1));
                Assert.That(context.SlimeManager.MaxAliveCount, Is.EqualTo(20));
                Assert.That(context.MechanismSystem.MaximumActivePlacedMechanisms,
                    Is.EqualTo(20));
                Assert.That(context.ResultRules.RequiredCount, Is.EqualTo(6));
            }
            finally
            {
                manager?.Dispose();
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void FormalFirstLevelSceneUsesPersistentManagerAndBuildEntry()
        {
            var formalPath = "Assets/Game/Scenes/FirstLevel.unity";
            var buildEntry = System.Array.Find(EditorBuildSettings.scenes,
                scene => scene.path == formalPath);
            Assert.IsNotNull(buildEntry);
            Assert.IsTrue(buildEntry.enabled);

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(formalPath, OpenSceneMode.Additive);
            try
            {
                Assert.AreEqual("FirstLevel", scene.name);
                Assert.IsTrue(SceneManager.SetActiveScene(scene));
                LevelManager sceneManager = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    sceneManager = root.GetComponentInChildren<LevelManager>(true);
                    if (sceneManager != null) break;
                }
                Assert.IsNull(sceneManager);

                var composition = Object.FindObjectOfType<FirstLevelCompositionRoot>();
                Assert.IsNotNull(composition);
                var serialized = new SerializedObject(composition);
                Assert.IsNull(serialized.FindProperty("levelManager").objectReferenceValue);
                var mapRoot = serialized.FindProperty("mapRoot").objectReferenceValue as Transform;
                Assert.IsNotNull(mapRoot);
                var mapPrefab = PrefabUtility.GetCorrespondingObjectFromSource(mapRoot.gameObject);
                Assert.AreEqual("Assets/Game/Prefabs/Maps/PF_FirstLevelMapData.prefab",
                    AssetDatabase.GetAssetPath(mapPrefab));
                Assert.IsFalse(serialized.FindProperty("startCurrentSceneOnPlay").boolValue);
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool TryFindInteriorPoint(Collider2D collider, out Vector2 point)
        {
            var bounds = collider.bounds;
            const float step = 0.2f;
            for (var x = bounds.min.x; x <= bounds.max.x; x += step)
                for (var y = bounds.min.y; y <= bounds.max.y; y += step)
                {
                    var candidate = new Vector2(x, y);
                    if (!collider.OverlapPoint(candidate)) continue;
                    point = candidate;
                    return true;
                }
            point = default;
            return false;
        }

        [Test]
        public void FormalFirstLevelUsesEscortObjectiveAndAuthoredWeaponRoute()
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(
                "Assets/Game/Configs/Levels/FirstLevel/CFG_Level_FirstLevel.asset");
            Assert.IsNotNull(config);
            Assert.That(config.ObjectiveKind, Is.EqualTo(LevelObjectiveKind.EscortWeapon));
            Assert.That(config.MaximumActivePlacedMechanisms, Is.EqualTo(20));

            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/FirstLevel.unity", OpenSceneMode.Additive);
            try
            {
                Assert.IsTrue(SceneManager.SetActiveScene(scene));
                var weapon = Object.FindObjectOfType<EscortWeaponAuthoring>();
                Assert.IsNotNull(weapon);
                Assert.That(weapon.WeaponConfigId.Value, Is.EqualTo("mvp_escort_weapon"));
                Assert.That(weapon.RouteId.Value, Is.EqualTo("route_escort"));
                Assert.IsTrue(weapon.HasValidCarrierAnchors);
                var anchorNames = new[]
                {
                    "CarrierAnchor_Center", "CarrierAnchor_BottomRight",
                    "CarrierAnchor_TopRight", "CarrierAnchor_BottomLeft"
                };
                for (var i = 0; i < anchorNames.Length; i++)
                {
                    Assert.IsTrue(weapon.TryGetCarrierAnchor(i, out var anchor));
                    Assert.AreEqual(anchorNames[i], anchor.name);
                }
                var renderer = weapon.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(renderer);
                Assert.AreEqual("Assets/Game/Art/Sprites/SPR_EscortWoodCrate.png",
                    AssetDatabase.GetAssetPath(renderer.sprite));
                Assert.That(renderer.color.a, Is.EqualTo(0.82f).Within(0.001f));
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
