using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Tests
{
    public sealed class MovementSystemEditModeTests
    {
        [Test]
        public void AStarRoutesAroundAnObstacleWithoutCrossingItsInflatedBounds()
        {
            var bounds = new MapBounds(Vector2.zero, new Vector2(6f, 6f));
            bool CanOccupy(Vector2 position)
            {
                const float radius = .25f;
                if (position.x - radius < bounds.Min.x ||
                    position.x + radius > bounds.Max.x ||
                    position.y - radius < bounds.Min.y ||
                    position.y + radius > bounds.Max.y) return false;
                var closest = new Vector2(Mathf.Clamp(position.x, 2f, 3f),
                    Mathf.Clamp(position.y, 1f, 4f));
                return (closest - position).sqrMagnitude > radius * radius;
            }

            var start = new Vector2(1f, 3f);
            var destination = new Vector2(5f, 3f);
            Assert.IsTrue(AStarGridPathfinder.TryFindPath(start, destination,
                bounds, CanOccupy, out var waypoints));
            Assert.Greater(waypoints.Count, 1);
            Assert.AreEqual(destination, waypoints[waypoints.Count - 1]);

            var segmentStart = start;
            foreach (var waypoint in waypoints)
            {
                var distance = Vector2.Distance(segmentStart, waypoint);
                var steps = Mathf.CeilToInt(distance / .1f);
                for (var i = 0; i <= steps; i++)
                    Assert.IsTrue(CanOccupy(Vector2.Lerp(segmentStart, waypoint,
                        steps == 0 ? 1f : i / (float)steps)),
                        "Smoothed route must remain clear of the obstacle.");
                segmentStart = waypoint;
            }
        }

        [Test]
        public void IdleBodyLosesCollisionVelocityAndNewMoveStillWorks()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var boundsObject = new GameObject("Movement test bounds");
            SceneManager.MoveGameObjectToScene(boundsObject, scene);
            boundsObject.AddComponent<MapBoundsAuthoring>();
            var actorObject = new GameObject("Movement test actor");
            SceneManager.MoveGameObjectToScene(actorObject, scene);
            var body = actorObject.AddComponent<Rigidbody2D>();
            actorObject.AddComponent<CircleCollider2D>();
            var map = new MapManager(scene, null);
            var movement = new MovementSystem(map, null);
            try
            {
                var actorId = ActorId.NewId();
                Assert.IsTrue(movement.RegisterActor(actorId, body, 200f, 100f,
                    _ => { }, _ => true));

                body.velocity = Vector2.right * 3f;
                movement.FixedTick(.02f);
                Assert.AreEqual(Vector2.zero, body.velocity);
                Assert.IsTrue(movement.TryGetMovementState(actorId, out var idleState));
                Assert.AreEqual(Vector2.zero, idleState.Velocity);

                var requestId = movement.SubmitMovement(
                    new MovementRequest(actorId, Vector2.right * 2f));
                Assert.IsTrue(requestId.IsValid);
                movement.FixedTick(.02f);
                Assert.Greater(body.velocity.x, 0f);
            }
            finally
            {
                movement.Dispose();
                map.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void AStarMovementReplansWhenAStaticObstacleBlocksItsCurrentPath()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var boundsObject = new GameObject("AStar test bounds");
            SceneManager.MoveGameObjectToScene(boundsObject, scene);
            boundsObject.AddComponent<MapBoundsAuthoring>();
            var actorObject = new GameObject("AStar test actor");
            SceneManager.MoveGameObjectToScene(actorObject, scene);
            actorObject.transform.position = new Vector2(-2f, 0f);
            var body = actorObject.AddComponent<Rigidbody2D>();
            actorObject.AddComponent<CircleCollider2D>();
            var map = new MapManager(scene, null);
            var movement = new MovementSystem(map, null);
            try
            {
                var actorId = ActorId.NewId();
                Assert.IsTrue(movement.RegisterActor(actorId, body, 200f, 100f,
                    _ => { }, _ => true, requireRoadArea: false, useAStar: true));
                Assert.IsTrue(movement.SubmitMovement(new MovementRequest(actorId,
                    new Vector2(2f, 0f))).IsValid);

                var obstacle = new GameObject("new route blocker");
                SceneManager.MoveGameObjectToScene(obstacle, scene);
                obstacle.transform.position = new Vector2(-1.37f, 0f);
                var obstacleCollider = obstacle.AddComponent<BoxCollider2D>();
                obstacleCollider.size = new Vector2(.2f, 2f);
                Assert.IsTrue(map.RegisterMovementBlocker(obstacleCollider));
                Physics2D.SyncTransforms();

                movement.FixedTick(.02f);
                Assert.IsTrue(movement.TryGetMovementState(actorId, out var blockedState));
                Assert.AreEqual(MovementResult.InProgress, blockedState.Result,
                    "A blocked direct segment should be replanned instead of ending as Blocked.");

                movement.FixedTick(.02f);
                Assert.Greater(body.velocity.sqrMagnitude, 0f,
                    "The replanned path should continue moving around the obstacle.");
            }
            finally
            {
                movement.Dispose();
                map.Dispose();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void RegisteredActorsIgnoreEachOtherToAvoidPhysicalDeadlocks()
        {
            var firstObject = new GameObject("Movement test first actor");
            var secondObject = new GameObject("Movement test second actor");
            var map = new MapManager(SceneManager.GetActiveScene(), null);
            var movement = new MovementSystem(map, null);
            try
            {
                var firstBody = firstObject.AddComponent<Rigidbody2D>();
                var firstCollider = firstObject.AddComponent<CircleCollider2D>();
                var secondBody = secondObject.AddComponent<Rigidbody2D>();
                var secondCollider = secondObject.AddComponent<CircleCollider2D>();
                var firstId = ActorId.NewId();
                var secondId = ActorId.NewId();
                Assert.IsTrue(movement.RegisterActor(firstId, firstBody, 200f, 100f,
                    _ => { }, _ => true));
                Assert.IsTrue(movement.RegisterActor(secondId, secondBody, 200f, 100f,
                    _ => { }, _ => true));

                Assert.IsTrue(Physics2D.GetIgnoreCollision(firstCollider, secondCollider));

                movement.UnregisterActor(firstId);
                Assert.IsFalse(Physics2D.GetIgnoreCollision(firstCollider, secondCollider));
            }
            finally
            {
                movement.Dispose();
                map.Dispose();
                Object.DestroyImmediate(firstObject);
                Object.DestroyImmediate(secondObject);
            }
        }
    }
}
