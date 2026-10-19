using SlimeBattle.Data;
using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    [DisallowMultipleComponent]
    public sealed class EscortWeaponAuthoring : MonoBehaviour
    {
        public const int CenterAnchorIndex = 0;
        public const int BottomRightAnchorIndex = 1;
        public const int TopRightAnchorIndex = 2;
        public const int BottomLeftAnchorIndex = 3;

        [SerializeField] private string weaponConfigId;
        [SerializeField] private string routeId;
        [SerializeField, Tooltip("Ordered Center, BottomRight, TopRight, BottomLeft.")]
        private Transform[] carrierAnchors = new Transform[EscortWeapon.MaximumCarrierCount];

        public ConfigId WeaponConfigId => new ConfigId(weaponConfigId);
        public MapRouteId RouteId => new MapRouteId(routeId);

        public bool HasValidCarrierAnchors
        {
            get
            {
                if (carrierAnchors == null ||
                    carrierAnchors.Length != EscortWeapon.MaximumCarrierCount) return false;
                for (var i = 0; i < carrierAnchors.Length; i++)
                    if (carrierAnchors[i] == null || carrierAnchors[i].parent != transform)
                        return false;
                return true;
            }
        }

        public bool TryGetCarrierAnchor(int index, out Transform anchor)
        {
            anchor = null;
            if (!HasValidCarrierAnchors || index < 0 || index >= carrierAnchors.Length)
                return false;
            anchor = carrierAnchors[index];
            return true;
        }
    }
}
