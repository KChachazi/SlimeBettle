using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Actors.Guards
{
    internal sealed class GuardDecision : DecisionNode
    {
        private const float EscortArrivalTolerance = 0.05f;
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

            var nearest = FindNearest(context, standPosition,
                observation => observation.IsAttackingWeapon);
            if (!nearest.ActorId.IsValid)
                nearest = FindNearest(context, standPosition,
                    observation => observation.IsAttackingGuard ||
                        observation.IsRetaliationTarget);
            if (!nearest.ActorId.IsValid)
                nearest = FindNearest(context, standPosition,
                    observation => observation.IsNearWeapon &&
                        observation.IsAttacking &&
                        observation.Actor.IsAlive && observation.IsHostile);

            var nearestDistance = nearest.ActorId.IsValid
                ? Vector2.Distance(context.Actor.Position, nearest.Position)
                : float.PositiveInfinity;

            if (nearest.ActorId.IsValid)
                return nearestDistance <= attackRange
                    ? ActionIntent.AttackTarget(new AttackIntent(context.Actor.ActorId,
                        nearest.ActorId, abilityId), "Defend guard post.")
                    : ActionIntent.Move(nearest.Position, "Intercept nearby enemy.");

            return Vector2.Distance(context.Actor.Position,
                standPosition) > EscortArrivalTolerance
                ? ActionIntent.Move(standPosition, "Return to guard post.")
                : ActionIntent.Wait("Hold guard post.");
        }

        private ActorSnapshot FindNearest(DecisionContext context, Vector2 standPosition,
            System.Func<TargetObservation, bool> predicate)
        {
            var nearestDistance = float.PositiveInfinity;
            var nearest = default(ActorSnapshot);
            foreach (var observation in context.Perception.Targets)
            {
                if (!observation.IsVisible || !observation.IsHostile ||
                    !observation.Actor.IsAlive ||
                    (observation.Actor.ActorType != ActorType.Enemy &&
                     !(observation.Actor.ActorType == ActorType.Slime &&
                       observation.IsRetaliationTarget)) ||
                    !predicate(observation) ||
                    Vector2.Distance(observation.Actor.Position, standPosition) >
                        leashDistance) continue;
                var distance = Vector2.Distance(context.Actor.Position,
                    observation.Actor.Position);
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = observation.Actor;
            }
            return nearest;
        }
    }
}
