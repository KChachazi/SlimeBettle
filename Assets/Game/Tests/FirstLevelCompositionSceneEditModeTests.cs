using NUnit.Framework;
using SlimeBattle.Actors.Spawning;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.World;
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

        [Test]
        public void FormalFirstLevelUsesEscortObjectiveAndAuthoredWeaponRoute()
        {
            var config = AssetDatabase.LoadAssetAtPath<LevelConfig>(
                "Assets/Game/Configs/FirstLevel/CFG_Level_FirstLevel.asset");
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
            }
            finally
            {
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
