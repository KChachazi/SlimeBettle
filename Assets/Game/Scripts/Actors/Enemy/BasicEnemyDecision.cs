/* P0 敌人决策：只在活动范围内选择最近可见敌对目标。 */
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Actors.Enemies
{
    internal sealed class BasicEnemyDecision : DecisionNode
    {
        private readonly EnemyRuntimeData runtime;
        private readonly float attackRange;
        private readonly ConfigId abilityId;
        public BasicEnemyDecision(EnemyRuntimeData runtime, float attackRange, ConfigId abilityId)
        {
            this.runtime = runtime;
            this.attackRange = attackRange;
            this.abilityId = abilityId;
        }

        public override ActionIntent Evaluate(DecisionContext context)
        {
            var nearestDistance = float.PositiveInfinity;
            ActorSnapshot nearest = default(ActorSnapshot);
            var targets = context.Perception.Targets;
            if (targets != null)
            {
                foreach (var observation in targets)
                {
                    var actor = observation.Actor;
                    if (!observation.IsVisible || !observation.IsHostile || !actor.IsAlive ||
                        !runtime.ActivityBounds.Contains(actor.Position)) continue;
                    var distance = Vector2.Distance(context.Actor.Position, actor.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = actor;
                    }
                }
            }
            runtime.TargetId = nearest.ActorId;
            if (!nearest.ActorId.IsValid)
            {
                if (context.Perception.HasDestination &&
                    runtime.ActivityBounds.Contains(context.Perception.Destination))
                    return ActionIntent.Move(context.Perception.Destination, "无目标：沿引导目的地移动");
                if (TryFindWanderDestination(context, out var wander) &&
                    runtime.ActivityBounds.Contains(wander))
                    return ActionIntent.Move(wander, "无目标：随机游走");
                return ActionIntent.Wait("无目标：留在活动范围内");
            }
            return nearestDistance <= attackRange
                ? ActionIntent.AttackTarget(new AttackIntent(context.Actor.ActorId, nearest.ActorId, abilityId),
                    "索敌：攻击最近目标")
                : ActionIntent.Move(nearest.Position, "索敌：追击最近目标");
        }

        private static bool TryFindWanderDestination(DecisionContext context, out Vector2 position)
        {
            position = default(Vector2);
            var guides = context.Perception.Guides;
            if (guides == null) return false;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < guides.Count; i++)
            {
                if (guides[i].Kind != GuidanceKind.Wander) continue;
                var distance = Vector2.Distance(context.Actor.Position, guides[i].Position);
                if (distance >= nearest) continue;
                nearest = distance;
                position = guides[i].Position;
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
