/* 史莱姆本能决策：状态优先级与目标选择留在正式代码，场景只提供感知事实。 */
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Actors.Slimes
{
    internal sealed class BasicSlimeDecision : DecisionNode
    {
        private readonly SlimeRuntimeData runtime;
        private readonly float attackRange;
        private readonly ConfigId abilityId;
        public BasicSlimeDecision(SlimeRuntimeData runtime, float attackRange, ConfigId abilityId)
        {
            this.runtime = runtime;
            this.attackRange = attackRange;
            this.abilityId = abilityId;
        }

        public override ActionIntent Evaluate(DecisionContext context)
        {
            var statuses = context.Actor.StatusIds;
            if (SlimeStatusIds.Contains(statuses, SlimeStatusIds.Afraid))
            {
                runtime.TargetId = default(ActorId);
                return context.Perception.HasEscapeDestination
                    ? ActionIntent.Move(context.Perception.EscapeDestination, "害怕：离开危险，不响应引导源")
                    : ActionIntent.Wait("害怕：等待可用逃跑路线");
            }

            // 同时饥饿和寒冷时先满足饥饿；策划尚未规定二者的相互优先级。
            if (SlimeStatusIds.Contains(statuses, SlimeStatusIds.Hungry) &&
                TryFindGuide(context, GuidanceKind.Food, out var food))
            {
                runtime.TargetId = default(ActorId);
                return ActionIntent.Move(food, "饥饿：优先靠近食物");
            }
            if (SlimeStatusIds.Contains(statuses, SlimeStatusIds.Cold) &&
                TryFindGuide(context, GuidanceKind.Warmth, out var warmth))
            {
                runtime.TargetId = default(ActorId);
                return ActionIntent.Move(warmth, "寒冷：优先靠近热源");
            }

            var agitated = SlimeStatusIds.Contains(statuses, SlimeStatusIds.Agitated);
            var targets = context.Perception.Targets;
            var nearestDistance = float.PositiveInfinity;
            ActorSnapshot nearest = default(ActorSnapshot);
            var nearestHuman = false;
            if (targets != null)
            {
                foreach (var observation in targets)
                {
                    if (!observation.IsVisible || !observation.Actor.IsAlive ||
                        !(observation.IsHostile || (agitated && observation.IsHuman))) continue;
                    var distance = Vector2.Distance(context.Actor.Position, observation.Actor.Position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = observation.Actor;
                        nearestHuman = observation.IsHuman;
                    }
                }
            }

            if (nearest.ActorId.IsValid)
            {
                runtime.TargetId = nearest.ActorId;
                var healthRatio = context.Actor.CurrentHealth / context.Actor.MaxHealth;
                if (healthRatio < .5f && context.Perception.FleeCheckSucceeded &&
                    context.Perception.HasEscapeDestination)
                    return ActionIntent.Move(context.Perception.EscapeDestination,
                        healthRatio < .3f ? "生命低于30%：逃离战斗" : "生命低于50%：逃离战斗");
                return nearestDistance <= attackRange
                    ? ActionIntent.AttackTarget(new AttackIntent(context.Actor.ActorId, nearest.ActorId, abilityId),
                        agitated && nearestHuman ? "暴躁：攻击附近人类" : "遇到魔物：攻击")
                    : ActionIntent.Move(nearest.Position,
                        agitated && nearestHuman ? "暴躁：接近附近人类" : "遇到魔物：接近战斗");
            }
            runtime.TargetId = default(ActorId);
            return context.Perception.HasDestination
                ? ActionIntent.Move(context.Perception.Destination, "跟随普通引导")
                : runtime.GroupId.IsValid
                    ? ActionIntent.Move(runtime.GroupTarget, "跟随群体目标")
                    : ActionIntent.Wait("没有可用目标：等待");
        }

        private static bool TryFindGuide(DecisionContext context, GuidanceKind kind, out Vector2 position)
        {
            position = default(Vector2);
            var guides = context.Perception.Guides;
            if (guides == null) return false;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < guides.Count; i++)
            {
                if (guides[i].Kind != kind) continue;
                var distance = Vector2.Distance(context.Actor.Position, guides[i].Position);
                if (distance >= nearest) continue;
                nearest = distance;
                position = guides[i].Position;
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
