using SlimeBattle.Data;
using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    [DisallowMultipleComponent]
    public sealed class EscortWeaponAuthoring : MonoBehaviour
    {
        [SerializeField] private string weaponConfigId;
        [SerializeField] private string routeId;

        public ConfigId WeaponConfigId => new ConfigId(weaponConfigId);
        public MapRouteId RouteId => new MapRouteId(routeId);
    }
}
