/* 史莱姆本能决策：状态优先级与目标选择留在正式代码，场景只提供感知事实。 */
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Data;
using SlimeBattle.Status;
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
                    : TryFindGuide(context, GuidanceKind.Wander, out var fearWander)
                        ? ActionIntent.Move(fearWander, "Afraid: ignore guides and wander")
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
            var mayAttackHumans = agitated && context.StatusQuery != null &&
                context.StatusQuery.TryGetStatus(context.Actor.ActorId,
                    new StatusId(SlimeStatusIds.Agitated), out var rage) &&
                rage.ElapsedDurationSeconds >= rage.HumanAttackDelaySeconds;
            var targets = context.Perception.Targets;
            var nearestDistance = float.PositiveInfinity;
            ActorSnapshot nearest = default(ActorSnapshot);
            var nearestHuman = false;
            if (targets != null)
            {
                foreach (var observation in targets)
                {
                    if (!observation.IsVisible || !observation.Actor.IsAlive ||
                        (observation.IsHuman && !mayAttackHumans) ||
                        !(observation.IsHostile || (mayAttackHumans && observation.IsHuman))) continue;
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
                    return ActionIntent.MoveAndApplyStatus(context.Perception.EscapeDestination,
                        new StatusId(SlimeStatusIds.Afraid), 10f,
                        healthRatio < .3f ? "生命低于30%：逃离战斗" : "生命低于50%：逃离战斗");
                return nearestDistance <= attackRange
                    ? ActionIntent.AttackTarget(new AttackIntent(context.Actor.ActorId, nearest.ActorId, abilityId),
                        agitated && nearestHuman ? "暴躁：攻击附近人类" : "遇到魔物：攻击")
                    : ActionIntent.Move(nearest.Position,
                        agitated && nearestHuman ? "暴躁：接近附近人类" : "遇到魔物：接近战斗");
            }

            // 没有更高优先级的需求或战斗目标时，正常/暴躁状态接受食物和熔炉的普通引导。
            // 引导列表已经由感知层限制在有效吸引范围内，这里按距离选择最近的来源。
            if (TryFindNearestGuide(context, out var guide))
            {
                runtime.TargetId = default(ActorId);
                return ActionIntent.Move(guide, "正常/暴躁：优先靠近引导源");
            }

            runtime.TargetId = default(ActorId);
            return context.Perception.HasDestination
                ? ActionIntent.Move(context.Perception.Destination, "跟随普通引导")
                : TryFindGuide(context, GuidanceKind.Wander, out var wander)
                    ? ActionIntent.Move(wander, "没有其他目标：随机游走")
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

        private static bool TryFindNearestGuide(DecisionContext context, out Vector2 position)
        {
            position = default(Vector2);
            var guides = context.Perception.Guides;
            if (guides == null) return false;
            var nearest = float.PositiveInfinity;
            for (var i = 0; i < guides.Count; i++)
            {
                if (guides[i].Kind != GuidanceKind.Food &&
                    guides[i].Kind != GuidanceKind.Warmth) continue;
                var distance = Vector2.Distance(context.Actor.Position, guides[i].Position);
                if (distance >= nearest) continue;
                nearest = distance;
                position = guides[i].Position;
            }
            return nearest < float.PositiveInfinity;
        }
    }
}
