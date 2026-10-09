using SlimeBattle.Actors.AI.StateMachine;

namespace SlimeBattle.World.Movement
{
    /// <summary>Routes actor movement to MovementSystem and leaves effects with the existing port.</summary>
    internal sealed class MovementActorActionPort : IActorActionPort
    {
        private readonly MovementSystem movementSystem;
        private readonly IActorActionPort effectPort;

        public MovementActorActionPort(MovementSystem movementSystem, IActorActionPort effectPort)
        {
            this.movementSystem = movementSystem;
            this.effectPort = effectPort;
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
            effectPort?.ApplyEffect(request);
        }
    }
}
