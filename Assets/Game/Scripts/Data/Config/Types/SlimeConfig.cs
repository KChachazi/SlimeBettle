/* Slime 个体静态配置；同屏数量上限属于 LevelConfig，不由个体配置重复维护。 */
using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/Slime", fileName = "CFG_Slime")]
    public sealed class SlimeConfig : ActorConfig
    {
    }
}
