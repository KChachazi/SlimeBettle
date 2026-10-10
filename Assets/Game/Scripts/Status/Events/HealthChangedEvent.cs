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

        public EffectRequestId RequestId { get; }
        public HealthSnapshot Before { get; }
        public HealthSnapshot After { get; }
        public ResolvedEffect Effect { get; }
    }
}
