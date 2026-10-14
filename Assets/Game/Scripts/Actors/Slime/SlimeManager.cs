/* SlimeManager 只拥有同类 Actor 的集合、同屏上限与生命周期，不复制生命。 */
using System;
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Actors.SlimeGroups;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.World.Movement;
using UnityEngine;

namespace SlimeBattle.Slimes
{
    public sealed class SlimeManager : IDisposable
    {
        private readonly Dictionary<ActorId, SlimeActor> actors = new Dictionary<ActorId, SlimeActor>();
        private readonly Dictionary<GroupId, SlimeGroupActor> groups =
            new Dictionary<GroupId, SlimeGroupActor>();
        private readonly ConfigRegistry configs;
        private readonly ILevelEventBus eventBus;
        private readonly LevelId levelId;
        private readonly int maxAliveCount;
        private readonly SlimeActor prefab;
        private readonly MovementSystem movementSystem;
        private Transform actorParent;
        private bool disposed;
        private bool paused;
        private bool depletedPublished;

        public SlimeManager(ConfigRegistry configs, ILevelEventBus eventBus,
            int maxAliveCount = 30, SlimeActor prefab = null, Transform actorParent = null,
            MovementSystem movementSystem = null, LevelId levelId = default(LevelId))
        {
            if (maxAliveCount < 1 || maxAliveCount > 50)
                throw new ArgumentOutOfRangeException(nameof(maxAliveCount));
            this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
            this.eventBus = eventBus;
            this.levelId = levelId;
            this.maxAliveCount = maxAliveCount;
            this.prefab = prefab;
            this.actorParent = actorParent;
            this.movementSystem = movementSystem;
        }

        public string LastFailureReason { get; private set; } = string.Empty;
        public int MaxAliveCount => maxAliveCount;

        // 场景装配方指定后续生成实例的父节点；Actor 仍由 Manager 创建和销毁。
        public bool TrySetSpawnParent(Transform parent)
        {
            if (disposed || parent == null) return false;
            actorParent = parent;
            return true;
        }

        public ActorId SpawnSlime(SlimeSpawnRequest request)
        {
            LastFailureReason = string.Empty;
            if (disposed) return Reject("SlimeManager is disposed.");
            if (paused) return Reject("The level is paused.");
            if (prefab == null) return Reject("SlimeActor prefab is missing.");
            if (GetAliveCount() >= maxAliveCount) return Reject("Slime alive-count limit reached.");
            if (!configs.TryGet(request.ConfigId, out SlimeConfig config))
                return Reject("SlimeConfig is missing or ambiguous.");
            if (config.MaxHealth <= 0) return Reject("SlimeConfig health is invalid.");

            var id = ActorId.NewId();
            var actor = UnityEngine.Object.Instantiate(prefab,
                new Vector3(request.Position.x, request.Position.y, 0f),
                Quaternion.identity, actorParent);
            actor.Initialize(config, id, request.Position,
                request.FactionId.IsValid ? request.FactionId : new FactionId("Slime"));
            actors.Add(id, actor);
            if (!TryRegisterMovement(actor, config.MoveSpeed))
            {
                actors.Remove(id);
                DestroyActor(actor);
                return Reject("MovementSystem could not register the Slime Rigidbody2D.");
            }
            actor.Died += OnActorDied;
            actor.ControlModeChanged += OnControlModeChanged;
            actor.AttackedWhileCarrying += OnAttackedWhileCarrying;
            eventBus?.Publish(new SlimeSpawnedEvent(actor.GetSnapshot()));
            return id;
        }

        public bool RequestDespawn(ActorId actorId, DespawnReason reason)
        {
            if (disposed || !actors.TryGetValue(actorId, out var actor)) return false;
            actor.Died -= OnActorDied;
            actor.ControlModeChanged -= OnControlModeChanged;
            actor.AttackedWhileCarrying -= OnAttackedWhileCarrying;
            movementSystem?.UnregisterActor(actorId, CancelReason.Despawn);
            DestroyActor(actor);
            actors.Remove(actorId);
            RemoveFromGroups(actorId);
            eventBus?.Publish(new SlimeDespawnedEvent(actorId, reason));
            PublishDepletedIfNeeded();
            return true;
        }

        public bool TryGetSnapshot(ActorId actorId, out SlimeSnapshot snapshot)
        {
            snapshot = default(SlimeSnapshot);
            if (disposed || !actors.TryGetValue(actorId, out var actor)) return false;
            snapshot = actor.GetSnapshot();
            return true;
        }

        internal bool TryAttachCarrier(ActorId actorId, Transform weapon,
            Vector2 localOffset)
        {
            if (disposed || paused || !actors.TryGetValue(actorId, out var actor) ||
                !actor.IsAlive || actor.IsCarrying || weapon == null) return false;
            movementSystem?.UnregisterActor(actorId, CancelReason.Interrupted);
            RemoveFromGroups(actorId);
            return actor.AttachToWeapon(weapon, localOffset);
        }

        internal bool TryDetachCarrier(ActorId actorId)
        {
            if (disposed || !actors.TryGetValue(actorId, out var actor) ||
                !actor.DetachFromWeapon(actorParent)) return false;
            if (configs.TryGet(actor.GetSnapshot().ConfigId, out SlimeConfig config))
                TryRegisterMovement(actor, config.MoveSpeed);
            return true;
        }

        public IReadOnlyList<SlimeSnapshot> GetAliveSnapshots()
        {
            var result = new List<SlimeSnapshot>();
            if (!disposed)
                foreach (var actor in actors.Values)
                    if (actor.IsAlive) result.Add(actor.GetSnapshot());
            return result.AsReadOnly();
        }

        public bool TryGetGroupSnapshot(GroupId groupId, out SlimeGroupSnapshot snapshot)
        {
            snapshot = default(SlimeGroupSnapshot);
            if (disposed || !groups.TryGetValue(groupId, out var group)) return false;
            snapshot = group.GetSnapshot();
            return true;
        }

        // P0 只接受明确的成员集合；自动成群规则尚未确定。
        public bool TryCreateGroup(IReadOnlyList<ActorId> members,
            UnityEngine.Vector2 target, out GroupId groupId)
        {
            groupId = default(GroupId);
            if (disposed || members == null || members.Count == 0) return false;
            var unique = new HashSet<ActorId>();
            foreach (var member in members)
            {
                if (!actors.TryGetValue(member, out var actor) || !actor.IsAlive ||
                    IsGrouped(member) || !unique.Add(member)) return false;
            }
            groupId = GroupId.NewId();
            var group = new SlimeGroupActor(groupId, target);
            foreach (var member in members) group.TryJoinMember(member);
            groups.Add(groupId, group);
            var snapshot = group.GetSnapshot();
            foreach (var member in members) actors[member].NotifyGroupContext(snapshot);
            eventBus?.Publish(new SlimeGroupCreatedEvent(snapshot));
            return true;
        }

        public bool TrySetGroupTarget(GroupId groupId, UnityEngine.Vector2 target)
        {
            if (disposed || !groups.TryGetValue(groupId, out var group)) return false;
            group.NotifyTargetChanged(target);
            var snapshot = group.GetSnapshot();
            foreach (var member in snapshot.MemberIds)
                if (actors.TryGetValue(member, out var actor)) actor.NotifyGroupContext(snapshot);
            return true;
        }

        public bool RequestDissolveGroup(GroupId groupId)
        {
            if (disposed || !groups.TryGetValue(groupId, out var group)) return false;
            foreach (var member in group.GetSnapshot().MemberIds)
                if (actors.TryGetValue(member, out var actor)) actor.ClearGroupContext();
            group.Dispose();
            groups.Remove(groupId);
            eventBus?.Publish(new SlimeGroupDissolvedEvent(groupId));
            return true;
        }

        public void Tick(LevelTickContext context)
        {
            if (disposed || paused || context.DeltaTime < 0f) return;
            PublishDepletedIfNeeded();
            var actorContext = new ActorContext(context.DeltaTime, context.Perception, context.Actions);
            foreach (var actor in new List<SlimeActor>(actors.Values))
                if (actor.IsAlive) actor.Tick(actorContext);
        }

        public void Pause()
        {
            if (disposed || paused) return;
            paused = true;
            foreach (var actor in actors.Values) actor.Pause();
        }

        public void Resume()
        {
            if (disposed || !paused) return;
            paused = false;
            foreach (var actor in actors.Values) actor.Resume();
        }

        internal bool TryGetActor(ActorId actorId, out SlimeActor actor)
        {
            actor = null;
            return !disposed && actors.TryGetValue(actorId, out actor);
        }

        internal IReadOnlyList<IEffectReceiver> GetAliveEffectReceivers()
        {
            var result = new List<IEffectReceiver>();
            if (!disposed)
                foreach (var actor in actors.Values)
                    if (actor.IsAlive) result.Add(actor);
            return result.AsReadOnly();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var actor in actors.Values)
            {
                actor.Died -= OnActorDied;
                actor.ControlModeChanged -= OnControlModeChanged;
                actor.AttackedWhileCarrying -= OnAttackedWhileCarrying;
                movementSystem?.UnregisterActor(actor.ActorId, CancelReason.Disposed);
                DestroyActor(actor);
            }
            actors.Clear();
            foreach (var group in groups.Values) group.Dispose();
            groups.Clear();
        }

        private int GetAliveCount()
        {
            var count = 0;
            foreach (var actor in actors.Values) if (actor.IsAlive) count++;
            return count;
        }

        private bool IsGrouped(ActorId actorId)
        {
            foreach (var group in groups.Values)
                foreach (var member in group.GetSnapshot().MemberIds)
                    if (member == actorId) return true;
            return false;
        }

        private void OnActorDied(SlimeActor actor, ResolvedEffect effect)
        {
            movementSystem?.UnregisterActor(actor.ActorId, CancelReason.Death);
            RemoveFromGroups(actor.ActorId);
            eventBus?.Publish(new SlimeDiedEvent(actor.GetSnapshot(), effect));
            PublishDepletedIfNeeded();
        }

        private void PublishDepletedIfNeeded()
        {
            if (depletedPublished || GetAliveCount() != 0) return;
            depletedPublished = true;
            eventBus?.Publish(new SlimeDepletedEvent(levelId));
        }

        private void OnAttackedWhileCarrying(SlimeActor actor)
        {
            TryDetachCarrier(actor.ActorId);
        }

        private void OnControlModeChanged(SlimeActor actor)
        {
            eventBus?.Publish(new SlimeControlChangedEvent(actor.GetSnapshot()));
        }

        private void RemoveFromGroups(ActorId actorId)
        {
            var emptyGroups = new List<GroupId>();
            foreach (var pair in groups)
            {
                if (!pair.Value.RemoveMember(actorId)) continue;
                if (pair.Value.Count == 0) emptyGroups.Add(pair.Key);
            }
            if (actors.TryGetValue(actorId, out var actor)) actor.ClearGroupContext();
            foreach (var groupId in emptyGroups) RequestDissolveGroup(groupId);
        }

        private ActorId Reject(string reason)
        {
            LastFailureReason = reason;
            return default(ActorId);
        }

        private bool TryRegisterMovement(SlimeActor actor, float moveSpeedPixelsPerSecond)
        {
            if (movementSystem == null) return true;
            var spriteRenderer = actor.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null) return false;
            return movementSystem.RegisterActor(actor.ActorId,
                actor.GetComponent<Rigidbody2D>(), moveSpeedPixelsPerSecond,
                spriteRenderer.sprite.pixelsPerUnit, actor.NotifyMovementResult,
                actor.NotifyMovementCompleted);
        }

        private static void DestroyActor(SlimeActor actor)
        {
            if (actor == null) return;
            actor.Dispose();
            if (Application.isPlaying) UnityEngine.Object.Destroy(actor.gameObject);
            else UnityEngine.Object.DestroyImmediate(actor.gameObject);
        }
    }
}
