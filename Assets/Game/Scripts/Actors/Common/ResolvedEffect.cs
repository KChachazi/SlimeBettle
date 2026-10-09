/* 统一状态系统已结算的效果；Actor 只接受结果，不自行计算攻击规则。 */
namespace SlimeBattle.Actors
{
    public readonly struct ResolvedEffect
    {
        public ResolvedEffect(EffectType type, float amount,
            ActorId sourceActorId = default(ActorId), string statusId = null)
        {
            Type = type;
            Amount = amount;
            SourceActorId = sourceActorId;
            StatusId = statusId ?? string.Empty;
        }
        public EffectType Type { get; }
        public float Amount { get; }
        public ActorId SourceActorId { get; }
        public string StatusId { get; }
        public bool IsDamage => Type == EffectType.Damage;
        public bool IsHeal => Type == EffectType.Heal;
        public static ResolvedEffect Damage(float amount, ActorId sourceActorId = default(ActorId))
            => new ResolvedEffect(EffectType.Damage, amount, sourceActorId);
        public static ResolvedEffect Heal(float amount, ActorId sourceActorId = default(ActorId))
            => new ResolvedEffect(EffectType.Heal, amount, sourceActorId);
    }
}
