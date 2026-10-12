using UnityEngine;

namespace SlimeBattle.World
{
    [DisallowMultipleComponent]
    public sealed class MapAreaAuthoring : MonoBehaviour
    {
        [SerializeField] private string areaId;
        [SerializeField] private MapAreaKind kind;
        [SerializeField] private MapRoadKind roadKind;
        [SerializeField] private MapTerrainKind terrainKind;
        [SerializeField] private MapRegionKind regionKind;
        [SerializeField, Min(0.01f)] private float movementSpeedMultiplier = 1f;
        [SerializeField] private string statusEffectConfigId;
        [SerializeField, Min(0f)] private float guidanceRadiusPixels = 500f;

        internal MapAreaId AreaId => new MapAreaId(areaId);
        internal MapAreaKind Kind => kind;
        internal Collider2D AreaCollider => GetComponent<Collider2D>();

        internal bool TryGetSnapshot(out MapAreaSnapshot snapshot, out string error)
        {
            snapshot = default(MapAreaSnapshot);
            if (!AreaId.IsValid)
            {
                error = "Area ID is empty.";
                return false;
            }

            if (AreaCollider == null || !AreaCollider.isTrigger)
            {
                error = "A trigger Collider2D is required to author a map area.";
                return false;
            }

            if (float.IsNaN(movementSpeedMultiplier) ||
                float.IsInfinity(movementSpeedMultiplier) || movementSpeedMultiplier <= 0f)
            {
                error = "Movement speed multiplier must be a finite positive value.";
                return false;
            }

            if (float.IsNaN(guidanceRadiusPixels) ||
                float.IsInfinity(guidanceRadiusPixels) || guidanceRadiusPixels < 0f)
            {
                error = "Guidance radius must be finite and non-negative.";
                return false;
            }

            if ((kind == MapAreaKind.Road && !System.Enum.IsDefined(typeof(MapRoadKind), roadKind)) ||
                (kind == MapAreaKind.Terrain &&
                    (!System.Enum.IsDefined(typeof(MapTerrainKind), terrainKind) || terrainKind == MapTerrainKind.None)) ||
                (kind == MapAreaKind.Region &&
                    (!System.Enum.IsDefined(typeof(MapRegionKind), regionKind) || regionKind == MapRegionKind.None)) ||
                !System.Enum.IsDefined(typeof(MapAreaKind), kind))
            {
                error = "The selected area kind requires a valid matching subtype.";
                return false;
            }

            snapshot = new MapAreaSnapshot(AreaId, kind, roadKind, terrainKind,
                regionKind, movementSpeedMultiplier, statusEffectConfigId,
                kind == MapAreaKind.Region && regionKind == MapRegionKind.Warm
                    ? guidanceRadiusPixels : 0f);
            error = string.Empty;
            return true;
        }
    }
}
