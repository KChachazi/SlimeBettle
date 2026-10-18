using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Mechanisms;
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
            private readonly SlimeManager slimes;
            private readonly MechanismSystem mechanisms;

            public FirstLevelPerception(IActorPerceptionSource targets,
                RandomWalkPerceptionSource wander, SlimeManager slimes,
                MechanismSystem mechanisms)
            {
                this.targets = targets;
                this.wander = wander;
                this.slimes = slimes;
                this.mechanisms = mechanisms;
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
                if (mechanisms != null && slimes != null &&
                    slimes.TryGetSnapshot(actorId, out var slime) &&
                    slime.IsAlive && !slime.IsCarrying)
                {
                    foreach (var source in mechanisms.GetGuideSourcesInRange(
                        slime.Position, slime.PixelsPerUnit))
                    {
                        if (source.Type == GuideSourceType.Food)
                            guides.Add(new GuidanceObservation(GuidanceKind.Food,
                                source.Position));
                        else if (source.Type == GuideSourceType.Warmth)
                            guides.Add(new GuidanceObservation(GuidanceKind.Warmth,
                                source.Position));
                    }
                }
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
        [SerializeField] private Vector2[] additionalSlimeSpawnOffsets =
            { new Vector2(-1.25f, 1f), new Vector2(1.25f, 1f) };
        [SerializeField] private float slimeWaitRadius = 1.5f;
        [SerializeField] private Vector2 enemyActivityHalfExtents =
            new Vector2(4f, 4f);
        private LevelContext installed;
        private FirstLevelRoutePerceptionSource routePerception;

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
            routePerception = null;
        }

        /// <summary>Releases waiting free Slimes into the authored route decision.</summary>
        public bool ReleaseSlimeRoutes()
        {
            if (routePerception == null) return false;
            routePerception.ReleaseRoutes();
            return true;
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
            var mapWanderBounds = default(EnemyActivityBounds);
            var hasMapBounds = context.MapManager.TryGetMapBounds(out var mapBounds);
            if (hasMapBounds)
                mapWanderBounds = new EnemyActivityBounds(mapBounds.Min, mapBounds.Max);
            var targetPerception = new ActorTargetPerceptionSource(
                context.SlimeManager, context.EnemyManager, context.ConfigRegistry,
                context.GuardManager, context.MechanismSystem);
            var wanderPerception = new RandomWalkPerceptionSource(
                context.SlimeManager, context.EnemyManager,
                context.MovementSystem, mapWanderBounds);
            var perception = new FirstLevelRoutePerceptionSource(
                new FirstLevelPerception(targetPerception, wanderPerception,
                    context.SlimeManager, context.MechanismSystem),
                context.SlimeManager, context.MapManager, context.MechanismSystem,
                routeIds, new MapRouteId(mainRouteId), waitForRouteTrigger: true,
                waitRadius: slimeWaitRadius);
            if (!context.SlimeManager.TrySetSpawnParent(entitiesRoot) ||
                !context.EnemyManager.TrySetSpawnParent(entitiesRoot) ||
                !context.GuardManager.TrySetSpawnParent(entitiesRoot) ||
                !context.MechanismSystem.TrySetPlacedParent(mechanismsRoot))
            {
                Debug.LogError("Actor managers could not accept EntitiesRoot.", this);
                return;
            }
            context.BindActorPorts(perception, null);
            routePerception = perception;
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
            if (additionalSlimeSpawnOffsets != null)
                foreach (var offset in additionalSlimeSpawnOffsets)
                {
                    var extraId = context.SlimeManager.SpawnSlime(new SlimeSpawnRequest(
                        slimeConfig.ConfigId, slimeSpawns[0].Position + offset));
                    if (!extraId.IsValid)
                        Debug.LogError("Additional Slime spawn failed: " +
                            context.SlimeManager.LastFailureReason, this);
                }
            foreach (var point in context.MapManager.GetMapPoints(MapPointKind.EnemySpawn))
            {
                var activityBounds = CreateEnemyActivityBounds(point.Position,
                    hasMapBounds ? mapBounds : default(MapBounds));
                if (!context.EnemyManager.SpawnEnemy(new EnemySpawnRequest(
                    enemyConfig.ConfigId, point.Position,
                    activityBounds: activityBounds)).IsValid)
                    Debug.LogError("Enemy spawn failed: " +
                        context.EnemyManager.LastFailureReason, this);
            }
            installed = context;
        }

        private EnemyActivityBounds CreateEnemyActivityBounds(Vector2 center,
            MapBounds mapBounds)
        {
            var halfExtents = new Vector2(
                Mathf.Max(0.1f, Mathf.Abs(enemyActivityHalfExtents.x)),
                Mathf.Max(0.1f, Mathf.Abs(enemyActivityHalfExtents.y)));
            var minimum = center - halfExtents;
            var maximum = center + halfExtents;
            if (mapBounds.Size != Vector2.zero)
            {
                minimum = Vector2.Max(minimum, mapBounds.Min);
                maximum = Vector2.Min(maximum, mapBounds.Max);
            }
            return new EnemyActivityBounds(minimum, maximum);
        }
    }
}
