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
        [SerializeField] private List<WaypointEntry> waypoints = new List<WaypointEntry>();

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

            if (goalCount != 1)
            {
                error = "A fixed route requires exactly one goal waypoint.";
                return false;
            }

            snapshot = new MapRouteSnapshot(RouteId, result);
            error = string.Empty;
            return snapshot.IsValid;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
