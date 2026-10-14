using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Actors.Guards
{
    internal sealed class GuardDecision : DecisionNode
    {
        private readonly ConfigId abilityId;
        private readonly float attackRange;
        private readonly float escortDistance;

        public GuardDecision(GuardConfig config)
        {
            abilityId = config.ConfigId;
            attackRange = config.AttackRange;
            escortDistance = config.EscortDistance;
        }

        public override ActionIntent Evaluate(DecisionContext context)
        {
            if (!context.Perception.HasDestination)
                return ActionIntent.Wait("Escort weapon unavailable.");

            var nearestDistance = float.PositiveInfinity;
            var nearest = default(ActorSnapshot);
            foreach (var observation in context.Perception.Targets)
            {
                if (!observation.IsVisible || !observation.IsHostile ||
                    !observation.Actor.IsAlive) continue;
                var distance = Vector2.Distance(context.Actor.Position,
                    observation.Actor.Position);
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearest = observation.Actor;
            }

            if (nearest.ActorId.IsValid)
                return nearestDistance <= attackRange
                    ? ActionIntent.AttackTarget(new AttackIntent(context.Actor.ActorId,
                        nearest.ActorId, abilityId), "Defend escort weapon.")
                    : ActionIntent.Move(nearest.Position, "Intercept nearby enemy.");

            return Vector2.Distance(context.Actor.Position,
                context.Perception.Destination) > escortDistance
                ? ActionIntent.Move(context.Perception.Destination, "Follow escort weapon.")
                : ActionIntent.Wait("Hold escort position.");
        }
    }
}
