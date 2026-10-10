using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    public readonly struct StatusExpiredEvent
    {
        public StatusExpiredEvent(ActorId actorId, StatusEntrySnapshot status)
        {
            ActorId = actorId;
            Status = status;
        }

        public ActorId ActorId { get; }
        public StatusEntrySnapshot Status { get; }
    }
}
