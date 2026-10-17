using System;
using System.Collections.Generic;
using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Data;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player.Placement;
using SlimeBattle.Status;
using SlimeBattle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Tests
{
    public sealed class FirstLevelMechanismPlacementEditModeTests
    {
        private readonly List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        private Scene scene;
        private MapManager map;
        private StatusManager status;
        private MechanismSystem mechanisms;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            var boundsObject = new GameObject("bounds");
            SceneManager.MoveGameObjectToScene(boundsObject, scene);
            var bounds = boundsObject.AddComponent<MapBoundsAuthoring>();
            Set(bounds, "localSize", new Vector2(20f, 20f));
        }

        [TearDown]
        public void TearDown()
        {
            mechanisms?.Dispose();
            status?.Dispose();
            map?.Dispose();
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
            foreach (var asset in assets)
                if (asset != null) UnityEngine.Object.DestroyImmediate(asset);
            assets.Clear();
        }

        [Test]
        public void PlacementBackendCreatesConfiguredMechanismAndKeepsOnlyTotalLimit()
        {
            var food = Config("food", MechanismKind.Food);
            var furnace = Config("furnace", MechanismKind.Furnace);
            var obstacle = Config("obstacle", MechanismKind.Obstacle);
            var configs = new ConfigRegistry(new ConfigAsset[]
                { food, furnace, obstacle });
            Assert.IsTrue(configs.ValidateAll().IsValid);

            var blocker = new GameObject("static blocker");
            SceneManager.MoveGameObjectToScene(blocker, scene);
            blocker.transform.position = new Vector2(1f, 0f);
            blocker.AddComponent<BoxCollider2D>().size = Vector2.one;

            map = new MapManager(scene, null);
            status = new StatusManager(configs, null, _ => null);
            mechanisms = new MechanismSystem(configs, 1, map, status, null, scene);
            var backend = new MechanismPlacementBackend(mechanisms);
            Assert.IsTrue(backend.IsAvailable);
            var first = backend.RequestPlacement(new PlacementRequest(
                PlacementItemId.Food, Vector3Int.zero, new Vector3(1f, 0f)));
            Assert.IsTrue(first.Succeeded, first.Reason);
            Assert.AreEqual(1, backend.GetBudgetSnapshot().ActiveCount);
            Assert.AreEqual(1, backend.ReadPlacedObjects().Count);
            var rejected = backend.RequestPlacement(new PlacementRequest(
                PlacementItemId.Furnace, Vector3Int.right, new Vector3(2f, 0f)));
            Assert.IsFalse(rejected.Succeeded);
            StringAssert.Contains("limit", rejected.Reason);
            Assert.IsTrue(mechanisms.RequestRemove(first.MechanismId));
            Assert.IsTrue(backend.RequestPlacement(new PlacementRequest(
                PlacementItemId.Obstacle, Vector3Int.right, new Vector3(2f, 0f))).Succeeded);
        }

        [Test]
        public void FormalMechanismPrefabsHaveWorldVisualHierarchy()
        {
            var paths = new[]
            {
                "Assets/Game/Prefabs/Mechanisms/PF_Food.prefab",
                "Assets/Game/Prefabs/Mechanisms/PF_Furnace.prefab",
                "Assets/Game/Prefabs/Mechanisms/PF_Obstacle.prefab"
            };

            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path + " must be importable.");
                Assert.IsNotNull(prefab.GetComponent<MechanismAuthoring>(),
                    path + " must keep MechanismAuthoring on the prefab root.");

                var visualRoot = prefab.transform.Find("VisualRoot");
                Assert.IsNotNull(visualRoot, path + " must have a VisualRoot child.");
                Assert.AreEqual(Vector3.zero, visualRoot.localPosition,
                    path + " VisualRoot must remain at the mechanism world position.");

                var renderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.IsNotEmpty(renderers, path + " must contain at least one world visual renderer.");
                foreach (var renderer in renderers)
                {
                    Assert.IsNotNull(renderer.sprite, path + " has a renderer without a sprite.");
                    Assert.AreEqual("Default", renderer.sortingLayerName,
                        path + " visuals must use the shared Default sorting layer.");
                    Assert.AreEqual(1, renderer.sortingOrder,
                        path + " visuals must render above the map and below actors/weapon.");
                }
            }
        }

        private MechanismConfig Config(string id, MechanismKind kind)
        {
            var config = ScriptableObject.CreateInstance<MechanismConfig>();
            assets.Add(config);
            config.SetConfigId(id);
            config.Configure(kind, true, kind == MechanismKind.Obstacle ? 0f : 500f,
                0f, kind == MechanismKind.Food ? 1 : 0, 0f, 0f);
            var prototype = new GameObject(id + " prototype");
            assets.Add(prototype);
            var authoring = prototype.AddComponent<MechanismAuthoring>();
            Set(authoring, "configId", id);
            if (kind == MechanismKind.Obstacle)
                prototype.AddComponent<BoxCollider2D>();
            else
                prototype.AddComponent<CircleCollider2D>().isTrigger = true;
            config.SetPlacementPrefab(authoring);
            return config;
        }

        private static void Set(UnityEngine.Object target, string property, object value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(property);
            if (value is Vector2 vector) field.vector2Value = vector;
            else field.stringValue = (string)value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
