/* 敌人生成请求：P0 不包含波次或 Boss 规则。 */
using SlimeBattle.Actors;
using SlimeBattle.Data;
using UnityEngine;

namespace SlimeBattle.Enemies
{
    public readonly struct EnemySpawnRequest
    {
        public EnemySpawnRequest(ConfigId configId, Vector2 position,
            FactionId factionId = default(FactionId),
            EnemyTargetPolicy targetPolicy = EnemyTargetPolicy.NearestHostile,
            EnemyActivityBounds activityBounds = default(EnemyActivityBounds))
        {
            ConfigId = configId;
            Position = position;
            FactionId = factionId;
            TargetPolicy = targetPolicy;
            ActivityBounds = activityBounds;
        }
        public ConfigId ConfigId { get; }
        public Vector2 Position { get; }
        public FactionId FactionId { get; }
        public EnemyTargetPolicy TargetPolicy { get; }
        public EnemyActivityBounds ActivityBounds { get; }
    }
}
