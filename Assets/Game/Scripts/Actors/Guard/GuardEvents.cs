using SlimeBattle.Actors;

namespace SlimeBattle.Guards
{
    public readonly struct GuardSpawnedEvent
    {
        public GuardSpawnedEvent(GuardSnapshot snapshot) { Snapshot = snapshot; }
        public GuardSnapshot Snapshot { get; }
    }

    public readonly struct GuardDiedEvent
    {
        public GuardDiedEvent(GuardSnapshot snapshot, ResolvedEffect effect)
        { Snapshot = snapshot; Effect = effect; }
        public GuardSnapshot Snapshot { get; }
        public ResolvedEffect Effect { get; }
    }

    public readonly struct GuardDespawnedEvent
    {
        public GuardDespawnedEvent(ActorId actorId, DespawnReason reason)
        { ActorId = actorId; Reason = reason; }
        public ActorId ActorId { get; }
        public DespawnReason Reason { get; }
    }
}
