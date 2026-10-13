/* 史莱姆生命周期的已发生结果；不用于提交命令。 */
using SlimeBattle.Actors;
using SlimeBattle.Actors.SlimeGroups;
using SlimeBattle.Core;

namespace SlimeBattle.Slimes
{
    public readonly struct SlimeDepletedEvent
    {
        public SlimeDepletedEvent(LevelId levelId) { LevelId = levelId; AliveCount = 0; }
        public LevelId LevelId { get; }
        public int AliveCount { get; }
    }
    public readonly struct SlimeSpawnedEvent
    {
        public SlimeSpawnedEvent(SlimeSnapshot snapshot) { Snapshot = snapshot; }
        public SlimeSnapshot Snapshot { get; }
    }
    public readonly struct SlimeControlChangedEvent
    {
        public SlimeControlChangedEvent(SlimeSnapshot snapshot) { Snapshot = snapshot; }
        public SlimeSnapshot Snapshot { get; }
    }
    public readonly struct SlimeDiedEvent
    {
        public SlimeDiedEvent(SlimeSnapshot snapshot, ResolvedEffect effect)
        { Snapshot = snapshot; Effect = effect; }
        public SlimeSnapshot Snapshot { get; }
        public ResolvedEffect Effect { get; }
    }
    public readonly struct SlimeDespawnedEvent
    {
        public SlimeDespawnedEvent(ActorId actorId, DespawnReason reason)
        { ActorId = actorId; Reason = reason; }
        public ActorId ActorId { get; }
        public DespawnReason Reason { get; }
    }
    public readonly struct SlimeGroupCreatedEvent
    {
        public SlimeGroupCreatedEvent(SlimeGroupSnapshot snapshot) { Snapshot = snapshot; }
        public SlimeGroupSnapshot Snapshot { get; }
    }
    public readonly struct SlimeGroupDissolvedEvent
    {
        public SlimeGroupDissolvedEvent(GroupId groupId) { GroupId = groupId; }
        public GroupId GroupId { get; }
    }
}
