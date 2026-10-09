using SlimeBattle.Actors;
using UnityEngine;

namespace SlimeBattle.World.Movement
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public sealed class ActorMovementBody : MonoBehaviour
    {
        private MovementSystem movementSystem;
        private ActorId actorId;

        internal void Bind(MovementSystem system, ActorId id)
        {
            movementSystem = system;
            actorId = id;
        }

        internal void Unbind(MovementSystem system)
        {
            if (movementSystem != system) return;
            movementSystem = null;
            actorId = default(ActorId);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            NotifyCollision(collision);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            NotifyCollision(collision);
        }

        private void NotifyCollision(Collision2D collision)
        {
            if (movementSystem == null || !actorId.IsValid || collision == null ||
                collision.contactCount == 0) return;

            var contact = collision.GetContact(0);
            movementSystem.NotifyCollision(actorId, collision.collider,
                collision.otherCollider, contact.point, contact.normal);
        }
    }
}
