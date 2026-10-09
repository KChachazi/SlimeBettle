using SlimeBattle.Actors;
using UnityEngine;

namespace SlimeBattle.World.Movement
{
    public readonly struct MovementState
    {
        public MovementState(ActorId actorId, MovementResult result,
            MovementRequestId requestId, Vector2 position, Vector2 destination,
            Vector2 velocity, ContactInfo contactInfo = default(ContactInfo),
            CancelReason cancelReason = CancelReason.Interrupted)
        {
            ActorId = actorId;
            Result = result;
            RequestId = requestId;
            Position = position;
            Destination = destination;
            Velocity = velocity;
            ContactInfo = contactInfo;
            CancelReason = cancelReason;
        }

        public ActorId ActorId { get; }
        public MovementResult Result { get; }
        public MovementRequestId RequestId { get; }
        public Vector2 Position { get; }
        public Vector2 Destination { get; }
        public Vector2 Velocity { get; }
        public ContactInfo ContactInfo { get; }
        public CancelReason CancelReason { get; }
        public bool IsMoving => Result == MovementResult.InProgress;
        public bool HasContact => ContactInfo.IsValid;
    }
}
