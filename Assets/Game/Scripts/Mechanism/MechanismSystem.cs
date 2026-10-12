using System;
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Slimes;
using SlimeBattle.Status;
using SlimeBattle.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.Mechanisms
{
    /// <summary>Level-scoped owner of scene and placed mechanisms and their rule state.</summary>
    public sealed class MechanismSystem : IDisposable
    {
        private abstract class MechanismRuntimeData
        {
            protected MechanismRuntimeData(MechanismId id, MechanismConfig config,
                MechanismAuthoring authoring, bool isPlayerPlaced)
            {
                Id = id;
                Config = config;
                Authoring = authoring;
                IsPlayerPlaced = isPlayerPlaced;
                State = MechanismState.Available;
                RemainingActivations = config.ActivationLimit;
            }

            public MechanismId Id { get; }
            public MechanismConfig Config { get; }
            public MechanismAuthoring Authoring { get; }
            public bool IsPlayerPlaced { get; }
            public MechanismState State { get; set; }
            public int RemainingActivations { get; set; }
            public float CooldownRemainingSeconds { get; set; }
            public bool RegisteredMovementBlocker { get; set; }
        }

        private sealed class FoodRuntimeData : MechanismRuntimeData
        {
            public FoodRuntimeData(MechanismId id, MechanismConfig config,
                MechanismAuthoring authoring, bool isPlayerPlaced)
                : base(id, config, authoring, isPlayerPlaced) { }
        }

        private sealed class FurnaceRuntimeData : MechanismRuntimeData
        {
            public FurnaceRuntimeData(MechanismId id, MechanismConfig config,
                MechanismAuthoring authoring, bool isPlayerPlaced)
                : base(id, config, authoring, isPlayerPlaced) { }

            public Dictionary<ActorId, HashSet<int>> ContactColliders { get; } =
                new Dictionary<ActorId, HashSet<int>>();
        }

        private sealed class ObstacleRuntimeData : MechanismRuntimeData
        {
            public ObstacleRuntimeData(MechanismId id, MechanismConfig config,
                MechanismAuthoring authoring, bool isPlayerPlaced)
                : base(id, config, authoring, isPlayerPlaced) { }
        }

        private readonly ConfigRegistry configs;
        private readonly MapManager mapManager;
        private readonly StatusManager statusManager;
        private readonly ILevelEventBus eventBus;
        private readonly Scene scene;
        private readonly int maximumActivePlacedMechanisms;
        private readonly Dictionary<MechanismId, MechanismRuntimeData> mechanisms =
            new Dictionary<MechanismId, MechanismRuntimeData>();
        private readonly Dictionary<ConfigId, float> placementCooldowns =
            new Dictionary<ConfigId, float>();
        private readonly HashSet<ActorId> actorsReportedFurnaceHeat = new HashSet<ActorId>();
        private readonly List<SubscriptionToken> subscriptions = new List<SubscriptionToken>();
        private bool paused;
        private bool disposed;

        internal MechanismSystem(ConfigRegistry configs, int maximumActivePlacedMechanisms,
            MapManager mapManager, StatusManager statusManager,
            ILevelEventBus eventBus, Scene scene)
        {
            this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
            this.mapManager = mapManager ?? throw new ArgumentNullException(nameof(mapManager));
            this.statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
            this.eventBus = eventBus;
            this.scene = scene;
            if (maximumActivePlacedMechanisms < 0 || maximumActivePlacedMechanisms > 5)
                throw new ArgumentOutOfRangeException(nameof(maximumActivePlacedMechanisms));
            this.maximumActivePlacedMechanisms = maximumActivePlacedMechanisms;
            BindSceneMechanisms();
            SubscribeToActorEvents();
        }

        public bool NotifyContact(MechanismTriggerContext context)
        {
            if (disposed || paused || !context.IsValid ||
                !mechanisms.TryGetValue(context.MechanismId, out var mechanism) ||
                mechanism.State == MechanismState.Removed || mechanism.Authoring == null)
                return false;

            if (mechanism.Config.Kind == MechanismKind.Food)
            {
                if (context.ContactKind != MechanismContactKind.Entered ||
                    context.ActorType != ActorType.Slime) return false;
                return RequestActivate(mechanism.Id, new MechanismActivationRequest(
                    context.ActorId, MechanismActivationKind.Contact, context.ActorPosition));
            }

            if (!(mechanism is FurnaceRuntimeData furnace) ||
                context.ActorType != ActorType.Slime)
                return false;

            if (context.ContactKind == MechanismContactKind.Exited)
                return NotifyFurnaceExited(furnace, context);

            return NotifyFurnaceEntered(furnace, context);
        }

        public bool RequestActivate(MechanismId mechanismId,
            MechanismActivationRequest request)
        {
            if (disposed || paused || !request.IsValid ||
                !mechanisms.TryGetValue(mechanismId, out var mechanism) ||
                mechanism.State != MechanismState.Available ||
                mechanism.Config.Kind == MechanismKind.Obstacle)
                return false;

            bool accepted;
            if (mechanism.Config.Kind == MechanismKind.Food)
            {
                accepted = statusManager.NotifyGuideReached(
                    request.ActorId, StatusGuideKind.Food);
            }
            else if (mechanism.Config.Kind == MechanismKind.Furnace)
            {
                accepted = statusManager.NotifyGuideReached(
                    request.ActorId, StatusGuideKind.Furnace);
            }
            else
            {
                return false;
            }

            if (!accepted) return false;

            var consumed = ConsumeActivation(mechanism, request.ActorId);
            var result = new MechanismActivationResult(mechanism.Id,
                request.ActorId, true, consumed, "Activation rule accepted.");
            eventBus?.Publish(new MechanismTriggeredEvent(result));
            return true;
        }

        public bool TryGetSnapshot(MechanismId mechanismId,
            out MechanismSnapshot snapshot)
        {
            if (!disposed && mechanismId.IsValid &&
                mechanisms.TryGetValue(mechanismId, out var mechanism))
            {
                snapshot = CreateSnapshot(mechanism);
                return true;
            }

            snapshot = default(MechanismSnapshot);
            return false;
        }

        public IReadOnlyList<MechanismSnapshot> GetActiveSnapshots()
        {
            if (disposed) return Array.Empty<MechanismSnapshot>();
            var result = new List<MechanismSnapshot>();
            foreach (var mechanism in mechanisms.Values)
            {
                if (mechanism.State == MechanismState.Removed ||
                    mechanism.State == MechanismState.Exhausted) continue;
                result.Add(CreateSnapshot(mechanism));
            }
            result.Sort((left, right) => string.CompareOrdinal(
                left.MechanismId.Value, right.MechanismId.Value));
            return result.AsReadOnly();
        }

        public IReadOnlyList<GuideSourceSnapshot> GetGuideSources()
        {
            if (disposed) return Array.Empty<GuideSourceSnapshot>();
            var result = new List<GuideSourceSnapshot>();
            foreach (var mechanism in mechanisms.Values)
            {
                var sourceType = mechanism.Config.GuideSourceType;
                if (sourceType == GuideSourceType.None ||
                    mechanism.State == MechanismState.Removed ||
                    mechanism.State == MechanismState.Exhausted ||
                    mechanism.Authoring == null) continue;
                result.Add(new GuideSourceSnapshot(sourceType, mechanism.Id,
                    default(MapAreaId), mechanism.Config.ConfigId,
                    mechanism.Authoring.transform.position,
                    mechanism.Config.AttractionRadiusPixels,
                    mechanism.State == MechanismState.Available, false));
            }

            foreach (var area in mapManager.GetMapAreaSnapshots())
            {
                if (area.Kind != MapAreaKind.Region || area.RegionKind != MapRegionKind.Warm ||
                    area.GuidanceRadiusPixels <= 0f ||
                    !mapManager.TryGetMapAreaCenter(area.AreaId, out var center)) continue;
                result.Add(new GuideSourceSnapshot(GuideSourceType.Warmth,
                    default(MechanismId), area.AreaId,
                    new ConfigId(area.StatusEffectConfigId), center,
                    area.GuidanceRadiusPixels, true, true));
            }

            result.Sort(CompareGuideSources);
            return result.AsReadOnly();
        }

        /// <summary>Converts authored pixel ranges to world units for this Actor's sprite scale.</summary>
        public IReadOnlyList<GuideSourceSnapshot> GetGuideSourcesInRange(
            Vector2 actorPosition, float pixelsPerUnit)
        {
            if (!IsFinite(pixelsPerUnit) || pixelsPerUnit <= 0f)
                return Array.Empty<GuideSourceSnapshot>();

            var sources = GetGuideSources();
            var result = new List<GuideSourceSnapshot>();
            for (var i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (!source.IsAvailable) continue;
                var rangeWorldUnits = source.AttractionRadiusPixels / pixelsPerUnit;
                if ((source.Position - actorPosition).sqrMagnitude <=
                    rangeWorldUnits * rangeWorldUnits)
                    result.Add(source);
            }
            return result.AsReadOnly();
        }

        public bool CanPlace(ConfigId configId, Vector2 position, out string failureReason)
        {
            failureReason = string.Empty;
            if (disposed || paused)
            {
                failureReason = "The level is not accepting placements.";
                return false;
            }
            if (!configs.TryGetMechanism(configId, out var config))
            {
                failureReason = "MechanismConfig is missing or ambiguous.";
                return false;
            }
            if (!config.PlayerPlaceable)
            {
                failureReason = "This mechanism cannot be placed by the player.";
                return false;
            }
            if (CountActivePlacedMechanisms() >= maximumActivePlacedMechanisms)
            {
                failureReason = "The active placed-mechanism limit was reached.";
                return false;
            }
            if (placementCooldowns.TryGetValue(configId, out var cooldown) && cooldown > 0f)
            {
                failureReason = "This mechanism is still on placement cooldown.";
                return false;
            }
            if (!IsFinite(position.x) || !IsFinite(position.y) ||
                !mapManager.CanOccupy(position, new OccupancyQuery(
                    config.PlacementRadiusWorldUnits, requireWithinBounds: true,
                    checkStaticBlockers: true)))
            {
                failureReason = "The requested position is outside the map or blocked.";
                return false;
            }
            return true;
        }

        /// <summary>Registers a prefab instance after PlacementSystem has accepted its inventory request.</summary>
        public MechanismPlacementResult RequestPlace(MechanismPlacementRequest request)
        {
            if (!request.IsValid)
                return new MechanismPlacementResult(false, default(MechanismId),
                    "The placement request is invalid.");

            var authoring = request.Authoring;
            if (authoring.IsBound || authoring.gameObject.scene.handle != scene.handle)
                return new MechanismPlacementResult(false, default(MechanismId),
                    "The mechanism instance must be unregistered and belong to this level scene.");
            var configId = new ConfigId(authoring.ConfigId);
            if (!CanPlace(configId, request.Position, out var failureReason))
                return new MechanismPlacementResult(false, default(MechanismId), failureReason);
            if (!configs.TryGetMechanism(configId, out var config))
                return new MechanismPlacementResult(false, default(MechanismId),
                    "MechanismConfig is missing or ambiguous.");

            var previousPosition = authoring.transform.position;
            authoring.transform.position = request.Position;
            var id = MechanismId.NewId();
            if (!TryRegisterAuthoring(authoring, id, config, isPlayerPlaced: true))
            {
                authoring.transform.position = previousPosition;
                return new MechanismPlacementResult(false, default(MechanismId),
                    "The mechanism instance could not be registered.");
            }

            return new MechanismPlacementResult(true, id, string.Empty);
        }

        public bool RequestRemove(MechanismId mechanismId)
        {
            if (disposed || paused || !mechanisms.TryGetValue(mechanismId, out var mechanism) ||
                !mechanism.IsPlayerPlaced || mechanism.State == MechanismState.Removed ||
                mechanism.State == MechanismState.Exhausted) return false;

            RemoveRuntimeMechanism(mechanism, "Removed by PlacementSystem.",
                startPlacementCooldown: true, hideAuthoring: true);
            return true;
        }

        public bool TryGetPlacementCooldown(ConfigId configId,
            out PlacementCooldown cooldown)
        {
            cooldown = default(PlacementCooldown);
            if (disposed || !configs.TryGetMechanism(configId, out var config)) return false;
            placementCooldowns.TryGetValue(configId, out var remaining);
            cooldown = new PlacementCooldown(configId, remaining);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (disposed || paused || !IsFinite(deltaTime) || deltaTime <= 0f) return;

            TickActivationCooldowns(deltaTime);
            TickPlacementCooldowns(deltaTime);
            PublishFurnaceHeatResults();
        }

        public void Pause()
        {
            if (!disposed) paused = true;
        }

        public void Resume()
        {
            if (!disposed) paused = false;
        }

        internal void NotifyAuthoringDestroyed(MechanismAuthoring authoring)
        {
            if (disposed || ReferenceEquals(authoring, null)) return;
            var id = authoring.MechanismId;
            if (!mechanisms.TryGetValue(id, out var mechanism) ||
                !ReferenceEquals(mechanism.Authoring, authoring))
                return;
            RemoveRuntimeMechanism(mechanism, "Mechanism authoring was destroyed.",
                startPlacementCooldown: mechanism.IsPlayerPlaced, hideAuthoring: false);
        }

        public void Dispose()
        {
            if (disposed) return;
            var snapshot = new List<MechanismRuntimeData>(mechanisms.Values);
            foreach (var mechanism in snapshot)
            {
                ReleaseFurnaceContacts(mechanism);
                if (mechanism.RegisteredMovementBlocker && mechanism.Authoring != null)
                    mapManager.UnregisterMovementBlocker(mechanism.Authoring.SolidCollider);
                if (mechanism.Authoring != null) mechanism.Authoring.Unbind(this);
                if (mechanism.IsPlayerPlaced && mechanism.Authoring != null)
                    DestroyPlacedObject(mechanism.Authoring.gameObject);
            }
            mechanisms.Clear();
            placementCooldowns.Clear();
            actorsReportedFurnaceHeat.Clear();
            if (eventBus != null)
                foreach (var token in subscriptions) eventBus.Unsubscribe(token);
            subscriptions.Clear();
            disposed = true;
        }

        private void BindSceneMechanisms()
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var authoring in root.GetComponentsInChildren<MechanismAuthoring>(true))
                {
                    if (authoring == null || authoring.gameObject.scene.handle != scene.handle ||
                        !authoring.isActiveAndEnabled) continue;
                    if (!configs.TryGetMechanism(new ConfigId(authoring.ConfigId), out var config))
                    {
                        Debug.LogError("MechanismSystem: MechanismConfig is missing or ambiguous for '" +
                            authoring.name + "'.", authoring);
                        continue;
                    }
                    if (!authoring.MechanismId.IsValid)
                    {
                        Debug.LogError("MechanismSystem: scene mechanisms require a stable MechanismId.",
                            authoring);
                        continue;
                    }
                    TryRegisterAuthoring(authoring, authoring.MechanismId, config,
                        isPlayerPlaced: false);
                }
            }
        }

        private bool TryRegisterAuthoring(MechanismAuthoring authoring, MechanismId id,
            MechanismConfig config, bool isPlayerPlaced)
        {
            if (disposed || authoring == null || !id.IsValid || config == null ||
                mechanisms.ContainsKey(id))
            {
                if (mechanisms.ContainsKey(id))
                    Debug.LogError("MechanismSystem: duplicate MechanismId '" + id + "'.", authoring);
                return false;
            }

            if (config.Kind == MechanismKind.Food || config.Kind == MechanismKind.Furnace)
            {
                var interaction = authoring.InteractionCollider;
                if (interaction == null || !interaction.isTrigger)
                {
                    Debug.LogError("MechanismSystem: Food and Furnace require an interaction trigger collider.",
                        authoring);
                    return false;
                }
            }
            else if (config.Kind == MechanismKind.Obstacle)
            {
                var solid = authoring.SolidCollider;
                if (solid == null || solid.isTrigger)
                {
                    Debug.LogError("MechanismSystem: Obstacle requires a solid Collider2D.", authoring);
                    return false;
                }
            }
            else
            {
                Debug.LogError("MechanismSystem: unsupported MechanismKind on '" + authoring.name + "'.",
                    authoring);
                return false;
            }

            var runtime = CreateRuntimeData(id, config, authoring, isPlayerPlaced);
            if (runtime is ObstacleRuntimeData && isPlayerPlaced)
            {
                if (!mapManager.RegisterMovementBlocker(authoring.SolidCollider))
                {
                    Debug.LogError("MechanismSystem: placed obstacle could not register with MapManager.",
                        authoring);
                    return false;
                }
                runtime.RegisteredMovementBlocker = true;
            }

            authoring.AssignMechanismId(id);
            authoring.Bind(this);
            mechanisms.Add(id, runtime);
            PublishStateChanged(runtime, MechanismState.Removed, "Registered.");
            return true;
        }

        private bool NotifyFurnaceEntered(FurnaceRuntimeData furnace,
            MechanismTriggerContext context)
        {
            var actorAlreadyInAnyFurnace = ActorIsInAnyFurnace(context.ActorId);
            var colliderId = context.ContactColliderId == 0
                ? context.ActorId.GetHashCode() : context.ContactColliderId;
            if (!furnace.ContactColliders.TryGetValue(context.ActorId, out var colliders))
            {
                colliders = new HashSet<int>();
                furnace.ContactColliders.Add(context.ActorId, colliders);
            }
            if (!colliders.Add(colliderId)) return false;
            if (actorAlreadyInAnyFurnace) return true;

            if (!statusManager.NotifyFurnaceCoreEntered(context.ActorId))
            {
                colliders.Remove(colliderId);
                if (colliders.Count == 0) furnace.ContactColliders.Remove(context.ActorId);
                return false;
            }
            statusManager.NotifyGuideReached(context.ActorId, StatusGuideKind.Furnace);
            if (statusManager.HasStatus(context.ActorId, StatusKind.Hot))
                actorsReportedFurnaceHeat.Add(context.ActorId);
            else
                actorsReportedFurnaceHeat.Remove(context.ActorId);

            var result = new MechanismActivationResult(furnace.Id, context.ActorId,
                true, false, "Furnace core exposure started.");
            eventBus?.Publish(new MechanismTriggeredEvent(result));
            return true;
        }

        private bool NotifyFurnaceExited(FurnaceRuntimeData furnace,
            MechanismTriggerContext context)
        {
            var colliderId = context.ContactColliderId == 0
                ? context.ActorId.GetHashCode() : context.ContactColliderId;
            if (!furnace.ContactColliders.TryGetValue(context.ActorId, out var colliders) ||
                !colliders.Remove(colliderId)) return false;
            if (colliders.Count > 0) return true;

            furnace.ContactColliders.Remove(context.ActorId);
            if (!ActorIsInAnyFurnace(context.ActorId))
            {
                statusManager.NotifyFurnaceCoreExited(context.ActorId);
                actorsReportedFurnaceHeat.Remove(context.ActorId);
            }
            return true;
        }

        private bool ConsumeActivation(MechanismRuntimeData mechanism, ActorId actorId)
        {
            if (mechanism.Config.ActivationLimit > 0)
            {
                mechanism.RemainingActivations = Mathf.Max(0,
                    mechanism.RemainingActivations - 1);
                if (mechanism.RemainingActivations == 0)
                {
                    SetMechanismState(mechanism, MechanismState.Exhausted,
                        "Activation limit reached.");
                    eventBus?.Publish(new MechanismExhaustedEvent(
                        CreateSnapshot(mechanism), actorId));
                    if (mechanism.Authoring != null)
                    {
                        mechanism.Authoring.Unbind(this);
                        mechanism.Authoring.SetMechanismVisible(false);
                    }
                    if (mechanism.IsPlayerPlaced)
                    {
                        StartPlacementCooldown(mechanism.Config);
                        if (mechanism.Authoring != null)
                            DestroyPlacedObject(mechanism.Authoring.gameObject);
                    }
                    return true;
                }
            }

            if (mechanism.Config.ActivationCooldownSeconds > 0f)
            {
                mechanism.CooldownRemainingSeconds = mechanism.Config.ActivationCooldownSeconds;
                SetMechanismState(mechanism, MechanismState.Cooldown,
                    "Activation cooldown started.");
            }
            return false;
        }

        private void TickActivationCooldowns(float deltaTime)
        {
            foreach (var mechanism in mechanisms.Values)
            {
                if (mechanism.State != MechanismState.Cooldown) continue;
                mechanism.CooldownRemainingSeconds = Mathf.Max(0f,
                    mechanism.CooldownRemainingSeconds - deltaTime);
                if (mechanism.CooldownRemainingSeconds <= 0f)
                    SetMechanismState(mechanism, MechanismState.Available,
                        "Activation cooldown completed.");
            }
        }

        private void TickPlacementCooldowns(float deltaTime)
        {
            if (placementCooldowns.Count == 0) return;
            var configIds = new List<ConfigId>(placementCooldowns.Keys);
            foreach (var configId in configIds)
            {
                var remaining = Mathf.Max(0f, placementCooldowns[configId] - deltaTime);
                if (remaining <= 0f) placementCooldowns.Remove(configId);
                else placementCooldowns[configId] = remaining;
                eventBus?.Publish(new MechanismPlacementCooldownChangedEvent(
                    new PlacementCooldown(configId, remaining)));
            }
        }

        private void PublishFurnaceHeatResults()
        {
            foreach (var mechanism in mechanisms.Values)
            {
                if (!(mechanism is FurnaceRuntimeData furnace)) continue;
                foreach (var actorId in furnace.ContactColliders.Keys)
                {
                    if (actorsReportedFurnaceHeat.Contains(actorId) ||
                        !statusManager.HasStatus(actorId, StatusKind.Hot)) continue;
                    actorsReportedFurnaceHeat.Add(actorId);
                    var result = new MechanismActivationResult(furnace.Id, actorId,
                        true, false, "Furnace exposure applied Hot.");
                    eventBus?.Publish(new MechanismTriggeredEvent(result));
                }
            }
        }

        private void SubscribeToActorEvents()
        {
            if (eventBus == null) return;
            subscriptions.Add(eventBus.Subscribe<SlimeDiedEvent>(
                e => NotifyActorRemoved(e.Snapshot.ActorId)));
            subscriptions.Add(eventBus.Subscribe<SlimeDespawnedEvent>(
                e => NotifyActorRemoved(e.ActorId)));
            subscriptions.Add(eventBus.Subscribe<EnemyDiedEvent>(
                e => NotifyActorRemoved(e.Snapshot.ActorId)));
            subscriptions.Add(eventBus.Subscribe<EnemyDespawnedEvent>(
                e => NotifyActorRemoved(e.ActorId)));
        }

        private void NotifyActorRemoved(ActorId actorId)
        {
            if (!actorId.IsValid) return;
            var wasInFurnace = false;
            foreach (var mechanism in mechanisms.Values)
            {
                if (mechanism is FurnaceRuntimeData furnace &&
                    furnace.ContactColliders.Remove(actorId)) wasInFurnace = true;
            }
            actorsReportedFurnaceHeat.Remove(actorId);
            if (wasInFurnace) statusManager.NotifyFurnaceCoreExited(actorId);
        }

        private void RemoveRuntimeMechanism(MechanismRuntimeData mechanism,
            string reason, bool startPlacementCooldown, bool hideAuthoring)
        {
            if (mechanism.State == MechanismState.Removed) return;
            ReleaseFurnaceContacts(mechanism);
            if (mechanism.RegisteredMovementBlocker && mechanism.Authoring != null)
            {
                mapManager.UnregisterMovementBlocker(mechanism.Authoring.SolidCollider);
                mechanism.RegisteredMovementBlocker = false;
            }
            if (mechanism.Authoring != null)
            {
                mechanism.Authoring.Unbind(this);
                if (hideAuthoring) mechanism.Authoring.SetMechanismVisible(false);
            }

            SetMechanismState(mechanism, MechanismState.Removed, reason);
            if (startPlacementCooldown && mechanism.Config.PlacementCooldownSeconds > 0f)
                StartPlacementCooldown(mechanism.Config);
            if (mechanism.IsPlayerPlaced && hideAuthoring && mechanism.Authoring != null)
                DestroyPlacedObject(mechanism.Authoring.gameObject);
        }

        private void ReleaseFurnaceContacts(MechanismRuntimeData mechanism)
        {
            if (!(mechanism is FurnaceRuntimeData furnace) ||
                furnace.ContactColliders.Count == 0) return;
            var actors = new List<ActorId>(furnace.ContactColliders.Keys);
            furnace.ContactColliders.Clear();
            foreach (var actorId in actors)
            {
                if (ActorIsInAnyFurnace(actorId)) continue;
                statusManager.NotifyFurnaceCoreExited(actorId);
                actorsReportedFurnaceHeat.Remove(actorId);
            }
        }

        private bool ActorIsInAnyFurnace(ActorId actorId)
        {
            foreach (var mechanism in mechanisms.Values)
            {
                if (mechanism is FurnaceRuntimeData furnace &&
                    furnace.ContactColliders.ContainsKey(actorId)) return true;
            }
            return false;
        }

        private int CountActivePlacedMechanisms()
        {
            var count = 0;
            foreach (var mechanism in mechanisms.Values)
            {
                if (mechanism.IsPlayerPlaced && mechanism.State != MechanismState.Removed &&
                    mechanism.State != MechanismState.Exhausted) count++;
            }
            return count;
        }

        private void StartPlacementCooldown(MechanismConfig config)
        {
            if (config == null || config.PlacementCooldownSeconds <= 0f) return;
            placementCooldowns[config.ConfigId] = config.PlacementCooldownSeconds;
            eventBus?.Publish(new MechanismPlacementCooldownChangedEvent(
                new PlacementCooldown(config.ConfigId, config.PlacementCooldownSeconds)));
        }

        private void SetMechanismState(MechanismRuntimeData mechanism,
            MechanismState state, string reason)
        {
            if (mechanism.State == state) return;
            var previous = mechanism.State;
            mechanism.State = state;
            PublishStateChanged(mechanism, previous, reason);
        }

        private void PublishStateChanged(MechanismRuntimeData mechanism,
            MechanismState previousState, string reason)
        {
            eventBus?.Publish(new MechanismStateChangedEvent(
                CreateSnapshot(mechanism), previousState, reason));
        }

        private static MechanismSnapshot CreateSnapshot(MechanismRuntimeData mechanism)
        {
            var position = mechanism.Authoring == null
                ? Vector2.zero : (Vector2)mechanism.Authoring.transform.position;
            return new MechanismSnapshot(mechanism.Id, mechanism.Config.ConfigId,
                mechanism.Config.Kind, position, mechanism.State, mechanism.IsPlayerPlaced,
                mechanism.Config.ActivationLimit, mechanism.RemainingActivations,
                mechanism.CooldownRemainingSeconds, mechanism.Config.GuideSourceType,
                mechanism.Config.AttractionRadiusPixels);
        }

        private static MechanismRuntimeData CreateRuntimeData(MechanismId id,
            MechanismConfig config, MechanismAuthoring authoring, bool isPlayerPlaced)
        {
            switch (config.Kind)
            {
                case MechanismKind.Food:
                    return new FoodRuntimeData(id, config, authoring, isPlayerPlaced);
                case MechanismKind.Furnace:
                    return new FurnaceRuntimeData(id, config, authoring, isPlayerPlaced);
                case MechanismKind.Obstacle:
                    return new ObstacleRuntimeData(id, config, authoring, isPlayerPlaced);
                default:
                    return null;
            }
        }

        private static int CompareGuideSources(GuideSourceSnapshot left,
            GuideSourceSnapshot right)
        {
            var typeOrder = left.Type.CompareTo(right.Type);
            if (typeOrder != 0) return typeOrder;
            var leftId = left.IsMapAreaSource ? left.AreaId.Value : left.MechanismId.Value;
            var rightId = right.IsMapAreaSource ? right.AreaId.Value : right.MechanismId.Value;
            return string.CompareOrdinal(leftId, rightId);
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static void DestroyPlacedObject(GameObject value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
