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
                var guides = new List<GuidanceObservation>();
                if (targetSnapshot.Guides != null)
                    guides.AddRange(targetSnapshot.Guides);
                if (targets.TryGetActorSnapshot(actorId, out var actor) &&
                    actor.ActorType == ActorType.Enemy)
                {
                    var wanderSnapshot = wander.GetPerception(actorId);
                    if (wanderSnapshot.Guides != null)
                        guides.AddRange(wanderSnapshot.Guides);
                }
                var slime = default(SlimeSnapshot);
                var canWander = slimes != null &&
                    slimes.TryGetSnapshot(actorId, out slime) &&
                    slime.IsAlive && !slime.IsCarrying;
                if (canWander)
                {
                    var wanderSnapshot = wander.GetPerception(actorId);
                    if (wanderSnapshot.Guides != null)
                        guides.AddRange(wanderSnapshot.Guides);
                }
                if (mechanisms != null && canWander)
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
        [SerializeField] private string defaultRouteId = "route_escort";
        [SerializeField, Min(0.1f)] private float slimeWanderRadius = 1.5f;
        [SerializeField] private Vector2 enemyActivityHalfExtents =
            new Vector2(4f, 4f);
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
                presentationRoot == null || context.GuardManager == null)
            {
                Debug.LogError("FirstLevelCompositionRoot references are incomplete.", this);
                return;
            }
            if (!context.MapManager.TryGetRoute(new MapRouteId(defaultRouteId), out var route) ||
                route.Role != MapRouteRole.Candidate || route.Waypoints.Count < 2)
            {
                Debug.LogError($"First level requires the authored default route '{defaultRouteId}'.", this);
                return;
            }
            var mapWanderBounds = default(EnemyActivityBounds);
            var hasMapBounds = context.MapManager.TryGetMapBounds(out var mapBounds);
            if (hasMapBounds)
                mapWanderBounds = new EnemyActivityBounds(mapBounds.Min, mapBounds.Max);
            var targetPerception = new ActorTargetPerceptionSource(
                context.SlimeManager, context.EnemyManager, context.ConfigRegistry,
                context.GuardManager, context.MechanismSystem);
            var wanderPerception = new RandomWalkPerceptionSource(
                context.SlimeManager, context.EnemyManager,
                context.MovementSystem, mapWanderBounds, context.MapManager,
                slimeWanderRadius);
            var perception = new FirstLevelPerception(targetPerception,
                wanderPerception, context.SlimeManager, context.MechanismSystem);
            if (!context.SlimeManager.TrySetSpawnParent(entitiesRoot) ||
                !context.EnemyManager.TrySetSpawnParent(entitiesRoot) ||
                !context.GuardManager.TrySetSpawnParent(entitiesRoot) ||
                !context.MechanismSystem.TrySetPlacedParent(mechanismsRoot))
            {
                Debug.LogError("Actor managers could not accept EntitiesRoot.", this);
                return;
            }
            context.BindActorPorts(perception, null);
            var slimeId = context.SlimeManager.SpawnSlime(new SlimeSpawnRequest(
                slimeConfig.ConfigId, route.Waypoints[0].Position));
            if (!slimeId.IsValid)
            {
                Debug.LogError("Initial Slime spawn failed: " +
                    context.SlimeManager.LastFailureReason, this);
                return;
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
