/*
 * 文件总述：ActorCore 是所有角色共用的纯 C# 状态核心。
 * 它不创建 GameObject；只接收 StatusManager 已结算的效果和 MovementSystem 的位置结果。
 */
using System;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Actors
{
    public sealed class ActorCore
    {
        public ActorCore(
            ActorId actorId,
            FactionId factionId,
            ActorType actorType,
            Vector2 position)
            : this(actorId, factionId, actorType, default(ConfigId), position, 1f)
        {
        }

        public ActorCore(
            ActorId actorId,
            FactionId factionId,
            ActorType actorType,
            ConfigId configId,
            Vector2 position,
            float maxHealth = 1f)
        {
            if (!actorId.IsValid)
            {
                throw new ArgumentException("A valid actor ID is required.", nameof(actorId));
            }

            if (maxHealth <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth));
            }

            RuntimeData = new ActorRuntimeData(
                actorId,
                factionId,
                actorType,
                configId,
                position,
                maxHealth);
        }

        public ActorRuntimeData RuntimeData { get; }

        public ActorId GetActorId()
        {
            return RuntimeData.ActorId;
        }

        public ActorType GetActorType()
        {
            return RuntimeData.ActorType;
        }

        public FactionId GetFactionId()
        {
            return RuntimeData.FactionId;
        }

        public bool IsAlive()
        {
            return RuntimeData.IsAlive;
        }

        public ActorSnapshot GetSnapshot()
        {
            return RuntimeData.GetSnapshot();
        }

        // 只允许效果结算适配层调用；此处不计算攻击力、抗性或命中。
        public EffectApplyResult ApplyResolvedEffect(ResolvedEffect effect)
        {
            if (!RuntimeData.IsAlive)
            {
                return EffectApplyResult.Rejected("Actor is dead.");
            }

            if (effect.IsDamage || effect.IsHeal)
            {
                if (effect.Amount <= 0f || float.IsNaN(effect.Amount) || float.IsInfinity(effect.Amount))
                {
                    return EffectApplyResult.Rejected("Resolved health amount is invalid.");
                }

                var before = RuntimeData.CurrentHealth;
                RuntimeData.CurrentHealth = effect.IsDamage
                    ? Math.Max(0f, before - effect.Amount)
                    : Math.Min(RuntimeData.MaxHealth, before + effect.Amount);
                if (RuntimeData.CurrentHealth == 0f)
                {
                    RuntimeData.IsAlive = false;
                    RuntimeData.ControlMode = ActorControlMode.Dead;
                }

                return EffectApplyResult.Accepted(
                    Math.Abs(RuntimeData.CurrentHealth - before),
                    RuntimeData.CurrentHealth,
                    !RuntimeData.IsAlive);
            }

            if (effect.Type == EffectType.Status && !string.IsNullOrEmpty(effect.StatusId))
            {
                return EffectApplyResult.AcceptedStatus(
                    RuntimeData.StatusIds.Add(effect.StatusId), RuntimeData.CurrentHealth);
            }

            return EffectApplyResult.Rejected("Unsupported resolved effect.");
        }

        public void SetControlMode(ActorControlMode mode)
        {
            if (RuntimeData.IsAlive)
            {
                RuntimeData.ControlMode = mode;
            }
        }

        public bool RemoveResolvedStatus(string statusId)
        {
            return !string.IsNullOrEmpty(statusId) &&
                RuntimeData.StatusIds.Remove(statusId);
        }

        public void SetMaxHealthMultiplier(float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0f)
                throw new ArgumentOutOfRangeException(nameof(multiplier));

            RuntimeData.MaxHealth = RuntimeData.BaseMaxHealth * multiplier;
            RuntimeData.CurrentHealth = Math.Min(RuntimeData.CurrentHealth, RuntimeData.MaxHealth);
        }

        internal void SetPosition(Vector2 position)
        {
            RuntimeData.Position = position;
        }
    }
}
