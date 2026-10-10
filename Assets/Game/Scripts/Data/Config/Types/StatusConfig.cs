using System;
using SlimeBattle.Actors;
using SlimeBattle.Status;
using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Status Config")]
    public sealed class StatusConfig : ConfigAsset
    {
        [SerializeField] private string statusId;
        [SerializeField, Min(0f)] private float durationSeconds;
        [SerializeField, Min(0f)] private float areaExitGraceSeconds;
        [SerializeField, Min(0f)] private float movementSpeedMultiplier = 1f;
        [SerializeField, Min(0f)] private float attackSpeedMultiplier = 1f;
        [SerializeField, Min(0f)] private float damageDealtMultiplier = 1f;
        [SerializeField, Min(0f)] private float damageTakenMultiplier = 1f;
        [SerializeField, Min(0.01f)] private float maximumHealthMultiplier = 1f;
        [SerializeField] private bool isRooted;
        [SerializeField] private StatusControlMode controlMode;
        [SerializeField] private StatusKind statusKind;
        [SerializeField] private StatusStackingPolicy stackingPolicy = StatusStackingPolicy.RefreshDuration;
        [SerializeField, Min(1)] private int maximumStacks = 1;
        [SerializeField] private string counteractsStatusId;
        [SerializeField] private ActorType[] immuneActorTypes = Array.Empty<ActorType>();
        [SerializeField] private EffectType periodicEffectType;
        [SerializeField, Min(0f)] private float periodicAmount;
        [SerializeField, Min(0f)] private float periodicIntervalSeconds;
        [SerializeField] private PeriodicAmountMode periodicAmountMode;
        [SerializeField] private string onHitStatusId;
        [SerializeField, Min(0f)] private float onHitStatusDurationSeconds;
        [SerializeField, Min(0f)] private float hungerTriggerSeconds;
        [SerializeField, Min(0f)] private float rageRollIntervalSeconds;
        [SerializeField, Range(0f, 1f)] private float hungerRageChance;
        [SerializeField, Range(0f, 1f)] private float humanBloodRageChance;
        [SerializeField, Min(0f)] private float humanBloodSmellRadiusPixels;
        [SerializeField, Range(0f, 1f)] private float companionDeathRageChance;
        [SerializeField, Range(0f, 1f)] private float companionDeathFearChance;
        [SerializeField, Min(0f)] private float rageHumanAttackDelaySeconds;
        [SerializeField, Range(0f, 1f)] private float fleeHealthThreshold = 0.5f;
        [SerializeField, Range(0f, 1f)] private float fleeChance = 0.2f;
        [SerializeField, Range(0f, 1f)] private float criticalFleeHealthThreshold = 0.3f;
        [SerializeField, Range(0f, 1f)] private float criticalFleeChance = 0.5f;
        [SerializeField, Min(0f)] private float deathBurstDamageFraction;
        [SerializeField, Min(0f)] private float deathBurstRadiusPixels;
        [SerializeField, Min(0f)] private float furnaceExposureSeconds;

        public StatusId StatusId => new StatusId(statusId);
        public float DurationSeconds => durationSeconds;
        public float AreaExitGraceSeconds => areaExitGraceSeconds;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;
        public float AttackSpeedMultiplier => attackSpeedMultiplier;
        public float DamageDealtMultiplier => damageDealtMultiplier;
        public float DamageTakenMultiplier => damageTakenMultiplier;
        public float MaximumHealthMultiplier => maximumHealthMultiplier;
        public bool IsRooted => isRooted;
        public StatusControlMode ControlMode => controlMode;
        public StatusKind Kind => statusKind;
        public StatusStackingPolicy StackingPolicy => stackingPolicy;
        public int MaximumStacks => maximumStacks;
        public StatusId CounteractsStatusId => new StatusId(counteractsStatusId);
        public EffectType PeriodicEffectType => periodicEffectType;
        public float PeriodicAmount => periodicAmount;
        public float PeriodicIntervalSeconds => periodicIntervalSeconds;
        public PeriodicAmountMode PeriodicValueMode => periodicAmountMode;
        public StatusId OnHitStatusId => new StatusId(onHitStatusId);
        public float OnHitStatusDurationSeconds => onHitStatusDurationSeconds;
        public float HungerTriggerSeconds => hungerTriggerSeconds;
        public float RageRollIntervalSeconds => rageRollIntervalSeconds;
        public float HungerRageChance => hungerRageChance;
        public float HumanBloodRageChance => humanBloodRageChance;
        public float HumanBloodSmellRadiusPixels => humanBloodSmellRadiusPixels;
        public float CompanionDeathRageChance => companionDeathRageChance;
        public float CompanionDeathFearChance => companionDeathFearChance;
        public float RageHumanAttackDelaySeconds => rageHumanAttackDelaySeconds;
        public float FleeHealthThreshold => fleeHealthThreshold;
        public float FleeChance => fleeChance;
        public float CriticalFleeHealthThreshold => criticalFleeHealthThreshold;
        public float CriticalFleeChance => criticalFleeChance;
        public float DeathBurstDamageFraction => deathBurstDamageFraction;
        public float DeathBurstRadiusPixels => deathBurstRadiusPixels;
        public float FurnaceExposureSeconds => furnaceExposureSeconds;
        public bool HasPeriodicEffect => periodicAmount > 0f;
        public bool HasOnHitStatus => OnHitStatusId.IsValid;
        public bool HasDeathBurst => deathBurstDamageFraction > 0f && deathBurstRadiusPixels > 0f;

        public bool IsImmuneTo(ActorType actorType)
        {
            if (immuneActorTypes == null) return false;
            for (var i = 0; i < immuneActorTypes.Length; i++)
                if (immuneActorTypes[i] == actorType) return true;
            return false;
        }

        protected override void ValidateSpecific(ConfigValidationReport report)
        {
            if (!StatusId.IsValid) report.AddError($"{name}: StatusId is empty.");
            if (!IsFiniteNonNegative(durationSeconds))
                report.AddError($"{name}: DurationSeconds must be finite and non-negative.");
            if (!IsFiniteNonNegative(areaExitGraceSeconds))
                report.AddError($"{name}: AreaExitGraceSeconds must be finite and non-negative.");
            if (!IsFiniteNonNegative(movementSpeedMultiplier))
                report.AddError($"{name}: MovementSpeedMultiplier must be finite and non-negative.");
            if (!IsFiniteNonNegative(attackSpeedMultiplier) || attackSpeedMultiplier <= 0f)
                report.AddError($"{name}: AttackSpeedMultiplier must be finite and greater than zero.");
            if (!IsFiniteNonNegative(damageDealtMultiplier) ||
                !IsFiniteNonNegative(damageTakenMultiplier))
                report.AddError($"{name}: Damage multipliers must be finite and non-negative.");
            if (!IsFiniteNonNegative(maximumHealthMultiplier) || maximumHealthMultiplier <= 0f)
                report.AddError($"{name}: MaximumHealthMultiplier must be finite and greater than zero.");
            if (maximumStacks < 1)
                report.AddError($"{name}: MaximumStacks must be at least one.");
            if (!Enum.IsDefined(typeof(StatusControlMode), controlMode))
                report.AddError($"{name}: ControlMode is invalid.");
            if (!Enum.IsDefined(typeof(StatusKind), statusKind))
                report.AddError($"{name}: StatusKind is invalid.");
            if (!Enum.IsDefined(typeof(StatusStackingPolicy), stackingPolicy))
                report.AddError($"{name}: StackingPolicy is invalid.");
            if (!Enum.IsDefined(typeof(PeriodicAmountMode), periodicAmountMode))
                report.AddError($"{name}: PeriodicAmountMode is invalid.");
            if (immuneActorTypes != null)
            {
                for (var i = 0; i < immuneActorTypes.Length; i++)
                    if (!Enum.IsDefined(typeof(ActorType), immuneActorTypes[i]))
                        report.AddError($"{name}: ImmuneActorTypes contains an invalid ActorType.");
            }

            if (!IsFiniteNonNegative(periodicAmount) ||
                !IsFiniteNonNegative(periodicIntervalSeconds))
            {
                report.AddError($"{name}: Periodic effect values must be finite and non-negative.");
            }
            else if (periodicAmount > 0f &&
                (periodicIntervalSeconds <= 0f ||
                 (periodicEffectType != EffectType.Damage && periodicEffectType != EffectType.Heal)))
            {
                report.AddError($"{name}: Periodic effects require Damage or Heal and a positive interval.");
            }

            if (!IsFiniteNonNegative(onHitStatusDurationSeconds) ||
                (OnHitStatusId.IsValid && onHitStatusDurationSeconds <= 0f))
                report.AddError($"{name}: On-hit statuses require a positive duration.");
            if (!IsFiniteNonNegative(hungerTriggerSeconds) ||
                !IsFiniteNonNegative(rageRollIntervalSeconds) ||
                !IsFiniteNonNegative(rageHumanAttackDelaySeconds) ||
                !IsProbability(fleeHealthThreshold) || !IsProbability(fleeChance) ||
                !IsProbability(criticalFleeHealthThreshold) || !IsProbability(criticalFleeChance) ||
                !IsFiniteNonNegative(humanBloodSmellRadiusPixels) ||
                !IsFiniteNonNegative(deathBurstDamageFraction) ||
                !IsFiniteNonNegative(deathBurstRadiusPixels) ||
                !IsFiniteNonNegative(furnaceExposureSeconds))
                report.AddError($"{name}: Status behavior timings and magnitudes must be finite and non-negative.");
            if (!IsProbability(hungerRageChance) ||
                !IsProbability(humanBloodRageChance) ||
                !IsProbability(companionDeathRageChance) ||
                !IsProbability(companionDeathFearChance) ||
                companionDeathRageChance + companionDeathFearChance > 1f)
                report.AddError($"{name}: Status behavior chances must be probabilities whose companion outcomes total at most one.");
            if (Kind == StatusKind.Hunger && (hungerTriggerSeconds <= 0f || rageRollIntervalSeconds <= 0f))
                report.AddError($"{name}: Hunger requires positive hunger and rage-roll intervals.");
            if (Kind == StatusKind.Fear && criticalFleeHealthThreshold >= fleeHealthThreshold)
                report.AddError($"{name}: CriticalFleeHealthThreshold must be below FleeHealthThreshold.");
            if (Kind == StatusKind.Burning && periodicAmountMode == PeriodicAmountMode.MaxHealthFraction && periodicAmount > 1f)
                report.AddError($"{name}: Max-health periodic damage fraction cannot exceed one.");
            if (deathBurstDamageFraction > 1f)
                report.AddError($"{name}: DeathBurstDamageFraction cannot exceed one.");
        }

        private static bool IsProbability(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f && value <= 1f;

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}