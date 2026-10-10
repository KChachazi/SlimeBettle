using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Actors;
using SlimeBattle.Status;

namespace SlimeBattle.World.Movement
{
    /// <summary>Routes actor movement to MovementSystem and leaves effects with the existing port.</summary>
    internal sealed class MovementActorActionPort : IActorActionPort, IActorModifierProvider,
        IActorStatusRequestPort, IActorStatusQuery
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

        public bool ApplyStatus(ActorId actorId, StatusId statusId, float durationSeconds = -1f) =>
            statusManager != null && statusManager.ApplyStatus(new StatusApplicationRequest(
                default(ActorId), actorId, statusId, durationSeconds));

        public bool HasStatus(ActorId actorId, StatusId statusId) =>
            statusManager != null && statusManager.HasStatus(actorId, statusId);

        public bool HasStatus(ActorId actorId, StatusKind kind) =>
            statusManager != null && statusManager.HasStatus(actorId, kind);

        public bool TryGetStatus(ActorId actorId, StatusId statusId,
            out StatusEntrySnapshot status)
        {
            if (statusManager != null)
                return statusManager.TryGetStatus(actorId, statusId, out status);
            status = default(StatusEntrySnapshot);
            return false;
        }

        public bool TryGetStatusSnapshot(ActorId actorId, out StatusSnapshot snapshot)
        {
            if (statusManager != null)
                return statusManager.TryGetStatusSnapshot(actorId, out snapshot);
            snapshot = default(StatusSnapshot);
            return false;
        }
    }
}
