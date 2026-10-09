/* Actor 对已结算效果的接收结果。 */
namespace SlimeBattle.Actors
{
    public readonly struct EffectApplyResult
    {
        private EffectApplyResult(bool isAccepted, bool changed, bool didKill,
            float appliedAmount, float remainingHealth, string reason)
        {
            IsAccepted = isAccepted;
            Changed = changed;
            DidKill = didKill;
            AppliedAmount = appliedAmount;
            RemainingHealth = remainingHealth;
            Reason = reason ?? string.Empty;
        }
        public bool IsAccepted { get; }
        public bool Applied => IsAccepted;
        public bool Changed { get; }
        public bool DidKill { get; }
        public bool Killed => DidKill;
        public float AppliedAmount { get; }
        public float RemainingHealth { get; }
        public string Reason { get; }
        public static EffectApplyResult Accepted(float appliedAmount,
            float remainingHealth, bool didKill = false)
            => new EffectApplyResult(true, appliedAmount > 0f, didKill,
                appliedAmount, remainingHealth, string.Empty);
        public static EffectApplyResult Rejected(string reason)
            => new EffectApplyResult(false, false, false, 0f, 0f, reason);
        public static EffectApplyResult AcceptedStatus(bool changed, float remainingHealth)
            => new EffectApplyResult(true, changed, false, 0f, remainingHealth, string.Empty);
    }
}
