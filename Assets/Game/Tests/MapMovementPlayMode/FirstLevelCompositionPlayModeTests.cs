#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Spawning;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlimeBattle.Tests.MapMovement
{
    public sealed class FirstLevelCompositionPlayModeTests
    {
        [UnityTest]
        public IEnumerator SceneStartsAndRestartsWithProductionActors()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Game/Tests/FirstLevelComposition/FirstLevelComposition_TEST.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!scene.isLoaded) yield return null;
            Assert.AreEqual(scene, SceneManager.GetActiveScene());
            LevelManager manager = null;
            foreach (var root in scene.GetRootGameObjects())
                if (manager == null) manager = root.GetComponentInChildren<LevelManager>();
            Assert.IsNotNull(manager);
            var presentation = Object.FindObjectOfType<FirstLevelPresentationRoot>();
            try
            {
                yield return null;
                yield return null;
                Assert.AreEqual(LevelState.Running, manager.GetLevelState());
                Assert.IsNotNull(presentation);
                Assert.IsNotNull(presentation.Placement);
                var first = manager.GetCurrentContext();
                Assert.IsNotNull(first);
                Assert.AreEqual(3, first.SlimeManager.GetAliveSnapshots().Count);
                Assert.AreEqual(1, first.GuardManager.GetAliveSnapshots().Count);
                Assert.Greater(first.EnemyManager.GetAliveSnapshots().Count, 0);
                Assert.IsTrue(first.MechanismSystem.TryGetPlayerConfigId(
                    MechanismKind.Food, out var foodConfigId));
                var food = first.MechanismSystem.RequestPlace(foodConfigId,
                    first.SlimeManager.GetAliveSnapshots()[0].Position);
                Assert.IsTrue(food.Succeeded, food.Reason);
                yield return new WaitForFixedUpdate();
                yield return null;
                Assert.AreEqual(4, first.SlimeManager.GetAliveSnapshots().Count);

                manager.RequestPause();
                Assert.AreEqual(LevelState.Paused, manager.GetLevelState());
                manager.RequestResume();
                manager.RequestRestartCurrentLevel();
                yield return null;
                yield return null;
                yield return null;
                Assert.AreEqual(LevelState.Running, manager.GetLevelState());
                Assert.AreNotSame(first, manager.GetCurrentContext());
                Assert.AreEqual(3, manager.GetCurrentContext().SlimeManager.GetAliveSnapshots().Count);
            }
            finally
            {
                manager.Dispose();
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }

        [UnityTest]
        public IEnumerator FreeSlimesWanderLocallyInsteadOfFollowingTheWeaponRoute()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Game/Tests/FirstLevelComposition/FirstLevelComposition_TEST.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!scene.isLoaded) yield return null;

            LevelManager manager = null;
            foreach (var root in scene.GetRootGameObjects())
                if (manager == null) manager = root.GetComponentInChildren<LevelManager>();
            Assert.IsNotNull(manager);
            try
            {
                for (var frame = 0; frame < 120 && manager.GetLevelState() != LevelState.Running;
                     frame++) yield return null;
                Assert.AreEqual(LevelState.Running, manager.GetLevelState());
                var context = manager.GetCurrentContext();
                var composition = Object.FindObjectOfType<FirstLevelCompositionRoot>();
                Assert.IsNotNull(context);
                Assert.IsNotNull(composition);
                Assert.IsTrue(context.MapManager.TryGetRoute(new MapRouteId("route_escort"),
                    out var route));

                var freeCount = 0;
                foreach (var slime in context.SlimeManager.GetAliveSnapshots())
                    if (!slime.IsCarrying) freeCount++;
                Assert.GreaterOrEqual(freeCount, 2);

                context.EnemyManager.Dispose();
                for (var frame = 0; frame < 60; frame++) yield return null;
                freeCount = 0;
                var localWanderCount = 0;
                foreach (var slime in context.SlimeManager.GetAliveSnapshots())
                    if (!slime.IsCarrying)
                    {
                        freeCount++;
                        SlimeBattle.Actors.Slimes.SlimeActor actor = null;
                        foreach (var candidate in Object.FindObjectsOfType<SlimeBattle.Actors.Slimes.SlimeActor>())
                            if (candidate.ActorId.Equals(slime.ActorId))
                            {
                                actor = candidate;
                                break;
                            }
                        Assert.IsNotNull(actor);
                        Assert.AreEqual("没有其他目标：随机游走", actor.LastDecisionReason);
                        Assert.IsTrue(context.MovementSystem.TryGetMovementState(
                            slime.ActorId, out var movement));
                        Assert.That(Vector2.Distance(slime.Position, movement.Destination),
                            Is.LessThanOrEqualTo(1.51f));
                        Assert.That(Vector2.Distance(movement.Destination,
                            route.Waypoints[1].Position), Is.GreaterThan(1f));
                        if (movement.IsMoving) localWanderCount++;
                    }
                Assert.GreaterOrEqual(freeCount, 2);
                Assert.GreaterOrEqual(localWanderCount, 2);
            }
            finally
            {
                manager?.Dispose();
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }

        [UnityTest]
        public IEnumerator BootstrapMainMenuStartsFormalFirstLevel()
        {
            var operation = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            while (operation != null && !operation.isDone) yield return null;

            MainMenuLevelEntry entry = null;
            for (var frame = 0; frame < 300 && entry == null; frame++)
            {
                if (SceneManager.GetActiveScene().name == "MainMenu")
                    entry = Object.FindObjectOfType<MainMenuLevelEntry>(true);
                if (entry == null) yield return null;
            }
            Assert.IsNotNull(entry);

            var root = Object.FindObjectOfType<GameRoot>();
            Assert.IsNotNull(root);
            var manager = root.GetLevelManager();
            Assert.IsNotNull(manager);
            string loadFailure = null;
            manager.LevelLoadFailed += failure => loadFailure = failure.Reason;
            entry.StartFirstLevel();

            for (var frame = 0; frame < 600; frame++)
            {
                if (manager.GetLevelState() == LevelState.Running &&
                    manager.GetCurrentLevelId().Value == "FirstLevel") break;
                yield return null;
            }

            Assert.AreEqual(LevelState.Running, manager.GetLevelState(), loadFailure);
            Assert.AreEqual("FirstLevel", manager.GetCurrentLevelId().Value);
            Assert.IsNotNull(manager.GetCurrentContext());
            Assert.IsNotNull(Object.FindObjectOfType<FirstLevelCompositionRoot>());
            Assert.AreEqual(1, manager.GetCurrentContext().SlimeManager.GetAliveSnapshots().Count);
            Assert.IsTrue(manager.GetCurrentContext().MechanismSystem.TryGetWeaponSnapshot(
                out var weapon));
            Assert.AreEqual("route_escort", weapon.RouteId.Value);
            var weaponStart = weapon.Position;
            for (var frame = 0; frame < 60; frame++) yield return null;
            Assert.IsTrue(manager.GetCurrentContext().MechanismSystem.TryGetWeaponSnapshot(
                out weapon));
            Assert.AreEqual(FixedRouteMovementState.Moving, weapon.TransportState);
            Assert.That(Vector2.Distance(weaponStart, weapon.Position), Is.GreaterThan(0.05f));
        }

        [UnityTest]
        public IEnumerator FormalFirstLevelEnemiesDoNotPatrol()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Game/Tests/FirstLevelComposition/FirstLevelComposition_TEST.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            while (!scene.isLoaded) yield return null;

            LevelManager manager = null;
            foreach (var root in scene.GetRootGameObjects())
                if (manager == null) manager = root.GetComponentInChildren<LevelManager>();
            Assert.IsNotNull(manager);
            try
            {
                for (var frame = 0; frame < 120; frame++)
                {
                    if (manager.GetLevelState() == LevelState.Running) break;
                    yield return null;
                }
                Assert.AreEqual(LevelState.Running, manager.GetLevelState());
                for (var frame = 0; frame < 30; frame++) yield return null;
                var enemies = Object.FindObjectsOfType<EnemyActor>();
                Assert.That(enemies, Is.Not.Empty);
                foreach (var enemy in enemies)
                    Assert.That(enemy.LastDecisionReason, Does.Not.Contain("游走"),
                        "Enemies without a detected hostile must remain at their spawn points.");
            }
            finally
            {
                manager?.Dispose();
                if (previous.IsValid() && previous.isLoaded)
                    SceneManager.SetActiveScene(previous);
            }
        }
    }
}
#endif
