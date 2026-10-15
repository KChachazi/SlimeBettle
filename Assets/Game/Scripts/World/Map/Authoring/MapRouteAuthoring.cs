using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBattle.World
{
    [DisallowMultipleComponent]
    public sealed class MapRouteAuthoring : MonoBehaviour
    {
        [Serializable]
        private sealed class WaypointEntry
        {
            [SerializeField] private string waypointId;
            [SerializeField] private Transform marker;
            [SerializeField] private bool isGoal;

            public MapRouteWaypointId WaypointId => new MapRouteWaypointId(waypointId);
            public Transform Marker => marker;
            public bool IsGoal => isGoal;
        }

        [SerializeField] private string routeId;
        [SerializeField] private MapRouteRole role;
        [SerializeField] private string mergePointId;
        [SerializeField] private List<WaypointEntry> waypoints = new List<WaypointEntry>();
        [SerializeField] private List<string> blockingAnchorIds = new List<string>();
        [SerializeField] private List<string> relatedPointIds = new List<string>();

        internal MapRouteId RouteId => new MapRouteId(routeId);

        internal bool TryGetSnapshot(out MapRouteSnapshot snapshot, out string error)
        {
            snapshot = default(MapRouteSnapshot);
            if (!RouteId.IsValid)
            {
                error = "Route ID is empty.";
                return false;
            }
            if (waypoints == null || waypoints.Count < 2)
            {
                error = "A fixed route requires at least two waypoints.";
                return false;
            }

            var ids = new HashSet<MapRouteWaypointId>();
            var goalCount = 0;
            var result = new List<MapRouteWaypointSnapshot>(waypoints.Count);
            for (var i = 0; i < waypoints.Count; i++)
            {
                var entry = waypoints[i];
                if (entry == null || !entry.WaypointId.IsValid)
                {
                    error = $"Waypoint {i} has an empty ID.";
                    return false;
                }
                if (!ids.Add(entry.WaypointId))
                {
                    error = $"Waypoint ID '{entry.WaypointId}' is duplicated in this route.";
                    return false;
                }
                if (entry.Marker == null || entry.Marker.gameObject.scene.handle != gameObject.scene.handle)
                {
                    error = $"Waypoint '{entry.WaypointId}' requires a marker in the same scene.";
                    return false;
                }

                var position = (Vector2)entry.Marker.position;
                if (!IsFinite(position.x) || !IsFinite(position.y))
                {
                    error = $"Waypoint '{entry.WaypointId}' has a non-finite world position.";
                    return false;
                }
                if (entry.IsGoal)
                {
                    goalCount++;
                    if (i != waypoints.Count - 1)
                    {
                        error = "The goal waypoint must be the final route entry.";
                        return false;
                    }
                }

                result.Add(new MapRouteWaypointSnapshot(entry.WaypointId,
                    i, position, entry.IsGoal));
            }

            var expectedGoalCount = role == MapRouteRole.Branch ? 0 : 1;
            if (goalCount != expectedGoalCount)
            {
                error = role == MapRouteRole.Branch
                    ? "A branch route ends at a merge point and must not mark a goal waypoint."
                    : "A complete fixed route requires exactly one goal waypoint.";
                return false;
            }

            if (!TryBuildIds<PlacementAnchorId>(blockingAnchorIds, out var anchors,
                    out error, "blocking anchor") ||
                !TryBuildIds<MapPointId>(relatedPointIds, out var relatedPoints,
                    out error, "related point"))
                return false;

            snapshot = new MapRouteSnapshot(RouteId, result, role,
                new MapPointId(mergePointId), anchors, relatedPoints);
            error = string.Empty;
            if (!snapshot.IsValid)
            {
                error = "Route role, merge point, or associated IDs are invalid.";
                return false;
            }
            return true;
        }

        private static bool TryBuildIds<TId>(List<string> values,
            out List<TId> result, out string error, string label)
        {
            result = new List<TId>();
            error = string.Empty;
            if (values == null) return true;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < values.Count; i++)
            {
                var value = values[i];
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value.Trim()))
                {
                    error = $"Route {label} ID at index {i} is empty or duplicated.";
                    return false;
                }
                if (typeof(TId) == typeof(PlacementAnchorId))
                    result.Add((TId)(object)new PlacementAnchorId(value));
                else if (typeof(TId) == typeof(MapPointId))
                    result.Add((TId)(object)new MapPointId(value));
            }
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
