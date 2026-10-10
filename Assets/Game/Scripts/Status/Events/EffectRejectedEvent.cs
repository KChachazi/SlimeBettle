using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    public readonly struct EffectRejectedEvent
    {
        public EffectRejectedEvent(EffectRequestId requestId, ActorId sourceActorId,
            ActorId targetActorId, EffectApplyResult result)
        {
            RequestId = requestId;
            SourceActorId = sourceActorId;
            TargetActorId = targetActorId;
            Result = result;
        }

        public EffectRequestId RequestId { get; }
        public ActorId SourceActorId { get; }
        public ActorId TargetActorId { get; }
        public EffectApplyResult Result { get; }
    }
}
