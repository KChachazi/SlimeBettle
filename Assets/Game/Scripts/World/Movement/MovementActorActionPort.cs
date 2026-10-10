using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Actors;
using SlimeBattle.Status;

namespace SlimeBattle.World.Movement
{
    /// <summary>Routes actor movement to MovementSystem and leaves effects with the existing port.</summary>
    internal sealed class MovementActorActionPort : IActorActionPort, IActorModifierProvider
    {
        private readonly MovementSystem movementSystem;
        private readonly IActorActionPort effectPort;
        private readonly StatusManager statusManager;

        public MovementActorActionPort(MovementSystem movementSystem,
            IActorActionPort effectPort, StatusManager statusManager = null)
        {
            this.movementSystem = movementSystem;
            this.effectPort = effectPort;
            this.statusManager = statusManager;
        }

        public string SubmitMovement(ActorMovementRequest request)
        {
            var requestId = movementSystem.SubmitMovement(
                new MovementRequest(request.ActorId, request.Destination));
            return requestId.Value ?? string.Empty;
        }

        public void CancelMovement(string requestId)
        {
            if (MovementRequestId.TryParse(requestId, out var id))
                movementSystem.CancelMovement(id, CancelReason.Interrupted);
        }

        public void ApplyEffect(ActorEffectRequest request)
        {
            if (statusManager != null)
            {
                statusManager.ApplyEffect(new EffectRequest(request.RequestId,
                    request.SourceActorId, request.TargetActorId, EffectType.Damage,
                    request.BaseAmount, contextConfigId: request.AbilityId,
                    relationshipPolicy: EffectRelationshipPolicy.HostileOnly));
                return;
            }
            effectPort?.ApplyEffect(request);
        }

        public float GetAttackSpeedMultiplier(ActorId actorId) =>
            statusManager == null ? 1f : statusManager.GetAttackSpeedMultiplier(actorId);
    }
}
