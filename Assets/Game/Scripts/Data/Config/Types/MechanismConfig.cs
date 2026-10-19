/* MechanismConfig 保存食物、熔炉和障碍的规则参数；运行时状态由 MechanismSystem 持有。 */
using System;
using SlimeBattle.Mechanisms;
using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/Mechanism", fileName = "CFG_Mechanism")]
    public sealed class MechanismConfig : ConfigAsset
    {
        [SerializeField] private MechanismKind kind;
        [SerializeField] private bool playerPlaceable;
        [SerializeField, Min(0f)] private float attractionRadiusPixels;
        [SerializeField, Min(0f)] private float placementRadiusWorldUnits = 0.25f;
        [SerializeField, Min(0)] private int activationLimit = 1;
        [SerializeField, Min(0f)] private float activationCooldownSeconds;
        [SerializeField, Min(0f)] private float placementCooldownSeconds;
        [SerializeField, Min(0)] private int durability;
        [SerializeField, Min(0f)] private float collisionRadiusPixels;
        [SerializeField] private MechanismAuthoring placementPrefab;

        public MechanismKind Kind => kind;
        public bool PlayerPlaceable => playerPlaceable;
        public float AttractionRadiusPixels => attractionRadiusPixels;
        public float PlacementRadiusWorldUnits => placementRadiusWorldUnits;
        /// <summary>Zero means unlimited uses for this instance.</summary>
        public int ActivationLimit => activationLimit;
        public float ActivationCooldownSeconds => activationCooldownSeconds;
        public float PlacementCooldownSeconds => placementCooldownSeconds;
        /// <summary>Zero means the mechanism has no durability rule.</summary>
        public int Durability => durability;
        public float CollisionRadiusPixels => collisionRadiusPixels;
        public MechanismAuthoring PlacementPrefab => placementPrefab;
        public GuideSourceType GuideSourceType => kind == MechanismKind.Food
            ? GuideSourceType.Food
            : kind == MechanismKind.Furnace ? GuideSourceType.Warmth : GuideSourceType.None;

        public void Configure(MechanismKind newKind, bool newPlayerPlaceable,
            float newAttractionRadiusPixels, float newPlacementRadiusWorldUnits,
            int newActivationLimit, float newActivationCooldownSeconds,
            float newPlacementCooldownSeconds)
        {
            EnsureAuthoring();
            kind = newKind;
            playerPlaceable = newPlayerPlaceable;
            attractionRadiusPixels = newAttractionRadiusPixels;
            placementRadiusWorldUnits = newPlacementRadiusWorldUnits;
            activationLimit = newActivationLimit;
            activationCooldownSeconds = newActivationCooldownSeconds;
            placementCooldownSeconds = newPlacementCooldownSeconds;
        }

        public void SetPlacementPrefab(MechanismAuthoring value)
        {
            EnsureAuthoring();
            placementPrefab = value;
        }

        public void ConfigureObstacleRules(int newDurability,
            float newCollisionRadiusPixels)
        {
            EnsureAuthoring();
            durability = newDurability;
            collisionRadiusPixels = newCollisionRadiusPixels;
        }

        protected override void ValidateSpecific(ConfigValidationReport report)
        {
            if (!Enum.IsDefined(typeof(MechanismKind), kind) || kind == MechanismKind.None)
                report.AddError($"{name}: MechanismKind must be Food, Furnace or Obstacle.");
            if (!IsFiniteNonNegative(attractionRadiusPixels))
                report.AddError($"{name}: AttractionRadiusPixels must be finite and non-negative.");
            if ((kind == MechanismKind.Food || kind == MechanismKind.Furnace) &&
                attractionRadiusPixels <= 0f)
                report.AddError($"{name}: Food and Furnace require a positive attraction radius.");
            if (kind == MechanismKind.Food && activationLimit == 0)
                report.AddError($"{name}: Food must have a finite activation limit.");
            if (!IsFiniteNonNegative(placementRadiusWorldUnits))
                report.AddError($"{name}: PlacementRadiusWorldUnits must be finite and non-negative.");
            if (activationLimit < 0)
                report.AddError($"{name}: ActivationLimit must be non-negative; zero means unlimited.");
            if (durability < 0 ||
                !IsFiniteNonNegative(collisionRadiusPixels))
                report.AddError($"{name}: Durability and collision radius values must be non-negative.");
            if (kind == MechanismKind.Obstacle &&
                (durability > 0 || collisionRadiusPixels > 0f) &&
                (durability <= 0 || collisionRadiusPixels <= 0f))
                report.AddError($"{name}: Obstacle durability and collision radius " +
                    "must either both be configured or both be zero.");
            if (!IsFiniteNonNegative(activationCooldownSeconds) ||
                !IsFiniteNonNegative(placementCooldownSeconds))
                report.AddError($"{name}: Cooldown values must be finite and non-negative.");
            if (playerPlaceable && placementPrefab == null)
                report.AddError($"{name}: Player-placeable mechanisms require a prefab.");
        }

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
    }
}
