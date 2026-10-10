using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    public readonly struct HealthChangedEvent
    {
        public HealthChangedEvent(EffectRequestId requestId, HealthSnapshot before,
            HealthSnapshot after, ResolvedEffect effect)
        {
            RequestId = requestId;
            Before = before;
            After = after;
            Effect = effect;
        }

        public HealthChangedEvent(HealthSnapshot before, HealthSnapshot after, StatusId causeStatusId)
        {
            RequestId = default(EffectRequestId);
            Before = before;
            After = after;
            Effect = new ResolvedEffect(EffectType.Status, 0f, default(ActorId),
                causeStatusId.Value);
        }

        public EffectRequestId RequestId { get; }
        public HealthSnapshot Before { get; }
        public HealthSnapshot After { get; }
        public ResolvedEffect Effect { get; }
    }
}
