using System;
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Guards;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Guards;
using SlimeBattle.Mechanisms;
using SlimeBattle.Slimes;
using SlimeBattle.Status;
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
        private readonly GuardActor guardPrefab;
        private SubscriptionToken foodTriggeredToken;

        public LevelContext(LevelId levelId, ConfigRegistry configRegistry = null,
            LevelConfig levelConfig = null, SlimeActor slimePrefab = null,
            EnemyActor enemyPrefab = null, GuardActor guardPrefab = null)
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
            this.guardPrefab = guardPrefab;
            state = LevelState.Unloaded;
        }

        public LevelId LevelId { get; }

        public LevelEventBus EventBus { get; }
        public ConfigRegistry ConfigRegistry { get; }
        public LevelConfig LevelConfig { get; }
        public SlimeManager SlimeManager { get; private set; }
        public EnemyManager EnemyManager { get; private set; }
        public GuardManager GuardManager { get; private set; }
        public LevelResultRules ResultRules { get; private set; }
        public MapManager MapManager { get; private set; }
        public StatusManager StatusManager { get; private set; }
        public MechanismSystem MechanismSystem { get; private set; }
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
            StatusManager = new StatusManager(ConfigRegistry, EventBus, ResolveActor,
                EnumerateAliveActors);
            MovementSystem = new MovementSystem(MapManager, EventBus,
                StatusManager.GetMovementModifiers);
            var goalAreaId = default(MapAreaId);
            foreach (var area in MapManager.GetMapAreaSnapshots())
                if (area.Kind == MapAreaKind.Goal)
                {
                    if (goalAreaId.IsValid)
                        throw new InvalidOperationException("Only one goal cave area is supported.");
                    goalAreaId = area.AreaId;
                }
            SlimeManager = new SlimeManager(ConfigRegistry, EventBus,
                LevelConfig.MaxAliveSlimes, slimePrefab, movementSystem: MovementSystem,
                levelId: LevelId, goalAreaId: goalAreaId);
            EnemyManager = new EnemyManager(ConfigRegistry, EventBus, enemyPrefab,
                movementSystem: MovementSystem);
            MechanismSystem = new MechanismSystem(ConfigRegistry,
                LevelConfig.MaximumActivePlacedMechanisms, MapManager, StatusManager,
                EventBus, SceneManager.GetActiveScene());
            if (LevelConfig.ObjectiveKind == LevelObjectiveKind.EscortWeapon &&
                !MechanismSystem.TryGetWeaponSnapshot(out _))
                throw new InvalidOperationException(
                    "EscortWeapon objective requires one valid scene escort weapon.");
            foodTriggeredToken = EventBus.Subscribe<MechanismTriggeredEvent>(OnMechanismTriggered);
            ResultRules = new LevelResultRules(EventBus, LevelConfig.ObjectiveKind);
            if (guardPrefab != null)
            {
                GuardConfig guardConfig = null;
                foreach (var asset in ConfigRegistry.Configs)
                    if (asset is GuardConfig found)
                    {
                        if (guardConfig != null)
                            throw new InvalidOperationException("Only one GuardConfig is supported.");
                        guardConfig = found;
                    }
                if (guardConfig != null)
                {
                    var guardPoints = MapManager.GetMapPoints(MapPointKind.GuardSpawn);
                    if (guardPoints.Count > 0 ||
                        MechanismSystem.TryGetWeaponSnapshot(out _))
                        GuardManager = new GuardManager(ConfigRegistry, EventBus,
                            guardPrefab, movementSystem: MovementSystem);
                    foreach (var point in guardPoints)
                        if (!GuardManager.SpawnGuard(new GuardSpawnRequest(
                            guardConfig.ConfigId, point.Position)).IsValid)
                            throw new InvalidOperationException(
                                "Guard spawn failed: " + GuardManager.LastFailureReason);
                    if (guardPoints.Count == 0 &&
                        MechanismSystem.TryGetWeaponSnapshot(out var weapon))
                        foreach (var offset in guardConfig.SpawnOffsets)
                            if (!GuardManager.SpawnGuard(new GuardSpawnRequest(
                                guardConfig.ConfigId, weapon.Position + offset)).IsValid)
                                throw new InvalidOperationException(
                                    "Guard spawn failed: " + GuardManager.LastFailureReason);
                }
            }
            MechanismSystem.BindCarrierActors(SlimeManager, EnemyManager, GuardManager);
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
                : new MovementActorActionPort(MovementSystem, actionPort,
                    StatusManager, MechanismSystem);
        }

        public void Tick(float deltaTime)
        {
            if (disposed || state != LevelState.Running)
                return;
            StatusManager?.Tick(new StatusTickContext(deltaTime));
            MechanismSystem?.Tick(deltaTime);
            MovementSystem?.SyncPositions();
            SlimeManager?.Tick(new LevelTickContext(deltaTime, perception, actions));
            if (perception != null && actions != null)
            {
                var context = new LevelTickContext(deltaTime, perception, actions);
                EnemyManager?.Tick(context);
                GuardManager?.Tick(context);
            }
            ResultRules?.EndFrame(deltaTime);
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
            if (GuardManager != null && GuardManager.TryGetActor(actorId, out var guard))
            { receiver = guard; return true; }
            return false;
        }

        private IEffectReceiver ResolveActor(ActorId actorId)
        {
            return TryResolveActor(actorId, out var receiver) ? receiver : null;
        }

        private IEnumerable<IEffectReceiver> EnumerateAliveActors()
        {
            if (SlimeManager != null)
                foreach (var actor in SlimeManager.GetAliveEffectReceivers()) yield return actor;
            if (EnemyManager != null)
                foreach (var actor in EnemyManager.GetAliveEffectReceivers()) yield return actor;
            // Guards take health damage through ResolveActor but do not enter StatusManager ticks.
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
                GuardManager?.Pause();
                StatusManager?.Pause();
                MechanismSystem?.Pause();
                MovementSystem?.Pause();
            }
            else if (nextState == LevelState.Running)
            {
                MovementSystem?.Resume();
                StatusManager?.Resume();
                MechanismSystem?.Resume();
                SlimeManager?.Resume();
                EnemyManager?.Resume();
                GuardManager?.Resume();
            }
            EventBus.Publish(new LevelStateChangedEvent(LevelId, previousState, nextState));
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            EventBus.Unsubscribe(foodTriggeredToken);
            MechanismSystem?.Dispose();
            StatusManager?.Dispose();
            SlimeManager?.Dispose();
            EnemyManager?.Dispose();
            GuardManager?.Dispose();
            MovementSystem?.Dispose();
            MapManager?.Dispose();
            ResultRules?.Dispose();
            MechanismSystem = null;
            EventBus.Dispose();
            perception = null;
            actions = null;
            state = LevelState.Unloaded;
            disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LevelContext));
            }
        }

        private void OnMechanismTriggered(MechanismTriggeredEvent triggered)
        {
            if (disposed || state != LevelState.Running || !triggered.Result.IsAccepted ||
                MechanismSystem == null || SlimeManager == null ||
                !MechanismSystem.TryGetSnapshot(triggered.MechanismId, out var mechanism) ||
                mechanism.Kind != MechanismKind.Food ||
                !SlimeManager.TryGetSnapshot(triggered.ActorId, out var parent) ||
                !parent.IsAlive) return;

            var offset = parent.PixelsPerUnit > 0f ? 35f / parent.PixelsPerUnit : 0.35f;
            SlimeManager.SpawnSlime(new SlimeSpawnRequest(parent.ConfigId,
                parent.Position + UnityEngine.Vector2.left * offset, parent.FactionId));
        }
    }
}
