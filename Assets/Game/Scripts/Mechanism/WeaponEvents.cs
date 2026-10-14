using SlimeBattle.Actors;
using SlimeBattle.Status;

namespace SlimeBattle.Mechanisms
{
    public readonly struct WeaponDamagedEvent
    {
        public WeaponDamagedEvent(EscortWeaponSnapshot snapshot, ActorId sourceActorId,
            EffectRequestId requestId, int actualHealthLoss)
        { Snapshot = snapshot; SourceActorId = sourceActorId; RequestId = requestId;
            ActualHealthLoss = actualHealthLoss; }
        public EscortWeaponSnapshot Snapshot { get; }
        public ActorId SourceActorId { get; }
        public EffectRequestId RequestId { get; }
        public int ActualHealthLoss { get; }
    }

    public readonly struct WeaponDestroyedEvent
    {
        public WeaponDestroyedEvent(EscortWeaponSnapshot snapshot) { Snapshot = snapshot; }
        public EscortWeaponSnapshot Snapshot { get; }
    }

    public readonly struct WeaponReachedGoalEvent
    {
        public WeaponReachedGoalEvent(EscortWeaponSnapshot snapshot) { Snapshot = snapshot; }
        public EscortWeaponSnapshot Snapshot { get; }
    }
}
