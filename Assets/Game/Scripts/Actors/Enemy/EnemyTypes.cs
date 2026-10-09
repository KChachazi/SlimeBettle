/* Enemy 领域枚举；P0 实际只支持 NearestHostile。 */

namespace SlimeBattle.Enemies
{
    public enum EnemyControlMode
    {
        Autonomous,
        Paused,
        Controlled,
        Dead
    }

    public enum EnemyTargetPolicy
    {
        NearestHostile,
        SlimeFirst,
        WeaponFirst
    }

}
