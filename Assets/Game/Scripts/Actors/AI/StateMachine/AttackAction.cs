/* AttackAction 只做命中时机的最终检查并提交一次效果请求，不修改生命。 */
using SlimeBattle.Actors.AI.DecisionTree;
using UnityEngine;

namespace SlimeBattle.Actors.AI.StateMachine
{
    public sealed class AttackAction
    {
        public bool ExecuteHit(AttackIntent intent, ActorSnapshot attacker,
            float attackRange, float baseAmount, IActorPerceptionSource source,
            IActorActionPort port)
        {
            if (!intent.IsValid || !attacker.IsAlive ||
                attacker.ActorId != intent.AttackerId ||
                attacker.ControlMode != ActorControlMode.Autonomous ||
                source == null || port == null ||
                !source.TryGetActorSnapshot(intent.TargetId, out var target) ||
                !target.IsAlive || target.FactionId == attacker.FactionId ||
                Vector2.Distance(attacker.Position, target.Position) > attackRange)
            {
                return false;
            }

            port.ApplyEffect(new ActorEffectRequest(
                intent.AttackerId, intent.TargetId, intent.AbilityId, baseAmount));
            return true;
        }
    }
}
