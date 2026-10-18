/* 敌人专属的目标和活动范围数据；生命与状态保存在 ActorCore。 */
using SlimeBattle.Actors;

namespace SlimeBattle.Actors.Enemies
{
    public sealed class EnemyRuntimeData
    {
        public ActorId TargetId { get; internal set; }
        // 当前决策是否把目标置于攻击范围；攻击间隔冷却不应让守卫丢失防守事实。
        public bool IsAttacking { get; internal set; }
        public SlimeBattle.Enemies.EnemyTargetPolicy TargetPolicy { get; internal set; }
        public SlimeBattle.Enemies.EnemyActivityBounds ActivityBounds { get; internal set; }
    }
}
