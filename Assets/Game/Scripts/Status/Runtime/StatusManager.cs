using System;
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Core;
using SlimeBattle.Data;
using SlimeBattle.Enemies;
using SlimeBattle.Slimes;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEngine;

namespace SlimeBattle.Status
{
    /// <summary>
    /// The level-scoped effect and status rules service. ActorCore remains the owner
    /// of health and active status IDs; this service owns status timing and modifiers.
    /// </summary>
    public sealed class StatusManager : IDisposable, IActorStatusQuery
    {
        private sealed class HungerRuntime
        {
            public float TimeSinceFedSeconds;
            public float RageRollRemainingSeconds;
        }

        private sealed class ActiveStatus
        {
            public ActiveStatus(StatusConfig config)
            {
                Config = config;
                DirectRemainingSeconds = float.PositiveInfinity;
                ExitGraceRemainingSeconds = -1f;
                PeriodicRemainingSeconds = config.HasPeriodicEffect
                    ? config.PeriodicIntervalSeconds : float.PositiveInfinity;
            }

            public StatusConfig Config { get; set; }
            public bool HasDirectSource { get; set; }
            public float DirectRemainingSeconds { get; set; }
            public float ExitGraceRemainingSeconds { get; set; }
            public float PeriodicRemainingSeconds { get; set; }
            public float ElapsedSeconds { get; set; }
            public ActorId SourceActorId { get; set; }
            public int StackCount { get; set; } = 1;
            public HashSet<MapAreaId> AreaSources { get; } = new HashSet<MapAreaId>();
            public bool IsActive => HasDirectSource || AreaSources.Count > 0 ||
                ExitGraceRemainingSeconds >= 0f;
        }

        private const int MaximumPeriodicTicksPerStatusPerTick = 16;

        private readonly ConfigRegistry configs;
        private readonly ILevelEventBus eventBus;
        private readonly Func<ActorId, IEffectReceiver> actorResolver;
        private readonly Func<IEnumerable<IEffectReceiver>> actorEnumerator;
        private readonly Func<float> randomValue;
        private readonly Dictionary<ActorId, Dictionary<StatusId, ActiveStatus>> statuses =
            new Dictionary<ActorId, Dictionary<StatusId, ActiveStatus>>();
        private readonly Dictionary<ActorId, ActorControlMode> controlModeBaselines =
            new Dictionary<ActorId, ActorControlMode>();
        private readonly HashSet<EffectRequestId> processedRequests =
            new HashSet<EffectRequestId>();
        private readonly Dictionary<ActorId, HungerRuntime> hungerRuntimes =
            new Dictionary<ActorId, HungerRuntime>();
        private readonly HashSet<ActorId> actorsInFurnaceCores = new HashSet<ActorId>();
        private readonly Dictionary<ActorId, float> furnaceExposureSeconds =
            new Dictionary<ActorId, float>();
        private readonly List<SubscriptionToken> subscriptions = new List<SubscriptionToken>();
        private bool paused;
        private bool disposed;

        internal StatusManager(ConfigRegistry configs, ILevelEventBus eventBus,
            Func<ActorId, IEffectReceiver> actorResolver,
            Func<IEnumerable<IEffectReceiver>> actorEnumerator = null,
            Func<float> randomValue = null)
        {
            this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
            this.eventBus = eventBus;
            this.actorResolver = actorResolver ?? throw new ArgumentNullException(nameof(actorResolver));
            this.actorEnumerator = actorEnumerator ?? (() => Array.Empty<IEffectReceiver>());
            this.randomValue = randomValue ?? (() => UnityEngine.Random.value);
            SubscribeToLevelEvents();
        }

        public EffectApplyResult ApplyEffect(EffectRequest request)
        {
            if (disposed) return EffectApplyResult.Rejected("StatusManager is disposed.");
            if (!request.RequestId.IsValid)
                return Reject(request, EffectApplyResult.Rejected("A valid request ID is required."));
            if (!processedRequests.Add(request.RequestId))
                return EffectApplyResult.Duplicate("This effect request was already processed.");
            if (paused)
                return Reject(request, EffectApplyResult.Rejected("The level is paused."));
            if (!IsValidRequest(request))
                return Reject(request, EffectApplyResult.Rejected("The effect request is invalid."));

            if (!TryResolveTarget(request.TargetActorId, out var target))
                return Reject(request, EffectApplyResult.TargetUnavailable("The target Actor is missing or dead."));
            if (!ValidateSourceAndRelationship(request, target))
                return Reject(request, EffectApplyResult.Rejected("The source or Actor relationship is invalid."));

            if (request.Type == EffectType.Status || request.Type == EffectType.Control)
            {
                if (!configs.TryGetStatus(request.StatusId, out var statusConfig))
                    return Reject(request, EffectApplyResult.Rejected("StatusConfig is missing or ambiguous."));
                if (request.Type == EffectType.Control &&
                    statusConfig.ControlMode == StatusControlMode.None && !statusConfig.IsRooted)
                    return Reject(request, EffectApplyResult.Rejected("The StatusConfig has no control effect."));

                var statusResult = ApplyStatusCore(new StatusApplicationRequest(
                    request.SourceActorId, request.TargetActorId, request.StatusId,
                    durationOverrideSeconds: request.DurationSeconds,
                    requestId: request.RequestId), target, statusConfig);
                if (!statusResult.IsAccepted) return Reject(request, statusResult);

                var resolvedStatus = new ResolvedEffect(EffectType.Status, 0f,
                    request.SourceActorId, request.StatusId.Value);
                eventBus?.Publish(new EffectAppliedEvent(request, resolvedStatus, statusResult));
                return statusResult;
            }

            var beforeActor = target.GetSnapshot();
            var resolvedAmount = request.Amount;
            if (request.Type == EffectType.Damage)
            {
                resolvedAmount *= GetDamageTakenMultiplier(request.TargetActorId);
                if (request.SourceActorId.IsValid)
                    resolvedAmount *= GetDamageDealtMultiplier(request.SourceActorId);
            }
            if (!IsFinite(resolvedAmount))
                return Reject(request, EffectApplyResult.Rejected(
                    "The resolved effect amount is outside the supported range."));

            var resolvedEffect = new ResolvedEffect(request.Type, resolvedAmount,
                request.SourceActorId);
            var result = resolvedAmount <= 0f
                ? EffectApplyResult.Accepted(0f, beforeActor.CurrentHealth)
                : target.ApplyResolvedEffect(resolvedEffect);
            if (!result.IsAccepted)
                return Reject(request, result);

            var afterActor = target.GetSnapshot();
            var beforeHealth = HealthSnapshot.FromActor(beforeActor);
            var afterHealth = HealthSnapshot.FromActor(afterActor);
            if (beforeHealth.CurrentHealth != afterHealth.CurrentHealth)
                eventBus?.Publish(new HealthChangedEvent(request.RequestId,
                    beforeHealth, afterHealth, resolvedEffect));

            if (request.Type == EffectType.Damage &&
                beforeHealth.CurrentHealth != afterHealth.CurrentHealth &&
                target.GetActorType() == ActorType.Guard)
                NotifyNearbySlimesOfHumanBlood(target, afterActor.Position);

            if (request.Type == EffectType.Damage && resolvedAmount > 0f && target.IsAlive())
                ApplyOnHitStatus(request.SourceActorId, request.TargetActorId);

            eventBus?.Publish(new EffectAppliedEvent(request, resolvedEffect, result));
            return result;
        }

        public bool ApplyStatus(StatusApplicationRequest request)
        {
            if (disposed || !request.RequestId.IsValid)
                return false;
            if (paused)
            {
                PublishRejected(request.RequestId, request.SourceActorId,
                    request.TargetActorId, EffectApplyResult.Rejected("The level is paused."));
                return false;
            }
            if (!processedRequests.Add(request.RequestId))
            {
                return false;
            }

            if (!request.TargetActorId.IsValid || !request.StatusId.IsValid ||
                !IsFinite(request.DurationOverrideSeconds) || request.DurationOverrideSeconds < -1f)
            {
                PublishRejected(request.RequestId, request.SourceActorId,
                    request.TargetActorId, EffectApplyResult.Rejected("The status request is invalid."));
                return false;
            }

            if (!TryResolveTarget(request.TargetActorId, out var target))
            {
                PublishRejected(request.RequestId, request.SourceActorId,
                    request.TargetActorId, EffectApplyResult.TargetUnavailable(
                        "The target Actor is missing or dead."));
                return false;
            }

            if (request.SourceActorId.IsValid &&
                (!TryResolveTarget(request.SourceActorId, out var source) ||
                 source.GetActorId() != request.SourceActorId))
            {
                PublishRejected(request.RequestId, request.SourceActorId,
                    request.TargetActorId, EffectApplyResult.Rejected("The source Actor is unavailable."));
                return false;
            }

            if (!configs.TryGetStatus(request.StatusId, out var statusConfig))
            {
                PublishRejected(request.RequestId, request.SourceActorId,
                    request.TargetActorId, EffectApplyResult.Rejected(
                        "StatusConfig is missing or ambiguous."));
                return false;
            }

            var result = ApplyStatusCore(request, target, statusConfig);
            if (!result.IsAccepted)
            {
                PublishRejected(request.RequestId, request.SourceActorId,
                    request.TargetActorId, result);
                return false;
            }

            var resolved = new ResolvedEffect(EffectType.Status, 0f,
                request.SourceActorId, request.StatusId.Value);
            var effectRequest = new EffectRequest(request.RequestId,
                request.SourceActorId, request.TargetActorId, EffectType.Status,
                statusId: request.StatusId);
            eventBus?.Publish(new EffectAppliedEvent(effectRequest, resolved, result));
            return true;
        }

        public bool RemoveStatus(ActorId actorId, StatusId statusId,
            StatusRemoveReason reason = StatusRemoveReason.Dispelled)
        {
            if (disposed || !actorId.IsValid || !statusId.IsValid ||
                !statuses.TryGetValue(actorId, out var actorStatuses) ||
                !actorStatuses.ContainsKey(statusId)) return false;

            RemoveStatusInternal(actorId, statusId, reason, allowDeadActor: false);
            return true;
        }

        /// <summary>Records feeding, clears Hunger/Rage, and optionally applies cold or hot food.</summary>
        public bool NotifyFoodConsumed(ActorId actorId, StatusId foodStatusId = default(StatusId))
        {
            if (disposed || paused || !TryResolveTarget(actorId, out var receiver) ||
                receiver.GetActorType() != ActorType.Slime) return false;

            StatusConfig foodConfig = null;
            if (foodStatusId.IsValid &&
                (!configs.TryGetStatus(foodStatusId, out foodConfig) ||
                 (foodConfig.Kind != StatusKind.Cold && foodConfig.Kind != StatusKind.Hot)))
                return false;

            if (!hungerRuntimes.TryGetValue(actorId, out var hunger))
            {
                hunger = new HungerRuntime();
                hungerRuntimes.Add(actorId, hunger);
            }
            hunger.TimeSinceFedSeconds = 0f;
            hunger.RageRollRemainingSeconds = 0f;
            RemoveStatusByKind(actorId, StatusKind.Hunger, StatusRemoveReason.Dispelled);
            RemoveStatusByKind(actorId, StatusKind.Rage, StatusRemoveReason.Dispelled);

            if (foodConfig == null) return true;
            var oppositeKind = foodConfig.Kind == StatusKind.Cold ? StatusKind.Hot : StatusKind.Cold;
            if (configs.TryGetStatus(oppositeKind, out var opposite) && HasStatus(actorId, opposite.StatusId))
            {
                RemoveStatus(actorId, opposite.StatusId, StatusRemoveReason.Counteracted);
                return true;
            }

            return ApplyStatus(new StatusApplicationRequest(default(ActorId), actorId,
                foodConfig.StatusId));
        }

        public bool NotifyGuideReached(ActorId actorId, StatusGuideKind guideKind)
        {
            if (!Enum.IsDefined(typeof(StatusGuideKind), guideKind)) return false;
            switch (guideKind)
            {
                case StatusGuideKind.Food:
                    return NotifyFoodConsumed(actorId);
                case StatusGuideKind.Warmth:
                    return RemoveStatusByKind(actorId, StatusKind.Cold, StatusRemoveReason.Dispelled) |
                        RemoveStatusByKind(actorId, StatusKind.Rage, StatusRemoveReason.Dispelled);
                case StatusGuideKind.ColdArea:
                    return RemoveStatusByKind(actorId, StatusKind.Hot, StatusRemoveReason.Dispelled) |
                        RemoveStatusByKind(actorId, StatusKind.Rage, StatusRemoveReason.Dispelled);
                case StatusGuideKind.Furnace:
                    return RemoveStatusByKind(actorId, StatusKind.Cold, StatusRemoveReason.Dispelled) |
                        RemoveStatusByKind(actorId, StatusKind.Rage, StatusRemoveReason.Dispelled);
                default:
                    return false;
            }
        }

        /// <summary>Called by perception when an injured human is close enough for blood smell.</summary>
        public bool NotifyInjuredHumanNearby(ActorId slimeActorId)
        {
            if (disposed || paused || !TryResolveTarget(slimeActorId, out var slime) ||
                slime.GetActorType() != ActorType.Slime ||
                HasStatus(slimeActorId, StatusKind.Rage) ||
                !configs.TryGetStatus(StatusKind.Rage, out var rage) ||
                randomValue() >= rage.HumanBloodRageChance)
                return false;
            return ApplyStatus(new StatusApplicationRequest(default(ActorId), slimeActorId,
                rage.StatusId));
        }

        public bool NotifyFurnaceCoreEntered(ActorId actorId)
        {
            if (disposed || paused || !TryResolveTarget(actorId, out var receiver) ||
                receiver.GetActorType() != ActorType.Slime) return false;
            actorsInFurnaceCores.Add(actorId);
            furnaceExposureSeconds[actorId] = 0f;
            return true;
        }

        public bool NotifyFurnaceCoreExited(ActorId actorId)
        {
            furnaceExposureSeconds.Remove(actorId);
            return actorsInFurnaceCores.Remove(actorId);
        }

        public bool HasStatus(ActorId actorId, StatusId statusId)
        {
            if (disposed || !statusId.IsValid || !TryResolveActor(actorId, out var receiver))
                return false;
            return ContainsStatus(receiver.GetSnapshot(), statusId);
        }

        public bool HasStatus(ActorId actorId, StatusKind kind)
        {
            return !disposed && configs.TryGetStatus(kind, out var config) &&
                HasStatus(actorId, config.StatusId);
        }

        public bool TryGetStatus(ActorId actorId, StatusId statusId,
            out StatusEntrySnapshot status)
        {
            status = default(StatusEntrySnapshot);
            if (disposed || !statusId.IsValid || !TryResolveActor(actorId, out var receiver))
                return false;
            if (!ContainsStatus(receiver.GetSnapshot(), statusId)) return false;
            if (statuses.TryGetValue(actorId, out var actorStatuses) &&
                actorStatuses.TryGetValue(statusId, out var active))
            {
                status = CreateSnapshot(active);
                return true;
            }
            status = new StatusEntrySnapshot(statusId, default(ConfigId),
                float.PositiveInfinity, 0, true, 1, 1f, 1f, 1f, 1f,
                false, StatusControlMode.None);
            return true;
        }

        public bool TryGetHealthSnapshot(ActorId actorId, out HealthSnapshot snapshot)
        {
            snapshot = default(HealthSnapshot);
            if (disposed || !TryResolveActor(actorId, out var receiver)) return false;
            snapshot = HealthSnapshot.FromActor(receiver.GetSnapshot());
            return true;
        }

        public bool TryGetStatusSnapshot(ActorId actorId, out StatusSnapshot snapshot)
        {
            snapshot = default(StatusSnapshot);
            if (disposed || !TryResolveActor(actorId, out var receiver)) return false;

            var actor = receiver.GetSnapshot();
            var entries = new List<StatusEntrySnapshot>();
            if (statuses.TryGetValue(actorId, out var actorStatuses))
            {
                foreach (var active in actorStatuses.Values)
                    entries.Add(CreateSnapshot(active));
            }

            for (var i = 0; actor.StatusIds != null && i < actor.StatusIds.Count; i++)
            {
                var statusId = new StatusId(actor.StatusIds[i]);
                if (!statusId.IsValid || (actorStatuses != null && actorStatuses.ContainsKey(statusId)))
                    continue;
                entries.Add(CreateSnapshot(statusId));
            }
            entries.Sort((left, right) => string.CompareOrdinal(
                left.StatusId.Value, right.StatusId.Value));
            snapshot = new StatusSnapshot(actorId, actor.ControlMode, entries);
            return true;
        }

        public MovementModifierSnapshot GetMovementModifiers(ActorId actorId)
        {
            if (disposed || !statuses.TryGetValue(actorId, out var actorStatuses))
                return MovementModifierSnapshot.Neutral;

            var speedMultiplier = 1f;
            var isRooted = false;
            foreach (var active in actorStatuses.Values)
            {
                if (!active.IsActive) continue;
                speedMultiplier *= Mathf.Pow(active.Config.MovementSpeedMultiplier, active.StackCount);
                isRooted |= active.Config.IsRooted;
            }
            return new MovementModifierSnapshot(speedMultiplier, isRooted);
        }

        public float GetAttackSpeedMultiplier(ActorId actorId) =>
            GetStatusMultiplier(actorId, config => config.AttackSpeedMultiplier);

        public float GetDamageDealtMultiplier(ActorId actorId) =>
            GetStatusMultiplier(actorId, config => config.DamageDealtMultiplier);

        public float GetDamageTakenMultiplier(ActorId actorId) =>
            GetStatusMultiplier(actorId, config => config.DamageTakenMultiplier);

        private float GetStatusMultiplier(ActorId actorId, Func<StatusConfig, float> selector)
        {
            if (disposed || !statuses.TryGetValue(actorId, out var actorStatuses)) return 1f;
            var multiplier = 1f;
            foreach (var active in actorStatuses.Values)
            {
                if (active.IsActive)
                    multiplier *= Mathf.Pow(selector(active.Config), active.StackCount);
            }
            return multiplier;
        }

        public void Tick(StatusTickContext context)
        {
            if (disposed || paused || !IsFinite(context.DeltaTime) || context.DeltaTime <= 0f)
                return;

            TickHunger(context.DeltaTime);
            TickFurnaceExposure(context.DeltaTime);
            var actorIds = new List<ActorId>(statuses.Keys);
            foreach (var actorId in actorIds)
            {
                if (!statuses.TryGetValue(actorId, out var actorStatuses)) continue;
                if (!TryResolveActor(actorId, out var receiver) || !receiver.IsAlive())
                {
                    ClearActor(actorId, StatusRemoveReason.ActorDespawned, allowDeadActor: true);
                    continue;
                }

                var actorFrozen = HasStatus(actorId, StatusKind.Frozen);
                var statusIds = new List<StatusId>(actorStatuses.Keys);
                foreach (var statusId in statusIds)
                {
                    if (!statuses.TryGetValue(actorId, out actorStatuses) ||
                        !actorStatuses.TryGetValue(statusId, out var active)) continue;
                    if (actorFrozen && active.Config.Kind != StatusKind.Frozen) continue;
                    var previousModifiers = GetMovementModifiers(actorId);
                    var activeDeltaTime = GetActiveDeltaTime(active, context.DeltaTime);
                    active.ElapsedSeconds += activeDeltaTime;
                    if (active.Config.HasPeriodicEffect)
                        TickPeriodicEffect(actorId, active, activeDeltaTime);

                    if (active.HasDirectSource && !float.IsPositiveInfinity(active.DirectRemainingSeconds))
                    {
                        active.DirectRemainingSeconds -= context.DeltaTime;
                        if (active.DirectRemainingSeconds <= 0f)
                        {
                            active.HasDirectSource = false;
                            active.StackCount = 1;
                        }
                    }
                    if (active.AreaSources.Count == 0 && active.ExitGraceRemainingSeconds >= 0f)
                        active.ExitGraceRemainingSeconds -= context.DeltaTime;

                    if (!active.IsActive)
                        RemoveStatusInternal(actorId, statusId, StatusRemoveReason.Expired,
                            allowDeadActor: false, previousModifiers: previousModifiers);
                }
            }
        }

        public void Pause()
        {
            if (!disposed) paused = true;
        }

        public void Resume()
        {
            if (!disposed) paused = false;
        }

        public void Dispose()
        {
            if (disposed) return;
            for (var i = 0; i < subscriptions.Count; i++)
                eventBus?.Unsubscribe(subscriptions[i]);
            subscriptions.Clear();
            statuses.Clear();
            controlModeBaselines.Clear();
            processedRequests.Clear();
            hungerRuntimes.Clear();
            actorsInFurnaceCores.Clear();
            furnaceExposureSeconds.Clear();
            disposed = true;
        }

        private EffectApplyResult ApplyStatusCore(StatusApplicationRequest request,
            IEffectReceiver target, StatusConfig config)
        {
            if (config.IsImmuneTo(target.GetActorType()))
                return EffectApplyResult.Immune("The target Actor is immune to this status.");
            if (config.Kind == StatusKind.Burning &&
                (HasStatus(request.TargetActorId, StatusKind.Frozen) ||
                 HasStatus(request.TargetActorId, StatusKind.Cold)))
                return EffectApplyResult.Immune("Burning is suppressed by cold or freezing.");

            statuses.TryGetValue(request.TargetActorId, out var actorStatuses);
            ActiveStatus existing = null;
            if (actorStatuses != null) actorStatuses.TryGetValue(request.StatusId, out existing);

            if (request.HasAreaSource && !request.SourceAreaId.IsValid)
                return EffectApplyResult.Rejected("The map area source ID is invalid.");

            if (existing != null && existing.Config != config &&
                (request.HasAreaSource || config.StackingPolicy != StatusStackingPolicy.Replace))
                return EffectApplyResult.Rejected("An active StatusId resolved to a different StatusConfig.");

            if (existing != null && !request.HasAreaSource &&
                config.StackingPolicy == StatusStackingPolicy.IgnoreIfActive)
            {
                var actorSnapshot = target.GetSnapshot();
                return EffectApplyResult.AcceptedStatus(false, actorSnapshot.CurrentHealth);
            }

            if (config.CounteractsStatusId.IsValid && config.CounteractsStatusId != request.StatusId)
                RemoveStatus(request.TargetActorId, config.CounteractsStatusId,
                    StatusRemoveReason.Counteracted);
            ApplyStatusCancellationRules(request.TargetActorId, config.Kind);

            statuses.TryGetValue(request.TargetActorId, out actorStatuses);
            existing = null;
            if (actorStatuses != null) actorStatuses.TryGetValue(request.StatusId, out existing);
            var beforeActor = target.GetSnapshot();
            var previousModifiers = GetMovementModifiers(request.TargetActorId);
            var previousControlMode = beforeActor.ControlMode;
            var actorHadStatus = ContainsStatus(beforeActor, request.StatusId);

            if (!actorHadStatus)
            {
                var addResult = target.ApplyResolvedEffect(new ResolvedEffect(
                    EffectType.Status, 0f, request.SourceActorId, request.StatusId.Value));
                if (!addResult.IsAccepted) return addResult;
            }

            if (actorStatuses == null)
            {
                actorStatuses = new Dictionary<StatusId, ActiveStatus>();
                statuses.Add(request.TargetActorId, actorStatuses);
            }

            var added = existing == null;
            var replaced = false;
            var changed = added || !actorHadStatus;
            if (existing != null && !request.HasAreaSource &&
                config.StackingPolicy == StatusStackingPolicy.Replace)
            {
                eventBus?.Publish(new StatusRemovedEvent(request.TargetActorId,
                    CreateSnapshot(existing), StatusRemoveReason.Replaced));
                var areaSources = new List<MapAreaId>(existing.AreaSources);
                existing = new ActiveStatus(config);
                for (var i = 0; i < areaSources.Count; i++)
                    existing.AreaSources.Add(areaSources[i]);
                actorStatuses[request.StatusId] = existing;
                replaced = true;
                changed = true;
            }
            else if (existing == null)
            {
                existing = new ActiveStatus(config);
                actorStatuses.Add(request.StatusId, existing);
            }
            existing.SourceActorId = request.SourceActorId;
            if (request.HasAreaSource)
            {
                var sourceAdded = existing.AreaSources.Add(request.SourceAreaId);
                changed |= sourceAdded;
                existing.ExitGraceRemainingSeconds = -1f;
                if (!sourceAdded && !added)
                    return EffectApplyResult.AcceptedStatus(false, beforeActor.CurrentHealth);
            }
            else
            {
                var duration = request.DurationOverrideSeconds < 0f
                    ? config.DurationSeconds : request.DurationOverrideSeconds;
                if (existing.HasDirectSource && config.StackingPolicy == StatusStackingPolicy.AddStack)
                {
                    var nextStackCount = existing.StackCount < config.MaximumStacks
                        ? existing.StackCount + 1 : existing.StackCount;
                    changed |= nextStackCount != existing.StackCount;
                    existing.StackCount = nextStackCount;
                }
                else if (!existing.HasDirectSource && added)
                {
                    existing.StackCount = 1;
                }
                if (config.StackingPolicy != StatusStackingPolicy.IgnoreIfActive ||
                    !existing.HasDirectSource)
                {
                    changed |= !existing.HasDirectSource ||
                        !Mathf.Approximately(existing.DirectRemainingSeconds, duration);
                    existing.HasDirectSource = true;
                    existing.DirectRemainingSeconds = duration <= 0f
                        ? float.PositiveInfinity : duration;
                    existing.ElapsedSeconds = 0f;
                    existing.ExitGraceRemainingSeconds = -1f;
                    existing.PeriodicRemainingSeconds = config.HasPeriodicEffect
                        ? config.PeriodicIntervalSeconds : float.PositiveInfinity;
                }
            }

            if (added || replaced)
                eventBus?.Publish(new StatusAddedEvent(request.TargetActorId, CreateSnapshot(existing)));
            else if (changed)
                eventBus?.Publish(new StatusChangedEvent(request.TargetActorId, CreateSnapshot(existing)));

            RecalculateMaximumHealth(request.TargetActorId, target, beforeActor, request.StatusId);
            RecalculateControlMode(request.TargetActorId, target, previousControlMode);
            PublishMovementModifierChange(request.TargetActorId, previousModifiers);
            return EffectApplyResult.AcceptedStatus(changed || replaced,
                target.GetSnapshot().CurrentHealth);
        }

        private void TickPeriodicEffect(ActorId actorId, ActiveStatus active, float deltaTime)
        {
            if (deltaTime <= 0f) return;
            var interval = active.Config.PeriodicIntervalSeconds;
            if (interval <= 0f || !IsFinite(interval)) return;
            active.PeriodicRemainingSeconds -= deltaTime;
            var ticks = 0;
            while (active.PeriodicRemainingSeconds <= 0f &&
                   ticks < MaximumPeriodicTicksPerStatusPerTick && active.IsActive)
            {
                active.PeriodicRemainingSeconds += interval;
                ticks++;
                var amount = active.Config.PeriodicAmount;
                if (active.Config.PeriodicValueMode == PeriodicAmountMode.MaxHealthFraction &&
                    TryResolveActor(actorId, out var affectedActor))
                    amount *= affectedActor.GetSnapshot().MaxHealth;
                var request = new EffectRequest(EffectRequestId.NewId(),
                    active.SourceActorId, actorId, active.Config.PeriodicEffectType,
                    amount * active.StackCount,
                    relationshipPolicy: EffectRelationshipPolicy.Any,
                    requireLivingSource: false);
                ApplyEffect(request);
                if (!TryResolveActor(actorId, out var receiver) || !receiver.IsAlive()) break;
            }
            if (ticks == MaximumPeriodicTicksPerStatusPerTick &&
                active.PeriodicRemainingSeconds <= 0f)
                active.PeriodicRemainingSeconds = interval;
        }

        private static float GetActiveDeltaTime(ActiveStatus active, float deltaTime)
        {
            if (active.AreaSources.Count > 0) return deltaTime;
            if (active.HasDirectSource && !float.IsPositiveInfinity(active.DirectRemainingSeconds))
                return Mathf.Min(deltaTime, Mathf.Max(0f, active.DirectRemainingSeconds));
            if (!active.HasDirectSource && active.ExitGraceRemainingSeconds >= 0f)
                return Mathf.Min(deltaTime, Mathf.Max(0f, active.ExitGraceRemainingSeconds));
            return deltaTime;
        }

        private void ApplyOnHitStatus(ActorId sourceActorId, ActorId targetActorId)
        {
            if (!TryResolveTarget(sourceActorId, out var source) ||
                !statuses.TryGetValue(sourceActorId, out var sourceStatuses)) return;
            foreach (var active in sourceStatuses.Values)
            {
                if (!active.IsActive || !active.Config.HasOnHitStatus) continue;
                ApplyStatus(new StatusApplicationRequest(sourceActorId, targetActorId,
                    active.Config.OnHitStatusId,
                    durationOverrideSeconds: active.Config.OnHitStatusDurationSeconds));
                return;
            }
        }

        private void ApplyStatusCancellationRules(ActorId actorId, StatusKind kind)
        {
            switch (kind)
            {
                case StatusKind.Cold:
                    RemoveStatusByKind(actorId, StatusKind.Hot, StatusRemoveReason.Counteracted);
                    RemoveStatusByKind(actorId, StatusKind.Burning, StatusRemoveReason.Counteracted);
                    break;
                case StatusKind.Frozen:
                    RemoveStatusByKind(actorId, StatusKind.Burning, StatusRemoveReason.Counteracted);
                    break;
                case StatusKind.Hot:
                    RemoveStatusByKind(actorId, StatusKind.Cold, StatusRemoveReason.Counteracted);
                    break;
            }
        }

        private bool RemoveStatusByKind(ActorId actorId, StatusKind kind,
            StatusRemoveReason reason)
        {
            return configs.TryGetStatus(kind, out var config) &&
                RemoveStatus(actorId, config.StatusId, reason);
        }

        private void RecalculateMaximumHealth(ActorId actorId, IEffectReceiver receiver,
            ActorSnapshot before, StatusId causeStatusId)
        {
            if (!(receiver is IActorHealthLimitReceiver healthLimitReceiver) || !receiver.IsAlive())
                return;
            var multiplier = 1f;
            if (statuses.TryGetValue(actorId, out var actorStatuses))
                foreach (var active in actorStatuses.Values)
                    if (active.IsActive)
                        multiplier *= Mathf.Pow(active.Config.MaximumHealthMultiplier, active.StackCount);

            healthLimitReceiver.SetMaxHealthMultiplier(multiplier);
            var after = receiver.GetSnapshot();
            if (!Mathf.Approximately(before.MaxHealth, after.MaxHealth) ||
                !Mathf.Approximately(before.CurrentHealth, after.CurrentHealth))
                eventBus?.Publish(new HealthChangedEvent(HealthSnapshot.FromActor(before),
                    HealthSnapshot.FromActor(after), causeStatusId));
        }

        private void TickHunger(float deltaTime)
        {
            if (!configs.TryGetStatus(StatusKind.Hunger, out var hungerConfig)) return;
            var actorIds = new List<ActorId>(hungerRuntimes.Keys);
            foreach (var actorId in actorIds)
            {
                if (!hungerRuntimes.TryGetValue(actorId, out var runtime)) continue;
                if (!TryResolveTarget(actorId, out var actor) || actor.GetActorType() != ActorType.Slime)
                {
                    hungerRuntimes.Remove(actorId);
                    continue;
                }
                if (HasStatus(actorId, StatusKind.Frozen)) continue;

                var wasHungry = HasStatus(actorId, StatusKind.Hunger);
                runtime.TimeSinceFedSeconds += deltaTime;
                if (!wasHungry && runtime.TimeSinceFedSeconds >= hungerConfig.HungerTriggerSeconds)
                {
                    ApplyStatus(new StatusApplicationRequest(default(ActorId), actorId,
                        hungerConfig.StatusId));
                    runtime.RageRollRemainingSeconds = hungerConfig.RageRollIntervalSeconds;
                    continue;
                }
                if (!wasHungry) continue;

                var interval = hungerConfig.RageRollIntervalSeconds;
                if (runtime.RageRollRemainingSeconds <= 0f)
                    runtime.RageRollRemainingSeconds = interval;
                runtime.RageRollRemainingSeconds -= deltaTime;
                var rolls = 0;
                while (runtime.RageRollRemainingSeconds <= 0f && rolls++ < 16)
                {
                    runtime.RageRollRemainingSeconds += interval;
                    if (!HasStatus(actorId, StatusKind.Rage) &&
                        randomValue() < hungerConfig.HungerRageChance &&
                        configs.TryGetStatus(StatusKind.Rage, out var rageConfig))
                        ApplyStatus(new StatusApplicationRequest(default(ActorId), actorId,
                            rageConfig.StatusId));
                }
                if (rolls >= 16 && runtime.RageRollRemainingSeconds <= 0f)
                    runtime.RageRollRemainingSeconds = interval;
            }
        }

        private void TickFurnaceExposure(float deltaTime)
        {
            if (!configs.TryGetStatus(StatusKind.Hot, out var hotConfig) ||
                hotConfig.FurnaceExposureSeconds <= 0f) return;
            var actorIds = new List<ActorId>(actorsInFurnaceCores);
            foreach (var actorId in actorIds)
            {
                if (!TryResolveTarget(actorId, out var actor) || actor.GetActorType() != ActorType.Slime)
                {
                    NotifyFurnaceCoreExited(actorId);
                    continue;
                }
                if (HasStatus(actorId, StatusKind.Frozen)) continue;
                furnaceExposureSeconds.TryGetValue(actorId, out var elapsed);
                elapsed += deltaTime;
                furnaceExposureSeconds[actorId] = elapsed;
                if (elapsed < hotConfig.FurnaceExposureSeconds) continue;
                NotifyFurnaceCoreExited(actorId);
                ApplyStatus(new StatusApplicationRequest(default(ActorId), actorId,
                    hotConfig.StatusId));
            }
        }

        private void OnSlimeSpawned(SlimeSpawnedEvent eventData)
        {
            hungerRuntimes[eventData.Snapshot.ActorId] = new HungerRuntime();
        }

        private void OnSlimeDied(SlimeDiedEvent eventData)
        {
            var actorId = eventData.Snapshot.ActorId;
            ApplyHotDeathBurst(eventData.Snapshot.ActorId, eventData.Snapshot.Position,
                eventData.Snapshot.ConfigId);
            hungerRuntimes.Remove(actorId);
            NotifyFurnaceCoreExited(actorId);

            if (!configs.TryGetStatus(StatusKind.Hunger, out var hungerConfig)) return;
            var companions = new List<IEffectReceiver>(actorEnumerator());
            for (var i = 0; i < companions.Count; i++)
            {
                var companion = companions[i];
                if (companion == null || !companion.IsAlive() ||
                    companion.GetActorType() != ActorType.Slime || companion.GetActorId() == actorId)
                    continue;
                var roll = randomValue();
                if (roll < hungerConfig.CompanionDeathRageChance &&
                    configs.TryGetStatus(StatusKind.Rage, out var rage))
                    ApplyStatus(new StatusApplicationRequest(default(ActorId), companion.GetActorId(), rage.StatusId));
                else if (roll < hungerConfig.CompanionDeathRageChance +
                    hungerConfig.CompanionDeathFearChance &&
                    configs.TryGetStatus(StatusKind.Fear, out var fear))
                    ApplyStatus(new StatusApplicationRequest(default(ActorId), companion.GetActorId(), fear.StatusId));
            }
        }

        private void ApplyHotDeathBurst(ActorId sourceActorId, Vector2 position, ConfigId actorConfigId)
        {
            if (!TryGetActiveStatus(sourceActorId, StatusKind.Hot, out var hot) ||
                !hot.Config.HasDeathBurst || !configs.TryGetActor(actorConfigId, out var actorConfig))
                return;
            var damage = actorConfig.AttackPower * hot.Config.DeathBurstDamageFraction;
            if (damage <= 0f || !IsFinite(damage)) return;
            var pixelsPerUnit = GetPixelsPerUnit(actorResolver(sourceActorId));
            var radius = hot.Config.DeathBurstRadiusPixels / pixelsPerUnit;
            var targets = new List<IEffectReceiver>(actorEnumerator());
            for (var i = 0; i < targets.Count; i++)
            {
                var target = targets[i];
                if (target == null || !target.IsAlive() || target.GetActorType() != ActorType.Enemy ||
                    Vector2.Distance(position, target.GetSnapshot().Position) > radius) continue;
                ApplyEffect(new EffectRequest(EffectRequestId.NewId(), sourceActorId,
                    target.GetActorId(), EffectType.Damage, damage,
                    relationshipPolicy: EffectRelationshipPolicy.Any,
                    requireLivingSource: false));
            }
        }

        private void NotifyNearbySlimesOfHumanBlood(IEffectReceiver human, Vector2 position)
        {
            if (!configs.TryGetStatus(StatusKind.Rage, out var rage) ||
                rage.HumanBloodSmellRadiusPixels <= 0f) return;
            var radius = rage.HumanBloodSmellRadiusPixels / GetPixelsPerUnit(human);
            var slimes = new List<IEffectReceiver>(actorEnumerator());
            for (var i = 0; i < slimes.Count; i++)
            {
                var slime = slimes[i];
                if (slime == null || !slime.IsAlive() || slime.GetActorType() != ActorType.Slime ||
                    Vector2.Distance(position, slime.GetSnapshot().Position) > radius) continue;
                NotifyInjuredHumanNearby(slime.GetActorId());
            }
        }

        private static float GetPixelsPerUnit(IEffectReceiver receiver)
        {
            if (receiver is IGameplayPixelsPerUnitProvider provider)
            {
                var gameplayPixelsPerUnit = provider.GameplayPixelsPerUnit;
                if (gameplayPixelsPerUnit > 0f && !float.IsNaN(gameplayPixelsPerUnit) &&
                    !float.IsInfinity(gameplayPixelsPerUnit))
                    return gameplayPixelsPerUnit;
            }
            if (receiver is Component component)
            {
                var renderer = component.GetComponent<SpriteRenderer>();
                if (renderer != null && renderer.sprite != null && renderer.sprite.pixelsPerUnit > 0f)
                    return renderer.sprite.pixelsPerUnit;
            }
            return 100f;
        }

        private bool TryGetActiveStatus(ActorId actorId, StatusKind kind, out ActiveStatus active)
        {
            active = null;
            if (!configs.TryGetStatus(kind, out var config) ||
                !statuses.TryGetValue(actorId, out var actorStatuses) ||
                !actorStatuses.TryGetValue(config.StatusId, out active)) return false;
            return active.IsActive;
        }

        private void OnMapAreaEntered(MapAreaEnteredEvent eventData)
        {
            if (disposed || paused || string.IsNullOrWhiteSpace(eventData.Area.StatusEffectConfigId))
                return;
            var configId = new ConfigId(eventData.Area.StatusEffectConfigId);
            if (!configs.TryGet(configId, out StatusConfig config))
            {
                PublishRejected(EffectRequestId.NewId(), default(ActorId), eventData.ActorId,
                    EffectApplyResult.Rejected("Map area StatusConfig is missing or ambiguous."));
                return;
            }

            ApplyStatus(new StatusApplicationRequest(default(ActorId), eventData.ActorId,
                config.StatusId, sourceAreaId: eventData.Area.AreaId));
        }

        private void OnMapAreaExited(MapAreaExitedEvent eventData)
        {
            if (disposed || !statuses.TryGetValue(eventData.ActorId, out var actorStatuses)) return;
            var matchingIds = new List<StatusId>();
            foreach (var pair in actorStatuses)
                if (pair.Value.AreaSources.Contains(eventData.Area.AreaId))
                    matchingIds.Add(pair.Key);

            foreach (var statusId in matchingIds)
            {
                if (!statuses.TryGetValue(eventData.ActorId, out actorStatuses) ||
                    !actorStatuses.TryGetValue(statusId, out var active)) continue;
                var receiver = actorResolver(eventData.ActorId);
                var previousModifiers = GetMovementModifiers(eventData.ActorId);
                var previousControl = receiver != null && receiver.IsAlive()
                    ? receiver.GetSnapshot().ControlMode : ActorControlMode.Autonomous;
                if (active.AreaSources.Count == 1 && !active.HasDirectSource &&
                    active.Config.AreaExitGraceSeconds <= 0f)
                {
                    RemoveStatusInternal(eventData.ActorId, statusId,
                        StatusRemoveReason.AreaExitGraceElapsed, allowDeadActor: false,
                        previousModifiers: previousModifiers);
                    continue;
                }
                active.AreaSources.Remove(eventData.Area.AreaId);
                if (active.AreaSources.Count == 0 && !active.HasDirectSource)
                {
                    active.ExitGraceRemainingSeconds = active.Config.AreaExitGraceSeconds;
                }
                eventBus?.Publish(new StatusChangedEvent(eventData.ActorId, CreateSnapshot(active)));
                if (receiver != null && receiver.IsAlive())
                    RecalculateControlMode(eventData.ActorId, receiver, previousControl);
                PublishMovementModifierChange(eventData.ActorId, previousModifiers);
            }
        }

        private void OnActorDied(SlimeDiedEvent eventData)
        {
            OnSlimeDied(eventData);
            ClearActor(eventData.Snapshot.ActorId, StatusRemoveReason.ActorDied, allowDeadActor: true);
        }

        private void OnActorDied(EnemyDiedEvent eventData) =>
            ClearActor(eventData.Snapshot.ActorId, StatusRemoveReason.ActorDied, allowDeadActor: true);

        private void OnActorDespawned(SlimeDespawnedEvent eventData) =>
            ClearActor(eventData.ActorId, StatusRemoveReason.ActorDespawned, allowDeadActor: true);

        private void OnActorDespawned(EnemyDespawnedEvent eventData) =>
            ClearActor(eventData.ActorId, StatusRemoveReason.ActorDespawned, allowDeadActor: true);

        private void ClearActor(ActorId actorId, StatusRemoveReason reason, bool allowDeadActor)
        {
            IEffectReceiver receiver = actorResolver(actorId);
            var activeEntries = new Dictionary<StatusId, ActiveStatus>();
            if (statuses.TryGetValue(actorId, out var actorStatuses))
            {
                foreach (var pair in actorStatuses) activeEntries.Add(pair.Key, pair.Value);
                statuses.Remove(actorId);
            }
            controlModeBaselines.Remove(actorId);

            if (receiver != null && (receiver.IsAlive() || allowDeadActor))
            {
                var actorSnapshot = receiver.GetSnapshot();
                foreach (var statusId in activeEntries.Keys)
                    if (ContainsStatus(actorSnapshot, statusId))
                        receiver.RemoveResolvedStatus(statusId.Value);
            }
            hungerRuntimes.Remove(actorId);
            NotifyFurnaceCoreExited(actorId);

            foreach (var pair in activeEntries)
                eventBus?.Publish(new StatusRemovedEvent(actorId,
                    CreateSnapshot(pair.Value), reason));
        }

        private void RemoveStatusInternal(ActorId actorId, StatusId statusId,
            StatusRemoveReason reason, bool allowDeadActor,
            MovementModifierSnapshot? previousModifiers = null)
        {
            if (!statuses.TryGetValue(actorId, out var actorStatuses) ||
                !actorStatuses.TryGetValue(statusId, out var active)) return;

            var receiver = actorResolver(actorId);
            var modifiersBeforeRemoval = previousModifiers ?? GetMovementModifiers(actorId);
            var beforeHealthActor = receiver != null && receiver.IsAlive()
                ? receiver.GetSnapshot() : default(ActorSnapshot);
            var previousControl = receiver != null && receiver.IsAlive()
                ? receiver.GetSnapshot().ControlMode : ActorControlMode.Autonomous;
            actorStatuses.Remove(statusId);
            if (actorStatuses.Count == 0) statuses.Remove(actorId);
            if (receiver != null && (receiver.IsAlive() || allowDeadActor))
                receiver.RemoveResolvedStatus(statusId.Value);

            var snapshot = CreateSnapshot(active);
            if (reason == StatusRemoveReason.Expired ||
                reason == StatusRemoveReason.AreaExitGraceElapsed)
                eventBus?.Publish(new StatusExpiredEvent(actorId, snapshot));
            eventBus?.Publish(new StatusRemovedEvent(actorId, snapshot, reason));

            if (receiver != null && receiver.IsAlive())
            {
                RecalculateMaximumHealth(actorId, receiver, beforeHealthActor, statusId);
                RecalculateControlMode(actorId, receiver, previousControl);
                PublishMovementModifierChange(actorId, modifiersBeforeRemoval);
            }
        }

        private void RecalculateControlMode(ActorId actorId, IEffectReceiver receiver,
            ActorControlMode previousMode)
        {
            if (receiver == null || !receiver.IsAlive() || !(receiver is IActorControlReceiver controlReceiver))
                return;

            var hasControlStatus = false;
            var pauseActor = false;
            if (statuses.TryGetValue(actorId, out var actorStatuses))
            {
                foreach (var active in actorStatuses.Values)
                {
                    if (!active.IsActive) continue;
                    if (active.Config.ControlMode == StatusControlMode.Paused)
                    {
                        hasControlStatus = true;
                        pauseActor = true;
                        break;
                    }
                    if (active.Config.ControlMode == StatusControlMode.Controlled)
                        hasControlStatus = true;
                }
            }

            var currentMode = receiver.GetSnapshot().ControlMode;
            ActorControlMode desiredMode;
            if (hasControlStatus)
            {
                desiredMode = pauseActor ? ActorControlMode.Paused : ActorControlMode.Controlled;
                if (currentMode == desiredMode) return;
                if (!controlModeBaselines.ContainsKey(actorId))
                    controlModeBaselines.Add(actorId, currentMode);
            }
            else
            {
                if (!controlModeBaselines.TryGetValue(actorId, out desiredMode)) return;
                controlModeBaselines.Remove(actorId);
                if (currentMode != ActorControlMode.Controlled && currentMode != ActorControlMode.Paused)
                    return;
            }
            if (currentMode == desiredMode) return;
            controlReceiver.SetControlMode(desiredMode);
            var updatedMode = receiver.GetSnapshot().ControlMode;
            if (updatedMode != previousMode)
                eventBus?.Publish(new ActorControlChangedEvent(actorId, previousMode, updatedMode));
        }

        private void PublishMovementModifierChange(ActorId actorId,
            MovementModifierSnapshot previous)
        {
            var current = GetMovementModifiers(actorId);
            if (!Mathf.Approximately(previous.SpeedMultiplier, current.SpeedMultiplier) ||
                previous.IsRooted != current.IsRooted)
                eventBus?.Publish(new MovementModifierChangedEvent(actorId, previous, current));
        }

        private void SubscribeToLevelEvents()
        {
            if (eventBus == null) return;
            subscriptions.Add(eventBus.Subscribe<MapAreaEnteredEvent>(OnMapAreaEntered));
            subscriptions.Add(eventBus.Subscribe<MapAreaExitedEvent>(OnMapAreaExited));
            subscriptions.Add(eventBus.Subscribe<SlimeSpawnedEvent>(OnSlimeSpawned));
            subscriptions.Add(eventBus.Subscribe<SlimeDiedEvent>(OnActorDied));
            subscriptions.Add(eventBus.Subscribe<EnemyDiedEvent>(OnActorDied));
            subscriptions.Add(eventBus.Subscribe<SlimeDespawnedEvent>(OnActorDespawned));
            subscriptions.Add(eventBus.Subscribe<EnemyDespawnedEvent>(OnActorDespawned));
        }

        private bool TryResolveTarget(ActorId actorId, out IEffectReceiver receiver)
        {
            receiver = null;
            if (!actorId.IsValid) return false;
            receiver = actorResolver(actorId);
            return receiver != null && receiver.GetActorId() == actorId && receiver.IsAlive();
        }

        private bool TryResolveActor(ActorId actorId, out IEffectReceiver receiver)
        {
            receiver = null;
            if (!actorId.IsValid) return false;
            receiver = actorResolver(actorId);
            return receiver != null && receiver.GetActorId() == actorId;
        }

        private bool ValidateSourceAndRelationship(EffectRequest request,
            IEffectReceiver target)
        {
            if (!request.SourceActorId.IsValid)
                return request.RelationshipPolicy == EffectRelationshipPolicy.Any;

            if (!TryResolveActor(request.SourceActorId, out var source))
                return !request.RequireLivingSource &&
                    request.RelationshipPolicy == EffectRelationshipPolicy.Any;
            if (request.RequireLivingSource && !source.IsAlive()) return false;

            switch (request.RelationshipPolicy)
            {
                case EffectRelationshipPolicy.Any:
                    return true;
                case EffectRelationshipPolicy.HostileOnly:
                    return source.GetFactionId() != target.GetFactionId();
                case EffectRelationshipPolicy.FriendlyOnly:
                    return source.GetFactionId() == target.GetFactionId();
                case EffectRelationshipPolicy.SelfOnly:
                    return source.GetActorId() == target.GetActorId();
                default:
                    return false;
            }
        }

        private static bool IsValidRequest(EffectRequest request)
        {
            if (!request.TargetActorId.IsValid || !Enum.IsDefined(typeof(EffectType), request.Type) ||
                !Enum.IsDefined(typeof(EffectRelationshipPolicy), request.RelationshipPolicy) ||
                !IsFinite(request.DurationSeconds) || request.DurationSeconds < -1f ||
                !IsFinite(request.Direction.x) || !IsFinite(request.Direction.y))
                return false;

            if (request.Type == EffectType.Damage || request.Type == EffectType.Heal)
                return IsFinite(request.Amount) && request.Amount >= 0f;
            if (request.Type == EffectType.Status || request.Type == EffectType.Control)
                return request.StatusId.IsValid;
            return false;
        }

        private bool ContainsStatus(ActorSnapshot snapshot, StatusId statusId)
        {
            if (snapshot.StatusIds == null) return false;
            for (var i = 0; i < snapshot.StatusIds.Count; i++)
                if (string.Equals(snapshot.StatusIds[i], statusId.Value, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private StatusEntrySnapshot CreateSnapshot(ActiveStatus active)
        {
            var isPermanent = active.AreaSources.Count > 0 ||
                (active.HasDirectSource && float.IsPositiveInfinity(active.DirectRemainingSeconds));
            var remaining = active.AreaSources.Count > 0
                ? float.PositiveInfinity
                : active.HasDirectSource ? active.DirectRemainingSeconds
                : active.ExitGraceRemainingSeconds;
            return new StatusEntrySnapshot(active.Config.StatusId, active.Config.ConfigId,
                remaining, active.AreaSources.Count, isPermanent, active.StackCount,
                active.Config.MovementSpeedMultiplier, active.Config.AttackSpeedMultiplier,
                active.Config.DamageDealtMultiplier, active.Config.DamageTakenMultiplier,
                active.Config.IsRooted, active.Config.ControlMode, active.Config.Kind,
                active.ElapsedSeconds, active.Config.MaximumHealthMultiplier,
                active.Config.RageHumanAttackDelaySeconds);
        }

        private StatusEntrySnapshot CreateSnapshot(StatusId statusId)
        {
            if (configs.TryGetStatus(statusId, out var config))
                return new StatusEntrySnapshot(statusId, config.ConfigId,
                    float.PositiveInfinity, 0, true, 1,
                    config.MovementSpeedMultiplier, config.AttackSpeedMultiplier,
                    config.DamageDealtMultiplier, config.DamageTakenMultiplier,
                    config.IsRooted, config.ControlMode, config.Kind, 0f,
                    config.MaximumHealthMultiplier, config.RageHumanAttackDelaySeconds);
            return new StatusEntrySnapshot(statusId, default(ConfigId),
                float.PositiveInfinity, 0, true, 1, 1f, 1f, 1f, 1f,
                false, StatusControlMode.None);
        }

        private EffectApplyResult Reject(EffectRequest request, EffectApplyResult result)
        {
            PublishRejected(request.RequestId, request.SourceActorId,
                request.TargetActorId, result);
            return result;
        }

        private void PublishRejected(EffectRequestId requestId, ActorId sourceActorId,
            ActorId targetActorId, EffectApplyResult result)
        {
            eventBus?.Publish(new EffectRejectedEvent(requestId,
                sourceActorId, targetActorId, result));
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
