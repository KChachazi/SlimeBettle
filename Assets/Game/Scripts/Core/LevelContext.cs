using System;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Objectives;
using SlimeBattle.Slimes;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Core
{
    public sealed class LevelContext : IDisposable
    {
        private LevelState state;
        private bool disposed;
        private IActorPerceptionSource perception;
        private IActorActionPort actions;
        private readonly SlimeActor slimePrefab;
        private readonly EnemyActor enemyPrefab;

        public LevelContext(LevelId levelId, ConfigRegistry configRegistry = null,
            LevelConfig levelConfig = null, SlimeActor slimePrefab = null,
            EnemyActor enemyPrefab = null)
        {
            if (!levelId.IsValid)
            {
                throw new ArgumentException("A valid level ID is required.", nameof(levelId));
            }

            LevelId = levelId;
            EventBus = new LevelEventBus();
            ConfigRegistry = configRegistry;
            LevelConfig = levelConfig;
            this.slimePrefab = slimePrefab;
            this.enemyPrefab = enemyPrefab;
            state = LevelState.Unloaded;
        }

        public LevelId LevelId { get; }

        public LevelEventBus EventBus { get; }
        public ConfigRegistry ConfigRegistry { get; }
        public LevelConfig LevelConfig { get; }
        public SlimeManager SlimeManager { get; private set; }
        public EnemyManager EnemyManager { get; private set; }
        public ObjectiveSystem ObjectiveSystem { get; private set; }
        public MapManager MapManager { get; private set; }
        public MovementSystem MovementSystem { get; private set; }

        public LevelId GetLevelId()
        {
            return LevelId;
        }

        public LevelState GetLevelState()
        {
            return state;
        }

        public ILevelEventBus GetEventBus()
        {
            return EventBus;
        }

        public void Initialize()
        {
            ThrowIfDisposed();
            SetState(LevelState.Initializing);
            if (ConfigRegistry == null) return;
            if (LevelConfig == null || LevelConfig.LevelId != LevelId)
                throw new InvalidOperationException("Matching LevelConfig is required.");
            if (slimePrefab == null || enemyPrefab == null)
                throw new InvalidOperationException("SlimeActor and EnemyActor prefabs are required.");

            MapManager = new MapManager(SceneManager.GetActiveScene(), EventBus);
            MovementSystem = new MovementSystem(MapManager, EventBus);
            SlimeManager = new SlimeManager(ConfigRegistry, EventBus,
                LevelConfig.MaxAliveSlimes, slimePrefab, movementSystem: MovementSystem);
            EnemyManager = new EnemyManager(ConfigRegistry, EventBus, enemyPrefab,
                movementSystem: MovementSystem);
            ObjectiveSystem = new ObjectiveSystem(ConfigRegistry, EventBus);
            if (!ObjectiveSystem.InitializeFromRegistry(
                new ObjectiveId(LevelConfig.ObjectiveConfigId.Value), WeaponId.NewId()))
                throw new InvalidOperationException("Objective or weapon config is missing.");
            EventBus.Subscribe<SlimeSpawnedEvent>(_ => UpdateSlimeCount());
            EventBus.Subscribe<SlimeDiedEvent>(_ => UpdateSlimeCount());
            EventBus.Subscribe<SlimeDespawnedEvent>(_ => UpdateSlimeCount());
            MapManager.PublishReady();
        }

        // MovementSystem 接管移动请求，原行动端口继续处理效果；传 null 表示仅接入移动。
        public void BindActorPorts(IActorPerceptionSource perceptionSource,
            IActorActionPort actionPort)
        {
            ThrowIfDisposed();
            perception = perceptionSource;
            actions = MovementSystem == null
                ? actionPort
                : new MovementActorActionPort(MovementSystem, actionPort);
        }

        public void Tick(float deltaTime)
        {
            if (disposed || state != LevelState.Running)
                return;
            MovementSystem?.SyncPositions();
            if (perception == null || actions == null) return;
            var context = new LevelTickContext(deltaTime, perception, actions);
            SlimeManager?.Tick(context);
            EnemyManager?.Tick(context);
        }

        internal void FixedTick(float fixedDeltaTime)
        {
            if (!disposed && state == LevelState.Running)
                MovementSystem?.FixedTick(fixedDeltaTime);
        }

        // 仅供当前关卡的效果结算层解析目标；调用者仍须先完成统一效果结算。
        internal bool TryResolveActor(ActorId actorId, out IEffectReceiver receiver)
        {
            receiver = null;
            if (disposed) return false;
            if (SlimeManager != null && SlimeManager.TryGetActor(actorId, out var slime))
            { receiver = slime; return true; }
            if (EnemyManager != null && EnemyManager.TryGetActor(actorId, out var enemy))
            { receiver = enemy; return true; }
            return false;
        }

        internal void SetState(LevelState nextState)
        {
            ThrowIfDisposed();

            if (state == nextState)
            {
                return;
            }

            var previousState = state;
            state = nextState;
            if (nextState == LevelState.Paused || nextState == LevelState.Failed ||
                nextState == LevelState.Succeeded || nextState == LevelState.Unloading)
            {
                SlimeManager?.Pause();
                EnemyManager?.Pause();
                MovementSystem?.Pause();
            }
            else if (nextState == LevelState.Running)
            {
                MovementSystem?.Resume();
                SlimeManager?.Resume();
                EnemyManager?.Resume();
            }
            EventBus.Publish(new LevelStateChangedEvent(LevelId, previousState, nextState));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            SlimeManager?.Dispose();
            EnemyManager?.Dispose();
            MovementSystem?.Dispose();
            MapManager?.Dispose();
            ObjectiveSystem?.Dispose();
            EventBus.Dispose();
            perception = null;
            actions = null;
            state = LevelState.Unloaded;
            disposed = true;
        }

        private void UpdateSlimeCount()
        {
            ObjectiveSystem?.NotifySlimeCountChanged(
                SlimeManager == null ? 0 : SlimeManager.GetAliveSnapshots().Count);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LevelContext));
            }
        }
    }
}
