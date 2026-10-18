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
            Vertices = Array.Empty<UnityEngine.Vector2>();
        }

        public MapAreaVolumeSnapshot(MapAreaSnapshot area,
            IEnumerable<UnityEngine.Vector2> vertices)
        {
            Area = area;
            var points = vertices == null
                ? Array.Empty<UnityEngine.Vector2>()
                : System.Linq.Enumerable.ToArray(vertices);
            Vertices = Array.AsReadOnly(points);
            var center = UnityEngine.Vector2.zero;
            for (var i = 0; i < points.Length; i++) center += points[i];
            Center = points.Length == 0 ? center : center / points.Length;
            Radius = 0f;
        }

        public MapAreaSnapshot Area { get; }
        public UnityEngine.Vector2 Center { get; }
        public float Radius { get; }
        public IReadOnlyList<UnityEngine.Vector2> Vertices { get; }
        public bool IsPolygon => Vertices != null && Vertices.Count >= 3;

        public UnityEngine.Vector2 BoundsMin
        {
            get
            {
                if (!IsPolygon) return Center - UnityEngine.Vector2.one * Radius;
                var result = Vertices[0];
                for (var i = 1; i < Vertices.Count; i++)
                    result = UnityEngine.Vector2.Min(result, Vertices[i]);
                return result;
            }
        }

        public UnityEngine.Vector2 BoundsMax
        {
            get
            {
                if (!IsPolygon) return Center + UnityEngine.Vector2.one * Radius;
                var result = Vertices[0];
                for (var i = 1; i < Vertices.Count; i++)
                    result = UnityEngine.Vector2.Max(result, Vertices[i]);
                return result;
            }
        }

        public bool Contains(UnityEngine.Vector2 point, float tolerance = 0f)
        {
            tolerance = UnityEngine.Mathf.Max(0f, tolerance);
            if (!IsPolygon)
            {
                var expandedRadius = Radius + tolerance;
                return (point - Center).sqrMagnitude <= expandedRadius * expandedRadius;
            }

            var inside = false;
            var previous = Vertices.Count - 1;
            var edgeTolerance = UnityEngine.Mathf.Max(tolerance, 0.001f);
            var edgeToleranceSquared = edgeTolerance * edgeTolerance;
            for (var current = 0; current < Vertices.Count; current++)
            {
                var a = Vertices[current];
                var b = Vertices[previous];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) /
                    (b.y - a.y) + a.x)
                    inside = !inside;
                if (DistanceToSegmentSquared(point, a, b) <= edgeToleranceSquared)
                    return true;
                previous = current;
            }
            return inside;
        }

        private static float DistanceToSegmentSquared(UnityEngine.Vector2 point,
            UnityEngine.Vector2 start, UnityEngine.Vector2 end)
        {
            var segment = end - start;
            var length = segment.sqrMagnitude;
            var t = length <= 0f ? 0f : UnityEngine.Mathf.Clamp01(
                UnityEngine.Vector2.Dot(point - start, segment) / length);
            return (point - (start + segment * t)).sqrMagnitude;
        }
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
