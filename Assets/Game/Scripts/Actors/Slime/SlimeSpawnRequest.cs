/* 史莱姆生成请求携带配置、出生位置和可选阵营。 */
using SlimeBattle.Actors;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Slimes
{
    public readonly struct SlimeSpawnRequest
    {
        public SlimeSpawnRequest(ConfigId configId, Vector2 position,
            FactionId factionId = default(FactionId))
        {
            ConfigId = configId;
            Position = position;
            FactionId = factionId;
        }
        public ConfigId ConfigId { get; }
        public Vector2 Position { get; }
        public FactionId FactionId { get; }
    }
}
