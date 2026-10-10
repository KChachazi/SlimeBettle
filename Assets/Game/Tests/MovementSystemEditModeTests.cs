using NUnit.Framework;
using SlimeBattle.Actors;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Tests
{
    public sealed class MovementSystemEditModeTests
    {
        [Test]
        public void IdleBodyLosesCollisionVelocityAndNewMoveStillWorks()
        {
            var actorObject = new GameObject("Movement test actor");
            var body = actorObject.AddComponent<Rigidbody2D>();
            actorObject.AddComponent<CircleCollider2D>();
            var map = new MapManager(SceneManager.GetActiveScene(), null);
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
                Object.DestroyImmediate(actorObject);
            }
        }
    }
}
