using SlimeBattle.Actors;

namespace SlimeBattle.Status
{
    public readonly struct EffectAppliedEvent
    {
        public EffectAppliedEvent(EffectRequest request, ResolvedEffect effect,
            EffectApplyResult result)
        {
            Request = request;
            Effect = effect;
            Result = result;
        }

        public EffectRequest Request { get; }
        public ResolvedEffect Effect { get; }
        public EffectApplyResult Result { get; }
    }
}
