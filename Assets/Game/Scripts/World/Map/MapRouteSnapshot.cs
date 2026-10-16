using System;
using System.Collections.Generic;
using System.Linq;

namespace SlimeBattle.World
{
    public readonly struct MapRouteSnapshot
    {
        private readonly IReadOnlyList<MapRouteWaypointSnapshot> waypoints;
        private readonly IReadOnlyList<PlacementAnchorId> blockingAnchorIds;
        private readonly IReadOnlyList<MapPointId> relatedPointIds;

        public MapRouteSnapshot(MapRouteId routeId,
            IEnumerable<MapRouteWaypointSnapshot> routeWaypoints,
            MapRouteRole role = MapRouteRole.Candidate,
            MapPointId mergePointId = default(MapPointId),
            IEnumerable<PlacementAnchorId> routeBlockingAnchorIds = null,
            IEnumerable<MapPointId> routeRelatedPointIds = null)
        {
            RouteId = routeId;
            Role = role;
            MergePointId = mergePointId;
            var values = routeWaypoints == null
                ? Array.Empty<MapRouteWaypointSnapshot>()
                : routeWaypoints.ToArray();
            waypoints = Array.AsReadOnly(values);
            var anchorValues = routeBlockingAnchorIds == null
                ? Array.Empty<PlacementAnchorId>()
                : routeBlockingAnchorIds.ToArray();
            blockingAnchorIds = Array.AsReadOnly(anchorValues);
            var pointValues = routeRelatedPointIds == null
                ? Array.Empty<MapPointId>()
                : routeRelatedPointIds.ToArray();
            relatedPointIds = Array.AsReadOnly(pointValues);
        }

        public MapRouteId RouteId { get; }
        public MapRouteRole Role { get; }
        public MapPointId MergePointId { get; }
        public IReadOnlyList<MapRouteWaypointSnapshot> Waypoints =>
            waypoints ?? Array.Empty<MapRouteWaypointSnapshot>();
        public IReadOnlyList<PlacementAnchorId> BlockingAnchorIds =>
            blockingAnchorIds ?? Array.Empty<PlacementAnchorId>();
        public IReadOnlyList<MapPointId> RelatedPointIds =>
            relatedPointIds ?? Array.Empty<MapPointId>();
        public bool HasGoal => IsValid && Role != MapRouteRole.Branch;
        public MapRouteWaypointSnapshot EndWaypoint => IsValid
            ? Waypoints[Waypoints.Count - 1]
            : default(MapRouteWaypointSnapshot);
        public MapRouteWaypointSnapshot GoalWaypoint => HasGoal
            ? Waypoints[Waypoints.Count - 1]
            : default(MapRouteWaypointSnapshot);

        public bool IsValid
        {
            get
            {
                var points = Waypoints;
                if (!RouteId.IsValid || points.Count < 2 ||
                    !Enum.IsDefined(typeof(MapRouteRole), Role) ||
                    (Role == MapRouteRole.Branch && !MergePointId.IsValid)) return false;
                var ids = new HashSet<MapRouteWaypointId>();
                for (var i = 0; i < points.Count; i++)
                {
                    if (!points[i].WaypointId.IsValid || !ids.Add(points[i].WaypointId) ||
                        points[i].Sequence != i || !IsFinite(points[i].Position) ||
                        (Role == MapRouteRole.Branch && points[i].IsGoal) ||
                        (Role != MapRouteRole.Branch &&
                            points[i].IsGoal != (i == points.Count - 1))) return false;
                }
                var anchors = new HashSet<PlacementAnchorId>();
                for (var i = 0; i < BlockingAnchorIds.Count; i++)
                    if (!BlockingAnchorIds[i].IsValid || !anchors.Add(BlockingAnchorIds[i]))
                        return false;
                var relatedPoints = new HashSet<MapPointId>();
                for (var i = 0; i < RelatedPointIds.Count; i++)
                    if (!RelatedPointIds[i].IsValid || !relatedPoints.Add(RelatedPointIds[i]))
                        return false;
                return true;
            }
        }

        private static bool IsFinite(UnityEngine.Vector2 position) =>
            !float.IsNaN(position.x) && !float.IsInfinity(position.x) &&
            !float.IsNaN(position.y) && !float.IsInfinity(position.y);
    }
}
