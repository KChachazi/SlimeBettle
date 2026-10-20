using System;
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Actors.Guards;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.World.Movement;
using UnityEngine;

namespace SlimeBattle.Guards
{
    public sealed class GuardManager : IDisposable
    {
        private readonly Dictionary<ActorId, GuardActor> actors =
            new Dictionary<ActorId, GuardActor>();
        private readonly ConfigRegistry configs;
        private readonly ILevelEventBus eventBus;
        private readonly GuardActor prefab;
        private readonly MovementSystem movementSystem;
        private Transform actorParent;
        private bool paused;
        private bool disposed;

        public GuardManager(ConfigRegistry configs, ILevelEventBus eventBus,
            GuardActor prefab, Transform actorParent = null,
            MovementSystem movementSystem = null)
        {
            this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
            this.eventBus = eventBus;
            this.prefab = prefab;
            this.actorParent = actorParent;
            this.movementSystem = movementSystem;
        }

        public string LastFailureReason { get; private set; } = string.Empty;

        public bool TrySetSpawnParent(Transform parent)
        {
            if (disposed || parent == null) return false;
            actorParent = parent;
            foreach (var actor in actors.Values)
                if (actor != null) actor.transform.SetParent(parent, true);
            return true;
        }

        public ActorId SpawnGuard(GuardSpawnRequest request)
        {
            LastFailureReason = string.Empty;
            if (disposed || paused || prefab == null ||
                !configs.TryGet(request.ConfigId, out GuardConfig config))
                return Reject("GuardManager is unavailable or GuardConfig is missing.");

            var id = ActorId.NewId();
            var actor = UnityEngine.Object.Instantiate(prefab, request.Position,
                Quaternion.identity, actorParent);
            actor.Initialize(config, id, request.Position,
                request.FactionId.IsValid ? request.FactionId : new FactionId("Slime"),
                request.EscortOffset);
            actors.Add(id, actor);
            if (!TryRegisterMovement(actor, config.MoveSpeed))
            {
                actors.Remove(id);
                DestroyActor(actor);
                return Reject("MovementSystem could not register Guard Rigidbody2D.");
            }
            actor.Died += OnActorDied;
            eventBus?.Publish(new GuardSpawnedEvent(actor.GetSnapshot()));
            return id;
        }

        public bool RequestDespawn(ActorId actorId, DespawnReason reason)
        {
            if (disposed || !actors.TryGetValue(actorId, out var actor)) return false;
            actor.Died -= OnActorDied;
            movementSystem?.UnregisterActor(actorId, CancelReason.Despawn);
            DestroyActor(actor);
            actors.Remove(actorId);
            eventBus?.Publish(new GuardDespawnedEvent(actorId, reason));
            return true;
        }

        public bool TryGetSnapshot(ActorId actorId, out GuardSnapshot snapshot)
        {
            snapshot = default(GuardSnapshot);
            if (disposed || !actors.TryGetValue(actorId, out var actor)) return false;
            snapshot = actor.GetSnapshot();
            return true;
        }

        public IReadOnlyList<GuardSnapshot> GetAliveSnapshots()
        {
            var result = new List<GuardSnapshot>();
            if (!disposed)
                foreach (var actor in actors.Values)
                    if (actor.IsAlive) result.Add(actor.GetSnapshot());
            return result.AsReadOnly();
        }

        public void Tick(LevelTickContext context)
        {
            if (disposed || paused || context.DeltaTime < 0f) return;
            var actorContext = new ActorContext(context.DeltaTime,
                context.Perception, context.Actions);
            foreach (var actor in new List<GuardActor>(actors.Values))
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

        internal bool TryGetActor(ActorId actorId, out GuardActor actor)
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
                movementSystem?.UnregisterActor(actor.ActorId, CancelReason.Disposed);
                DestroyActor(actor);
            }
            actors.Clear();
        }

        private ActorId Reject(string reason)
        {
            LastFailureReason = reason;
            return default(ActorId);
        }

        private bool TryRegisterMovement(GuardActor actor, float moveSpeedPixelsPerSecond)
        {
            if (movementSystem == null) return true;
            var spriteRenderer = actor.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null || spriteRenderer.sprite == null) return false;
            return movementSystem.RegisterActor(actor.ActorId,
                actor.GetComponent<Rigidbody2D>(), moveSpeedPixelsPerSecond,
                spriteRenderer.sprite.pixelsPerUnit, actor.NotifyMovementResult,
                actor.NotifyMovementCompleted, requireRoadArea: false);
        }

        private void OnActorDied(GuardActor actor, ResolvedEffect effect)
        {
            movementSystem?.UnregisterActor(actor.ActorId, CancelReason.Death);
            eventBus?.Publish(new GuardDiedEvent(actor.GetSnapshot(), effect));
        }

        private static void DestroyActor(GuardActor actor)
        {
            if (actor == null) return;
            actor.Dispose();
            if (Application.isPlaying) UnityEngine.Object.Destroy(actor.gameObject);
            else UnityEngine.Object.DestroyImmediate(actor.gameObject);
        }
    }
}
