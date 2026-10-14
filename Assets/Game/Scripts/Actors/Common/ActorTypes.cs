/* Actor 的共享枚举；数据快照、效果结果和接收契约分别放在同名文件。 */

namespace SlimeBattle.Actors
{
    public enum ActorType
    {
        Unknown,
        Slime,
        Enemy,
        Guard,
        EscortWeapon
    }

    public enum ActorControlMode
    {
        Autonomous,
        Paused,
        Controlled,
        Dead
    }

    public enum DespawnReason
    {
        Unknown,
        Removed,
        LevelRestart,
        LevelUnload,
        Replaced
    }

    public enum EffectType
    {
        Unknown,
        Damage,
        Heal,
        Status,
        Control
    }

}
