/* ObjectiveConfig 保存目标与武器配置之间的静态引用。 */
using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/Objective", fileName = "CFG_Objective")]
    public sealed class ObjectiveConfig : ConfigAsset
    {
        [SerializeField]
        private string weaponConfigId;

        [SerializeField]
        private bool hasSuccessCondition;

        [SerializeField]
        private bool failWhenNoSlimes = true;

        public ConfigId WeaponConfigId => new ConfigId(weaponConfigId);

        public bool HasSuccessCondition => hasSuccessCondition;
        public bool FailWhenNoSlimes => failWhenNoSlimes;

        public void ConfigureWeapon(ConfigId newWeaponConfigId)
        {
            EnsureAuthoring();
            weaponConfigId = newWeaponConfigId.Value;
        }

        public void SetHasSuccessCondition(bool value)
        {
            EnsureAuthoring();
            hasSuccessCondition = value;
        }

        public void SetFailWhenNoSlimes(bool value)
        {
            EnsureAuthoring();
            failWhenNoSlimes = value;
        }

        protected override void ValidateSpecific(ConfigValidationReport report)
        {
            if (!WeaponConfigId.IsValid)
            {
                report.AddError($"{name}: WeaponConfigId is empty.");
            }
        }
    }
}
