/* WeaponConfig 保存护送武器的生命、固定路线速度与碰撞半径。 */
using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/Weapon", fileName = "CFG_Weapon")]
    public sealed class WeaponConfig : ConfigAsset
    {
        [SerializeField]
        private int maxHealth = 1;
        [SerializeField, Min(0f)] private float transportSpeedWorldUnits = 1.5f;
        [SerializeField, Min(0f)] private float footprintRadiusWorldUnits = 0.4f;
        [SerializeField, Min(0f)] private float boardingRadiusWorldUnits = 0.6f;

        public int MaxHealth => maxHealth;
        public float TransportSpeedWorldUnits => transportSpeedWorldUnits;
        public float FootprintRadiusWorldUnits => footprintRadiusWorldUnits;
        public float BoardingRadiusWorldUnits => boardingRadiusWorldUnits;

        public void SetMaxHealth(int value)
        {
            EnsureAuthoring();
            maxHealth = value;
        }

        public void ConfigureTransport(float speedWorldUnits, float footprintRadiusWorldUnits)
        {
            EnsureAuthoring();
            transportSpeedWorldUnits = speedWorldUnits;
            this.footprintRadiusWorldUnits = footprintRadiusWorldUnits;
        }

        public void SetBoardingRadius(float radiusWorldUnits)
        {
            EnsureAuthoring();
            boardingRadiusWorldUnits = radiusWorldUnits;
        }

        protected override void ValidateSpecific(ConfigValidationReport report)
        {
            if (maxHealth <= 0)
            {
                report.AddError($"{name}: MaxHealth must be greater than zero.");
            }
            if (float.IsNaN(transportSpeedWorldUnits) ||
                float.IsInfinity(transportSpeedWorldUnits) || transportSpeedWorldUnits <= 0f)
                report.AddError($"{name}: TransportSpeedWorldUnits must be positive.");
            if (float.IsNaN(footprintRadiusWorldUnits) ||
                float.IsInfinity(footprintRadiusWorldUnits) || footprintRadiusWorldUnits < 0f)
                report.AddError($"{name}: FootprintRadiusWorldUnits must be non-negative.");
            if (float.IsNaN(boardingRadiusWorldUnits) ||
                float.IsInfinity(boardingRadiusWorldUnits) ||
                boardingRadiusWorldUnits <= footprintRadiusWorldUnits)
                report.AddError($"{name}: BoardingRadiusWorldUnits must exceed the footprint radius.");
        }
    }
}
