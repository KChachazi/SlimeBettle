/* 场景初始角色清单；仅史莱姆接收局部漫游感知，Actor 生命周期仍由正式 Manager 管理。 */
using SlimeBattle.Core;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Data;
using SlimeBattle.Mechanisms;
using SlimeBattle.Enemies;
using SlimeBattle.Slimes;
using SlimeBattle.Status;
using UnityEngine;

namespace SlimeBattle.Actors.Spawning
{
    public sealed class LevelActorSpawner : MonoBehaviour
    {
        private sealed class LevelPerception : IActorPerceptionSource
        {
            private readonly ActorTargetPerceptionSource targets;
            private readonly RandomWalkPerceptionSource wander;
            private readonly SlimeBattle.Slimes.SlimeManager slimes;
            private readonly MechanismSystem mechanisms;
            public LevelPerception(ActorTargetPerceptionSource targets,
                RandomWalkPerceptionSource wander, SlimeBattle.Slimes.SlimeManager slimes,
                MechanismSystem mechanisms)
            { this.targets = targets; this.wander = wander;
                this.slimes = slimes; this.mechanisms = mechanisms; }
            public bool TryGetActorSnapshot(ActorId id, out ActorSnapshot snapshot) =>
                targets.TryGetActorSnapshot(id, out snapshot);
            public PerceptionSnapshot GetPerception(ActorId id)
            {
                var target = targets.GetPerception(id);
                var guides = new System.Collections.Generic.List<GuidanceObservation>();
                if (target.Guides != null) guides.AddRange(target.Guides);
                if (slimes.TryGetSnapshot(id, out var slime) && slime.IsAlive &&
                    !slime.IsCarrying)
                {
                    var roaming = wander.GetPerception(id);
                    if (roaming.Guides != null) guides.AddRange(roaming.Guides);
                    if (mechanisms != null)
                        foreach (var source in mechanisms.GetGuideSourcesInRange(
                            slime.Position, slime.PixelsPerUnit))
                            guides.Add(new GuidanceObservation(source.Type == GuideSourceType.Food
                                ? GuidanceKind.Food : GuidanceKind.Warmth, source.Position));
                }
                return new PerceptionSnapshot(target.SampleTick, target.Targets,
                    target.HasDestination, target.Destination, guides,
                    target.HasEscapeDestination, target.EscapeDestination,
                    target.FleeCheckSucceeded);
            }
        }
        [SerializeField] private SlimeConfig slimeConfig;
        [SerializeField] private EnemyConfig enemyConfig;
        [SerializeField] private Transform slimeRoot;
        [SerializeField] private Transform enemyRoot;
        [SerializeField] private Transform guardRoot;
        [SerializeField] private Transform[] slimeSpawnPoints;
        [SerializeField] private Transform[] enemySpawnPoints;
        [SerializeField] private Vector2 enemyBoundsMinimum = new Vector2(-6f, -4f);
        [SerializeField] private Vector2 enemyBoundsMaximum = new Vector2(6f, 4f);

        private LevelManager levelManager;
        private LevelContext spawnedContext;
        private ILevelEventBus statusEventBus;
        private SubscriptionToken statusSubscription;

        private void OnEnable()
        {
            var root = FindObjectOfType<GameRoot>();
            if (root == null) return;
            levelManager = root.GetLevelManager();
            if (levelManager == null) return;
            levelManager.LevelStarted += OnLevelStarted;
            if (levelManager.GetLevelState() == LevelState.Running)
                Spawn(levelManager.GetCurrentContext());
        }

        private void OnDisable()
        {
            if (levelManager != null) levelManager.LevelStarted -= OnLevelStarted;
            UnsubscribeFromStatusEvents();
            levelManager = null;
            spawnedContext = null;
        }

        private void OnLevelStarted(LevelStartedEvent started)
        {
            if (started.LevelId.Value == gameObject.scene.name) Spawn(started.Context);
        }

        private void Spawn(LevelContext context)
        {
            if (context == null || context == spawnedContext) return;
            if (slimeConfig == null || enemyConfig == null ||
                slimeRoot == null || enemyRoot == null || guardRoot == null ||
                slimeSpawnPoints == null || enemySpawnPoints == null)
            {
                Debug.LogError("LevelActorSpawner: configs, actor roots or spawn points are missing.", this);
                return;
            }
            if (!context.SlimeManager.TrySetSpawnParent(slimeRoot) ||
                !context.EnemyManager.TrySetSpawnParent(enemyRoot) ||
                context.GuardManager == null ||
                !context.GuardManager.TrySetSpawnParent(guardRoot))
            {
                Debug.LogError("LevelActorSpawner: actor roots could not be bound.", this);
                return;
            }

            spawnedContext = context;
            SubscribeToStatusEvents(context);
            var slimeCount = 0;
            var enemyCount = 0;
            var bounds = new EnemyActivityBounds(enemyBoundsMinimum, enemyBoundsMaximum);
            context.BindActorPorts(new LevelPerception(
                new ActorTargetPerceptionSource(context.SlimeManager,
                    context.EnemyManager, context.ConfigRegistry,
                    context.GuardManager, context.MechanismSystem),
                new RandomWalkPerceptionSource(context.SlimeManager,
                    context.EnemyManager, context.MovementSystem, bounds),
                context.SlimeManager, context.MechanismSystem), null);
            foreach (var point in slimeSpawnPoints)
            {
                if (point == null) continue;
                var id = context.SlimeManager.SpawnSlime(
                    new SlimeSpawnRequest(slimeConfig.ConfigId, point.position));
                if (id.IsValid) slimeCount++;
                else Debug.LogError("Slime spawn failed: " +
                    context.SlimeManager.LastFailureReason, this);
            }
            foreach (var point in enemySpawnPoints)
            {
                if (point == null) continue;
                var id = context.EnemyManager.SpawnEnemy(
                    new EnemySpawnRequest(enemyConfig.ConfigId, point.position,
                        activityBounds: bounds));
                if (id.IsValid) enemyCount++;
                else Debug.LogError("Enemy spawn failed: " +
                    context.EnemyManager.LastFailureReason, this);
            }
            Debug.Log("Level_test initial actors: " + slimeCount +
                " Slimes, " + enemyCount + " Enemies.", this);
        }

        private void SubscribeToStatusEvents(LevelContext context)
        {
            UnsubscribeFromStatusEvents();
            statusEventBus = context.GetEventBus();
            if (statusEventBus != null)
                statusSubscription = statusEventBus.Subscribe<StatusAddedEvent>(OnStatusAdded);
        }

        private void UnsubscribeFromStatusEvents()
        {
            if (statusEventBus != null && statusSubscription.IsValid)
                statusEventBus.Unsubscribe(statusSubscription);
            statusEventBus = null;
            statusSubscription = default(SubscriptionToken);
        }

        private void OnStatusAdded(StatusAddedEvent statusAdded)
        {
            if (spawnedContext == null ||
                !spawnedContext.SlimeManager.TryGetActor(statusAdded.ActorId, out var slime))
                return;

            var status = statusAdded.Status;
            Debug.Log("[Level_test] Slime " + statusAdded.ActorId + " gained status " +
                status.StatusId + " (" + status.Kind + "); move x" +
                status.MovementSpeedMultiplier.ToString("0.##") + ", max HP x" +
                status.MaximumHealthMultiplier.ToString("0.##") + ".", slime);
        }
    }
}
