using SlimeBattle.Actors;
using UnityEngine;

namespace SlimeBattle.World
{
    public readonly struct MapAreaExitedEvent
    {
        public MapAreaExitedEvent(ActorId actorId, MapAreaSnapshot area, Vector2 position)
        {
            ActorId = actorId;
            Area = area;
            Position = position;
        }

        public ActorId ActorId { get; }
        public MapAreaSnapshot Area { get; }
        public Vector2 Position { get; }
    }
}
