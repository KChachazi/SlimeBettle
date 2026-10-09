/* P0 群体只维护成员身份和共同目标；自动成群阈值与阵型尚待策划确认。 */
using System.Collections.Generic;
using SlimeBattle.Actors;
using UnityEngine;

namespace SlimeBattle.Actors.SlimeGroups
{
    public readonly struct SlimeGroupSnapshot
    {
        public SlimeGroupSnapshot(GroupId groupId, IReadOnlyList<ActorId> memberIds,
            Vector2 target)
        {
            GroupId = groupId;
            MemberIds = memberIds;
            Target = target;
        }
        public GroupId GroupId { get; }
        public IReadOnlyList<ActorId> MemberIds { get; }
        public Vector2 Target { get; }
    }

    public sealed class SlimeGroupActor
    {
        private readonly HashSet<ActorId> members = new HashSet<ActorId>();
        private bool disposed;

        public SlimeGroupActor(GroupId groupId, Vector2 target)
        {
            GroupId = groupId;
            Target = target;
        }
        public GroupId GroupId { get; }
        public Vector2 Target { get; private set; }
        public int Count => members.Count;

        public bool TryJoinMember(ActorId actorId)
        {
            return !disposed && actorId.IsValid && members.Add(actorId);
        }
        public bool RemoveMember(ActorId actorId) { return !disposed && members.Remove(actorId); }
        public void NotifyTargetChanged(Vector2 target) { if (!disposed) Target = target; }
        public SlimeGroupSnapshot GetSnapshot() => new SlimeGroupSnapshot(GroupId,
            new List<ActorId>(members).AsReadOnly(), Target);
        public void Dispose() { members.Clear(); disposed = true; }
    }
}
