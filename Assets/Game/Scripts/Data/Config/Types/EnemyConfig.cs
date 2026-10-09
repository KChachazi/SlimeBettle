/* Enemy 当前复用 ActorConfig，后续可在这里增加 Enemy 专属静态字段。 */
using UnityEngine;

namespace SlimeBattle.Data
{
    [CreateAssetMenu(menuName = "SlimeBattle/Configs/Enemy", fileName = "CFG_Enemy")]
    public sealed class EnemyConfig : ActorConfig
    {
    }
}
