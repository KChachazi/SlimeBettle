using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Actors.Guards
{
    internal sealed class GuardDecision : DecisionNode
    {
        private readonly ConfigId abilityId;
        private readonly float attackRange;
        private readonly float leashDistance;

        public GuardDecision(GuardConfig config)
        {
            abilityId = config.ConfigId;
            attackRange = config.AttackRange;
            leashDistance = config.LeashDistance;
        }

        public override ActionIntent Evaluate(DecisionContext context)
        {
            if (!context.Perception.HasDestination)
                return ActionIntent.Wait("Guard post unavailable.");

            var standPosition = context.Perception.Destination;
            if (Vector2.Distance(context.Actor.Position, standPosition) > leashDistance)
                return ActionIntent.Move(standPosition, "Return to guard post.");

            var nearestDistance = float.PositiveInfinity;
            var nearest = default(ActorSnapshot);
            foreach (var observation in context.Perception.Targets)
            {
                if (!observation.IsVisible || !observation.IsHostile ||
                    !observation.Actor.IsAlive ||
                    observation.Actor.ActorType != ActorType.Enemy ||
                    Vector2.Distance(observation.Actor.Position, standPosition) >
                        leashDistance) continue;
                var distance = Vector2.Distance(context.Actor.Position,
                    observation.Actor.Position);
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = observation.Actor;
            }

            if (nearest.ActorId.IsValid)
                return nearestDistance <= attackRange
                    ? ActionIntent.AttackTarget(new AttackIntent(context.Actor.ActorId,
                        nearest.ActorId, abilityId), "Defend guard post.")
                    : ActionIntent.Move(nearest.Position, "Intercept nearby enemy.");

            return Vector2.Distance(context.Actor.Position,
                standPosition) > .1f
                ? ActionIntent.Move(standPosition, "Return to guard post.")
                : ActionIntent.Wait("Hold guard post.");
        }
    }
}
