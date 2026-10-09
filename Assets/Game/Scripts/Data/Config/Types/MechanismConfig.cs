/* P0 只保留 MechanismConfig 的配置入口，具体机制规则暂未实现。 */
using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/Mechanism", fileName = "CFG_Mechanism")]
    public sealed class MechanismConfig : ConfigAsset
    {
    }
}
