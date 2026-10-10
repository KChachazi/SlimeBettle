using SlimeBattle.Actors;
using SlimeBattle.World.Movement;

namespace SlimeBattle.Status
{
    public readonly struct MovementModifierChangedEvent
    {
        public MovementModifierChangedEvent(ActorId actorId,
            MovementModifierSnapshot previous, MovementModifierSnapshot current)
        {
            ActorId = actorId;
            Previous = previous;
            Current = current;
        }

        public ActorId ActorId { get; }
        public MovementModifierSnapshot Previous { get; }
        public MovementModifierSnapshot Current { get; }
    }
}
