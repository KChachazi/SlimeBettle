using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    public readonly struct ActorControlChangedEvent
    {
        public ActorControlChangedEvent(ActorId actorId, ActorControlMode previous,
            ActorControlMode current)
        {
            ActorId = actorId;
            Previous = previous;
            Current = current;
        }

        public ActorId ActorId { get; }
        public ActorControlMode Previous { get; }
        public ActorControlMode Current { get; }
    }
}
