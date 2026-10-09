/* 敌人生命周期与控制的已发生结果。 */
using SlimeBattle.Actors;

namespace SlimeBattle.Enemies
{
    public readonly struct EnemySpawnedEvent
    {
        public EnemySpawnedEvent(EnemySnapshot snapshot) { Snapshot = snapshot; }
        public EnemySnapshot Snapshot { get; }
    }
    public readonly struct EnemyStateChangedEvent
    {
        public EnemyStateChangedEvent(EnemySnapshot snapshot) { Snapshot = snapshot; }
        public EnemySnapshot Snapshot { get; }
    }
    public readonly struct EnemyDiedEvent
    {
        public EnemyDiedEvent(EnemySnapshot snapshot, ResolvedEffect effect)
        { Snapshot = snapshot; Effect = effect; }
        public EnemySnapshot Snapshot { get; }
        public ResolvedEffect Effect { get; }
    }
    public readonly struct EnemyDespawnedEvent
    {
        public EnemyDespawnedEvent(ActorId actorId, DespawnReason reason)
        { ActorId = actorId; Reason = reason; }
        public ActorId ActorId { get; }
        public DespawnReason Reason { get; }
    }
}
