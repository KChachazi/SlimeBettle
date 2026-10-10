/* Actor 对已结算效果的接收结果。 */
namespace SlimeBattle.Actors
{
    public readonly struct EffectApplyResult
    {
        private EffectApplyResult(EffectApplyOutcome outcome, bool changed,
            bool didKill, float appliedAmount, float remainingHealth, string reason)
        {
            Outcome = outcome;
            IsAccepted = outcome == EffectApplyOutcome.Applied ||
                outcome == EffectApplyOutcome.NoChange;
            Changed = changed;
            DidKill = didKill;
            AppliedAmount = appliedAmount;
            RemainingHealth = remainingHealth;
            Reason = reason ?? string.Empty;
        }
        public EffectApplyOutcome Outcome { get; }
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
            => new EffectApplyResult(appliedAmount > 0f
                    ? EffectApplyOutcome.Applied : EffectApplyOutcome.NoChange,
                appliedAmount > 0f, didKill,
                appliedAmount, remainingHealth, string.Empty);
        public static EffectApplyResult AcceptedStatus(bool changed, float remainingHealth)
            => new EffectApplyResult(changed ? EffectApplyOutcome.Applied : EffectApplyOutcome.NoChange,
                changed, false, 0f, remainingHealth, string.Empty);
        public static EffectApplyResult Rejected(string reason)
            => new EffectApplyResult(EffectApplyOutcome.Rejected, false, false, 0f, 0f, reason);
        public static EffectApplyResult TargetUnavailable(string reason)
            => new EffectApplyResult(EffectApplyOutcome.TargetUnavailable,
                false, false, 0f, 0f, reason);
        public static EffectApplyResult Immune(string reason)
            => new EffectApplyResult(EffectApplyOutcome.Immune, false, false, 0f, 0f, reason);
        public static EffectApplyResult Duplicate(string reason)
            => new EffectApplyResult(EffectApplyOutcome.Duplicate, false, false, 0f, 0f, reason);
    }
}
