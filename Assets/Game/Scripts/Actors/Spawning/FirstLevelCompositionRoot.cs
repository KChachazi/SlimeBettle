using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Slimes;
using SlimeBattle.World;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBattle.Actors.Spawning
{
    /// <summary>Binds authored first-level facts to the current LevelContext.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class FirstLevelCompositionRoot : MonoBehaviour
    {
        private sealed class FirstLevelPerception : IActorPerceptionSource
        {
            private readonly IActorPerceptionSource targets;
            private readonly RandomWalkPerceptionSource wander;

            public FirstLevelPerception(IActorPerceptionSource targets,
                RandomWalkPerceptionSource wander)
            {
                this.targets = targets;
                this.wander = wander;
            }

            public bool TryGetActorSnapshot(ActorId actorId,
                out ActorSnapshot snapshot) =>
                targets.TryGetActorSnapshot(actorId, out snapshot);

            public PerceptionSnapshot GetPerception(ActorId actorId)
            {
                var targetSnapshot = targets.GetPerception(actorId);
                var wanderSnapshot = wander.GetPerception(actorId);
                var guides = new List<GuidanceObservation>();
                if (targetSnapshot.Guides != null)
                    guides.AddRange(targetSnapshot.Guides);
                if (wanderSnapshot.Guides != null)
                    guides.AddRange(wanderSnapshot.Guides);
                return new PerceptionSnapshot(targetSnapshot.SampleTick,
                    targetSnapshot.Targets, targetSnapshot.HasDestination,
                    targetSnapshot.Destination, guides,
                    targetSnapshot.HasEscapeDestination,
                    targetSnapshot.EscapeDestination,
                    targetSnapshot.FleeCheckSucceeded);
            }
        }

        [SerializeField] private LevelManager levelManager;
        [SerializeField] private SlimeConfig slimeConfig;
        [SerializeField] private EnemyConfig enemyConfig;
        [SerializeField] private Transform mapRoot;
        [SerializeField] private Transform entitiesRoot;
        [SerializeField] private Transform mechanismsRoot;
        [SerializeField] private Transform presentationRoot;
        [SerializeField] private bool startCurrentSceneOnPlay;
        [SerializeField] private string[] branchRouteIds =
            { "route_branch_1", "route_branch_2", "route_branch_3" };
        [SerializeField] private string mainRouteId = "route_main";
        private LevelContext installed;

        private void OnEnable()
        {
            if (levelManager == null) levelManager = FindObjectOfType<LevelManager>();
            if (levelManager == null) return;
            levelManager.LevelStarted += OnLevelStarted;
            var current = levelManager.GetCurrentContext();
            if (current != null && levelManager.GetLevelState() == LevelState.Running)
                Install(current);
        }

        private void Start()
        {
            if (startCurrentSceneOnPlay && levelManager != null &&
                levelManager.GetLevelState() == LevelState.Unloaded)
                levelManager.StartCurrentSceneLevel();
        }

        private void OnDisable()
        {
            if (levelManager != null) levelManager.LevelStarted -= OnLevelStarted;
            installed = null;
        }

        private void OnLevelStarted(LevelStartedEvent started)
        {
            if (started.LevelId.Value == gameObject.scene.name) Install(started.Context);
        }

        private void Install(LevelContext context)
        {
            if (context == null || context == installed) return;
            if (slimeConfig == null || enemyConfig == null || mapRoot == null ||
                entitiesRoot == null || mechanismsRoot == null ||
                presentationRoot == null || branchRouteIds == null ||
                branchRouteIds.Length != 3 || context.GuardManager == null)
            {
                Debug.LogError("FirstLevelCompositionRoot references are incomplete.", this);
                return;
            }
            var routeIds = new MapRouteId[3];
            for (var i = 0; i < routeIds.Length; i++)
                routeIds[i] = new MapRouteId(branchRouteIds[i]);
            var enemyBounds = default(EnemyActivityBounds);
            if (context.MapManager.TryGetMapBounds(out var mapBounds))
                enemyBounds = new EnemyActivityBounds(mapBounds.Min, mapBounds.Max);
            var targetPerception = new ActorTargetPerceptionSource(
                context.SlimeManager, context.EnemyManager, context.ConfigRegistry,
                context.GuardManager, context.MechanismSystem);
            var wanderPerception = new RandomWalkPerceptionSource(
                context.SlimeManager, context.EnemyManager,
                context.MovementSystem, enemyBounds);
            var perception = new FirstLevelRoutePerceptionSource(
                new FirstLevelPerception(targetPerception, wanderPerception),
                context.SlimeManager, context.MapManager, context.MechanismSystem,
                routeIds, new MapRouteId(mainRouteId));
            if (!context.SlimeManager.TrySetSpawnParent(entitiesRoot) ||
                !context.EnemyManager.TrySetSpawnParent(entitiesRoot) ||
                !context.GuardManager.TrySetSpawnParent(entitiesRoot) ||
                !context.MechanismSystem.TrySetPlacedParent(mechanismsRoot))
            {
                Debug.LogError("Actor managers could not accept EntitiesRoot.", this);
                return;
            }
            context.BindActorPorts(perception, null);
            var slimeSpawns = context.MapManager.GetMapPoints(MapPointKind.Spawn);
            if (slimeSpawns.Count != 1)
            {
                Debug.LogError("First level requires exactly one Slime spawn point.", this);
                return;
            }
            var slimeId = context.SlimeManager.SpawnSlime(new SlimeSpawnRequest(
                slimeConfig.ConfigId, slimeSpawns[0].Position));
            if (!slimeId.IsValid)
            {
                Debug.LogError("Initial Slime spawn failed: " +
                    context.SlimeManager.LastFailureReason, this);
                return;
            }
            foreach (var point in context.MapManager.GetMapPoints(MapPointKind.EnemySpawn))
                if (!context.EnemyManager.SpawnEnemy(new EnemySpawnRequest(
                    enemyConfig.ConfigId, point.Position,
                    activityBounds: enemyBounds)).IsValid)
                    Debug.LogError("Enemy spawn failed: " +
                        context.EnemyManager.LastFailureReason, this);
            installed = context;
        }
    }
}
