/* 敌人个体公开快照；生命与状态列表来自 ActorCore 的复制。 */
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Enemies
{
    public readonly struct EnemySnapshot
    {
        public EnemySnapshot(ActorSnapshot actor, ConfigId configId,
            float currentHealth, float maxHealth, EnemyControlMode controlMode,
            EnemyTargetPolicy targetPolicy, EnemyActivityBounds activityBounds,
            ActorId targetActorId = default(ActorId),
            StateId actionState = StateId.Idle, bool isAttacking = false)
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
            TargetActorId = targetActorId;
            ActionState = actionState;
            IsAttacking = isAttacking;
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
        public ActorId TargetActorId { get; }
        public StateId ActionState { get; }
        public bool IsAttacking { get; }
        public IReadOnlyList<string> StatusIds { get; }
    }
}
