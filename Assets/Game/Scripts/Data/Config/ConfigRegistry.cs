/*
 * 文件总述：
 * - 将 ConfigAsset 建立为按 ConfigId 查询的内存索引。
 * - 重复 ID 会被保留并报告，而不是静默覆盖，便于定位策划配置错误。
 * - Registry 只负责查找和校验，不负责创建配置资产或修改业务运行态。
 */
using System;
using System.Collections.Generic;
using SlimeBattle.Core;
using SlimeBattle.Status;

namespace SlimeBattle.Data
{
    // 业务系统通过公开 TryGet* 查询配置，避免跨系统直接访问配置集合内部结构。
    public sealed class ConfigRegistry
    {
        private readonly List<ConfigAsset> configs = new List<ConfigAsset>();
        private readonly Dictionary<ConfigId, List<ConfigAsset>> configsById =
            new Dictionary<ConfigId, List<ConfigAsset>>();
        private bool frozen;

        public ConfigRegistry()
        {
        }

        public ConfigRegistry(IEnumerable<ConfigAsset> initialConfigs)
        {
            SetConfigs(initialConfigs);
        }

        public IReadOnlyList<ConfigAsset> Configs => configs.AsReadOnly();

        public void SetConfigs(IEnumerable<ConfigAsset> newConfigs)
        {
            if (frozen) throw new InvalidOperationException("ConfigRegistry is frozen.");
            configs.Clear();
            configsById.Clear();

            if (newConfigs == null)
            {
                return;
            }

            foreach (var config in newConfigs)
            {
                Register(config);
            }
        }

        public bool Register(ConfigAsset config)
        {
            if (frozen) throw new InvalidOperationException("ConfigRegistry is frozen.");
            if (config == null)
            {
                configs.Add(null);
                return false;
            }

            configs.Add(config);

            var id = config.ConfigId;
            if (!configsById.TryGetValue(id, out var matchingConfigs))
            {
                matchingConfigs = new List<ConfigAsset>();
                configsById.Add(id, matchingConfigs);
            }

            matchingConfigs.Add(config);
            return id.IsValid && matchingConfigs.Count == 1;
        }

        public bool TryGet<TConfig>(ConfigId configId, out TConfig config)
            where TConfig : ConfigAsset
        {
            config = null;

            if (!configId.IsValid ||
                !configsById.TryGetValue(configId, out var matchingConfigs) ||
                matchingConfigs.Count != 1)
            {
                return false;
            }

            config = matchingConfigs[0] as TConfig;
            return config != null;
        }

        public bool TryGetLevel(LevelId levelId, out LevelConfig config)
        {
            return TryGet(new ConfigId(levelId.Value), out config);
        }

        public bool TryGetActor(ConfigId configId, out ActorConfig config)
        {
            return TryGet(configId, out config);
        }

        public bool TryGetMechanism(ConfigId configId, out MechanismConfig config)
        {
            return TryGet(configId, out config);
        }

        public bool TryGetObjective(
            SlimeBattle.Objectives.ObjectiveId objectiveId,
            out ObjectiveConfig config)
        {
            return TryGet(new ConfigId(objectiveId.Value), out config);
        }

        public bool TryGetWeapon(ConfigId configId, out WeaponConfig config)
        {
            return TryGet(configId, out config);
        }

        public bool TryGetStatus(StatusId statusId, out StatusConfig config)
        {
            config = null;
            if (!statusId.IsValid) return false;

            foreach (var candidate in configs)
            {
                if (!(candidate is StatusConfig status) || status.StatusId != statusId)
                    continue;
                if (config != null)
                {
                    config = null;
                    return false;
                }
                config = status;
            }
            return config != null;
        }

        public bool TryGetStatus(StatusKind kind, out StatusConfig config)
        {
            config = null;
            if (kind == StatusKind.None || !Enum.IsDefined(typeof(StatusKind), kind)) return false;
            foreach (var candidate in configs)
            {
                if (!(candidate is StatusConfig status) || status.Kind != kind) continue;
                if (config != null)
                {
                    config = null;
                    return false;
                }
                config = status;
            }
            return config != null;
        }

        public ConfigValidationReport ValidateAll()
        {
            var report = new ConfigValidationReport();

            foreach (var config in configs)
            {
                if (config == null)
                {
                    report.AddError("A null config reference was registered.");
                    continue;
                }

                config.AddValidationErrors(report);
            }

            foreach (var pair in configsById)
            {
                if (pair.Key.IsValid && pair.Value.Count > 1)
                {
                    report.AddError(
                        $"ConfigId '{pair.Key.Value}' is registered {pair.Value.Count} times.");
                }
            }

            var statusIds = new HashSet<StatusId>();
            var statusKinds = new HashSet<StatusKind>();
            foreach (var config in configs)
            {
                if (!(config is StatusConfig status) || !status.StatusId.IsValid) continue;
                if (!statusIds.Add(status.StatusId))
                    report.AddError($"{status.name}: StatusId '{status.StatusId}' is registered more than once.");
                if (status.Kind != StatusKind.None && !statusKinds.Add(status.Kind))
                    report.AddError($"{status.name}: StatusKind '{status.Kind}' is registered more than once.");
            }

            foreach (var config in configs)
            {
                if (config is StatusConfig status && status.CounteractsStatusId.IsValid)
                {
                    if (status.CounteractsStatusId == status.StatusId)
                        report.AddError($"{status.name}: CounteractsStatusId cannot reference itself.");
                    else if (!TryGetStatus(status.CounteractsStatusId, out StatusConfig ignoredStatus))
                        report.AddError($"{status.name}: CounteractsStatusId does not resolve uniquely.");
                }
                if (config is StatusConfig statusConfig && statusConfig.OnHitStatusId.IsValid &&
                    !TryGetStatus(statusConfig.OnHitStatusId, out StatusConfig ignoredOnHitStatus))
                {
                    report.AddError($"{statusConfig.name}: OnHitStatusId does not resolve uniquely.");
                }
                if (config is LevelConfig level && level.ObjectiveConfigId.IsValid &&
                    !TryGet(level.ObjectiveConfigId, out ObjectiveConfig ignoredObjective))
                {
                    report.AddError($"{level.name}: ObjectiveConfigId does not resolve uniquely.");
                }
                if (config is ObjectiveConfig objective && objective.WeaponConfigId.IsValid &&
                    !TryGet(objective.WeaponConfigId, out WeaponConfig ignoredWeapon))
                {
                    report.AddError($"{objective.name}: WeaponConfigId does not resolve uniquely.");
                }
            }

            return report;
        }

        internal void Freeze() { frozen = true; }
    }
}
