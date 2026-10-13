/* 外部只读的单只史莱姆快照；不暴露可写 RuntimeData。 */
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Slimes
{
    public readonly struct SlimeSnapshot
    {
        public SlimeSnapshot(ActorSnapshot actor, ConfigId configId,
            float currentHealth, float maxHealth, SlimeControlMode controlMode,
            SlimeNeedState needState, bool isCarrying = false,
            float pixelsPerUnit = 0f)
        {
            ActorId = actor.ActorId;
            FactionId = actor.FactionId;
            Position = actor.Position;
            IsAlive = actor.IsAlive;
            ConfigId = configId;
            CurrentHealth = currentHealth;
            MaxHealth = maxHealth;
            ControlMode = controlMode;
            NeedState = needState;
            StatusIds = actor.StatusIds;
            IsCarrying = isCarrying;
            PixelsPerUnit = pixelsPerUnit;
        }
        public ActorId ActorId { get; }
        public FactionId FactionId { get; }
        public Vector2 Position { get; }
        public bool IsAlive { get; }
        public ConfigId ConfigId { get; }
        public float CurrentHealth { get; }
        public float MaxHealth { get; }
        public SlimeControlMode ControlMode { get; }
        public SlimeNeedState NeedState { get; }
        public IReadOnlyList<string> StatusIds { get; }
        public bool IsCarrying { get; }
        public float PixelsPerUnit { get; }
    }
}
