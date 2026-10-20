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
        [Header("First-level map props")]
        [SerializeField] private Sprite rollingRockSprite;
        [SerializeField] private Sprite treasureChestSprite;
        [SerializeField] private Sprite swampSprite;
        [SerializeField] private Sprite eliteAltarSprite;
        [SerializeField, Min(0f)] private float rollingRockDamage = 20f;
        [SerializeField, Min(0.01f)] private float rollingRockSpeed = 6f;
        [SerializeField, Min(0f)] private float rollingRockDelaySeconds = 1f;
        [SerializeField, Min(0)] private int treasureChestFoodReward = 4;
        [SerializeField] private bool startCurrentSceneOnPlay;
        [SerializeField] private string defaultRouteId = "route_escort";
        [SerializeField, Min(0.1f)] private float slimeWanderRadius = 1.5f;
        [SerializeField] private Vector2 enemyActivityHalfExtents =
            new Vector2(4f, 4f);
        private LevelContext installed;
        private readonly List<GameObject> runtimeTrapSensors =
            new List<GameObject>();
        private readonly List<GameObject> runtimeMapObjects =
            new List<GameObject>();

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
            ClearRuntimeTrapSensors();
            ClearRuntimeMapObjects();
        }

        private void OnLevelStarted(LevelStartedEvent started)
        {
            if (started.LevelId.Value == gameObject.scene.name) Install(started.Context);
        }

        private void Install(LevelContext context)
        {
            if (context == null || context == installed) return;
            ClearRuntimeTrapSensors();
            ClearRuntimeMapObjects();
            if (slimeConfig == null || enemyConfig == null || mapRoot == null ||
                entitiesRoot == null || mechanismsRoot == null ||
                presentationRoot == null || context.GuardManager == null)
            {
                Debug.LogError($"FirstLevelCompositionRoot references are incomplete " +
                    $"(slimeConfig={slimeConfig != null}, enemyConfig={enemyConfig != null}, " +
                    $"mapRoot={mapRoot != null}, entitiesRoot={entitiesRoot != null}, " +
                    $"mechanismsRoot={mechanismsRoot != null}, " +
                    $"presentationRoot={presentationRoot != null}, " +
                    $"guardManager={context.GuardManager != null}).", this);
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
            var slimeSpawnPoints = new List<MapPointSnapshot>(
                context.MapManager.GetMapPoints(MapPointKind.SlimeSpawn));
            slimeSpawnPoints.Sort((left, right) => string.CompareOrdinal(
                left.PointId.Value, right.PointId.Value));
            var slimeCount = context.CampaignResources.SlimeCount;
            if (slimeCount > slimeSpawnPoints.Count)
            {
                Debug.LogError($"First level requires {slimeCount} Slime spawn points; " +
                    $"MAP_FirstLevel provides {slimeSpawnPoints.Count}.", this);
                return;
            }
            for (var i = 0; i < slimeCount; i++)
            {
                var slimeId = context.SlimeManager.SpawnSlime(new SlimeSpawnRequest(
                    slimeConfig.ConfigId, slimeSpawnPoints[i].Position));
                if (!slimeId.IsValid)
                {
                    Debug.LogError("Initial Slime spawn failed: " +
                        context.SlimeManager.LastFailureReason, this);
                    return;
                }
            }
            SpawnEnemiesAt(context, context.MapManager.GetMapPoints(MapPointKind.EnemySpawn),
                hasMapBounds ? mapBounds : default(MapBounds));
            // The current level has no dedicated elite EnemyConfig yet; the altar point is
            // connected to the same EnemyConfig until one is authored.
            SpawnEnemiesAt(context, context.MapManager.GetMapPoints(MapPointKind.EliteSpawn),
                hasMapBounds ? mapBounds : default(MapBounds));
            InstallRuntimeTrapSensors(context);
            InstallRuntimeMapObjects(context);
            installed = context;
        }

        private void SpawnEnemiesAt(LevelContext context,
            IReadOnlyList<MapPointSnapshot> points, MapBounds bounds)
        {
            foreach (var point in points)
            {
                var activityBounds = CreateEnemyActivityBounds(point.Position,
                    bounds);
                if (!context.EnemyManager.SpawnEnemy(new EnemySpawnRequest(
                    enemyConfig.ConfigId, point.Position,
                    activityBounds: activityBounds)).IsValid)
                    Debug.LogError("Enemy spawn failed: " +
                        context.EnemyManager.LastFailureReason, this);
            }
        }

        private void InstallRuntimeMapObjects(LevelContext context)
        {
            if (context == null || context.MapManager == null) return;

            CreateMapDecoration(context, "swamp_art_01", swampSprite,
                "Swamp", -95);
            CreateMapDecoration(context, "stone_art_02", rollingRockSprite,
                "Static Stone", -80);
            CreateMapDecoration(context, "elite_altar_art_01", eliteAltarSprite,
                "Elite Altar", -80);

            if (context.MapManager.TryGetMapPoint(new MapPointId("treasure_chest_01"),
                out var chestPoint) && treasureChestSprite != null)
            {
                var chest = CreateMapObject("TreasureChest", treasureChestSprite,
                    chestPoint.Position, -80);
                var trigger = chest.AddComponent<BoxCollider2D>();
                trigger.isTrigger = true;
                trigger.size = chest.GetComponent<SpriteRenderer>().bounds.size;
                chest.AddComponent<TreasureChestPickupAuthoring>().Initialize(
                    levelManager, treasureChestFoodReward);
                runtimeMapObjects.Add(chest);
            }
            else
            {
                Debug.LogError("First-level treasure chest requires the " +
                    "treasure_chest_01 map point and its sprite reference.", this);
            }

            if (!context.MapManager.TryGetMapPoint(new MapPointId("rolling_rock_01"),
                out var rockPoint) || rollingRockSprite == null)
            {
                Debug.LogError("First-level rolling rock requires the " +
                    "rolling_rock_01 map point and its sprite reference.", this);
                return;
            }

            if (!TryGetAreaBounds(new MapAreaId("rolling_rock_trigger_01"),
                out var triggerMin, out var triggerMax))
            {
                Debug.LogError("First-level rolling rock requires the " +
                    "rolling_rock_trigger_01 hazard area.", this);
                return;
            }

            var rockObject = CreateMapObject("RollingRock", rollingRockSprite,
                rockPoint.Position, -80);
            var rockBody = rockObject.AddComponent<Rigidbody2D>();
            rockBody.bodyType = RigidbodyType2D.Kinematic;
            var rockCollider = rockObject.AddComponent<BoxCollider2D>();
            rockCollider.size = rockObject.GetComponent<SpriteRenderer>().bounds.size;
            var rock = rockObject.AddComponent<RollingRockAuthoring>();
            rock.Initialize(levelManager, rollingRockDamage, rollingRockSpeed,
                rollingRockDelaySeconds, triggerMax.x);
            runtimeMapObjects.Add(rockObject);

            var triggerObject = new GameObject("RollingRockTrigger");
            triggerObject.transform.SetParent(mapRoot, false);
            triggerObject.transform.position = new Vector3(
                (triggerMin.x + triggerMax.x) * 0.5f,
                (triggerMin.y + triggerMax.y) * 0.5f,
                mapRoot.position.z);
            var areaTrigger = triggerObject.AddComponent<BoxCollider2D>();
            areaTrigger.isTrigger = true;
            areaTrigger.size = triggerMax - triggerMin;
            triggerObject.AddComponent<RollingRockTriggerAuthoring>().Initialize(rock);
            runtimeMapObjects.Add(triggerObject);
        }

        private void CreateMapDecoration(LevelContext context, string pointId,
            Sprite sprite, string objectName, int sortingOrder)
        {
            if (sprite == null || !context.MapManager.TryGetMapPoint(
                new MapPointId(pointId), out var point))
            {
                Debug.LogError($"First-level {objectName} requires map point '{pointId}' " +
                    "and its sprite reference.", this);
                return;
            }
            runtimeMapObjects.Add(CreateMapObject(objectName, sprite,
                point.Position, sortingOrder));
        }

        private GameObject CreateMapObject(string objectName, Sprite sprite,
            Vector2 position, int sortingOrder)
        {
            var instance = new GameObject(objectName);
            instance.transform.SetParent(mapRoot, false);
            instance.transform.position = new Vector3(position.x, position.y,
                mapRoot.position.z);
            var renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return instance;
        }

        private bool TryGetAreaBounds(MapAreaId areaId,
            out Vector2 minimum, out Vector2 maximum)
        {
            minimum = default(Vector2);
            maximum = default(Vector2);
            var layout = mapRoot == null
                ? null : mapRoot.GetComponentInChildren<MapLayoutAuthoring>(true);
            if (layout == null || !layout.TryGetSnapshot(out var snapshot, out _))
                return false;
            foreach (var area in snapshot.Areas)
            {
                if (area.Area.AreaId != areaId) continue;
                minimum = area.BoundsMin;
                maximum = area.BoundsMax;
                return maximum.x > minimum.x && maximum.y > minimum.y;
            }
            return false;
        }

        private void ClearRuntimeMapObjects()
        {
            foreach (var instance in runtimeMapObjects)
            {
                if (instance == null) continue;
                instance.SetActive(false);
                Destroy(instance);
            }
            runtimeMapObjects.Clear();
        }

        private void InstallRuntimeTrapSensors(LevelContext context)
        {
            if (context == null || context.MapManager == null) return;
            var trapPoints = context.MapManager.GetMapPoints(MapPointKind.Trap);
            if (trapPoints.Count == 0) return;

            var layout = mapRoot.GetComponentInChildren<MapLayoutAuthoring>(true);
            var definition = layout == null ? null : layout.Definition;
            if (definition == null || definition.DesignPixelsPerUnityUnit <= 0f)
            {
                Debug.LogError("First-level trap points require a map layout with a valid design scale.", this);
                return;
            }

            foreach (var point in trapPoints)
            {
                if (point.InteractionRadiusPixels <= 0f)
                {
                    Debug.LogWarning($"Trap point '{point.PointId}' has no interaction radius; " +
                        "no damage trigger was created.", this);
                    continue;
                }

                var sensorObject = new GameObject("TrapDamage_" + point.PointId.Value);
                sensorObject.transform.SetParent(mapRoot, false);
                sensorObject.transform.position = point.Position;
                var sensor = sensorObject.AddComponent<CircleCollider2D>();
                sensor.isTrigger = true;
                sensor.radius = point.InteractionRadiusPixels /
                    definition.DesignPixelsPerUnityUnit;
                sensorObject.AddComponent<TrapDamageAuthoring>();
                runtimeTrapSensors.Add(sensorObject);
            }
        }

        private void ClearRuntimeTrapSensors()
        {
            foreach (var sensor in runtimeTrapSensors)
            {
                if (sensor == null) continue;
                sensor.SetActive(false);
                Destroy(sensor);
            }
            runtimeTrapSensors.Clear();
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
