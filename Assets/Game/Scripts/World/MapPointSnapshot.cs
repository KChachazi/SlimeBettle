using UnityEngine;

namespace SlimeBattle.World
{
    public readonly struct MapPointSnapshot
    {
        public MapPointSnapshot(MapPointId pointId, MapPointKind kind,
            Vector2 position, string configId)
        {
            PointId = pointId;
            Kind = kind;
            Position = position;
            ConfigId = configId ?? string.Empty;
        }

        public MapPointId PointId { get; }
        public MapPointKind Kind { get; }
        public Vector2 Position { get; }
        public string ConfigId { get; }
    }
}
