using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    public readonly struct StatusRemovedEvent
    {
        public StatusRemovedEvent(ActorId actorId, StatusEntrySnapshot status,
            StatusRemoveReason reason)
        {
            ActorId = actorId;
            Status = status;
            Reason = reason;
        }

        public ActorId ActorId { get; }
        public StatusEntrySnapshot Status { get; }
        public StatusRemoveReason Reason { get; }
    }
}
