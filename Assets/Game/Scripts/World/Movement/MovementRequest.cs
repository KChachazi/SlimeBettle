using SlimeBattle.Actors;
using UnityEngine;

namespace SlimeBattle.World.Movement
{
    public readonly struct MovementRequest
    {
        public MovementRequest(ActorId actorId, Vector2 destination)
        {
            ActorId = actorId;
            Destination = destination;
        }

        public ActorId ActorId { get; }
        public Vector2 Destination { get; }

        public bool IsValid => ActorId.IsValid &&
            !float.IsNaN(Destination.x) && !float.IsInfinity(Destination.x) &&
            !float.IsNaN(Destination.y) && !float.IsInfinity(Destination.y);
    }
}
