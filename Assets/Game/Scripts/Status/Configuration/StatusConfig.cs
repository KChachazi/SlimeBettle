using System;
using SlimeBattle.Actors;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Status
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
        [SerializeField] private bool isRooted;
        [SerializeField] private StatusControlMode controlMode;
        [SerializeField] private StatusStackingPolicy stackingPolicy = StatusStackingPolicy.RefreshDuration;
        [SerializeField, Min(1)] private int maximumStacks = 1;
        [SerializeField] private string counteractsStatusId;
        [SerializeField] private ActorType[] immuneActorTypes = Array.Empty<ActorType>();
        [SerializeField] private EffectType periodicEffectType;
        [SerializeField, Min(0f)] private float periodicAmount;
        [SerializeField, Min(0f)] private float periodicIntervalSeconds;

        public StatusId StatusId => new StatusId(statusId);
        public float DurationSeconds => durationSeconds;
        public float AreaExitGraceSeconds => areaExitGraceSeconds;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;
        public float AttackSpeedMultiplier => attackSpeedMultiplier;
        public float DamageDealtMultiplier => damageDealtMultiplier;
        public float DamageTakenMultiplier => damageTakenMultiplier;
        public bool IsRooted => isRooted;
        public StatusControlMode ControlMode => controlMode;
        public StatusStackingPolicy StackingPolicy => stackingPolicy;
        public int MaximumStacks => maximumStacks;
        public StatusId CounteractsStatusId => new StatusId(counteractsStatusId);
        public EffectType PeriodicEffectType => periodicEffectType;
        public float PeriodicAmount => periodicAmount;
        public float PeriodicIntervalSeconds => periodicIntervalSeconds;
        public bool HasPeriodicEffect => periodicAmount > 0f;

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
            if (maximumStacks < 1)
                report.AddError($"{name}: MaximumStacks must be at least one.");
            if (!Enum.IsDefined(typeof(StatusControlMode), controlMode))
                report.AddError($"{name}: ControlMode is invalid.");
            if (!Enum.IsDefined(typeof(StatusStackingPolicy), stackingPolicy))
                report.AddError($"{name}: StackingPolicy is invalid.");
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
        }

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
