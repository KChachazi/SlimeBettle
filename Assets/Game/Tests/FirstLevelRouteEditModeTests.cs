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
                    "Assets/Game/Configs/Level_test/CFG_Slime_MVP.asset");
                var slimePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Prefabs/Actors/PF_Slime_Green.prefab").GetComponent<SlimeActor>();
                var obstacle = AssetDatabase.LoadAssetAtPath<MechanismConfig>(
                    "Assets/Game/Configs/FirstLevelMechanisms/CFG_Obstacle.asset");
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
