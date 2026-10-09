/* 值快照不暴露 ActorRuntimeData；状态列表由 ActorRuntimeData 复制。 */
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBattle.Actors
{
    public readonly struct ActorSnapshot
    {
        public ActorSnapshot(ActorId actorId, FactionId factionId,
            ActorType actorType, bool isAlive, Vector2 position,
            float currentHealth = 1f, float maxHealth = 1f,
            ActorControlMode controlMode = ActorControlMode.Autonomous,
            IReadOnlyList<string> statusIds = null)
        {
            ActorId = actorId;
            FactionId = factionId;
            ActorType = actorType;
            IsAlive = isAlive;
            Position = position;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            ControlMode = controlMode;
            StatusIds = statusIds ?? Array.Empty<string>();
        }
        public ActorId ActorId { get; }
        public FactionId FactionId { get; }
        public ActorType ActorType { get; }
        public bool IsAlive { get; }
        public Vector2 Position { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public ActorControlMode ControlMode { get; }
        public IReadOnlyList<string> StatusIds { get; }
    }
}
