using SlimeBattle.Actors;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Guards
{
    public readonly struct GuardSpawnRequest
    {
        public GuardSpawnRequest(ConfigId configId, Vector2 position,
            FactionId factionId = default(FactionId))
        { ConfigId = configId; Position = position; FactionId = factionId; }

        public ConfigId ConfigId { get; }
        public Vector2 Position { get; }
        public FactionId FactionId { get; }
    }
}
