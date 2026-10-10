using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    public readonly struct StatusAddedEvent
    {
        public StatusAddedEvent(ActorId actorId, StatusEntrySnapshot status)
        {
            ActorId = actorId;
            Status = status;
        }

        public ActorId ActorId { get; }
        public StatusEntrySnapshot Status { get; }
    }
}
