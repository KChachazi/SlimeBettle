/*
 * 文件总述：Objective 的公开 ID、结果、快照和事件类型。
 * 结果判定逻辑集中在 ObjectiveSystem，避免值类型承担业务流程。
 */
using System;
using SlimeBattle.Actors;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Objectives
{
    public readonly struct ObjectiveId : IEquatable<ObjectiveId>
    {
        public ObjectiveId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public bool Equals(ObjectiveId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ObjectiveId && Equals((ObjectiveId)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(ObjectiveId left, ObjectiveId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ObjectiveId left, ObjectiveId right)
        {
            return !left.Equals(right);
        }
    }

    public readonly struct WeaponId : IEquatable<WeaponId>
    {
        public WeaponId(string value)
        {
            Value = value == null ? string.Empty : value.Trim();
        }

        public string Value { get; }
        public bool IsValid => !string.IsNullOrEmpty(Value);

        public static WeaponId NewId()
        {
            return new WeaponId(Guid.NewGuid().ToString("N"));
        }

        public bool Equals(WeaponId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is WeaponId && Equals((WeaponId)obj);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        }

        public override string ToString()
        {
            return Value ?? string.Empty;
        }

        public static bool operator ==(WeaponId left, WeaponId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WeaponId left, WeaponId right)
        {
            return !left.Equals(right);
        }
    }

    public enum ObjectivePhase
    {
        Pending,
        Succeeded,
        Failed
    }

    public enum ObjectiveResultReason
    {
        None,
        AwaitingSuccessCondition,
        AllSlimesDead,
        WeaponDestroyed,
        InvalidConfiguration
    }

    public readonly struct ObjectiveResult
    {
        public ObjectiveResult(ObjectivePhase phase, ObjectiveResultReason reason)
        {
            Phase = phase;
            Reason = reason;
        }

        public ObjectivePhase Phase { get; }
        public ObjectiveResultReason Reason { get; }
        public bool IsFinal => Phase != ObjectivePhase.Pending;
        public bool Succeeded => Phase == ObjectivePhase.Succeeded;
        public bool Failed => Phase == ObjectivePhase.Failed;

        public static ObjectiveResult Pending(ObjectiveResultReason reason)
        {
            return new ObjectiveResult(ObjectivePhase.Pending, reason);
        }
    }

    public readonly struct ObjectiveSnapshot
    {
        public ObjectiveSnapshot(
            ObjectiveId objectiveId,
            ObjectivePhase phase,
            bool isSettled,
            ObjectiveResultReason resultReason,
            WeaponId weaponId)
        {
            ObjectiveId = objectiveId;
            Phase = phase;
            IsSettled = isSettled;
            ResultReason = resultReason;
            WeaponId = weaponId;
        }

        public ObjectiveId ObjectiveId { get; }
        public ObjectivePhase Phase { get; }
        public bool IsSettled { get; }
        public ObjectiveResultReason ResultReason { get; }
        public WeaponId WeaponId { get; }
    }

    public readonly struct WeaponSnapshot
    {
        public WeaponSnapshot(
            WeaponId weaponId,
            ConfigId configId,
            float maxHealth,
            float currentHealth,
            Vector2 position,
            bool isDestroyed)
        {
            WeaponId = weaponId;
            ConfigId = configId;
            MaxHealth = maxHealth;
            CurrentHealth = currentHealth;
            Position = position;
            IsDestroyed = isDestroyed;
        }

        public WeaponId WeaponId { get; }
        public ConfigId ConfigId { get; }
        public float MaxHealth { get; }
        public float CurrentHealth { get; }
        public Vector2 Position { get; }
        public bool IsDestroyed { get; }
    }

    public readonly struct ObjectiveStateChangedEvent
    {
        public ObjectiveStateChangedEvent(ObjectiveSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public ObjectiveSnapshot Snapshot { get; }
    }

    public readonly struct WeaponDamagedEvent
    {
        public WeaponDamagedEvent(WeaponSnapshot snapshot, ResolvedEffect effect)
        {
            Snapshot = snapshot;
            Effect = effect;
        }

        public WeaponSnapshot Snapshot { get; }
        public ResolvedEffect Effect { get; }
    }

    public readonly struct WeaponDestroyedEvent
    {
        public WeaponDestroyedEvent(WeaponSnapshot snapshot)
        {
            Snapshot = snapshot;
        }

        public WeaponSnapshot Snapshot { get; }
    }

    public readonly struct ObjectiveSucceededEvent
    {
        public ObjectiveSucceededEvent(ObjectiveResult result, ObjectiveSnapshot snapshot)
        {
            Result = result;
            Snapshot = snapshot;
        }

        public ObjectiveResult Result { get; }
        public ObjectiveSnapshot Snapshot { get; }
    }

    public readonly struct ObjectiveFailedEvent
    {
        public ObjectiveFailedEvent(ObjectiveResult result, ObjectiveSnapshot snapshot)
        {
            Result = result;
            Snapshot = snapshot;
        }

        public ObjectiveResult Result { get; }
        public ObjectiveSnapshot Snapshot { get; }
    }
}
