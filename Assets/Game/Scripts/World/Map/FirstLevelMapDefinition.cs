using System;
using System.Collections.Generic;
using System.Linq;
using SlimeBattle.Mechanisms;
using UnityEngine;

namespace SlimeBattle.World
{
    /// <summary>
    /// Offline First Level map facts authored in the design document's 5760px coordinate space.
    /// The provisional conversion is 100 design pixels per Unity world unit.
    /// </summary>
    [CreateAssetMenu(menuName = "SlimeBattle/Maps/First Level Definition",
        fileName = "MAP_FirstLevel")]
    public sealed class FirstLevelMapDefinition : ScriptableObject
    {
        [Serializable]
        private sealed class PointEntry
        {
            [SerializeField] private string pointId;
            [SerializeField] private MapPointKind kind;
            [SerializeField] private Vector2 designPosition;
            [SerializeField] private string configId;
            [SerializeField, Min(0f)] private float interactionRadiusPixels;

            public string PointId => pointId;
            public MapPointKind Kind => kind;
            public Vector2 DesignPosition => designPosition;
            public string ConfigId => configId;
            public float InteractionRadiusPixels => interactionRadiusPixels;
        }

        [Serializable]
        private sealed class AreaEntry
        {
            [SerializeField] private string areaId;
            [SerializeField] private MapAreaKind kind;
            [SerializeField] private MapRoadKind roadKind;
            [SerializeField] private MapTerrainKind terrainKind;
            [SerializeField] private MapRegionKind regionKind;
            [SerializeField] private bool usePolygon;
            [SerializeField] private Vector2 designCenter;
            [SerializeField, Min(0.01f)] private float radiusPixels = 1f;
            [SerializeField] private List<Vector2> designVertices = new List<Vector2>();
            [SerializeField, Min(0.01f)] private float movementSpeedMultiplier = 1f;
            [SerializeField] private string statusEffectConfigId;
            [SerializeField, Min(0f)] private float guidanceRadiusPixels = 500f;

            public string AreaId => areaId;
            public MapAreaKind Kind => kind;
            public MapRoadKind RoadKind => roadKind;
            public MapTerrainKind TerrainKind => terrainKind;
            public MapRegionKind RegionKind => regionKind;
            public bool UsePolygon => usePolygon;
            public Vector2 DesignCenter => designCenter;
            public float RadiusPixels => radiusPixels;
            public List<Vector2> DesignVertices => designVertices;
            public float MovementSpeedMultiplier => movementSpeedMultiplier;
            public string StatusEffectConfigId => statusEffectConfigId;
            public float GuidanceRadiusPixels => guidanceRadiusPixels;
        }

        [Serializable]
        private sealed class WaypointEntry
        {
            [SerializeField] private string waypointId;
            [SerializeField] private Vector2 designPosition;
            [SerializeField] private bool isGoal;

            public string WaypointId => waypointId;
            public Vector2 DesignPosition => designPosition;
            public bool IsGoal => isGoal;
        }

        [Serializable]
        private sealed class RouteEntry
        {
            [SerializeField] private string routeId;
            [SerializeField] private MapRouteRole role;
            [SerializeField] private string mergePointId;
            [SerializeField] private List<WaypointEntry> waypoints = new List<WaypointEntry>();
            [SerializeField] private List<string> blockingAnchorIds = new List<string>();
            [SerializeField] private List<string> relatedPointIds = new List<string>();

            public string RouteId => routeId;
            public MapRouteRole Role => role;
            public string MergePointId => mergePointId;
            public List<WaypointEntry> Waypoints => waypoints;
            public List<string> BlockingAnchorIds => blockingAnchorIds;
            public List<string> RelatedPointIds => relatedPointIds;
        }

        [Serializable]
        private sealed class AnchorEntry
        {
            [SerializeField] private string anchorId;
            [SerializeField] private PlacementAnchorRole role;
            [SerializeField] private Vector2 designPosition;
            [SerializeField, Min(0f)] private float snapRadiusPixels = 80f;
            [SerializeField] private List<MechanismKind> acceptedMechanismKinds =
                new List<MechanismKind> { MechanismKind.Obstacle };
            [SerializeField] private bool allowsReplacement = true;
            [SerializeField] private string associatedRouteId;
            [SerializeField] private string associatedPointId;

            public string AnchorId => anchorId;
            public PlacementAnchorRole Role => role;
            public Vector2 DesignPosition => designPosition;
            public float SnapRadiusPixels => snapRadiusPixels;
            public List<MechanismKind> AcceptedMechanismKinds => acceptedMechanismKinds;
            public bool AllowsReplacement => allowsReplacement;
            public string AssociatedRouteId => associatedRouteId;
            public string AssociatedPointId => associatedPointId;
        }

        [SerializeField, Min(1f)] private float designPixelsPerUnityUnit = 100f;
        [SerializeField] private Vector2 designSizePixels = new Vector2(5760f, 5760f);
        [SerializeField] private List<PointEntry> points = new List<PointEntry>();
        [SerializeField] private List<AreaEntry> areas = new List<AreaEntry>();
        [SerializeField] private List<RouteEntry> routes = new List<RouteEntry>();
        [SerializeField] private List<AnchorEntry> anchors = new List<AnchorEntry>();

        public float DesignPixelsPerUnityUnit => designPixelsPerUnityUnit;
        public Vector2 DesignSizePixels => designSizePixels;

        internal bool TryBuildSnapshot(out MapLayoutSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (!IsPositiveFinite(designPixelsPerUnityUnit) ||
                !IsPositiveFinite(designSizePixels.x) || !IsPositiveFinite(designSizePixels.y))
            {
                error = "Design scale and map size must be finite positive values.";
                return false;
            }

            var scale = 1f / designPixelsPerUnityUnit;
            var builtAreas = new List<MapAreaVolumeSnapshot>();
            var builtPoints = new List<MapPointSnapshot>();
            var builtRoutes = new List<MapRouteSnapshot>();
            var builtAnchors = new List<PlacementAnchorSnapshot>();
            var waypointIds = new HashSet<MapRouteWaypointId>();

            if (areas != null)
                foreach (var entry in areas)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.AreaId) ||
                        !IsFinite(entry.DesignCenter) ||
                        (!entry.UsePolygon && !IsPositiveFinite(entry.RadiusPixels)) ||
                        (entry.UsePolygon && (entry.DesignVertices == null ||
                            entry.DesignVertices.Count < 3 ||
                            entry.DesignVertices.Exists(vertex => !IsFinite(vertex)))) ||
                        !IsPositiveFinite(entry.MovementSpeedMultiplier) ||
                        !IsFiniteNonNegative(entry.GuidanceRadiusPixels) ||
                        !IsValidArea(entry))
                    {
                        error = "A map area has invalid identity, geometry or semantic data.";
                        return false;
                    }

                    var guidanceRadius = entry.Kind == MapAreaKind.Region &&
                        entry.RegionKind == MapRegionKind.Warm
                        ? entry.GuidanceRadiusPixels : 0f;
                    var area = new MapAreaSnapshot(new MapAreaId(entry.AreaId), entry.Kind,
                        entry.RoadKind, entry.TerrainKind, entry.RegionKind,
                        entry.MovementSpeedMultiplier, entry.StatusEffectConfigId,
                        guidanceRadius);
                    if (entry.UsePolygon)
                    {
                        var vertices = new List<Vector2>(entry.DesignVertices.Count);
                        foreach (var vertex in entry.DesignVertices)
                            vertices.Add(vertex * scale);
                        builtAreas.Add(new MapAreaVolumeSnapshot(area, vertices));
                    }
                    else
                    {
                        builtAreas.Add(new MapAreaVolumeSnapshot(area,
                            entry.DesignCenter * scale, entry.RadiusPixels * scale));
                    }
                }

            if (points != null)
                foreach (var entry in points)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.PointId) ||
                        !Enum.IsDefined(typeof(MapPointKind), entry.Kind) ||
                        !IsFinite(entry.DesignPosition) ||
                        !IsFiniteNonNegative(entry.InteractionRadiusPixels))
                    {
                        error = "A map point has invalid identity, kind or coordinate data.";
                        return false;
                    }

                    builtPoints.Add(new MapPointSnapshot(new MapPointId(entry.PointId),
                        entry.Kind, entry.DesignPosition * scale, entry.ConfigId,
                        entry.InteractionRadiusPixels));
                }

            if (routes != null)
                foreach (var entry in routes)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.RouteId) ||
                        entry.Waypoints == null || entry.Waypoints.Count < 2)
                    {
                        error = "A route requires an ID and at least two ordered waypoints.";
                        return false;
                    }

                    var waypoints = new List<MapRouteWaypointSnapshot>(entry.Waypoints.Count);
                    for (var i = 0; i < entry.Waypoints.Count; i++)
                    {
                        var waypoint = entry.Waypoints[i];
                        if (waypoint == null || string.IsNullOrWhiteSpace(waypoint.WaypointId) ||
                            !IsFinite(waypoint.DesignPosition) ||
                            !waypointIds.Add(new MapRouteWaypointId(waypoint.WaypointId)) ||
                            (entry.Role == MapRouteRole.Branch && waypoint.IsGoal) ||
                            (entry.Role != MapRouteRole.Branch &&
                                waypoint.IsGoal != (i == entry.Waypoints.Count - 1)))
                        {
                            error = $"Route '{entry.RouteId}' has an invalid waypoint at index {i}.";
                            return false;
                        }
                        waypoints.Add(new MapRouteWaypointSnapshot(
                            new MapRouteWaypointId(waypoint.WaypointId), i,
                            waypoint.DesignPosition * scale, waypoint.IsGoal));
                    }

                    var blockingAnchors = BuildAnchorIds(entry.BlockingAnchorIds);
                    var relatedPoints = BuildPointIds(entry.RelatedPointIds);
                    var route = new MapRouteSnapshot(new MapRouteId(entry.RouteId), waypoints,
                        entry.Role, new MapPointId(entry.MergePointId),
                        blockingAnchors, relatedPoints);
                    if (!route.IsValid)
                    {
                        error = $"Route '{entry.RouteId}' has invalid role, merge or relationship data.";
                        return false;
                    }
                    builtRoutes.Add(route);
                }

            if (anchors != null)
                foreach (var entry in anchors)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.AnchorId) ||
                        !IsFinite(entry.DesignPosition))
                    {
                        error = "A placement anchor has invalid identity or coordinates.";
                        return false;
                    }
                    var anchor = new PlacementAnchorSnapshot(
                        new PlacementAnchorId(entry.AnchorId), entry.Role,
                        entry.DesignPosition * scale, entry.SnapRadiusPixels,
                        entry.AcceptedMechanismKinds, entry.AllowsReplacement,
                        new MapRouteId(entry.AssociatedRouteId),
                        new MapPointId(entry.AssociatedPointId));
                    if (!anchor.IsValid)
                    {
                        error = $"Placement anchor '{entry.AnchorId}' has invalid associations or rules.";
                        return false;
                    }
                    builtAnchors.Add(anchor);
                }

            var bounds = new MapBounds(Vector2.zero, designSizePixels * scale);
            foreach (var area in builtAreas)
                if (!bounds.Contains(area.BoundsMin) || !bounds.Contains(area.BoundsMax))
                {
                    error = $"Map area '{area.Area.AreaId}' extends beyond the map bounds.";
                    return false;
                }
            foreach (var point in builtPoints)
                if (!bounds.Contains(point.Position))
                {
                    error = $"Map point '{point.PointId}' is outside the map bounds.";
                    return false;
                }
            foreach (var route in builtRoutes)
                foreach (var waypoint in route.Waypoints)
                    if (!bounds.Contains(waypoint.Position))
                    {
                        error = $"Route '{route.RouteId}' has a waypoint outside the map bounds.";
                        return false;
                    }
            foreach (var anchor in builtAnchors)
                if (!bounds.Contains(anchor.Position))
                {
                    error = $"Placement anchor '{anchor.AnchorId}' is outside the map bounds.";
                    return false;
                }

            snapshot = new MapLayoutSnapshot(bounds, builtAreas, builtPoints,
                builtRoutes, builtAnchors);
            error = string.Empty;
            return true;
        }

        private static bool IsValidArea(AreaEntry entry)
        {
            if (!Enum.IsDefined(typeof(MapAreaKind), entry.Kind)) return false;
            if (entry.Kind == MapAreaKind.Road)
                return Enum.IsDefined(typeof(MapRoadKind), entry.RoadKind);
            if (entry.Kind == MapAreaKind.Terrain)
                return Enum.IsDefined(typeof(MapTerrainKind), entry.TerrainKind) &&
                    entry.TerrainKind != MapTerrainKind.None;
            if (entry.Kind == MapAreaKind.Region)
                return Enum.IsDefined(typeof(MapRegionKind), entry.RegionKind) &&
                    entry.RegionKind != MapRegionKind.None;
            return entry.Kind == MapAreaKind.Placement || entry.Kind == MapAreaKind.Goal;
        }

        private static List<PlacementAnchorId> BuildAnchorIds(List<string> values)
        {
            var result = new List<PlacementAnchorId>();
            if (values != null)
                foreach (var value in values) result.Add(new PlacementAnchorId(value));
            return result;
        }

        private static List<MapPointId> BuildPointIds(List<string> values)
        {
            var result = new List<MapPointId>();
            if (values != null)
                foreach (var value in values) result.Add(new MapPointId(value));
            return result;
        }

        private static bool IsFinite(Vector2 value) =>
            IsFinite(value.x) && IsFinite(value.y);

        private static bool IsPositiveFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;

        private static bool IsFiniteNonNegative(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
