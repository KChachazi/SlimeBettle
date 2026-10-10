using SlimeBattle.Actors;

namespace SlimeBattle.Mechanisms
{
    public readonly struct MechanismTriggeredEvent
    {
        public MechanismTriggeredEvent(MechanismActivationResult result)
        {
            Result = result;
        }

        public MechanismActivationResult Result { get; }
        public MechanismId MechanismId => Result.MechanismId;
        public ActorId ActorId => Result.ActorId;
    }

    public readonly struct MechanismStateChangedEvent
    {
        public MechanismStateChangedEvent(MechanismSnapshot snapshot,
            MechanismState previousState, string reason)
        {
            Snapshot = snapshot;
            PreviousState = previousState;
            Reason = reason ?? string.Empty;
        }

        public MechanismSnapshot Snapshot { get; }
        public MechanismState PreviousState { get; }
        public MechanismState State => Snapshot.State;
        public string Reason { get; }
    }

    public readonly struct MechanismExhaustedEvent
    {
        public MechanismExhaustedEvent(MechanismSnapshot snapshot, ActorId actorId)
        {
            Snapshot = snapshot;
            ActorId = actorId;
        }

        public MechanismSnapshot Snapshot { get; }
        public ActorId ActorId { get; }
    }

    public readonly struct MechanismPlacementCooldownChangedEvent
    {
        public MechanismPlacementCooldownChangedEvent(PlacementCooldown cooldown)
        {
            Cooldown = cooldown;
        }

        public PlacementCooldown Cooldown { get; }
    }
}
