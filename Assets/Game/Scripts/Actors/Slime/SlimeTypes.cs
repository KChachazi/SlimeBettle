/* Slime 领域枚举；生成请求、快照和结果事件放在各自的文件。 */

namespace SlimeBattle.Slimes
{
    public enum SlimeControlMode
    {
        Autonomous,
        Paused,
        Controlled,
        Dead
    }

    public enum SlimeNeedState
    {
        None,
        Hungry,
        Cold,
        Hot,
        Afraid,
        Agitated
    }

}
