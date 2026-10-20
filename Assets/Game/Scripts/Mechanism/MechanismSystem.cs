using System;
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Actors.Slimes;
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
                MechanismAuthoring authoring, bool isPlayerPlaced,
                PlacementAnchorId anchorId = default)
            {
                Id = id;
                Config = config;
                Authoring = authoring;
                IsPlayerPlaced = isPlayerPlaced;
                AnchorId = anchorId;
                State = MechanismState.Available;
                RemainingActivations = config.ActivationLimit;
            }

            public MechanismId Id { get; }
            public MechanismConfig Config { get; }
            public MechanismAuthoring Authoring { get; }
            public bool IsPlayerPlaced { get; }
            public PlacementAnchorId AnchorId { get; set; }
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

            public MechanismId FoodCartId { get; set; }
        }

        private sealed class FoodCartRuntimeData
        {
            public FoodCartRuntimeData(DiningCartAuthoring authoring)
            {
                Authoring = authoring;
            }

            public DiningCartAuthoring Authoring { get; }
            public MechanismId LoadedFoodId { get; set; }
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
        private readonly CampaignResourceLedger campaignResources;
        private readonly Dictionary<MechanismId, MechanismRuntimeData> mechanisms =
            new Dictionary<MechanismId, MechanismRuntimeData>();
        private readonly Dictionary<MechanismId, FoodCartRuntimeData> foodCarts =
            new Dictionary<MechanismId, FoodCartRuntimeData>();
        private readonly Dictionary<ConfigId, float> placementCooldowns =
            new Dictionary<ConfigId, float>();
        private readonly List<SubscriptionToken> subscriptions = new List<SubscriptionToken>();
        private Transform placedParent;
        private EscortWeapon weapon;
        private EscortWeaponAuthoring weaponAuthoring;
        private Transform weaponTransform;
        private float boardingRadius;
        private SlimeManager carrierSlimes;
        private EnemyManager carrierEnemies;
        private SlimeBattle.Guards.GuardManager carrierGuards;
        private readonly HashSet<ActorId> mustLeaveBoardingZone = new HashSet<ActorId>();
        private readonly Dictionary<ActorId, float> guideDepartureTimers =
            new Dictionary<ActorId, float>();
        private readonly System.Random departureRandom = new System.Random();
        private bool paused;
        private bool disposed;

        public int MaximumActivePlacedMechanisms => maximumActivePlacedMechanisms;
        public int ActivePlacedMechanismCount => disposed ? 0 : CountActivePlacedMechanisms();
        public bool HasCampaignInventory => campaignResources != null;
        public int GetAvailablePlacementStock(MechanismKind kind) =>
            disposed || campaignResources == null ? 0 : campaignResources.GetMechanismCount(kind);

        public bool TrySetPlacedParent(Transform parent)
        {
            if (disposed || parent == null || parent.gameObject.scene.handle != scene.handle)
                return false;
            placedParent = parent;
            return true;
        }

        internal MechanismSystem(ConfigRegistry configs, int maximumActivePlacedMechanisms,
            MapManager mapManager, StatusManager statusManager,
            ILevelEventBus eventBus, Scene scene,
            CampaignResourceLedger campaignResources = null)
        {
            this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
            this.mapManager = mapManager ?? throw new ArgumentNullException(nameof(mapManager));
            this.statusManager = statusManager ?? throw new ArgumentNullException(nameof(statusManager));
            this.eventBus = eventBus;
            this.scene = scene;
            this.campaignResources = campaignResources;
            if (maximumActivePlacedMechanisms < 0 || maximumActivePlacedMechanisms > 20)
                throw new ArgumentOutOfRangeException(nameof(maximumActivePlacedMechanisms));
            this.maximumActivePlacedMechanisms = maximumActivePlacedMechanisms;
            BindSceneMechanisms();
            BindSceneWeapon();
            SubscribeToActorEvents();
        }

        public bool TryGetWeaponSnapshot(out EscortWeaponSnapshot snapshot)
        {
            snapshot = default(EscortWeaponSnapshot);
            if (disposed || weapon == null) return false;
            snapshot = weapon.GetSnapshot();
            return true;
        }

        public IReadOnlyList<MechanismId> GetFoodCartIds()
        {
            if (disposed || foodCarts.Count == 0) return Array.Empty<MechanismId>();
            var result = new List<MechanismId>(foodCarts.Keys);
            result.Sort((left, right) => string.CompareOrdinal(left.Value, right.Value));
            return result.AsReadOnly();
        }

        public bool TryGetFoodCartAtPosition(Vector2 worldPosition, out MechanismId cartId)
        {
            cartId = default;
            if (disposed) return false;
            foreach (var pair in foodCarts)
            {
                var authoring = pair.Value.Authoring;
                if (authoring == null || !authoring.isActiveAndEnabled ||
                    !authoring.ContainsPointer(worldPosition)) continue;
                cartId = pair.Key;
                return true;
            }
            return false;
        }

        public bool TryGetFoodCartPosition(MechanismId cartId, out Vector2 position)
        {
            position = default;
            if (disposed || !foodCarts.TryGetValue(cartId, out var cart) ||
                cart.Authoring == null) return false;
            position = cart.Authoring.Position;
            return true;
        }

        public bool CanLoadFoodCart(MechanismId cartId, out string failureReason)
        {
            failureReason = string.Empty;
            if (disposed || paused)
            {
                failureReason = "The level is not accepting cart interactions.";
                return false;
            }
            if (!foodCarts.TryGetValue(cartId, out var cart) || cart.Authoring == null)
            {
                failureReason = "The dining cart is unavailable.";
                return false;
            }
            if (cart.LoadedFoodId.IsValid)
            {
                failureReason = "The dining cart already has food loaded.";
                return false;
            }
            if (!TryGetPlayerConfigId(MechanismKind.Food, out var foodConfigId) ||
                !configs.TryGetMechanism(foodConfigId, out _))
            {
                failureReason = "The player food mechanism is not configured.";
                return false;
            }
            if (campaignResources != null &&
                campaignResources.GetMechanismCount(MechanismKind.Food) <= 0)
            {
                failureReason = "The food inventory is empty.";
                return false;
            }
            return true;
        }

        public MechanismPlacementResult RequestLoadFoodCart(MechanismId cartId)
        {
            if (!CanLoadFoodCart(cartId, out var reason))
                return new MechanismPlacementResult(false, default, reason);
            if (!foodCarts.TryGetValue(cartId, out var cart) ||
                cart.Authoring.FoodSlot == null ||
                !TryGetPlayerConfigId(MechanismKind.Food, out var configId) ||
                !configs.TryGetMechanism(configId, out var config) ||
                config.PlacementPrefab == null ||
                new ConfigId(config.PlacementPrefab.ConfigId) != configId)
                return new MechanismPlacementResult(false, default,
                    "The dining cart or matching food prefab is incomplete.");

            var food = UnityEngine.Object.Instantiate(config.PlacementPrefab);
            SceneManager.MoveGameObjectToScene(food.gameObject, scene);
            food.transform.SetParent(cart.Authoring.FoodSlot, false);
            food.transform.localPosition = Vector3.zero;
            food.transform.localRotation = Quaternion.identity;
            cart.Authoring.SetFoodVisualSorting(food);

            var foodId = MechanismId.NewId();
            if (!TryRegisterAuthoring(food, foodId, config, isPlayerPlaced: false,
                publishEvent: false))
            {
                DestroyPlacedObject(food.gameObject);
                return new MechanismPlacementResult(false, default,
                    "The food mechanism could not be loaded onto the dining cart.");
            }

            var foodRuntime = (FoodRuntimeData)mechanisms[foodId];
            foodRuntime.FoodCartId = cartId;
            foodRuntime.RemainingActivations = 1;
            if (campaignResources != null &&
                !campaignResources.TrySpendMechanism(MechanismKind.Food))
            {
                RemoveRuntimeMechanism(foodRuntime, "Cart food inventory changed.",
                    startPlacementCooldown: false, hideAuthoring: false);
                DestroyPlacedObject(food.gameObject);
                return new MechanismPlacementResult(false, default,
                    "The food inventory is empty.");
            }

            cart.LoadedFoodId = foodId;
            PublishStateChanged(foodRuntime, MechanismState.Removed, "Loaded onto dining cart.");
            StartPlacementCooldown(config);
            NotifyPlacedCarrierContacts(foodRuntime);
            return new MechanismPlacementResult(true, foodId, string.Empty);
        }

        public bool RequestMoveFoodCart(MechanismId cartId, Vector2 worldPosition,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (disposed || paused || !foodCarts.TryGetValue(cartId, out var cart) ||
                cart.Authoring == null)
            {
                failureReason = "The dining cart cannot move right now.";
                return false;
            }
            if (!cart.Authoring.TryProjectToTrack(worldPosition, out var projected))
            {
                failureReason = "The dining cart must stay on its configured track.";
                return false;
            }
            if (!mapManager.CanOccupy(projected, new OccupancyQuery(
                cart.Authoring.MovementClearanceRadius, ignoreRoadArea: true)))
            {
                failureReason = "The track position is outside the map or blocked.";
                return false;
            }
            cart.Authoring.MoveTo(projected);
            return true;
        }

        public void SetFoodCartPlacementPrompt(MechanismId cartId, bool visible, bool hovered)
        {
            if (!disposed && foodCarts.TryGetValue(cartId, out var cart))
                cart.Authoring?.SetFoodPlacementPrompt(visible, hovered);
        }

        public bool RequestWeaponDamage(ActorId sourceActorId,
            EffectRequestId requestId, int amount)
        {
            return !disposed && !paused && weapon != null &&
                weapon.RequestDamage(sourceActorId, requestId, amount);
        }

        public void BindCarrierActors(SlimeManager slimes, EnemyManager enemies,
            SlimeBattle.Guards.GuardManager guards)
        {
            if (disposed) return;
            carrierSlimes = slimes;
            carrierEnemies = enemies;
            carrierGuards = guards;
            if (weapon != null && carrierSlimes != null)
                foreach (var slime in carrierSlimes.GetAliveSnapshots())
                    TryAttachCarrier(slime, weapon.GetSnapshot());
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
            if (campaignResources != null &&
                campaignResources.GetMechanismCount(config.Kind) <= 0)
            {
                failureReason = "The mechanism inventory is empty.";
                return false;
            }
            if (placementCooldowns.TryGetValue(configId, out var cooldown) && cooldown > 0f)
            {
                failureReason = "The mechanism placement is cooling down.";
                return false;
            }
            if (!IsFinite(position.x) || !IsFinite(position.y) ||
                !mapManager.TryGetMapBounds(out var bounds) || !bounds.Contains(position))
            {
                failureReason = "The requested position is outside the map.";
                return false;
            }
            if (!mapManager.CanOccupy(position,
                new OccupancyQuery(config.PlacementRadiusWorldUnits)))
            {
                failureReason = "The requested position is outside a walkable placement area or is blocked.";
                return false;
            }
            return true;
        }

        /// <summary>Creates and registers a configured player mechanism in this level scene.</summary>
        public MechanismPlacementResult RequestPlace(ConfigId configId, Vector2 position)
        {
            if (!CanPlace(configId, position, out var reason))
                return new MechanismPlacementResult(false, default(MechanismId), reason);
            if (!configs.TryGetMechanism(configId, out var config) ||
                config.PlacementPrefab == null ||
                new ConfigId(config.PlacementPrefab.ConfigId) != configId)
                return new MechanismPlacementResult(false, default(MechanismId),
                    "A matching mechanism prefab is required.");

            var instance = UnityEngine.Object.Instantiate(config.PlacementPrefab,
                new Vector3(position.x, position.y, 0f), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(instance.gameObject, scene);
            if (placedParent != null) instance.transform.SetParent(placedParent, true);
            var result = RequestPlace(new MechanismPlacementRequest(instance, position));
            if (!result.Succeeded) DestroyPlacedObject(instance.gameObject);
            return result;
        }

        /// <summary>Replaces a player obstacle at a configured anchor without losing it on failure.</summary>
        public MechanismPlacementResult RequestPlaceAtAnchor(ConfigId configId,
            PlacementAnchorId anchorId)
        {
            if (disposed || paused || !anchorId.IsValid ||
                !mapManager.TryGetPlacementAnchor(anchorId, out var anchor) ||
                !configs.TryGetMechanism(configId, out var config) ||
                !config.PlayerPlaceable || !anchor.Accepts(config.Kind) ||
                config.PlacementPrefab == null ||
                new ConfigId(config.PlacementPrefab.ConfigId) != configId ||
                !mapManager.TryGetMapBounds(out var bounds) || !bounds.Contains(anchor.Position))
                return new MechanismPlacementResult(false, default,
                    "The anchor or matching mechanism prefab is unavailable.");

            MechanismRuntimeData previous = null;
            foreach (var mechanism in mechanisms.Values)
            {
                if (mechanism.State == MechanismState.Removed ||
                    mechanism.State == MechanismState.Exhausted ||
                    mechanism.Authoring == null) continue;
                if (mechanism.AnchorId != anchorId &&
                    (mechanism.Authoring.transform.position -
                     (Vector3)anchor.Position).sqrMagnitude > 0.00000001f) continue;
                if (previous != null || !mechanism.IsPlayerPlaced ||
                    mechanism.Config.Kind != MechanismKind.Obstacle ||
                    !anchor.AllowsReplacement)
                    return new MechanismPlacementResult(false, default,
                        "The anchor contains a mechanism that cannot be replaced.");
                previous = mechanism;
            }
            if (previous == null &&
                CountActivePlacedMechanisms() >= maximumActivePlacedMechanisms)
                return new MechanismPlacementResult(false, default,
                    "The active placed-mechanism limit was reached.");
            if (campaignResources != null &&
                campaignResources.GetMechanismCount(config.Kind) <= 0)
                return new MechanismPlacementResult(false, default,
                    "The mechanism inventory is empty.");
            if (placementCooldowns.TryGetValue(configId, out var cooldown) && cooldown > 0f)
                return new MechanismPlacementResult(false, default,
                    "The mechanism placement is cooling down.");

            var instance = UnityEngine.Object.Instantiate(config.PlacementPrefab,
                new Vector3(anchor.Position.x, anchor.Position.y, 0f), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(instance.gameObject, scene);
            if (placedParent != null) instance.transform.SetParent(placedParent, true);
            var id = MechanismId.NewId();
            if (!TryRegisterAuthoring(instance, id, config, true,
                anchorId, publishEvent: false))
            {
                DestroyPlacedObject(instance.gameObject);
                return new MechanismPlacementResult(false, default,
                    "The new mechanism could not be registered; the previous one remains.");
            }

            if (campaignResources != null &&
                !campaignResources.TrySpendMechanism(config.Kind))
            {
                RemoveRuntimeMechanism(mechanisms[id], "Placement inventory changed.",
                    startPlacementCooldown: false, hideAuthoring: false);
                DestroyPlacedObject(instance.gameObject);
                return new MechanismPlacementResult(false, default,
                    "The mechanism inventory is empty.");
            }

            if (previous != null)
                RemoveRuntimeMechanism(previous, "Replaced at anchor.",
                    startPlacementCooldown: false, hideAuthoring: true);
            PublishStateChanged(mechanisms[id], MechanismState.Removed,
                "Registered at anchor.");
            StartPlacementCooldown(config);
            NotifyPlacedCarrierContacts(mechanisms[id]);
            return new MechanismPlacementResult(true, id, string.Empty);
        }

        public bool TryGetPlayerConfigId(MechanismKind kind, out ConfigId configId)
        {
            configId = default(ConfigId);
            if (disposed || kind == MechanismKind.None ||
                kind == MechanismKind.EscortWeapon) return false;
            foreach (var asset in configs.Configs)
            {
                if (!(asset is MechanismConfig config) || !config.PlayerPlaceable ||
                    config.Kind != kind) continue;
                if (configId.IsValid)
                {
                    configId = default(ConfigId);
                    return false;
                }
                configId = config.ConfigId;
            }
            return configId.IsValid;
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
            if (!TryRegisterAuthoring(authoring, id, config, isPlayerPlaced: true,
                publishEvent: false))
            {
                authoring.transform.position = previousPosition;
                return new MechanismPlacementResult(false, default(MechanismId),
                    "The mechanism instance could not be registered.");
            }

            if (campaignResources != null &&
                !campaignResources.TrySpendMechanism(config.Kind))
            {
                RemoveRuntimeMechanism(mechanisms[id], "Placement inventory changed.",
                    startPlacementCooldown: false, hideAuthoring: false);
                return new MechanismPlacementResult(false, default(MechanismId),
                    "The mechanism inventory is empty.");
            }

            PublishStateChanged(mechanisms[id], MechanismState.Removed, "Registered.");
            StartPlacementCooldown(config);
            NotifyPlacedCarrierContacts(mechanisms[id]);
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
            UpdateCarriers(deltaTime);
            weapon?.Tick(deltaTime);
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

        internal void NotifyFoodCartDestroyed(DiningCartAuthoring authoring)
        {
            if (disposed || ReferenceEquals(authoring, null)) return;
            var id = authoring.CartId;
            if (foodCarts.TryGetValue(id, out var cart) &&
                ReferenceEquals(cart.Authoring, authoring))
                foodCarts.Remove(id);
            authoring.Unbind(this);
        }

        public void Dispose()
        {
            if (disposed) return;
            weapon?.Dispose();
            weapon = null;
            carrierSlimes = null;
            carrierEnemies = null;
            carrierGuards = null;
            mustLeaveBoardingZone.Clear();
            guideDepartureTimers.Clear();
            foreach (var cart in foodCarts.Values)
            {
                cart.Authoring?.SetFoodPlacementPrompt(false, false);
                cart.Authoring?.Unbind(this);
            }
            foodCarts.Clear();
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

                foreach (var authoring in root.GetComponentsInChildren<DiningCartAuthoring>(true))
                {
                    if (authoring == null || authoring.gameObject.scene.handle != scene.handle ||
                        !authoring.isActiveAndEnabled) continue;
                    var id = authoring.CartId;
                    var collider = authoring.InteractionCollider;
                    if (!id.IsValid || foodCarts.ContainsKey(id) || mechanisms.ContainsKey(id) ||
                        authoring.FoodSlot == null || !authoring.HasValidTrack ||
                        collider == null || !collider.isTrigger ||
                        !authoring.TryProjectToTrack(authoring.Position, out var trackPosition))
                    {
                        Debug.LogError("MechanismSystem: dining cart requires a unique ID, a food slot, " +
                            "a trigger collider, and at least two nearby track points.", authoring);
                        continue;
                    }
                    authoring.MoveTo(trackPosition);
                    authoring.Bind(this);
                    foodCarts.Add(id, new FoodCartRuntimeData(authoring));
                }
            }
        }

        private void BindSceneWeapon()
        {
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var authoring in root.GetComponentsInChildren<EscortWeaponAuthoring>(true))
                {
                    if (authoring == null || !authoring.isActiveAndEnabled ||
                        authoring.gameObject.scene.handle != scene.handle) continue;
                    if (weapon != null)
                    {
                        Debug.LogError("MechanismSystem: only one escort weapon is supported.",
                            authoring);
                        continue;
                    }
                    if (!configs.TryGetWeapon(authoring.WeaponConfigId, out var config) ||
                        !mapManager.TryGetRoute(authoring.RouteId, out var route))
                    {
                        Debug.LogError("MechanismSystem: escort weapon config or route is missing.",
                            authoring);
                        continue;
                    }
                    if (!authoring.HasValidCarrierAnchors)
                    {
                        Debug.LogError("MechanismSystem: escort weapon requires four child carrier anchors.",
                            authoring);
                        continue;
                    }
                    try
                    {
                        weapon = new EscortWeapon(MechanismId.NewId(), config,
                            route, authoring.transform, mapManager);
                        weaponAuthoring = authoring;
                        weaponTransform = authoring.transform;
                        boardingRadius = config.BoardingRadiusWorldUnits;
                        weapon.Damaged += (snapshot, source, request, loss) =>
                            eventBus?.Publish(new WeaponDamagedEvent(snapshot, source, request, loss));
                        weapon.Destroyed += snapshot =>
                            eventBus?.Publish(new WeaponDestroyedEvent(snapshot));
                        weapon.ReachedGoal += snapshot =>
                            eventBus?.Publish(new WeaponReachedGoalEvent(snapshot));
                    }
                    catch (Exception error)
                    {
                        Debug.LogError("MechanismSystem: " + error.Message, authoring);
                    }
                }
            }
        }

        private void UpdateCarriers(float deltaTime)
        {
            if (weapon == null || carrierSlimes == null || weaponTransform == null) return;
            var weaponSnapshot = weapon.GetSnapshot();
            if (weaponSnapshot.IsDestroyed || weaponSnapshot.HasReachedGoal) return;
            foreach (var id in new List<ActorId>(weapon.CarrierIds))
            {
                if (carrierSlimes.TryGetSnapshot(id, out var current) && current.IsAlive &&
                    current.IsCarrying) continue;
                weapon.RemoveCarrier(id);
                mustLeaveBoardingZone.Add(id);
                guideDepartureTimers.Remove(id);
            }
            weaponSnapshot = weapon.GetSnapshot();
            var candidates = new List<SlimeSnapshot>(carrierSlimes.GetAliveSnapshots());
            candidates.Sort((left, right) => string.CompareOrdinal(
                left.ActorId.Value, right.ActorId.Value));
            var radiusSquared = boardingRadius * boardingRadius;
            foreach (var slime in candidates)
            {
                var inside = (slime.Position - weaponSnapshot.Position).sqrMagnitude <= radiusSquared;
                if (!inside) mustLeaveBoardingZone.Remove(slime.ActorId);
                if (slime.IsCarrying)
                {
                    if ((MustLeaveWeapon(slime) || RollGuideDeparture(slime, deltaTime)) &&
                        carrierSlimes.TryDetachCarrier(slime.ActorId))
                    {
                        weapon.RemoveCarrier(slime.ActorId);
                        mustLeaveBoardingZone.Add(slime.ActorId);
                        guideDepartureTimers.Remove(slime.ActorId);
                    }
                    continue;
                }
                if (TryAttachCarrier(slime, weaponSnapshot))
                    weaponSnapshot = weapon.GetSnapshot();
            }
        }

        private bool TryAttachCarrier(SlimeSnapshot slime,
            EscortWeaponSnapshot weaponSnapshot)
        {
            if (weapon == null || weaponAuthoring == null || carrierSlimes == null ||
                weaponTransform == null ||
                !slime.IsAlive || slime.IsCarrying || weaponSnapshot.IsDestroyed ||
                weaponSnapshot.HasReachedGoal ||
                (slime.Position - weaponSnapshot.Position).sqrMagnitude >
                    boardingRadius * boardingRadius ||
                mustLeaveBoardingZone.Contains(slime.ActorId) ||
                weaponSnapshot.CarrierCount >= EscortWeapon.MaximumCarrierCount ||
                MustLeaveWeapon(slime))
                return false;

            if (!weapon.TryAddCarrier(slime.ActorId, out var anchorIndex))
                return false;
            if (weaponAuthoring.TryGetCarrierAnchor(anchorIndex, out var anchor) &&
                carrierSlimes.TryAttachCarrier(slime.ActorId, weaponTransform,
                    anchor.localPosition)) return true;

            weapon.RemoveCarrier(slime.ActorId);
            return false;
        }

        private void TryAttachSpawnedSlime(SlimeSpawnedEvent spawned)
        {
            if (disposed || paused || weapon == null || carrierSlimes == null ||
                !spawned.Snapshot.IsAlive) return;
            TryAttachCarrier(spawned.Snapshot, weapon.GetSnapshot());
        }

        // Carried slimes have their colliders disabled while parented to the weapon.
        // Apply the same contact rule once when a player places a guide on them so
        // placement remains observable and deterministic in the escort flow.
        private void NotifyPlacedCarrierContacts(MechanismRuntimeData mechanism)
        {
            if (mechanism == null || carrierSlimes == null || mechanism.Authoring == null ||
                (mechanism.Config.Kind != MechanismKind.Food &&
                 mechanism.Config.Kind != MechanismKind.Furnace)) return;
            var interaction = mechanism.Authoring.InteractionCollider;
            if (interaction == null || !interaction.isTrigger) return;
            foreach (var slime in carrierSlimes.GetAliveSnapshots())
            {
                if (!slime.IsCarrying || !interaction.OverlapPoint(slime.Position)) continue;
                var contactColliderId = 0;
                if (carrierSlimes.TryGetActor(slime.ActorId, out var actor))
                {
                    var collider = actor.GetComponent<Collider2D>();
                    if (collider != null) contactColliderId = collider.GetInstanceID();
                }
                NotifyContact(new MechanismTriggerContext(mechanism.Id, slime.ActorId,
                    ActorType.Slime, MechanismContactKind.Entered, slime.Position,
                    contactColliderId));
            }
        }

        private bool RollGuideDeparture(SlimeSnapshot slime, float deltaTime)
        {
            var hungry = SlimeStatusIds.Contains(slime.StatusIds, SlimeStatusIds.Hungry);
            var cold = SlimeStatusIds.Contains(slime.StatusIds, SlimeStatusIds.Cold);
            if (!hungry && !cold)
            { guideDepartureTimers.Remove(slime.ActorId); return false; }
            var guidePresent = false;
            foreach (var source in GetGuideSourcesInRange(slime.Position, slime.PixelsPerUnit))
                if ((hungry && source.Type == GuideSourceType.Food) ||
                    (cold && source.Type == GuideSourceType.Warmth))
                { guidePresent = true; break; }
            if (!guidePresent)
            { guideDepartureTimers.Remove(slime.ActorId); return false; }
            guideDepartureTimers.TryGetValue(slime.ActorId, out var elapsed);
            elapsed += deltaTime;
            if (elapsed < 5f)
            { guideDepartureTimers[slime.ActorId] = elapsed; return false; }
            guideDepartureTimers[slime.ActorId] = elapsed - 5f;
            return departureRandom.NextDouble() < .6;
        }

        private bool MustLeaveWeapon(SlimeSnapshot slime)
        {
            if (SlimeStatusIds.Contains(slime.StatusIds, SlimeStatusIds.Afraid)) return true;
            if (!configs.TryGet(slime.ConfigId, out SlimeConfig slimeConfig)) return false;
            var rangeSquared = slimeConfig.DetectionRange * slimeConfig.DetectionRange;
            if (carrierEnemies != null)
                foreach (var enemy in carrierEnemies.GetAliveSnapshots())
                    if ((enemy.Position - slime.Position).sqrMagnitude <= rangeSquared)
                        return true;
            if (carrierGuards != null &&
                SlimeStatusIds.Contains(slime.StatusIds, SlimeStatusIds.Agitated) &&
                statusManager.TryGetStatus(slime.ActorId,
                    new StatusId(SlimeStatusIds.Agitated), out var rage) &&
                rage.ElapsedDurationSeconds >= rage.HumanAttackDelaySeconds)
                foreach (var guard in carrierGuards.GetAliveSnapshots())
                    if ((guard.Position - slime.Position).sqrMagnitude <= rangeSquared)
                        return true;
            return false;
        }

        private bool TryRegisterAuthoring(MechanismAuthoring authoring, MechanismId id,
            MechanismConfig config, bool isPlayerPlaced,
            PlacementAnchorId anchorId = default, bool publishEvent = true)
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
            runtime.AnchorId = anchorId;
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
            if (publishEvent)
                PublishStateChanged(runtime, MechanismState.Removed, "Registered.");
            return true;
        }

        private bool NotifyFurnaceEntered(FurnaceRuntimeData furnace,
            MechanismTriggerContext context)
        {
            var colliderId = context.ContactColliderId == 0
                ? context.ActorId.GetHashCode() : context.ContactColliderId;
            if (!furnace.ContactColliders.TryGetValue(context.ActorId, out var colliders))
            {
                colliders = new HashSet<int>();
                furnace.ContactColliders.Add(context.ActorId, colliders);
            }
            if (!colliders.Add(colliderId)) return false;
            if (colliders.Count > 1) return true;
            statusManager.NotifyGuideReached(context.ActorId, StatusGuideKind.Furnace);

            var result = new MechanismActivationResult(furnace.Id, context.ActorId,
                true, false, "Furnace guidance reached.");
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
            return true;
        }

        private bool ConsumeActivation(MechanismRuntimeData mechanism, ActorId actorId)
        {
            var cartFood = mechanism as FoodRuntimeData;
            var isCartFood = cartFood != null && cartFood.FoodCartId.IsValid;
            if (isCartFood && foodCarts.TryGetValue(cartFood.FoodCartId, out var cart) &&
                cart.LoadedFoodId == mechanism.Id)
                cart.LoadedFoodId = default;

            if (GetActivationLimit(mechanism) > 0)
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
                    if (isCartFood)
                    {
                        if (mechanism.Authoring != null)
                            DestroyPlacedObject(mechanism.Authoring.gameObject);
                        return true;
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

        private void SubscribeToActorEvents()
        {
            if (eventBus == null) return;
            subscriptions.Add(eventBus.Subscribe<SlimeSpawnedEvent>(TryAttachSpawnedSlime));
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
            foreach (var mechanism in mechanisms.Values)
            {
                if (mechanism is FurnaceRuntimeData furnace &&
                    furnace.ContactColliders.ContainsKey(actorId))
                    furnace.ContactColliders.Remove(actorId);
            }
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
            furnace.ContactColliders.Clear();
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
                GetActivationLimit(mechanism), mechanism.RemainingActivations,
                mechanism.CooldownRemainingSeconds, mechanism.Config.GuideSourceType,
                mechanism.Config.AttractionRadiusPixels, mechanism.AnchorId);
        }

        private static int GetActivationLimit(MechanismRuntimeData mechanism) =>
            mechanism is FoodRuntimeData food && food.FoodCartId.IsValid
                ? 1 : mechanism.Config.ActivationLimit;

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
