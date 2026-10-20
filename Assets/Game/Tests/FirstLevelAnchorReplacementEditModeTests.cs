using NUnit.Framework;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player.Placement;
using SlimeBattle.Status;
using SlimeBattle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimeBattle.Tests
{
    public sealed class FirstLevelAnchorReplacementEditModeTests
    {
        [Test]
        public void AnchorReplacementKeepsOldOnFailureAndPublishesOnlyCommittedChanges()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            MapManager map = null;
            StatusManager status = null;
            MechanismSystem mechanisms = null;
            PlacementSystem placement = null;
            try
            {
                var mapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Tests/FirstLevelMap/PF_FirstLevelMapData_TEST.prefab");
                Assert.IsNotNull(mapPrefab);
                PrefabUtility.InstantiatePrefab(mapPrefab, scene);
                var food = AssetDatabase.LoadAssetAtPath<MechanismConfig>(
                    "Assets/Game/Configs/Mechanisms/PlayerPlaceable/CFG_Food.asset");
                var furnace = AssetDatabase.LoadAssetAtPath<MechanismConfig>(
                    "Assets/Game/Configs/Mechanisms/PlayerPlaceable/CFG_Furnace.asset");
                var obstacle = AssetDatabase.LoadAssetAtPath<MechanismConfig>(
                    "Assets/Game/Configs/Mechanisms/PlayerPlaceable/CFG_Obstacle.asset");
                var registry = new ConfigRegistry(new ConfigAsset[] { food, furnace, obstacle });
                map = new MapManager(scene, null);
                status = new StatusManager(registry, null, _ => null);
                mechanisms = new MechanismSystem(registry, 1, map, status, null, scene);
                var anchorId = new PlacementAnchorId("L1");
                var first = mechanisms.RequestPlaceAtAnchor(obstacle.ConfigId, anchorId);
                Assert.IsTrue(first.Succeeded, first.Reason);
                Assert.That(mechanisms.ActivePlacedMechanismCount, Is.EqualTo(1));
                Assert.That(mechanisms.TryGetSnapshot(first.MechanismId, out var old), Is.True);
                Assert.That(old.AnchorId, Is.EqualTo(anchorId));

                var rejected = mechanisms.RequestPlaceAtAnchor(new ConfigId("missing"), anchorId);
                Assert.IsFalse(rejected.Succeeded);
                Assert.That(mechanisms.TryGetSnapshot(first.MechanismId, out old), Is.True);
                Assert.That(old.State, Is.EqualTo(MechanismState.Available));

                mechanisms.Tick(1f);
                var gridObject = new GameObject("Placement grid", typeof(Grid),
                    typeof(PlacementGridAuthoring));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gridObject, scene);
                var backend = new MechanismPlacementBackend(mechanisms);
                Assert.IsTrue(map.TryGetMapBounds(out var bounds));
                placement = new PlacementSystem(
                    gridObject.GetComponent<PlacementGridAuthoring>(),
                    () => bounds, () => LevelState.Running, backend,
                    PlacementConfirmationMode.Single, map.GetPlacementAnchors, 100f);
                placement.BeginPlacement(PlacementItemId.Obstacle);
                Assert.IsTrue(map.TryGetPlacementAnchor(anchorId, out var anchor));
                placement.PreviewPlacement(anchor.Position);
                Assert.That(placement.GetCurrentPreview().Status,
                    Is.EqualTo(PlacementValidationStatus.AnchorReplaceable));
                var second = placement.ConfirmPlacement();
                Assert.IsTrue(second.Succeeded, second.Reason);
                Assert.That(mechanisms.ActivePlacedMechanismCount, Is.EqualTo(1));
                Assert.That(mechanisms.TryGetSnapshot(first.MechanismId, out old), Is.True);
                Assert.That(old.State, Is.EqualTo(MechanismState.Removed));
                Assert.That(mechanisms.TryGetSnapshot(second.MechanismId, out var current), Is.True);
                Assert.That(current.AnchorId, Is.EqualTo(anchorId));
            }
            finally
            {
                placement?.Dispose();
                mechanisms?.Dispose();
                status?.Dispose();
                map?.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
