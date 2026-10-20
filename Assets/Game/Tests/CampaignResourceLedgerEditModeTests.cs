using System.Collections.Generic;
using NUnit.Framework;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Mechanisms;
using SlimeBattle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Tests
{
    public sealed class CampaignResourceLedgerEditModeTests
    {
        private CampaignResourceConfig config;
        private CampaignResourceLedger resources;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<CampaignResourceConfig>();
            config.ConfigureInitialResources(8, 20, 10, 30, 3, 10, 5, 15);
            config.SetLevelRewards(new List<CampaignResourceConfig.LevelRewardEntry>
            {
                new CampaignResourceConfig.LevelRewardEntry("FirstLevel", 6, 10, 3, 5),
                new CampaignResourceConfig.LevelRewardEntry("SecondLevel", 6, 10, 5, 5),
                new CampaignResourceConfig.LevelRewardEntry("ThirdLevel", 12, 15, 5, 5)
            });
            resources = new CampaignResourceLedger(config);
            resources.StartNewCampaign();
        }

        [TearDown]
        public void TearDown()
        {
            if (config != null) Object.DestroyImmediate(config);
        }

        [Test]
        public void RestartRestoresEntryStocksAndVictoryAwardsLivingSlimesOnce()
        {
            var firstLevel = new LevelId("FirstLevel");
            resources.BeginLevel(firstLevel, false);

            Assert.IsTrue(resources.TrySpendMechanism(MechanismKind.Food));
            Assert.IsTrue(resources.TrySpendMechanism(MechanismKind.Food));
            Assert.IsTrue(resources.TrySpendMechanism(MechanismKind.Food));
            Assert.IsTrue(resources.TrySpendMechanism(MechanismKind.Furnace));
            Assert.IsTrue(resources.TrySpendMechanism(MechanismKind.Obstacle));
            Assert.AreEqual(7, resources.FoodCount);

            resources.AwardLevel(firstLevel, 5);
            resources.AwardLevel(firstLevel, 5);
            Assert.AreEqual(11, resources.SlimeCount);
            Assert.AreEqual(17, resources.FoodCount);
            Assert.AreEqual(5, resources.FurnaceCount);
            Assert.AreEqual(9, resources.ObstacleCount);

            resources.BeginLevel(firstLevel, true);
            Assert.AreEqual(8, resources.SlimeCount);
            Assert.AreEqual(10, resources.FoodCount);
            Assert.AreEqual(3, resources.FurnaceCount);
            Assert.AreEqual(5, resources.ObstacleCount);

            resources.AwardLevel(firstLevel, 7);
            Assert.AreEqual(13, resources.SlimeCount);
            Assert.AreEqual(20, resources.FoodCount);
            Assert.AreEqual(6, resources.FurnaceCount);
            Assert.AreEqual(10, resources.ObstacleCount);
        }

        [Test]
        public void RewardCountsClampToConfiguredCapsAndNewCampaignResetsStocks()
        {
            config.ConfigureInitialResources(8, 20, 29, 30, 9, 10, 14, 15);
            resources = new CampaignResourceLedger(config);
            resources.StartNewCampaign();
            var firstLevel = new LevelId("FirstLevel");
            resources.BeginLevel(firstLevel, false);
            resources.AwardLevel(firstLevel, 19);

            Assert.AreEqual(20, resources.SlimeCount);
            Assert.AreEqual(30, resources.FoodCount);
            Assert.AreEqual(10, resources.FurnaceCount);
            Assert.AreEqual(15, resources.ObstacleCount);

            resources.StartNewCampaign();
            Assert.AreEqual(8, resources.SlimeCount);
            Assert.AreEqual(29, resources.FoodCount);
            Assert.AreEqual(9, resources.FurnaceCount);
            Assert.AreEqual(14, resources.ObstacleCount);
            Assert.IsFalse(resources.FoodReproductionEnabled);
        }

        [Test]
        public void CampaignAssetContainsConfirmedStartsCapsAndLevelRewards()
        {
            var asset = AssetDatabase.LoadAssetAtPath<CampaignResourceConfig>(
                "Assets/Game/Configs/Campaign/CFG_CampaignResources.asset");
            Assert.IsNotNull(asset);
            Assert.IsTrue(asset.TryValidate(out var error), error);
            Assert.AreEqual(8, asset.InitialSlimes);
            Assert.AreEqual(20, asset.MaximumSlimes);
            Assert.AreEqual(10, asset.InitialFood);
            Assert.AreEqual(30, asset.MaximumFood);
            Assert.AreEqual(3, asset.InitialFurnaces);
            Assert.AreEqual(10, asset.MaximumFurnaces);
            Assert.AreEqual(5, asset.InitialObstacles);
            Assert.AreEqual(15, asset.MaximumObstacles);
            Assert.IsFalse(asset.FoodReproductionEnabled);

            Assert.IsTrue(asset.TryGetReward(new LevelId("FirstLevel"), out var first));
            Assert.AreEqual(6, first.SlimeReward);
            Assert.AreEqual(10, first.FoodReward);
            Assert.AreEqual(3, first.FurnaceReward);
            Assert.AreEqual(5, first.ObstacleReward);
            Assert.IsTrue(asset.TryGetReward(new LevelId("SecondLevel"), out var second));
            Assert.AreEqual(6, second.SlimeReward);
            Assert.AreEqual(10, second.FoodReward);
            Assert.AreEqual(5, second.FurnaceReward);
            Assert.AreEqual(5, second.ObstacleReward);
            Assert.IsTrue(asset.TryGetReward(new LevelId("ThirdLevel"), out var third));
            Assert.AreEqual(12, third.SlimeReward);
            Assert.AreEqual(15, third.FoodReward);
            Assert.AreEqual(5, third.FurnaceReward);
            Assert.AreEqual(5, third.ObstacleReward);
        }

        [Test]
        public void FirstLevelMapProvidesTwentyOrderedSpawnPointsNearEscortStart()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewPreviewScene();
            MapManager map = null;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Tests/FirstLevelMap/PF_FirstLevelMapData_TEST.prefab");
                Assert.IsNotNull(prefab);
                PrefabUtility.InstantiatePrefab(prefab, scene);
                map = new MapManager(scene, null);
                var points = new List<MapPointSnapshot>(
                    map.GetMapPoints(MapPointKind.SlimeSpawn));
                points.Sort((left, right) => string.CompareOrdinal(
                    left.PointId.Value, right.PointId.Value));
                Assert.AreEqual(20, points.Count);
                Assert.IsTrue(map.TryGetRoute(new MapRouteId("route_escort"), out var route));
                for (var i = 0; i < 20; i++)
                    Assert.AreEqual("slime_spawn_" + (i + 1).ToString("D2"),
                        points[i].PointId.Value);
                for (var i = 0; i < 8; i++)
                    Assert.Less(Vector2.Distance(points[i].Position,
                        route.Waypoints[0].Position), 1.2f);
            }
            finally
            {
                map?.Dispose();
                if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
        }

        [Test]
        public void CastleMapProvidesTwentyOrderedSpawnPointsNearWeaponStart()
        {
            var definition = AssetDatabase.LoadAssetAtPath<FirstLevelMapDefinition>(
                "Assets/Game/Configs/Maps/Castle/MAP_Castle.asset");
            Assert.IsNotNull(definition);
            Assert.IsTrue(definition.TryBuildSnapshot(out var layout, out var error), error);

            var points = new List<MapPointSnapshot>();
            var weapons = new List<MapPointSnapshot>();
            foreach (var point in layout.Points)
            {
                if (point.Kind == MapPointKind.SlimeSpawn) points.Add(point);
                else if (point.Kind == MapPointKind.Weapon) weapons.Add(point);
            }
            points.Sort((left, right) => string.CompareOrdinal(
                left.PointId.Value, right.PointId.Value));

            Assert.AreEqual(20, points.Count);
            Assert.AreEqual(1, weapons.Count);
            var previousDistance = 0f;
            for (var i = 0; i < points.Count; i++)
            {
                Assert.AreEqual("castle_slime_spawn_" + (i + 1).ToString("D2"),
                    points[i].PointId.Value);
                var distance = Vector2.Distance(points[i].Position,
                    weapons[0].Position);
                Assert.GreaterOrEqual(distance, previousDistance);
                previousDistance = distance;
            }
        }
    }
}
