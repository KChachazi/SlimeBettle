using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SlimeBattle.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Tests
{
    public sealed class MapManagerEditModeTests
    {
        private Scene testScene;
        private MapManager mapManager;

        [SetUp]
        public void SetUp()
        {
            testScene = EditorSceneManager.NewPreviewScene();
        }

        [TearDown]
        public void TearDown()
        {
            mapManager?.Dispose();
            mapManager = null;
            if (testScene.IsValid()) EditorSceneManager.ClosePreviewScene(testScene);
        }

        [Test]
        public void FixedRouteReturnsStableOrderAndExplicitGoal()
        {
            AddBounds(new Vector2(20f, 20f));
            var routeId = "route_" + Guid.NewGuid().ToString("N");
            var points = new[]
            {
                CreateMarker("start", new Vector2(-5f, 0f)),
                CreateMarker("bend", new Vector2(0f, 3f)),
                CreateMarker("goal", new Vector2(5f, 0f))
            };
            var routeObject = CreateObject("Fixed route");
            var authoring = routeObject.AddComponent<MapRouteAuthoring>();
            ConfigureRoute(authoring, routeId, points);

            mapManager = new MapManager(testScene, null);

            Assert.IsTrue(mapManager.TryGetRoute(new MapRouteId(routeId), out var route));
            Assert.IsTrue(route.IsValid);
            Assert.AreEqual(3, route.Waypoints.Count);
            Assert.AreEqual("start", route.Waypoints[0].WaypointId.Value);
            Assert.AreEqual(0, route.Waypoints[0].Sequence);
            Assert.AreEqual(new Vector2(-5f, 0f), route.Waypoints[0].Position);
            Assert.AreEqual("bend", route.Waypoints[1].WaypointId.Value);
            Assert.AreEqual("goal", route.GoalWaypoint.WaypointId.Value);
            Assert.IsTrue(route.GoalWaypoint.IsGoal);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<MapRouteWaypointSnapshot>)route.Waypoints).Add(default(MapRouteWaypointSnapshot)));
        }

        [Test]
        public void BoundsStaticBlockersAndTerrainMultiplierAreQueryable()
        {
            var bounds = AddBounds(new Vector2(20f, 20f));
            var blocker = CreateObject("Static blocker");
            var blockerCollider = blocker.AddComponent<BoxCollider2D>();
            blockerCollider.size = new Vector2(2f, 2f);

            var terrain = CreateObject("Swamp terrain");
            terrain.transform.position = new Vector2(4f, 0f);
            var terrainCollider = terrain.AddComponent<BoxCollider2D>();
            terrainCollider.isTrigger = true;
            terrainCollider.size = new Vector2(4f, 4f);
            var area = terrain.AddComponent<MapAreaAuthoring>();
            ConfigureArea(area, "terrain_" + Guid.NewGuid().ToString("N"),
                MapAreaKind.Terrain, MapTerrainKind.Swamp, .5f);

            mapManager = new MapManager(testScene, null);

            Assert.IsTrue(mapManager.TryGetMapBounds(out var worldBounds));
            Assert.AreEqual(new Vector2(-10f, -10f), worldBounds.Min);
            Assert.AreEqual(new Vector2(10f, 10f), worldBounds.Max);
            Assert.IsTrue(mapManager.CanOccupy(new Vector2(8f, 8f),
                new OccupancyQuery(.5f)));
            Assert.IsFalse(mapManager.CanOccupy(new Vector2(10f, 0f),
                new OccupancyQuery(.5f)));
            Assert.IsFalse(mapManager.CanOccupy(Vector2.zero,
                new OccupancyQuery(0f)));
            Assert.AreEqual(.5f, mapManager.GetMovementSpeedMultiplier(new Vector2(4f, 0f)));
            Assert.AreEqual(1f, mapManager.GetMovementSpeedMultiplier(new Vector2(-4f, 0f)));
            Assert.IsNotNull(bounds);
        }

        [Test]
        public void ScenePointsExposeSpawnWeaponEnemyAndGoalFacts()
        {
            AddBounds(new Vector2(20f, 20f));
            AddPoint("spawn", MapPointKind.Spawn, new Vector2(-5f, 0f));
            AddPoint("weapon", MapPointKind.Weapon, Vector2.zero);
            AddPoint("enemy", MapPointKind.EnemySpawn, new Vector2(2f, 0f));
            AddPoint("goal", MapPointKind.Target, new Vector2(5f, 0f));
            var placementAreaObject = CreateObject("Placement zone");
            var placementCollider = placementAreaObject.AddComponent<BoxCollider2D>();
            placementCollider.isTrigger = true;
            placementCollider.size = new Vector2(6f, 6f);
            var placementArea = placementAreaObject.AddComponent<MapAreaAuthoring>();
            ConfigureArea(placementArea, "placement_" + Guid.NewGuid().ToString("N"),
                MapAreaKind.Placement, MapTerrainKind.None, 1f);
            mapManager = new MapManager(testScene, null);

            Assert.AreEqual(1, mapManager.GetMapPoints(MapPointKind.Spawn).Count);
            Assert.AreEqual(1, mapManager.GetMapPoints(MapPointKind.Weapon).Count);
            Assert.AreEqual(1, mapManager.GetMapPoints(MapPointKind.EnemySpawn).Count);
            Assert.IsTrue(mapManager.TryGetMapPoint(new MapPointId("goal"), out var goal));
            Assert.AreEqual(MapPointKind.Target, goal.Kind);
            Assert.AreEqual(new Vector2(5f, 0f), goal.Position);
            var spatial = mapManager.Query(Vector2.zero);
            Assert.IsTrue(spatial.Areas.Any(area => area.Kind == MapAreaKind.Placement));
        }

        private MapBoundsAuthoring AddBounds(Vector2 size)
        {
            var bounds = CreateObject("Map bounds").AddComponent<MapBoundsAuthoring>();
            SetSerializedField(bounds, "localCenter", Vector2.zero);
            SetSerializedField(bounds, "localSize", size);
            return bounds;
        }

        private MapPointAuthoring AddPoint(string id, MapPointKind kind, Vector2 position)
        {
            var pointObject = CreateObject("Map point " + id);
            pointObject.transform.position = position;
            var authoring = pointObject.AddComponent<MapPointAuthoring>();
            SetSerializedField(authoring, "pointId", id);
            SetSerializedField(authoring, "kind", (int)kind);
            SetSerializedField(authoring, "configId", string.Empty);
            return authoring;
        }

        private Transform CreateMarker(string id, Vector2 position)
        {
            var marker = CreateObject("Route marker " + id);
            marker.transform.position = position;
            return marker.transform;
        }

        private GameObject CreateObject(string name)
        {
            var value = new GameObject(name);
            if (value.scene != testScene)
                SceneManager.MoveGameObjectToScene(value, testScene);
            return value;
        }

        private static void ConfigureRoute(MapRouteAuthoring authoring,
            string routeId, Transform[] markers)
        {
            var serialized = new SerializedObject(authoring);
            serialized.FindProperty("routeId").stringValue = routeId;
            var entries = serialized.FindProperty("waypoints");
            entries.arraySize = markers.Length;
            for (var i = 0; i < markers.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("waypointId").stringValue =
                    i == markers.Length - 1 ? "goal" : i == 0 ? "start" : "bend";
                entry.FindPropertyRelative("marker").objectReferenceValue = markers[i];
                entry.FindPropertyRelative("isGoal").boolValue = i == markers.Length - 1;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureArea(MapAreaAuthoring authoring, string id,
            MapAreaKind kind, MapTerrainKind terrainKind, float speedMultiplier)
        {
            var serialized = new SerializedObject(authoring);
            serialized.FindProperty("areaId").stringValue = id;
            serialized.FindProperty("kind").enumValueIndex = (int)kind;
            serialized.FindProperty("terrainKind").enumValueIndex = (int)terrainKind;
            serialized.FindProperty("movementSpeedMultiplier").floatValue = speedMultiplier;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetSerializedField<T>(UnityEngine.Object target,
            string fieldName, T value)
        {
            var serialized = new SerializedObject(target);
            var field = serialized.FindProperty(fieldName);
            if (typeof(T) == typeof(int)) field.intValue = (int)(object)value;
            else if (typeof(T) == typeof(string)) field.stringValue = (string)(object)value;
            else if (typeof(T) == typeof(Vector2)) field.vector2Value = (Vector2)(object)value;
            else throw new ArgumentException("Unsupported serialized test field type.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
