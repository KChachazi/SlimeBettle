/*
 * 文件总述：Objective 的运行态和结果判定入口。
 * - P0 默认实现 Slime 数量为 0、Weapon 被摧毁两个失败条件；
 *   单关卡可通过 ObjectiveConfig 关闭前者，Level_test 只按护送对象生命判负。
 * - 成功条件尚未明确，因此未猜测正式规则，未满足失败时保持 Pending。
 * - 不直接改动 GameRoot、LevelManager 或 LevelContext。
 */
using System;
using SlimeBattle.Actors;
using SlimeBattle.Core;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Objectives
{
    public sealed class ObjectiveSystem : IDisposable
    {
        private readonly ConfigRegistry configRegistry;
        private readonly ILevelEventBus eventBus;
        private ObjectiveRuntimeData runtimeData;
        private int aliveSlimeCount = -1;
        private bool failWhenNoSlimes = true;
        private bool disposed;

        public ObjectiveSystem(
            ConfigRegistry configRegistry = null,
            ILevelEventBus eventBus = null)
        {
            this.configRegistry = configRegistry;
            this.eventBus = eventBus;
        }

        public ObjectiveSystem(
            ObjectiveId objectiveId,
            WeaponId weaponId,
            int maxWeaponHealth,
            ILevelEventBus eventBus = null)
            : this(null, eventBus)
        {
            Initialize(objectiveId, weaponId, maxWeaponHealth, false);
        }

        public event Action<ObjectiveFailedEvent> Failed;

        public bool Initialize(
            ObjectiveConfig objectiveConfig,
            WeaponConfig weaponConfig,
            WeaponId weaponId,
            Vector2 weaponPosition = default(Vector2))
        {
            if (objectiveConfig == null || weaponConfig == null ||
                objectiveConfig.WeaponConfigId != weaponConfig.ConfigId)
            {
                return false;
            }

            return Initialize(
                new ObjectiveId(objectiveConfig.ConfigId.Value),
                weaponId,
                weaponConfig.MaxHealth,
                objectiveConfig.HasSuccessCondition,
                weaponConfig.ConfigId,
                weaponPosition,
                objectiveConfig.FailWhenNoSlimes);
        }

        public bool Initialize(
            ObjectiveId objectiveId,
            WeaponId weaponId,
            int maxWeaponHealth,
            bool hasSuccessCondition = false)
        {
            return Initialize(
                objectiveId,
                weaponId,
                maxWeaponHealth,
                hasSuccessCondition,
                new ConfigId(weaponId.Value),
                Vector2.zero);
        }

        public bool InitializeFromRegistry(
            ObjectiveId objectiveId,
            WeaponId weaponId,
            Vector2 weaponPosition = default(Vector2))
        {
            if (configRegistry == null ||
                !configRegistry.TryGetObjective(objectiveId, out var objectiveConfig) ||
                !configRegistry.TryGetWeapon(objectiveConfig.WeaponConfigId, out var weaponConfig))
            {
                return false;
            }

            return Initialize(objectiveConfig, weaponConfig, weaponId, weaponPosition);
        }

        public bool TryGetObjectiveSnapshot(
            ObjectiveId objectiveId,
            out ObjectiveSnapshot snapshot)
        {
            snapshot = default(ObjectiveSnapshot);
            return !disposed &&
                runtimeData != null &&
                runtimeData.ObjectiveId == objectiveId &&
                (snapshot = runtimeData.GetSnapshot()).ObjectiveId.IsValid;
        }

        public bool TryGetWeaponSnapshot(
            WeaponId weaponId,
            out WeaponSnapshot snapshot)
        {
            snapshot = default(WeaponSnapshot);
            return !disposed &&
                runtimeData != null &&
                runtimeData.Weapon.WeaponId == weaponId &&
                (snapshot = runtimeData.Weapon.GetSnapshot()).WeaponId.IsValid;
        }

        public EffectApplyResult ApplyWeaponEffect(WeaponId weaponId, ResolvedEffect effect)
        {
            if (disposed || runtimeData == null || runtimeData.Weapon.WeaponId != weaponId)
            {
                return EffectApplyResult.Rejected("The weapon was not found.");
            }

            if (runtimeData.IsSettled)
            {
                return EffectApplyResult.Rejected("The objective has already been settled.");
            }

            if (runtimeData.Weapon.IsDestroyed)
            {
                return EffectApplyResult.Rejected("The weapon is already destroyed.");
            }

            if ((!effect.IsDamage && !effect.IsHeal) || effect.Amount <= 0f)
            {
                return EffectApplyResult.Rejected("Only positive damage or healing effects are supported.");
            }

            if (effect.IsDamage)
            {
                var weapon = runtimeData.Weapon;
                var appliedAmount = Math.Min(weapon.CurrentHealth, effect.Amount);
                weapon.CurrentHealth -= appliedAmount;
                eventBus?.Publish(new WeaponDamagedEvent(weapon.GetSnapshot(), effect));

                if (weapon.IsDestroyed)
                {
                    eventBus?.Publish(new WeaponDestroyedEvent(weapon.GetSnapshot()));
                }

                var result = EvaluateResult();
                if (!result.IsFinal)
                {
                    eventBus?.Publish(new ObjectiveStateChangedEvent(runtimeData.GetSnapshot()));
                }
                return EffectApplyResult.Accepted(
                    appliedAmount,
                    weapon.CurrentHealth,
                    weapon.IsDestroyed);
            }

            var previousHealth = runtimeData.Weapon.CurrentHealth;
            runtimeData.Weapon.CurrentHealth = Math.Min(
                runtimeData.Weapon.MaxHealth,
                runtimeData.Weapon.CurrentHealth + effect.Amount);
            eventBus?.Publish(new ObjectiveStateChangedEvent(runtimeData.GetSnapshot()));
            return EffectApplyResult.Accepted(
                runtimeData.Weapon.CurrentHealth - previousHealth,
                runtimeData.Weapon.CurrentHealth);
        }

        public void NotifySlimeCountChanged(int aliveCount)
        {
            if (disposed || runtimeData == null || runtimeData.IsSettled)
            {
                return;
            }

            aliveSlimeCount = Math.Max(0, aliveCount);
            EvaluateResult();
        }

        public ObjectiveResult EvaluateResult()
        {
            if (disposed || runtimeData == null)
            {
                return ObjectiveResult.Pending(ObjectiveResultReason.InvalidConfiguration);
            }

            if (runtimeData.IsSettled)
            {
                return new ObjectiveResult(runtimeData.Phase, runtimeData.ResultReason);
            }

            if (failWhenNoSlimes && aliveSlimeCount == 0)
            {
                return ConfirmFailure(ObjectiveResultReason.AllSlimesDead);
            }

            if (runtimeData.Weapon.IsDestroyed)
            {
                return ConfirmFailure(ObjectiveResultReason.WeaponDestroyed);
            }

            // The final success rule is not defined in the current design docs.
            return ObjectiveResult.Pending(ObjectiveResultReason.AwaitingSuccessCondition);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            runtimeData = null;
            Failed = null;
            disposed = true;
        }

        private bool Initialize(
            ObjectiveId objectiveId,
            WeaponId weaponId,
            int maxWeaponHealth,
            bool hasSuccessCondition,
            ConfigId weaponConfigId,
            Vector2 weaponPosition,
            bool shouldFailWhenNoSlimes = true)
        {
            if (disposed || runtimeData != null || !objectiveId.IsValid ||
                !weaponId.IsValid || maxWeaponHealth <= 0)
            {
                return false;
            }

            var weapon = new WeaponRuntimeData(
                weaponId,
                weaponConfigId,
                maxWeaponHealth,
                weaponPosition);
            runtimeData = new ObjectiveRuntimeData(
                objectiveId,
                weapon,
                hasSuccessCondition);
            failWhenNoSlimes = shouldFailWhenNoSlimes;
            aliveSlimeCount = -1;
            return true;
        }

        private ObjectiveResult ConfirmFailure(ObjectiveResultReason reason)
        {
            runtimeData.Phase = ObjectivePhase.Failed;
            runtimeData.ResultReason = reason;
            var result = new ObjectiveResult(runtimeData.Phase, reason);
            var snapshot = runtimeData.GetSnapshot();
            var eventData = new ObjectiveFailedEvent(result, snapshot);
            Failed?.Invoke(eventData);
            eventBus?.Publish(eventData);
            eventBus?.Publish(new ObjectiveStateChangedEvent(snapshot));
            return result;
        }
    }
}
