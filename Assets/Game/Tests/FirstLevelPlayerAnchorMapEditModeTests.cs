using NUnit.Framework;
using SlimeBattle.Core;
using SlimeBattle.Player;
using SlimeBattle.Player.Placement;
using SlimeBattle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SlimeBattle.Tests
{
    /// <summary>Uses the existing P0 test assembly's Map fixture access; production Player reads only public snapshots.</summary>
    public sealed class FirstLevelPlayerAnchorMapEditModeTests
    {
        [Test] public void ExistingMapPrefabProvidesAllSixAnchorsToPlayerThroughPublicQueries()
        {
            var scene = EditorSceneManager.NewPreviewScene(); MapManager map = null;
            var owner = new GameObject("TEST_ONLY_AnchorConsumer", typeof(Grid), typeof(PlacementGridAuthoring));
            var backend = new SnapshotBackend(); PlacementSystem placement = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Tests/FirstLevelMap/PF_FirstLevelMapData_TEST.prefab");
                Assert.IsNotNull(prefab); PrefabUtility.InstantiatePrefab(prefab, scene);
                map = new MapManager(scene, null);
                Assert.That(map.GetPlacementAnchors().Count, Is.EqualTo(6));
                Assert.That(map.TryGetMapBounds(out var bounds), Is.True);
                var definition = AssetDatabase.LoadAssetAtPath<FirstLevelMapDefinition>("Assets/Game/Configs/Maps/FirstLevel/MAP_FirstLevel.asset");
                var grid = owner.GetComponent<PlacementGridAuthoring>(); grid.Grid.cellSize = new Vector3(1f, 1f, 0f);
                placement = new PlacementSystem(grid, () => bounds, () => LevelState.Running,
                    backend, PlacementConfirmationMode.Single, map.GetPlacementAnchors, definition.DesignPixelsPerUnityUnit);
                foreach (var anchor in map.GetPlacementAnchors())
                {
                    placement.BeginPlacement(PlacementItemId.Obstacle); placement.PreviewPlacement(anchor.Position);
                    var preview = placement.GetCurrentPreview();
                    Assert.That(preview.Anchor.Value.AnchorId, Is.EqualTo(anchor.AnchorId));
                    Assert.That(preview.Status, Is.EqualTo(PlacementValidationStatus.AnchorFree));
                    Assert.That(preview.WorldCenter, Is.EqualTo((Vector3)anchor.Position));
                }
                var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/UI/Prefabs/PF_FirstLevelPlayer.prefab");
                var root = player.GetComponent<FirstLevelPresentationRoot>();
                Assert.That(new SerializedObject(root).FindProperty("anchorMapDefinition").objectReferenceValue, Is.EqualTo(definition));
            }
            finally
            {
                placement?.Dispose(); map?.Dispose(); Object.DestroyImmediate(owner); EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        private sealed class SnapshotBackend : IPlacementBackend
        {
            public bool IsAvailable => true;
            public PlacementResult RequestPlacement(PlacementRequest request) => new PlacementResult(false, default, "TEST_ONLY preview");
            public System.Collections.Generic.IReadOnlyList<PlacementObjectSnapshot> ReadPlacedObjects() => System.Array.Empty<PlacementObjectSnapshot>();
            public PlacementBudgetSnapshot GetBudgetSnapshot() => default;
        }
    }
}
