#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Spawning;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player;
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
                Assert.AreEqual(1, first.SlimeManager.GetAliveSnapshots().Count);
                Assert.AreEqual(1, first.GuardManager.GetAliveSnapshots().Count);
                Assert.Greater(first.EnemyManager.GetAliveSnapshots().Count, 0);
                Assert.IsTrue(first.MechanismSystem.TryGetPlayerConfigId(
                    MechanismKind.Food, out var foodConfigId));
                var food = first.MechanismSystem.RequestPlace(foodConfigId,
                    first.SlimeManager.GetAliveSnapshots()[0].Position);
                Assert.IsTrue(food.Succeeded, food.Reason);
                yield return new WaitForFixedUpdate();
                yield return null;
                Assert.AreEqual(2, first.SlimeManager.GetAliveSnapshots().Count);

                manager.RequestPause();
                Assert.AreEqual(LevelState.Paused, manager.GetLevelState());
                manager.RequestResume();
                manager.RequestRestartCurrentLevel();
                yield return null;
                yield return null;
                yield return null;
                Assert.AreEqual(LevelState.Running, manager.GetLevelState());
                Assert.AreNotSame(first, manager.GetCurrentContext());
                Assert.AreEqual(1, manager.GetCurrentContext().SlimeManager.GetAliveSnapshots().Count);
            }
            finally
            {
                manager.Dispose();
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
        }

        [UnityTest]
        public IEnumerator FormalFirstLevelEnemiesReceiveWanderAndMove()
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
                EnemyActor enemy = null;
                for (var frame = 0; frame < 120; frame++)
                {
                    if (manager.GetLevelState() == LevelState.Running)
                    {
                        foreach (var candidate in Object.FindObjectsOfType<EnemyActor>())
                            if (candidate.LastDecisionReason.Contains("游走"))
                            {
                                enemy = candidate;
                                break;
                            }
                    }
                    if (enemy != null) break;
                    yield return null;
                }

                Assert.IsNotNull(enemy, "Formal first-level enemies must receive a bounded wander intent.");
                Assert.IsTrue(manager.GetCurrentContext().MovementSystem.TryGetMovementState(
                    enemy.ActorId, out var movement));
                Assert.IsTrue(movement.IsMoving, "A wander intent must submit a movement request.");
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
