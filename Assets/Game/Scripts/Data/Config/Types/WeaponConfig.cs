/* WeaponConfig 保存目标武器的最小生命值配置。 */
using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/Weapon", fileName = "CFG_Weapon")]
    public sealed class WeaponConfig : ConfigAsset
    {
        [SerializeField]
        private int maxHealth = 1;

        public int MaxHealth => maxHealth;

        public void SetMaxHealth(int value)
        {
            EnsureAuthoring();
            maxHealth = value;
        }

        protected override void ValidateSpecific(ConfigValidationReport report)
        {
            if (maxHealth <= 0)
            {
                report.AddError($"{name}: MaxHealth must be greater than zero.");
            }
        }
    }
}
