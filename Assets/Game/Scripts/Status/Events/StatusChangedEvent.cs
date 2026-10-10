using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    public readonly struct StatusChangedEvent
    {
        public StatusChangedEvent(ActorId actorId, StatusEntrySnapshot status)
        {
            ActorId = actorId;
            Status = status;
        }

        public ActorId ActorId { get; }
        public StatusEntrySnapshot Status { get; }
    }
}
