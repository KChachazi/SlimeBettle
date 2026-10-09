/* 攻击意图只携带身份与技能配置 ID，不携带已结算伤害。 */
using SlimeBattle.Data;

namespace SlimeBattle.Actors.AI.DecisionTree
{
    public readonly struct AttackIntent
    {
        public AttackIntent(ActorId attackerId, ActorId targetId, ConfigId abilityId)
        {
            AttackerId = attackerId;
            TargetId = targetId;
            AbilityId = abilityId;
        }

        public ActorId AttackerId { get; }
        public ActorId TargetId { get; }
        public ConfigId AbilityId { get; }
        public bool IsValid => AttackerId.IsValid && TargetId.IsValid && AbilityId.IsValid;
    }
}
