/* ActorRuntimeData 是单个 Actor 唯一可写的生命、状态与位置数据。 */
using System.Collections.Generic;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Actors
{
    public sealed class ActorRuntimeData
    {
        internal ActorRuntimeData(
            ActorId actorId,
            FactionId factionId,
            ActorType actorType,
            ConfigId configId,
            Vector2 position,
            float maxHealth)
        {
            ActorId = actorId;
            FactionId = factionId;
            ActorType = actorType;
            ConfigId = configId;
            IsAlive = true;
            Position = position;
            MaxHealth = maxHealth;
            CurrentHealth = maxHealth;
            StatusIds = new HashSet<string>();
        }

        public ActorId ActorId { get; }
        public FactionId FactionId { get; }
        public ActorType ActorType { get; }
        public ConfigId ConfigId { get; }
        public bool IsAlive { get; internal set; }
        public Vector2 Position { get; internal set; }
        public float MaxHealth { get; }
        public float CurrentHealth { get; internal set; }
        public ActorControlMode ControlMode { get; internal set; }
        internal HashSet<string> StatusIds { get; }

        public ActorSnapshot GetSnapshot()
        {
            return new ActorSnapshot(ActorId, FactionId, ActorType, IsAlive, Position,
                CurrentHealth, MaxHealth, ControlMode, StatusIds.Count == 0
                    ? System.Array.Empty<string>()
                    : new List<string>(StatusIds).AsReadOnly());
        }
    }
}
