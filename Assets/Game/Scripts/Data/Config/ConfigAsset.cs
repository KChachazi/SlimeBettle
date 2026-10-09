/*
 * 文件总述：Unity 编辑器中的配置资产基类。
 * ConfigAsset 只保存设计值；运行态由 Actor 和 ObjectiveSystem 持有。
 */
using UnityEngine;

namespace SlimeBattle.Data
{
    public abstract class ConfigAsset : ScriptableObject
    {
        [SerializeField]
        private string configId;

        public ConfigId ConfigId => new ConfigId(configId);

        public string ConfigIdValue => configId ?? string.Empty;

        // Authoring helper. Runtime systems never call this method.
        public void SetConfigId(string value)
        {
            EnsureAuthoring();
            configId = value == null ? string.Empty : value.Trim();
        }

        protected static void EnsureAuthoring()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException("Config assets are read-only during play.");
        }

        internal void AddValidationErrors(ConfigValidationReport report)
        {
            if (!ConfigId.IsValid)
            {
                report.AddError($"{name}: ConfigId is empty.");
            }

            ValidateSpecific(report);
        }

        protected virtual void ValidateSpecific(ConfigValidationReport report)
        {
        }
    }
}
