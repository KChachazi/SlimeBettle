/* LevelConfig 保存关卡设计值；同屏史莱姆上限默认 30，当前允许配置至 50。 */
using SlimeBattle.Core;
using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/Level", fileName = "CFG_Level")]
    public sealed class LevelConfig : ConfigAsset
    {
        [SerializeField]
        private int maxAliveSlimes = 30;

        [SerializeField]
        private string objectiveConfigId;

        [SerializeField, Range(0, 5)]
        private int maximumActivePlacedMechanisms = 5;

        public LevelId LevelId => new LevelId(ConfigIdValue);

        public int MaxAliveSlimes => maxAliveSlimes;
        public ConfigId ObjectiveConfigId => new ConfigId(objectiveConfigId);
        public int MaximumActivePlacedMechanisms => maximumActivePlacedMechanisms;

        public void SetMaxAliveSlimes(int value)
        {
            EnsureAuthoring();
            maxAliveSlimes = value;
        }

        public void SetObjectiveConfigId(ConfigId value)
        {
            EnsureAuthoring();
            objectiveConfigId = value.Value;
        }

        public void SetMaximumActivePlacedMechanisms(int value)
        {
            EnsureAuthoring();
            maximumActivePlacedMechanisms = value;
        }

        protected override void ValidateSpecific(ConfigValidationReport report)
        {
            if (maxAliveSlimes < 1 || maxAliveSlimes > 50)
            {
                report.AddError($"{name}: MaxAliveSlimes must be between 1 and 50.");
            }
            if (!ObjectiveConfigId.IsValid)
            {
                report.AddError($"{name}: ObjectiveConfigId is empty.");
            }
            if (maximumActivePlacedMechanisms < 0 || maximumActivePlacedMechanisms > 5)
            {
                report.AddError($"{name}: MaximumActivePlacedMechanisms must be between 0 and 5.");
            }
        }
    }
}
