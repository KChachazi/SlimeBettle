/* 敌人个体公开快照；生命与状态列表来自 ActorCore 的复制。 */
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Enemies
{
    public readonly struct EnemySnapshot
    {
        public EnemySnapshot(ActorSnapshot actor, ConfigId configId,
            float currentHealth, float maxHealth, EnemyControlMode controlMode,
            EnemyTargetPolicy targetPolicy, EnemyActivityBounds activityBounds)
        {
            ActorId = actor.ActorId;
            FactionId = actor.FactionId;
            Position = actor.Position;
            IsAlive = actor.IsAlive;
            ConfigId = configId;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            ControlMode = controlMode;
            TargetPolicy = targetPolicy;
            ActivityBounds = activityBounds;
            StatusIds = actor.StatusIds;
        }
        public ActorId ActorId { get; }
        public FactionId FactionId { get; }
        public Vector2 Position { get; }
        public bool IsAlive { get; }
        public ConfigId ConfigId { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public EnemyControlMode ControlMode { get; }
        public EnemyTargetPolicy TargetPolicy { get; }
        public EnemyActivityBounds ActivityBounds { get; }
        public IReadOnlyList<string> StatusIds { get; }
    }
}
