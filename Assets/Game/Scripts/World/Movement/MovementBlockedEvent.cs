using SlimeBattle.Actors;
using UnityEngine;

namespace SlimeBattle.World.Movement
{
    public readonly struct MovementBlockedEvent
    {
        public MovementBlockedEvent(ActorId actorId, MovementRequestId requestId,
            Vector2 position, ContactInfo contactInfo)
        {
            ActorId = actorId;
            RequestId = requestId;
            Position = position;
            ContactInfo = contactInfo;
        }

        public ActorId ActorId { get; }
        public MovementRequestId RequestId { get; }
        public Vector2 Position { get; }
        public ContactInfo ContactInfo { get; }
    }
}
