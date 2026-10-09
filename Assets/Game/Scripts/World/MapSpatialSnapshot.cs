using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlimeBattle.World
{
    public readonly struct MapSpatialSnapshot
    {
        private readonly IReadOnlyList<MapAreaSnapshot> areas;

        public MapSpatialSnapshot(Vector2 position, bool hasMapBounds,
            bool isWithinBounds, float movementSpeedMultiplier,
            IEnumerable<MapAreaSnapshot> areas)
        {
            Position = position;
            HasMapBounds = hasMapBounds;
            IsWithinBounds = isWithinBounds;
            MovementSpeedMultiplier = movementSpeedMultiplier;
            var areaArray = areas == null ? new MapAreaSnapshot[0] : areas.ToArray();
            this.areas = Array.AsReadOnly(areaArray);
        }

        public Vector2 Position { get; }
        public bool HasMapBounds { get; }
        public bool IsWithinBounds { get; }
        public float MovementSpeedMultiplier { get; }
        public IReadOnlyList<MapAreaSnapshot> Areas => areas ?? Array.Empty<MapAreaSnapshot>();
    }
}
