namespace SlimeBattle.World
{
    public readonly struct MapAreaSnapshot
    {
        public MapAreaSnapshot(MapAreaId areaId, MapAreaKind kind,
            MapRoadKind roadKind, MapTerrainKind terrainKind,
            MapRegionKind regionKind, float movementSpeedMultiplier,
            string statusEffectConfigId, float guidanceRadiusPixels = 0f)
        {
            AreaId = areaId;
            Kind = kind;
            RoadKind = roadKind;
            TerrainKind = terrainKind;
            RegionKind = regionKind;
            MovementSpeedMultiplier = movementSpeedMultiplier;
            StatusEffectConfigId = statusEffectConfigId ?? string.Empty;
            GuidanceRadiusPixels = guidanceRadiusPixels;
        }

        public MapAreaId AreaId { get; }
        public MapAreaKind Kind { get; }
        public MapRoadKind RoadKind { get; }
        public MapTerrainKind TerrainKind { get; }
        public MapRegionKind RegionKind { get; }
        public float MovementSpeedMultiplier { get; }
        public string StatusEffectConfigId { get; }
        public float GuidanceRadiusPixels { get; }
    }
}
