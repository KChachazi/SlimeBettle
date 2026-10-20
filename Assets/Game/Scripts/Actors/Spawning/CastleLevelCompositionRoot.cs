using System;
using System.Collections.Generic;
using SlimeBattle.Actors.AI.DecisionTree;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Mechanisms;
using SlimeBattle.Slimes;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEngine;

namespace SlimeBattle.Actors.Spawning
{
    /// <summary>Installs the castle level's authored spawn points and road-aware actor loop.</summary>
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class CastleLevelCompositionRoot : MonoBehaviour
    {
        private sealed class CastlePerception : IActorPerceptionSource
        {
            private readonly ActorTargetPerceptionSource targets;
            private readonly RandomWalkPerceptionSource wander;
            private readonly SlimeManager slimes;
            private readonly MechanismSystem mechanisms;

            public CastlePerception(ActorTargetPerceptionSource targets,
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
                var target = targets.GetPerception(actorId);
                var guides = new List<GuidanceObservation>();
                if (target.Guides != null) guides.AddRange(target.Guides);
                if (mechanisms != null && slimes != null &&
                    slimes.TryGetSnapshot(actorId, out var slime) &&
                    slime.IsAlive && !slime.IsCarrying)
                {
                    var roaming = wander.GetPerception(actorId);
                    if (roaming.Guides != null) guides.AddRange(roaming.Guides);
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
                return new PerceptionSnapshot(target.SampleTick, target.Targets,
                    target.HasDestination, target.Destination, guides,
                    target.HasEscapeDestination, target.EscapeDestination,
                    target.FleeCheckSucceeded);
            }
        }

        [SerializeField] private LevelManager levelManager;
        [SerializeField] private SlimeConfig slimeConfig;
        [SerializeField] private EnemyConfig enemyConfig;
        [SerializeField] private Transform entitiesRoot;
        [SerializeField, Min(0)] private int treasureChestFoodReward = 4;
        [SerializeField, Min(0.1f)] private float wanderDestinationRadius = 4f;
        private LevelContext installed;

        private void OnEnable()
        {
            if (levelManager == null) levelManager = FindObjectOfType<LevelManager>();
            if (levelManager == null) return;
            levelManager.LevelStarted += OnLevelStarted;
            var context = levelManager.GetCurrentContext();
            if (context != null && levelManager.GetLevelState() == LevelState.Running)
                Install(context);
        }

        private void OnDisable()
        {
            if (levelManager != null) levelManager.LevelStarted -= OnLevelStarted;
            installed = null;
        }

        private void OnLevelStarted(LevelStartedEvent started)
        {
            if (started.LevelId.Value == gameObject.scene.name)
                Install(started.Context);
        }

        private void Install(LevelContext context)
        {
            if (context == null || context == installed) return;
            if (slimeConfig == null || enemyConfig == null || entitiesRoot == null)
            {
                Debug.LogError("CastleLevelCompositionRoot references are incomplete.", this);
                return;
            }

            var spawnPoints = new List<MapPointSnapshot>(
                context.MapManager.GetMapPoints(MapPointKind.SlimeSpawn));
            spawnPoints.Sort((left, right) => string.CompareOrdinal(
                left.PointId.Value, right.PointId.Value));
            var enemyPoints = context.MapManager.GetMapPoints(MapPointKind.EnemySpawn);
            if (spawnPoints.Count == 0 || enemyPoints.Count == 0)
            {
                Debug.LogError("Castle map requires the supplied Slime and enemy spawn points.", this);
                return;
            }
            var slimeCount = context.CampaignResources.SlimeCount;
            if (slimeCount > spawnPoints.Count)
            {
                Debug.LogError($"Castle level requires {slimeCount} Slime spawn points; " +
                    $"MAP_Castle provides {spawnPoints.Count}.", this);
                return;
            }
            if (!context.SlimeManager.TrySetSpawnParent(entitiesRoot) ||
                !context.EnemyManager.TrySetSpawnParent(entitiesRoot) ||
                (context.GuardManager != null &&
                 !context.GuardManager.TrySetSpawnParent(entitiesRoot)))
            {
                Debug.LogError("Castle actor managers could not accept EntitiesRoot.", this);
                return;
            }

            if (!context.MapManager.TryGetMapBounds(out var mapBounds))
            {
                Debug.LogError("Castle map bounds are missing.", this);
                return;
            }

            var wanderBounds = new EnemyActivityBounds(mapBounds.Min, mapBounds.Max);
            var targets = new ActorTargetPerceptionSource(context.SlimeManager,
                context.EnemyManager, context.ConfigRegistry, context.GuardManager,
                context.MechanismSystem);
            var wander = new RandomWalkPerceptionSource(context.SlimeManager,
                context.EnemyManager, context.MovementSystem, wanderBounds,
                context.MapManager, Mathf.Max(0.1f, wanderDestinationRadius));
            context.BindActorPorts(new CastlePerception(targets, wander,
                context.SlimeManager, context.MechanismSystem), null);

            for (var i = 0; i < slimeCount; i++)
            {
                var point = spawnPoints[i];
                if (!context.SlimeManager.SpawnSlime(new SlimeSpawnRequest(
                    slimeConfig.ConfigId, point.Position)).IsValid)
                    Debug.LogError("Castle Slime spawn failed: " +
                        context.SlimeManager.LastFailureReason, this);
            }
            foreach (var point in enemyPoints)
            {
                if (!context.EnemyManager.SpawnEnemy(new EnemySpawnRequest(
                    enemyConfig.ConfigId, point.Position)).IsValid)
                    Debug.LogError("Castle enemy spawn failed: " +
                        context.EnemyManager.LastFailureReason, this);
            }
            InstallTreasureChest();
            installed = context;
        }

        private void InstallTreasureChest()
        {
            var chest = FindSceneObject("TreasureChest");
            if (chest == null)
            {
                Debug.LogWarning("Castle map has no TreasureChest object.", this);
                return;
            }

            var spriteRenderer = chest.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogWarning("Castle TreasureChest requires a SpriteRenderer.", chest);
                return;
            }

            if (!chest.activeSelf) chest.SetActive(true);
            var trigger = chest.GetComponent<BoxCollider2D>();
            if (trigger == null) trigger = chest.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            var scale = chest.transform.lossyScale;
            trigger.size = new Vector2(
                spriteRenderer.bounds.size.x / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                spriteRenderer.bounds.size.y / Mathf.Max(0.001f, Mathf.Abs(scale.y)));

            var pickup = chest.GetComponent<TreasureChestPickupAuthoring>();
            if (pickup == null) pickup = chest.AddComponent<TreasureChestPickupAuthoring>();
            pickup.Initialize(levelManager, treasureChestFoodReward);
        }

        private GameObject FindSceneObject(string objectName)
        {
            var roots = gameObject.scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var candidate in transforms)
                    if (candidate.name == objectName) return candidate.gameObject;
            }
            return null;
        }
    }
}
