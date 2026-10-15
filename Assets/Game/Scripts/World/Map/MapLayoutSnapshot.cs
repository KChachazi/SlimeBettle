using System;
using System.Collections.Generic;

namespace SlimeBattle.World
{
    internal readonly struct MapAreaVolumeSnapshot
    {
        public MapAreaVolumeSnapshot(MapAreaSnapshot area, UnityEngine.Vector2 center,
            float radius)
        {
            Area = area;
            Center = center;
            Radius = radius;
        }

        public MapAreaSnapshot Area { get; }
        public UnityEngine.Vector2 Center { get; }
        public float Radius { get; }
        public bool Contains(UnityEngine.Vector2 point) =>
            (point - Center).sqrMagnitude <= Radius * Radius;
    }

    internal sealed class MapLayoutSnapshot
    {
        public MapLayoutSnapshot(MapBounds bounds,
            IEnumerable<MapAreaVolumeSnapshot> areas,
            IEnumerable<MapPointSnapshot> points,
            IEnumerable<MapRouteSnapshot> routes,
            IEnumerable<PlacementAnchorSnapshot> anchors)
        {
            Bounds = bounds;
            Areas = Array.AsReadOnly(areas == null
                ? Array.Empty<MapAreaVolumeSnapshot>()
                : System.Linq.Enumerable.ToArray(areas));
            Points = Array.AsReadOnly(points == null
                ? Array.Empty<MapPointSnapshot>()
                : System.Linq.Enumerable.ToArray(points));
            Routes = Array.AsReadOnly(routes == null
                ? Array.Empty<MapRouteSnapshot>()
                : System.Linq.Enumerable.ToArray(routes));
            Anchors = Array.AsReadOnly(anchors == null
                ? Array.Empty<PlacementAnchorSnapshot>()
                : System.Linq.Enumerable.ToArray(anchors));
        }

        public MapBounds Bounds { get; }
        public IReadOnlyList<MapAreaVolumeSnapshot> Areas { get; }
        public IReadOnlyList<MapPointSnapshot> Points { get; }
        public IReadOnlyList<MapRouteSnapshot> Routes { get; }
        public IReadOnlyList<PlacementAnchorSnapshot> Anchors { get; }
    }
}
