using SlimeBattle.Actors;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Guards
{
    public readonly struct GuardSpawnRequest
    {
        public GuardSpawnRequest(ConfigId configId, Vector2 position,
            FactionId factionId = default(FactionId),
            Vector2 escortOffset = default(Vector2))
        {
            ConfigId = configId;
            Position = position;
            FactionId = factionId;
            EscortOffset = escortOffset;
        }

        public ConfigId ConfigId { get; }
        public Vector2 Position { get; }
        public FactionId FactionId { get; }
        public Vector2 EscortOffset { get; }
    }
}
