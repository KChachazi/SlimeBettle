using System;
using UnityEngine;

namespace SlimeBattle.Data
{
    public enum FirstLevelHazardKind
    {
        ContactDamage = 0,
        RollingRock = 1,
        MovementSlow = 2,
        CliffReturn = 3,
        Supply = 4
    }

    /// <summary>
    /// Static first-level trap and supply rules. Runtime activation remains owned by the
    /// corresponding map/mechanism feature and is not implemented by this config asset.
    /// </summary>
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/First Level Hazard",
        fileName = "CFG_FirstLevelHazard")]
    public sealed class FirstLevelHazardConfig : ConfigAsset
    {
        [SerializeField] private FirstLevelHazardKind kind;
        [SerializeField, Min(0f)] private float triggerRadiusPixels;
        [SerializeField, Min(0)] private int activationLimit;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField, Min(0)] private int damage;
        [SerializeField, Min(0f)] private float knockbackPixels;
        [SerializeField, Min(0f)] private float effectDurationSeconds;
        [SerializeField, Min(0.01f)] private float movementSpeedMultiplier = 1f;
        [SerializeField, Min(0f)] private float projectileRadiusPixels;
        [SerializeField, Min(0f)] private float projectileSpeedPixelsPerSecond;
        [SerializeField, Min(0f)] private float projectileTravelPixels;
        [SerializeField, Min(0)] private int supplyAmount;
        [SerializeField] private bool returnsToSafeEdge;

        public FirstLevelHazardKind Kind => kind;
        public float TriggerRadiusPixels => triggerRadiusPixels;
        /// <summary>Zero means the hazard can reactivate indefinitely.</summary>
        public int ActivationLimit => activationLimit;
        public float CooldownSeconds => cooldownSeconds;
        public int Damage => damage;
        public float KnockbackPixels => knockbackPixels;
        public float EffectDurationSeconds => effectDurationSeconds;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;
        public float ProjectileRadiusPixels => projectileRadiusPixels;
        public float ProjectileSpeedPixelsPerSecond => projectileSpeedPixelsPerSecond;
        public float ProjectileTravelPixels => projectileTravelPixels;
        public int SupplyAmount => supplyAmount;
        public bool ReturnsToSafeEdge => returnsToSafeEdge;

        protected override void ValidateSpecific(ConfigValidationReport report)
        {
            if (!Enum.IsDefined(typeof(FirstLevelHazardKind), kind))
            {
                report.AddError($"{name}: FirstLevelHazardKind is invalid.");
                return;
            }
            if (!IsFiniteNonNegative(triggerRadiusPixels) ||
                !IsFiniteNonNegative(cooldownSeconds) || damage < 0 ||
                !IsFiniteNonNegative(knockbackPixels) ||
                !IsFiniteNonNegative(effectDurationSeconds) ||
                !IsFiniteNonNegative(movementSpeedMultiplier) ||
                !IsFiniteNonNegative(projectileRadiusPixels) ||
                !IsFiniteNonNegative(projectileSpeedPixelsPerSecond) ||
                !IsFiniteNonNegative(projectileTravelPixels) ||
                activationLimit < 0 || supplyAmount < 0)
            {
                report.AddError($"{name}: Hazard values must be finite and non-negative.");
                return;
            }

            switch (kind)
            {
                case FirstLevelHazardKind.ContactDamage:
                    if (triggerRadiusPixels <= 0f || damage <= 0)
                        report.AddError($"{name}: Contact damage hazards need a radius and positive damage.");
                    break;
                case FirstLevelHazardKind.RollingRock:
                    if (triggerRadiusPixels <= 0f || damage <= 0 || activationLimit != 1 ||
                        projectileRadiusPixels <= 0f || projectileSpeedPixelsPerSecond <= 0f ||
                        projectileTravelPixels <= 0f)
                        report.AddError($"{name}: Rolling rocks need a one-shot trigger and complete travel data.");
                    break;
                case FirstLevelHazardKind.MovementSlow:
                    if (triggerRadiusPixels <= 0f || movementSpeedMultiplier <= 0f ||
                        movementSpeedMultiplier >= 1f || effectDurationSeconds <= 0f)
                        report.AddError($"{name}: Slow hazards need a radius, a reducing multiplier and a duration.");
                    break;
                case FirstLevelHazardKind.CliffReturn:
                    if (triggerRadiusPixels <= 0f || damage <= 0 || activationLimit != 1 ||
                        !returnsToSafeEdge)
                        report.AddError($"{name}: Cliff hazards need one-shot damage and a safe-edge return rule.");
                    break;
                case FirstLevelHazardKind.Supply:
                    if (triggerRadiusPixels <= 0f || supplyAmount <= 0)
                        report.AddError($"{name}: Supply points need a pickup radius and positive amount.");
                    break;
            }
        }

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
