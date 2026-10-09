/* EnemyManager 管理敌人索引和生命周期；单体行为与生命归 EnemyActor。 */
using System;
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.World.Movement;
using UnityEngine;

namespace SlimeBattle.Enemies
{
    public sealed class EnemyManager : IDisposable
    {
        private readonly Dictionary<ActorId, EnemyActor> actors = new Dictionary<ActorId, EnemyActor>();
        private readonly ConfigRegistry configs;
        private readonly ILevelEventBus eventBus;
        private readonly EnemyActor prefab;
        private readonly MovementSystem movementSystem;
        private Transform actorParent;
        private bool disposed;
        private bool paused;

        public EnemyManager(ConfigRegistry configs, ILevelEventBus eventBus,
            EnemyActor prefab = null, Transform actorParent = null,
            MovementSystem movementSystem = null)
        {
            this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
            this.eventBus = eventBus;
            this.prefab = prefab;
            this.actorParent = actorParent;
            this.movementSystem = movementSystem;
        }

        public string LastFailureReason { get; private set; } = string.Empty;

        // 场景装配方指定后续生成实例的父节点；Actor 仍由 Manager 创建和销毁。
        public bool TrySetSpawnParent(Transform parent)
        {
            if (disposed || parent == null) return false;
            actorParent = parent;
            return true;
        }

        public ActorId SpawnEnemy(EnemySpawnRequest request)
        {
            LastFailureReason = string.Empty;
            if (disposed) return Reject("EnemyManager is disposed.");
            if (paused) return Reject("The level is paused.");
            if (prefab == null) return Reject("EnemyActor prefab is missing.");
            if (!configs.TryGet(request.ConfigId, out EnemyConfig config))
                return Reject("EnemyConfig is missing or ambiguous.");
            if (config.MaxHealth <= 0) return Reject("EnemyConfig health is invalid.");
            if (request.TargetPolicy != EnemyTargetPolicy.NearestHostile)
                return Reject("Only NearestHostile targeting is available in P0.");
            if (!request.ActivityBounds.Contains(request.Position))
                return Reject("Enemy spawn is outside its activity bounds.");

            var id = ActorId.NewId();
            var actor = UnityEngine.Object.Instantiate(prefab,
                new Vector3(request.Position.x, request.Position.y, 0f),
                Quaternion.identity, actorParent);
            actor.Initialize(config, id, request.Position,
                request.FactionId.IsValid ? request.FactionId : new FactionId("Enemy"),
                request.TargetPolicy, request.ActivityBounds);
            if (!TryRegisterMovement(actor, config.MoveSpeed))
            {
                DestroyActor(actor);
                return Reject("MovementSystem could not register the Enemy Rigidbody2D.");
            }
            actor.Died += OnActorDied;
            actor.ControlModeChanged += OnControlModeChanged;
            actors.Add(id, actor);
            eventBus?.Publish(new EnemySpawnedEvent(actor.GetSnapshot()));
            return id;
        }

        public bool RequestDespawn(ActorId actorId, DespawnReason reason)
        {
            if (disposed || !actors.TryGetValue(actorId, out var actor)) return false;
            actor.Died -= OnActorDied;
            actor.ControlModeChanged -= OnControlModeChanged;
            movementSystem?.UnregisterActor(actorId, CancelReason.Despawn);
            DestroyActor(actor);
            actors.Remove(actorId);
            eventBus?.Publish(new EnemyDespawnedEvent(actorId, reason));
            return true;
        }

        public bool TryGetSnapshot(ActorId actorId, out EnemySnapshot snapshot)
        {
            snapshot = default(EnemySnapshot);
            if (disposed || !actors.TryGetValue(actorId, out var actor)) return false;
            snapshot = actor.GetSnapshot();
            return true;
        }

        public IReadOnlyList<EnemySnapshot> GetAliveSnapshots()
        {
            var result = new List<EnemySnapshot>();
            if (!disposed)
                foreach (var actor in actors.Values)
                    if (actor.IsAlive) result.Add(actor.GetSnapshot());
            return result.AsReadOnly();
        }

        public void Tick(LevelTickContext context)
        {
            if (disposed || paused || context.DeltaTime < 0f) return;
            var actorContext = new ActorContext(context.DeltaTime, context.Perception, context.Actions);
            foreach (var actor in new List<EnemyActor>(actors.Values))
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

        internal bool TryGetActor(ActorId actorId, out EnemyActor actor)
        {
            actor = null;
            return !disposed && actors.TryGetValue(actorId, out actor);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var actor in actors.Values)
            {
                actor.Died -= OnActorDied;
                actor.ControlModeChanged -= OnControlModeChanged;
                movementSystem?.UnregisterActor(actor.ActorId, CancelReason.Disposed);
                DestroyActor(actor);
            }
            actors.Clear();
        }

        private void OnActorDied(EnemyActor actor, ResolvedEffect effect)
        {
            movementSystem?.UnregisterActor(actor.ActorId, CancelReason.Death);
            eventBus?.Publish(new EnemyDiedEvent(actor.GetSnapshot(), effect));
        }

        private void OnControlModeChanged(EnemyActor actor)
        {
            eventBus?.Publish(new EnemyStateChangedEvent(actor.GetSnapshot()));
        }

        private ActorId Reject(string reason)
        {
            LastFailureReason = reason;
            return default(ActorId);
        }

        private bool TryRegisterMovement(EnemyActor actor, float moveSpeedPixelsPerSecond)
        {
            if (movementSystem == null) return true;
            var spriteRenderer = actor.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null) return false;
            return movementSystem.RegisterActor(actor.ActorId,
                actor.GetComponent<Rigidbody2D>(), moveSpeedPixelsPerSecond,
                spriteRenderer.sprite.pixelsPerUnit, actor.NotifyMovementResult,
                actor.NotifyMovementCompleted);
        }

        private static void DestroyActor(EnemyActor actor)
        {
            if (actor == null) return;
            actor.Dispose();
            if (Application.isPlaying) UnityEngine.Object.Destroy(actor.gameObject);
            else UnityEngine.Object.DestroyImmediate(actor.gameObject);
        }
    }
}
