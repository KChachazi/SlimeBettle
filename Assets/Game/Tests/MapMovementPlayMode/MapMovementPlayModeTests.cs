using System.Collections;
using System.Reflection;
using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.Core;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SlimeBattle.Tests.MapMovement
{
    public sealed class MapMovementPlayModeTests
    {
        [UnityTest]
        public IEnumerator BoundsQueryAndStaticCollisionStopMovement()
        {
            var scene = SceneManager.CreateScene("MapMovement_PlayModeFixture");
            var mapObject = CreateInScene(scene, "Map bounds");
            var boundsAuthoring = mapObject.AddComponent<MapBoundsAuthoring>();
            SetField(boundsAuthoring, "localCenter", Vector2.zero);
            SetField(boundsAuthoring, "localSize", new Vector2(20f, 10f));

            var obstacleObject = CreateInScene(scene, "Static wall");
            obstacleObject.transform.position = Vector2.zero;
            var obstacle = obstacleObject.AddComponent<BoxCollider2D>();
            obstacle.size = new Vector2(.5f, 5f);

            var map = new MapManager(scene, null);
            var eventBus = new LevelEventBus();
            var movement = new MovementSystem(map, eventBus);
            var wasBlocked = false;
            eventBus.Subscribe<MovementBlockedEvent>(_ => wasBlocked = true);
            var actorObject = CreateInScene(scene, "Test mover");
            actorObject.transform.position = new Vector2(-2f, 0f);
            var body = actorObject.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            var actorCollider = actorObject.AddComponent<CircleCollider2D>();
            actorCollider.radius = .2f;
            var actorId = ActorId.NewId();

            try
            {
                Assert.IsTrue(map.TryGetMapBounds(out var bounds));
                Assert.IsTrue(bounds.Contains(new Vector2(9f, 4f)));
                Assert.IsFalse(bounds.Contains(new Vector2(10.1f, 0f)));
                Assert.IsFalse(map.CanOccupy(new Vector2(10f, 0f), new OccupancyQuery(.2f)));
                Assert.IsFalse(map.CanOccupy(Vector2.zero, new OccupancyQuery(0f)));

                Assert.IsTrue(movement.RegisterActor(actorId, body, 500f, 100f,
                    _ => { }, _ => true));
                Assert.IsTrue(movement.SubmitMovement(new MovementRequest(
                    actorId, new Vector2(2f, 0f))).IsValid);

                for (var frame = 0; frame < 120 && !wasBlocked; frame++)
                {
                    movement.FixedTick(Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }

                Assert.IsTrue(wasBlocked, "A static map collider should publish MovementBlockedEvent.");
                Assert.IsTrue(movement.TryGetMovementState(actorId, out var state));
                Assert.AreEqual(MovementResult.Blocked, state.Result);
                Assert.Less(state.Position.x, 0f);
                Assert.AreEqual(Vector2.zero, body.velocity);
            }
            finally
            {
                movement.Dispose();
                map.Dispose();
                eventBus.Dispose();
                Object.Destroy(actorObject);
                Object.Destroy(obstacleObject);
                Object.Destroy(mapObject);
            }

            yield return null;
            yield return SceneManager.UnloadSceneAsync(scene);
        }

        private static GameObject CreateInScene(Scene scene, string name)
        {
            var value = new GameObject(name);
            SceneManager.MoveGameObjectToScene(value, scene);
            return value;
        }

        private static void SetField<T>(object target, string fieldName, T value)
        {
            var field = target.GetType().GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Expected serialized field '" + fieldName + "'.");
            field.SetValue(target, value);
        }
    }
}
