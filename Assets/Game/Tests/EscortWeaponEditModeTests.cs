using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Data;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Status;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Tests
{
    public sealed class EscortWeaponEditModeTests
    {
        private Scene scene;
        private MapManager map;
        private WeaponConfig config;
        private EscortWeapon weapon;

        [SetUp]
        public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            var bounds = CreateObject("bounds").AddComponent<MapBoundsAuthoring>();
            var serialized = new SerializedObject(bounds);
            serialized.FindProperty("localSize").vector2Value = new Vector2(20f, 20f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            config = ScriptableObject.CreateInstance<WeaponConfig>();
            config.SetConfigId("weapon");
            config.SetMaxHealth(10);
            config.ConfigureTransport(1f, .1f);
        }

        [TearDown]
        public void TearDown()
        {
            weapon?.Dispose();
            map?.Dispose();
            Object.DestroyImmediate(config);
            if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }

        [Test]
        public void DamageRequestsDeduplicateAndDestroyExactlyOnce()
        {
            map = new MapManager(scene, null);
            weapon = CreateWeapon();
            var damaged = 0;
            var destroyed = 0;
            weapon.Damaged += (_, __, ___, ____) => damaged++;
            weapon.Destroyed += _ => destroyed++;
            var attacker = ActorId.NewId();
            var first = EffectRequestId.NewId();
            Assert.IsTrue(weapon.RequestDamage(attacker, first, 4));
            Assert.IsFalse(weapon.RequestDamage(attacker, first, 4));
            Assert.AreEqual(6, weapon.GetSnapshot().Health);
            Assert.IsTrue(weapon.RequestDamage(attacker, EffectRequestId.NewId(), 10));
            Assert.IsFalse(weapon.RequestDamage(attacker, EffectRequestId.NewId(), 1));
            Assert.AreEqual(2, damaged);
            Assert.AreEqual(1, destroyed);
            Assert.IsTrue(weapon.GetSnapshot().IsDestroyed);
            weapon.Tick(5f);
            Assert.IsFalse(weapon.GetSnapshot().HasReachedGoal);
        }

        [Test]
        public void RouteStopsAtStaticObstacleThenResumesAndReachesGoalOnce()
        {
            var obstacle = CreateObject("obstacle");
            obstacle.transform.position = new Vector2(1f, 0f);
            var collider = obstacle.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(.25f, .25f);
            map = new MapManager(scene, null);
            weapon = CreateWeapon();
            var reached = 0;
            weapon.ReachedGoal += _ => reached++;
            Assert.IsTrue(weapon.AddCarrier(ActorId.NewId()));
            Assert.IsTrue(weapon.AddCarrier(ActorId.NewId()));
            Assert.IsTrue(weapon.AddCarrier(ActorId.NewId()));
            Assert.IsTrue(weapon.AddCarrier(ActorId.NewId()));
            for (var i = 0; i < 20; i++) weapon.Tick(.1f);
            Assert.AreEqual(FixedRouteMovementState.Blocked,
                weapon.GetSnapshot().TransportState);
            Assert.Less(weapon.GetSnapshot().Position.x, 1f);
            Object.DestroyImmediate(obstacle);
            for (var i = 0; i < 30; i++) weapon.Tick(.1f);
            Assert.IsTrue(weapon.GetSnapshot().HasReachedGoal);
            Assert.AreEqual(new Vector2(2f, 0f), weapon.GetSnapshot().Position);
            weapon.Tick(1f);
            Assert.AreEqual(1, reached);
        }

        [Test]
        public void DisposeCancelsTransportAndRejectsLaterDamage()
        {
            map = new MapManager(scene, null);
            weapon = CreateWeapon();
            weapon.Tick(.25f);
            var stoppedAt = weapon.GetSnapshot().Position;
            weapon.Dispose();
            weapon.Tick(2f);
            Assert.AreEqual(stoppedAt, weapon.GetSnapshot().Position);
            Assert.AreEqual(FixedRouteMovementState.Cancelled,
                weapon.GetSnapshot().TransportState);
            Assert.IsFalse(weapon.RequestDamage(ActorId.NewId(),
                EffectRequestId.NewId(), 1));
        }

        [Test]
        public void CarrierCountSetsFourSpeedStepsAndZeroCarriersStopTransport()
        {
            map = new MapManager(scene, null);
            weapon = CreateWeapon();
            var ids = new[] { ActorId.NewId(), ActorId.NewId(),
                ActorId.NewId(), ActorId.NewId() };
            weapon.Tick(1f);
            Assert.AreEqual(Vector2.zero, weapon.GetSnapshot().Position);
            for (var i = 0; i < ids.Length; i++)
            {
                Assert.IsTrue(weapon.AddCarrier(ids[i]));
                Assert.AreEqual((i + 1) * .25f,
                    weapon.GetSnapshot().CurrentSpeedWorldUnits, .0001f);
                if (i == 0)
                {
                    weapon.Tick(1f);
                    Assert.AreEqual(.25f, weapon.GetSnapshot().Position.x, .0001f);
                }
            }
            Assert.IsFalse(weapon.AddCarrier(ActorId.NewId()));
            for (var i = ids.Length - 1; i >= 0; i--)
                Assert.IsTrue(weapon.RemoveCarrier(ids[i]));
            Assert.AreEqual(0f, weapon.GetSnapshot().CurrentSpeedWorldUnits);
        }

        [Test]
        public void MechanismPublishesAcceptedDamageOnceAndStopsWhenPausedOrDisposed()
        {
            var routeObject = CreateObject("route");
            var start = CreateObject("start");
            var goal = CreateObject("goal");
            goal.transform.position = Vector2.right * 2f;
            var route = routeObject.AddComponent<MapRouteAuthoring>();
            var routeSerialized = new SerializedObject(route);
            routeSerialized.FindProperty("routeId").stringValue = "route";
            var points = routeSerialized.FindProperty("waypoints");
            points.arraySize = 2;
            points.GetArrayElementAtIndex(0).FindPropertyRelative("waypointId").stringValue = "start";
            points.GetArrayElementAtIndex(0).FindPropertyRelative("marker").objectReferenceValue =
                start.transform;
            points.GetArrayElementAtIndex(1).FindPropertyRelative("waypointId").stringValue = "goal";
            points.GetArrayElementAtIndex(1).FindPropertyRelative("marker").objectReferenceValue =
                goal.transform;
            points.GetArrayElementAtIndex(1).FindPropertyRelative("isGoal").boolValue = true;
            routeSerialized.ApplyModifiedPropertiesWithoutUndo();
            var authoring = CreateObject("weapon").AddComponent<EscortWeaponAuthoring>();
            var weaponSerialized = new SerializedObject(authoring);
            weaponSerialized.FindProperty("weaponConfigId").stringValue = "weapon";
            weaponSerialized.FindProperty("routeId").stringValue = "route";
            weaponSerialized.ApplyModifiedPropertiesWithoutUndo();

            using (var bus = new LevelEventBus())
            {
                var registry = new ConfigRegistry(new ConfigAsset[] { config });
                map = new MapManager(scene, bus);
                using (var status = new StatusManager(registry, bus, _ => null))
                using (var mechanisms = new MechanismSystem(registry, 0, map,
                    status, bus, scene))
                {
                    var damaged = 0;
                    var destroyed = 0;
                    bus.Subscribe<WeaponDamagedEvent>(e =>
                    { damaged++; Assert.AreEqual(10, e.ActualHealthLoss); });
                    bus.Subscribe<WeaponDestroyedEvent>(_ => destroyed++);
                    Assert.IsTrue(mechanisms.TryGetWeaponSnapshot(out var snapshot));
                    Assert.AreEqual(10, snapshot.Health);
                    mechanisms.Pause();
                    Assert.IsFalse(mechanisms.RequestWeaponDamage(ActorId.NewId(),
                        EffectRequestId.NewId(), 10));
                    mechanisms.Resume();
                    var attacker = ActorId.NewId();
                    var request = EffectRequestId.NewId();
                    Assert.IsTrue(mechanisms.RequestWeaponDamage(attacker, request, 12));
                    Assert.IsFalse(mechanisms.RequestWeaponDamage(attacker, request, 12));
                    Assert.AreEqual(1, damaged);
                    Assert.AreEqual(1, destroyed);
                    mechanisms.Dispose();
                    Assert.IsFalse(mechanisms.RequestWeaponDamage(attacker,
                        EffectRequestId.NewId(), 1));
                }
            }
        }

        private EscortWeapon CreateWeapon()
        {
            var body = CreateObject("escort weapon");
            var route = new MapRouteSnapshot(new MapRouteId("route"), new[]
            {
                new MapRouteWaypointSnapshot(new MapRouteWaypointId("start"),
                    0, Vector2.zero, false),
                new MapRouteWaypointSnapshot(new MapRouteWaypointId("goal"),
                    1, new Vector2(2f, 0f), true)
            });
            return new EscortWeapon(MechanismId.NewId(), config, route,
                body.transform, map);
        }

        private GameObject CreateObject(string name)
        {
            var value = new GameObject(name);
            SceneManager.MoveGameObjectToScene(value, scene);
            return value;
        }
    }
}
