using System;
using System.Collections.Generic;
using System.Linq;

namespace SlimeBattle.World
{
    public readonly struct MapRouteSnapshot
    {
        private readonly IReadOnlyList<MapRouteWaypointSnapshot> waypoints;

        public MapRouteSnapshot(MapRouteId routeId,
            IEnumerable<MapRouteWaypointSnapshot> routeWaypoints)
        {
            RouteId = routeId;
            var values = routeWaypoints == null
                ? Array.Empty<MapRouteWaypointSnapshot>()
                : routeWaypoints.ToArray();
            waypoints = Array.AsReadOnly(values);
        }

        public MapRouteId RouteId { get; }
        public IReadOnlyList<MapRouteWaypointSnapshot> Waypoints =>
            waypoints ?? Array.Empty<MapRouteWaypointSnapshot>();
        public bool HasGoal => IsValid;
        public MapRouteWaypointSnapshot GoalWaypoint => HasGoal
            ? Waypoints[Waypoints.Count - 1]
            : default(MapRouteWaypointSnapshot);

        public bool IsValid
        {
            get
            {
                var points = Waypoints;
                if (!RouteId.IsValid || points.Count < 2) return false;
                var ids = new HashSet<MapRouteWaypointId>();
                for (var i = 0; i < points.Count; i++)
                {
                    if (!points[i].WaypointId.IsValid || !ids.Add(points[i].WaypointId) ||
                        points[i].Sequence != i || !IsFinite(points[i].Position) ||
                        points[i].IsGoal != (i == points.Count - 1)) return false;
                }
                return true;
            }
        }

        private static bool IsFinite(UnityEngine.Vector2 position) =>
            !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
            !float.IsNaN(position.y) && !float.IsInfinity(position.y);
    }
}
