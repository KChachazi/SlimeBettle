/* 每个场景固定一个前置条件，避免按钮切换时把上一次行动带进下一次验收。 */
namespace SlimeBattle.Tests.AIBranches
{
    public enum AIBranchScenario
    {
        SlimeWait,
        SlimeChase,
        SlimeAttack,
        SlimeHungry,
        SlimeCold,
        SlimeAfraid,
        SlimeFleeBelow50,
        SlimeFleeBelow30,
        SlimeFightAfterFailedFlee,
        SlimeAgitatedHuman,
        EnemyWander,
        EnemyChase,
        EnemyAttack,
        EnemyOutsideActivityBounds
    }
}
