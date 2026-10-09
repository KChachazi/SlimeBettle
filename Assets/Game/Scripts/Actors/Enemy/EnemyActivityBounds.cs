/* 敌人的活动范围事实，不修改地图数据。未指定范围时不额外限制。 */
using UnityEngine;

namespace SlimeBattle.Enemies
{
    public readonly struct EnemyActivityBounds
    {
        public EnemyActivityBounds(Vector2 minimum, Vector2 maximum)
        {
            Minimum = Vector2.Min(minimum, maximum);
            Maximum = Vector2.Max(minimum, maximum);
            IsDefined = true;
        }
        public Vector2 Minimum { get; }
        public Vector2 Maximum { get; }
        public bool IsDefined { get; }
        public bool Contains(Vector2 position)
            => !IsDefined || position.x >= Minimum.x && position.x <= Maximum.x &&
                position.y >= Minimum.y && position.y <= Maximum.y;
    }
}
