using System;
using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Data;
using SlimeBattle.Mechanisms;
using SlimeBattle.Slimes;
using SlimeBattle.Status;
using SlimeBattle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimeBattle.Tests
{
    public sealed class FirstLevelRouteEditModeTests
    {
        [Test]
        public void SlimesChooseOnceAtForkAndWaitWhenEveryBranchIsBlocked()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            MapManager map = null;
            StatusManager status = null;
            MechanismSystem mechanisms = null;
            SlimeManager slimes = null;
            try
            {
                var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Tests/FirstLevelMap/PF_FirstLevelMapData_TEST.prefab");
                PrefabUtility.InstantiatePrefab(mapPrefab, scene);
                var parent = new GameObject("Slime parent").transform;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent.gameObject, scene);
                var slimeConfig = AssetDatabase.LoadAssetAtPath<SlimeConfig>(
                    "Assets/Game/Configs/Actors/CFG_Slime_MVP.asset");
                var slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab").GetComponent<SlimeActor>();
                var obstacle = AssetDatabase.LoadAssetAtPath<MechanismConfig>(
                    "Assets/Game/Configs/Mechanisms/PlayerPlaceable/CFG_Obstacle.asset");
                var registry = new ConfigRegistry(new ConfigAsset[] { slimeConfig, obstacle });
                map = new MapManager(scene, null);
                status = new StatusManager(registry, null, _ => null);
                mechanisms = new MechanismSystem(registry, 3, map, status, null, scene);
                slimes = new SlimeManager(registry, null, 20, slimePrefab, parent);
                var fork = map.GetMapPoints(MapPointKind.Fork)[0].Position;
                var routeIds = new[] { new MapRouteId("route_branch_1"),
                    new MapRouteId("route_branch_2"), new MapRouteId("route_branch_3") };
                var source = new FirstLevelRoutePerceptionSource(new EmptyPerception(),
                    slimes, map, mechanisms, routeIds, new MapRouteId("route_main"),
                    new System.Random(1));
                var first = slimes.SpawnSlime(new SlimeSpawnRequest(slimeConfig.ConfigId, fork));
                Assert.IsTrue(first.IsValid);
                Assert.IsTrue(map.TryGetRoute(routeIds[1], out var secondRoute));
                Assert.That(source.GetPerception(first).Destination,
                    Is.EqualTo(secondRoute.Waypoints[1].Position));

                var blockTwo = mechanisms.RequestPlaceAtAnchor(obstacle.ConfigId,
                    new PlacementAnchorId("L2"));
                Assert.IsTrue(blockTwo.Succeeded, blockTwo.Reason);
                Assert.That(source.GetPerception(first).Destination,
                    Is.EqualTo(secondRoute.Waypoints[1].Position),
                    "Already selected Slimes keep their route.");
                mechanisms.Tick(1f);
                var blockOne = mechanisms.RequestPlaceAtAnchor(obstacle.ConfigId,
                    new PlacementAnchorId("L1"));
                mechanisms.Tick(1f);
                var blockThree = mechanisms.RequestPlaceAtAnchor(obstacle.ConfigId,
                    new PlacementAnchorId("L3"));
                Assert.IsTrue(blockOne.Succeeded && blockThree.Succeeded);
                var waiting = slimes.SpawnSlime(new SlimeSpawnRequest(slimeConfig.ConfigId, fork));
                Assert.That(source.GetPerception(waiting).Destination, Is.EqualTo(fork));
                Assert.IsTrue(mechanisms.RequestRemove(blockOne.MechanismId));
                Assert.IsTrue(map.TryGetRoute(routeIds[0], out var firstRoute));
                Assert.That(source.GetPerception(waiting).Destination,
                    Is.EqualTo(firstRoute.Waypoints[1].Position));
            }
            finally
            {
                slimes?.Dispose();
                mechanisms?.Dispose();
                status?.Dispose();
                map?.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void WaitingSlimesStayNearTheirSpawnUntilRouteRelease()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            MapManager map = null;
            StatusManager status = null;
            MechanismSystem mechanisms = null;
            SlimeManager slimes = null;
            try
            {
                var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Tests/FirstLevelMap/PF_FirstLevelMapData_TEST.prefab");
                PrefabUtility.InstantiatePrefab(mapPrefab, scene);
                var parent = new GameObject("Slime parent").transform;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent.gameObject, scene);
                var slimeConfig = AssetDatabase.LoadAssetAtPath<SlimeConfig>(
                    "Assets/Game/Configs/Actors/CFG_Slime_MVP.asset");
                var slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab").GetComponent<SlimeActor>();
                var obstacle = AssetDatabase.LoadAssetAtPath<MechanismConfig>(
                    "Assets/Game/Configs/Mechanisms/PlayerPlaceable/CFG_Obstacle.asset");
                var registry = new ConfigRegistry(new ConfigAsset[] { slimeConfig, obstacle });
                map = new MapManager(scene, null);
                status = new StatusManager(registry, null, _ => null);
                mechanisms = new MechanismSystem(registry, 3, map, status, null, scene);
                slimes = new SlimeManager(registry, null, 20, slimePrefab, parent);
                var fork = map.GetMapPoints(MapPointKind.Fork)[0].Position;
                var routeIds = new[] { new MapRouteId("route_branch_1"),
                    new MapRouteId("route_branch_2"), new MapRouteId("route_branch_3") };
                var source = new FirstLevelRoutePerceptionSource(new EmptyPerception(),
                    slimes, map, mechanisms, routeIds, new MapRouteId("route_main"),
                    new System.Random(2), waitForRouteTrigger: true, waitRadius: 1.5f);
                var slime = slimes.SpawnSlime(new SlimeSpawnRequest(slimeConfig.ConfigId, fork));
                Assert.IsTrue(slime.IsValid);
                var waiting = source.GetPerception(slime);
                Assert.IsFalse(source.RoutesReleased);
                Assert.That(Vector2.Distance(waiting.Destination, fork), Is.LessThanOrEqualTo(1.5f));

                var block = mechanisms.RequestPlaceAtAnchor(obstacle.ConfigId,
                    new PlacementAnchorId("L2"));
                Assert.IsTrue(block.Succeeded, block.Reason);
                Assert.IsFalse(source.RoutesReleased);
                source.GetPerception(slime);
                Assert.IsTrue(mechanisms.RequestRemove(block.MechanismId));
                source.GetPerception(slime);
                Assert.IsTrue(source.RoutesReleased);
                Assert.IsTrue(map.TryGetRoute(routeIds[1], out var secondRoute));
                Assert.That(source.GetPerception(slime).Destination,
                    Is.EqualTo(secondRoute.Waypoints[1].Position));
            }
            finally
            {
                slimes?.Dispose();
                mechanisms?.Dispose();
                status?.Dispose();
                map?.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void FurnaceGuidanceRemainsActiveAfterArrival()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            MapManager map = null;
            StatusManager status = null;
            MechanismSystem mechanisms = null;
            SlimeManager slimes = null;
            try
            {
                var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Tests/FirstLevelMap/PF_FirstLevelMapData_TEST.prefab");
                PrefabUtility.InstantiatePrefab(mapPrefab, scene);
                var parent = new GameObject("Slime parent").transform;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent.gameObject, scene);
                var mechanismParent = new GameObject("Mechanism parent").transform;
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                    mechanismParent.gameObject, scene);
                var slimeConfig = AssetDatabase.LoadAssetAtPath<SlimeConfig>(
                    "Assets/Game/Configs/Actors/CFG_Slime_MVP.asset");
                var slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab").GetComponent<SlimeActor>();
                var obstacle = AssetDatabase.LoadAssetAtPath<MechanismConfig>(
                    "Assets/Game/Configs/Mechanisms/PlayerPlaceable/CFG_Obstacle.asset");
                var furnace = AssetDatabase.LoadAssetAtPath<MechanismConfig>(
                    "Assets/Game/Configs/Mechanisms/PlayerPlaceable/CFG_Furnace.asset");
                var registry = new ConfigRegistry(new ConfigAsset[] { slimeConfig, obstacle, furnace });
                map = new MapManager(scene, null);
                status = new StatusManager(registry, null, _ => null);
                mechanisms = new MechanismSystem(registry, 3, map, status, null, scene);
                Assert.IsTrue(mechanisms.TrySetPlacedParent(mechanismParent));
                slimes = new SlimeManager(registry, null, 20, slimePrefab, parent);
                var fork = map.GetMapPoints(MapPointKind.Fork)[0].Position;
                var routeIds = new[] { new MapRouteId("route_branch_1"),
                    new MapRouteId("route_branch_2"), new MapRouteId("route_branch_3") };
                Assert.IsTrue(map.TryGetRoute(routeIds[1], out var secondRoute));
                var furnacePosition = secondRoute.Waypoints[1].Position;
                var placement = mechanisms.RequestPlace(furnace.ConfigId, furnacePosition);
                Assert.IsTrue(placement.Succeeded, placement.Reason);
                var source = new FirstLevelRoutePerceptionSource(new EmptyPerception(),
                    slimes, map, mechanisms, routeIds, new MapRouteId("route_main"),
                    new System.Random(3));
                var slimeId = slimes.SpawnSlime(new SlimeSpawnRequest(slimeConfig.ConfigId, fork));
                Assert.IsTrue(slimeId.IsValid);
                Assert.AreEqual(furnacePosition, source.GetPerception(slimeId).Destination);

                Assert.IsTrue(slimes.TryGetActor(slimeId, out var actor));
                actor.NotifyMovementResult(furnacePosition);
                Assert.AreEqual(furnacePosition, source.GetPerception(slimeId).Destination,
                    "A live furnace remains the active route guide after the Slime reaches it.");
            }
            finally
            {
                slimes?.Dispose();
                mechanisms?.Dispose();
                status?.Dispose();
                map?.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private sealed class EmptyPerception : IActorPerceptionSource
        {
            public PerceptionSnapshot GetPerception(ActorId actorId) =>
                new PerceptionSnapshot(1, Array.Empty<TargetObservation>());
            public bool TryGetActorSnapshot(ActorId actorId, out ActorSnapshot snapshot)
            {
                snapshot = default;
                return false;
            }
        }
    }
}
