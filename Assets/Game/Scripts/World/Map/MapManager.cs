using System;
using System.Collections.Generic;
using System.Linq;
using SlimeBattle.Actors;
using SlimeBattle.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SlimeBattle.World
{
    /// <summary>
    /// Binds static map facts and exposes scene-authored areas and points to level systems.
    /// Actor area transitions are derived from positions reported by MovementSystem.
    /// </summary>
    public sealed class MapManager : IDisposable
    {
        private sealed class AreaBinding
        {
            public AreaBinding(MapAreaAuthoring authoring, MapAreaSnapshot snapshot)
            {
                Authoring = authoring;
                Snapshot = snapshot;
            }

            public AreaBinding(MapAreaVolumeSnapshot volume)
            {
                Snapshot = volume.Area;
                Volume = volume;
                HasLayoutVolume = true;
            }

            public MapAreaAuthoring Authoring { get; }
            public MapAreaSnapshot Snapshot { get; }
            private MapAreaVolumeSnapshot Volume { get; }
            private bool HasLayoutVolume { get; }

            public bool Contains(Vector2 position)
            {
                if (HasLayoutVolume) return Volume.Contains(position);
                var collider = Authoring == null ? null : Authoring.AreaCollider;
                return collider != null && collider.enabled &&
                    collider.gameObject.activeInHierarchy && collider.OverlapPoint(position);
            }

            public bool Contains(Vector2 position, float tolerance)
            {
                if (HasLayoutVolume) return Volume.Contains(position, tolerance);
                var collider = Authoring == null ? null : Authoring.AreaCollider;
                if (collider == null || !collider.enabled ||
                    !collider.gameObject.activeInHierarchy) return false;
                if (collider.OverlapPoint(position)) return true;
                if (tolerance <= 0f) return false;
                var closest = collider.ClosestPoint(position);
                return (closest - position).sqrMagnitude <= tolerance * tolerance;
            }

            public bool TryGetCenter(out Vector2 center)
            {
                center = HasLayoutVolume ? Volume.Center : Vector2.zero;
                if (HasLayoutVolume) return true;
                var collider = Authoring == null ? null : Authoring.AreaCollider;
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    return false;
                center = collider.bounds.center;
                return true;
            }
        }

        private readonly HashSet<Collider2D> movementBlockers = new HashSet<Collider2D>();
        private readonly Dictionary<MapAreaId, AreaBinding> areas =
            new Dictionary<MapAreaId, AreaBinding>();
        private readonly Dictionary<MapPointId, MapPointSnapshot> points =
            new Dictionary<MapPointId, MapPointSnapshot>();
        private readonly Dictionary<MapRouteId, MapRouteSnapshot> routes =
            new Dictionary<MapRouteId, MapRouteSnapshot>();
        private readonly Dictionary<PlacementAnchorId, PlacementAnchorSnapshot> placementAnchors =
            new Dictionary<PlacementAnchorId, PlacementAnchorSnapshot>();
        private readonly Dictionary<MapPointKind, List<MapPointSnapshot>> pointsByKind =
            new Dictionary<MapPointKind, List<MapPointSnapshot>>();
        private readonly Dictionary<ActorId, HashSet<MapAreaId>> actorAreas =
            new Dictionary<ActorId, HashSet<MapAreaId>>();
        private readonly ILevelEventBus eventBus;
        private Scene scene;
        private MapBounds? mapBounds;
        private bool hasRoadAreas;
        private bool disposed;
        private bool readyPublished;

        internal MapManager(Scene scene, ILevelEventBus eventBus)
        {
            this.eventBus = eventBus;
            Bind(scene);
        }

        private bool IsBound => !disposed && scene.IsValid() && scene.isLoaded;

        internal bool IsMovementBlocker(Collider2D collider)
        {
            return IsBound && collider != null && collider.enabled &&
                collider.gameObject.activeInHierarchy && movementBlockers.Contains(collider) &&
                IsSolidStaticCollider(collider);
        }

        public bool TryGetMapBounds(out MapBounds bounds)
        {
            if (IsBound && mapBounds.HasValue)
            {
                bounds = mapBounds.Value;
                return true;
            }

            bounds = default(MapBounds);
            return false;
        }

        public MapSpatialSnapshot Query(Vector2 worldPosition)
        {
            if (!IsBound)
            {
                return new MapSpatialSnapshot(worldPosition, false, false,
                    1f, Array.Empty<MapAreaSnapshot>());
            }

            var withinBounds = !mapBounds.HasValue || mapBounds.Value.Contains(worldPosition);
            var found = new List<MapAreaSnapshot>();
            var speedMultiplier = 1f;
            foreach (var binding in areas.Values)
            {
                if (!binding.Contains(worldPosition)) continue;

                found.Add(binding.Snapshot);
                if (binding.Snapshot.Kind == MapAreaKind.Terrain)
                    speedMultiplier *= binding.Snapshot.MovementSpeedMultiplier;
            }

            found.Sort((left, right) => string.CompareOrdinal(
                left.AreaId.Value, right.AreaId.Value));
            return new MapSpatialSnapshot(worldPosition, mapBounds.HasValue,
                withinBounds, speedMultiplier, found);
        }

        public float GetMovementSpeedMultiplier(Vector2 worldPosition)
        {
            return Query(worldPosition).MovementSpeedMultiplier;
        }

        public bool CanOccupy(Vector2 worldPosition, OccupancyQuery query)
        {
            if (!IsBound || !query.IsValid) return false;

            var radius = query.Radius;
            if (query.RequireWithinBounds)
            {
                if (!mapBounds.HasValue) return false;
                var bounds = mapBounds.Value;
                if (worldPosition.x - radius < bounds.Min.x ||
                    worldPosition.x + radius > bounds.Max.x ||
                    worldPosition.y - radius < bounds.Min.y ||
                    worldPosition.y + radius > bounds.Max.y)
                {
                    return false;
                }
            }

            if (!query.IgnoreRoadArea && hasRoadAreas)
            {
                var onRoad = false;
                foreach (var binding in areas.Values)
                {
                    if (binding.Snapshot.Kind != MapAreaKind.Road ||
                        !binding.Contains(worldPosition, query.RoadTolerance)) continue;
                    onRoad = true;
                    break;
                }
                if (!onRoad) return false;
            }

            if (!query.CheckStaticBlockers) return true;
            var radiusSquared = radius * radius;
            foreach (var collider in movementBlockers)
            {
                if (collider == null || !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (collider.OverlapPoint(worldPosition)) return false;
                var closestPoint = collider.ClosestPoint(worldPosition);
                if ((closestPoint - worldPosition).sqrMagnitude <= radiusSquared)
                    return false;
            }

            return true;
        }

        public bool TryGetMapArea(MapAreaId areaId, out MapAreaSnapshot area)
        {
            if (IsBound && areaId.IsValid && areas.TryGetValue(areaId, out var binding))
            {
                area = binding.Snapshot;
                return true;
            }

            area = default(MapAreaSnapshot);
            return false;
        }

        public bool TryGetMapPoint(MapPointId pointId, out MapPointSnapshot point)
        {
            if (IsBound && pointId.IsValid && points.TryGetValue(pointId, out point))
                return true;

            point = default(MapPointSnapshot);
            return false;
        }

        public bool TryGetRoute(MapRouteId routeId, out MapRouteSnapshot route)
        {
            if (IsBound && routeId.IsValid && routes.TryGetValue(routeId, out route))
                return true;

            route = default(MapRouteSnapshot);
            return false;
        }

        public bool TryGetPlacementAnchor(PlacementAnchorId anchorId,
            out PlacementAnchorSnapshot anchor)
        {
            if (IsBound && anchorId.IsValid &&
                placementAnchors.TryGetValue(anchorId, out anchor)) return true;

            anchor = default(PlacementAnchorSnapshot);
            return false;
        }

        public IReadOnlyList<PlacementAnchorSnapshot> GetPlacementAnchors()
        {
            if (!IsBound || placementAnchors.Count == 0)
                return Array.Empty<PlacementAnchorSnapshot>();

            var result = new List<PlacementAnchorSnapshot>(placementAnchors.Values);
            result.Sort((left, right) => string.CompareOrdinal(
                left.AnchorId.Value, right.AnchorId.Value));
            return result.AsReadOnly();
        }

        public IReadOnlyList<MapPointSnapshot> GetMapPoints(MapPointKind kind)
        {
            if (!IsBound || !pointsByKind.TryGetValue(kind, out var matches))
                return Array.Empty<MapPointSnapshot>();

            return matches.ToArray();
        }

        public IReadOnlyList<MapAreaSnapshot> GetMapAreaSnapshots()
        {
            if (!IsBound) return Array.Empty<MapAreaSnapshot>();
            var result = new List<MapAreaSnapshot>(areas.Count);
            foreach (var binding in areas.Values) result.Add(binding.Snapshot);
            result.Sort((left, right) => string.CompareOrdinal(
                left.AreaId.Value, right.AreaId.Value));
            return result.AsReadOnly();
        }

        public bool TryGetMapAreaCenter(MapAreaId areaId, out Vector2 center)
        {
            center = default(Vector2);
            if (!IsBound || !areaId.IsValid || !areas.TryGetValue(areaId, out var binding))
                return false;
            return binding.TryGetCenter(out center);
        }

        public bool RegisterMovementBlocker(Collider2D collider)
        {
            if (!IsBound || !IsSolidStaticCollider(collider)) return false;
            movementBlockers.Add(collider);
            return true;
        }

        public bool UnregisterMovementBlocker(Collider2D collider)
        {
            return IsBound && collider != null && movementBlockers.Remove(collider);
        }

        internal void NotifyActorPositionChanged(ActorId actorId, Vector2 position)
        {
            if (!IsBound || !actorId.IsValid) return;

            var previousAreas = actorAreas.TryGetValue(actorId, out var previous)
                ? previous : new HashSet<MapAreaId>();
            var currentAreas = GetAreaIdsAt(position);
            actorAreas[actorId] = currentAreas;

            foreach (var areaId in previousAreas.Except(currentAreas)
                         .OrderBy(id => id.Value, StringComparer.Ordinal))
            {
                if (areas.TryGetValue(areaId, out var binding))
                    eventBus?.Publish(new MapAreaExitedEvent(actorId, binding.Snapshot, position));
            }

            foreach (var areaId in currentAreas.Except(previousAreas)
                         .OrderBy(id => id.Value, StringComparer.Ordinal))
            {
                if (areas.TryGetValue(areaId, out var binding))
                    eventBus?.Publish(new MapAreaEnteredEvent(actorId, binding.Snapshot, position));
            }
        }

        internal void NotifyActorRemoved(ActorId actorId)
        {
            if (!disposed) actorAreas.Remove(actorId);
        }

        internal void PublishReady()
        {
            if (!IsBound || readyPublished) return;
            readyPublished = true;
            eventBus?.Publish(new MapReadyEvent(areas.Count, points.Count,
                movementBlockers.Count));
        }

        public void Dispose()
        {
            if (disposed) return;
            movementBlockers.Clear();
            areas.Clear();
            points.Clear();
            routes.Clear();
            pointsByKind.Clear();
            placementAnchors.Clear();
            actorAreas.Clear();
            mapBounds = null;
            hasRoadAreas = false;
            scene = default(Scene);
            disposed = true;
        }

        private HashSet<MapAreaId> GetAreaIdsAt(Vector2 position)
        {
            var result = new HashSet<MapAreaId>();
            foreach (var pair in areas)
            {
                if (pair.Value.Contains(position))
                    result.Add(pair.Key);
            }

            return result;
        }

        private void Bind(Scene activeScene)
        {
            if (!activeScene.IsValid() || !activeScene.isLoaded) return;

            scene = activeScene;
            MapBoundsAuthoring foundBounds = null;
            var routeAuthorings = new List<MapRouteAuthoring>();
            var layoutAuthorings = new List<MapLayoutAuthoring>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var authoring in root.GetComponentsInChildren<MapBoundsAuthoring>(true))
                {
                    if (!IsSceneAuthoring(authoring) || !authoring.isActiveAndEnabled) continue;
                    if (foundBounds != null)
                    {
                        Debug.LogError($"Scene '{scene.name}' contains more than one MapBoundsAuthoring.", authoring);
                        continue;
                    }

                    foundBounds = authoring;
                }

                foreach (var authoring in root.GetComponentsInChildren<MapAreaAuthoring>(true))
                {
                    if (!IsSceneAuthoring(authoring) || !authoring.isActiveAndEnabled) continue;
                    if (!authoring.TryGetSnapshot(out var snapshot, out var error))
                    {
                        Debug.LogError($"Invalid map area on '{authoring.name}': {error}", authoring);
                        continue;
                    }

                    if (areas.ContainsKey(snapshot.AreaId))
                    {
                        Debug.LogError($"Duplicate map area ID '{snapshot.AreaId}' in scene '{scene.name}'.", authoring);
                        continue;
                    }

                    areas.Add(snapshot.AreaId, new AreaBinding(authoring, snapshot));
                }

                foreach (var authoring in root.GetComponentsInChildren<MapPointAuthoring>(true))
                {
                    if (!IsSceneAuthoring(authoring) || !authoring.isActiveAndEnabled) continue;
                    if (!authoring.TryGetSnapshot(out var snapshot, out var error))
                    {
                        Debug.LogError($"Invalid map point on '{authoring.name}': {error}", authoring);
                        continue;
                    }

                    if (points.ContainsKey(snapshot.PointId))
                    {
                        Debug.LogError($"Duplicate map point ID '{snapshot.PointId}' in scene '{scene.name}'.", authoring);
                        continue;
                    }

                    points.Add(snapshot.PointId, snapshot);
                    if (!pointsByKind.TryGetValue(snapshot.Kind, out var group))
                    {
                        group = new List<MapPointSnapshot>();
                        pointsByKind.Add(snapshot.Kind, group);
                    }
                    group.Add(snapshot);
                }

                foreach (var authoring in root.GetComponentsInChildren<MapRouteAuthoring>(true))
                {
                    if (IsSceneAuthoring(authoring) && authoring.isActiveAndEnabled)
                        routeAuthorings.Add(authoring);
                }

                foreach (var authoring in root.GetComponentsInChildren<MapLayoutAuthoring>(true))
                {
                    if (IsSceneAuthoring(authoring) && authoring.isActiveAndEnabled)
                        layoutAuthorings.Add(authoring);
                }

                foreach (var authoring in root.GetComponentsInChildren<PlacementAnchorAuthoring>(true))
                {
                    if (!IsSceneAuthoring(authoring) || !authoring.isActiveAndEnabled) continue;
                    if (!authoring.TryGetSnapshot(out var snapshot, out var error))
                    {
                        Debug.LogError($"Invalid placement anchor on '{authoring.name}': {error}", authoring);
                        continue;
                    }
                    if (placementAnchors.ContainsKey(snapshot.AnchorId))
                    {
                        Debug.LogError($"Duplicate placement anchor ID '{snapshot.AnchorId}' in scene '{scene.name}'.",
                            authoring);
                        continue;
                    }
                    placementAnchors.Add(snapshot.AnchorId, snapshot);
                }

                foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
                {
                    if (IsSolidStaticCollider(collider)) movementBlockers.Add(collider);
                }
            }

            if (foundBounds != null)
            {
                if (foundBounds.TryGetWorldBounds(out var bounds)) mapBounds = bounds;
                else Debug.LogError($"MapBoundsAuthoring on '{foundBounds.name}' has invalid size.", foundBounds);
            }

            if (layoutAuthorings.Count > 1)
                Debug.LogError($"Scene '{scene.name}' contains more than one MapLayoutAuthoring.");
            if (layoutAuthorings.Count > 0)
            {
                var layoutAuthoring = layoutAuthorings[0];
                if (foundBounds != null)
                {
                    Debug.LogError("A scene cannot combine MapLayoutAuthoring and MapBoundsAuthoring; " +
                        "the offline layout supplies its own bounds.", layoutAuthoring);
                }
                else if (layoutAuthoring.TryGetSnapshot(out var layout, out var layoutError))
                {
                    mapBounds = layout.Bounds;
                    BindLayoutAreas(layout.Areas, layoutAuthoring);
                    BindLayoutPoints(layout.Points, layoutAuthoring);
                    BindLayoutRoutes(layout.Routes, layoutAuthoring);
                    BindLayoutAnchors(layout.Anchors, layoutAuthoring);
                }
                else Debug.LogError($"Invalid map layout on '{layoutAuthoring.name}': {layoutError}",
                    layoutAuthoring);
            }

            foreach (var authoring in routeAuthorings)
            {
                if (!authoring.TryGetSnapshot(out var route, out var error))
                {
                    Debug.LogError($"Invalid map route on '{authoring.name}': {error}", authoring);
                    continue;
                }
                if (mapBounds.HasValue && HasPointOutsideBounds(route, mapBounds.Value))
                {
                    Debug.LogError($"Map route '{route.RouteId}' contains a waypoint outside the map bounds.",
                        authoring);
                    continue;
                }
                if (routes.ContainsKey(route.RouteId))
                {
                    Debug.LogError($"Duplicate map route ID '{route.RouteId}' in scene '{scene.name}'.",
                        authoring);
                    continue;
                }
                routes.Add(route.RouteId, route);
            }

            foreach (var group in pointsByKind.Values)
                group.Sort((left, right) => string.CompareOrdinal(
                    left.PointId.Value, right.PointId.Value));
            hasRoadAreas = false;
            foreach (var binding in areas.Values)
                if (binding.Snapshot.Kind == MapAreaKind.Road)
                {
                    hasRoadAreas = true;
                    break;
                }
            ValidateMapReferences();
        }

        private void BindLayoutAreas(IReadOnlyList<MapAreaVolumeSnapshot> layoutAreas,
            MapLayoutAuthoring source)
        {
            foreach (var volume in layoutAreas)
            {
                var id = volume.Area.AreaId;
                if (areas.ContainsKey(id))
                {
                    Debug.LogError($"Duplicate map area ID '{id}' in scene '{scene.name}'.", source);
                    continue;
                }
                areas.Add(id, new AreaBinding(volume));
            }
        }

        private void BindLayoutPoints(IReadOnlyList<MapPointSnapshot> layoutPoints,
            MapLayoutAuthoring source)
        {
            foreach (var point in layoutPoints)
            {
                if (points.ContainsKey(point.PointId))
                {
                    Debug.LogError($"Duplicate map point ID '{point.PointId}' in scene '{scene.name}'.", source);
                    continue;
                }
                points.Add(point.PointId, point);
                if (!pointsByKind.TryGetValue(point.Kind, out var group))
                {
                    group = new List<MapPointSnapshot>();
                    pointsByKind.Add(point.Kind, group);
                }
                group.Add(point);
            }
        }

        private void BindLayoutRoutes(IReadOnlyList<MapRouteSnapshot> layoutRoutes,
            MapLayoutAuthoring source)
        {
            foreach (var route in layoutRoutes)
            {
                if (mapBounds.HasValue && HasPointOutsideBounds(route, mapBounds.Value))
                {
                    Debug.LogError($"Map route '{route.RouteId}' contains a waypoint outside the map bounds.",
                        source);
                    continue;
                }
                if (routes.ContainsKey(route.RouteId))
                    Debug.LogError($"Duplicate map route ID '{route.RouteId}' in scene '{scene.name}'.",
                        source);
                else routes.Add(route.RouteId, route);
            }
        }

        private void BindLayoutAnchors(
            IReadOnlyList<PlacementAnchorSnapshot> layoutAnchors, MapLayoutAuthoring source)
        {
            foreach (var anchor in layoutAnchors)
            {
                if (placementAnchors.ContainsKey(anchor.AnchorId))
                    Debug.LogError($"Duplicate placement anchor ID '{anchor.AnchorId}' in scene '{scene.name}'.",
                        source);
                else placementAnchors.Add(anchor.AnchorId, anchor);
            }
        }

        private void ValidateMapReferences()
        {
            var waypointIds = new HashSet<MapRouteWaypointId>();
            foreach (var route in routes.Values)
            {
                foreach (var waypoint in route.Waypoints)
                    if (!waypointIds.Add(waypoint.WaypointId))
                        Debug.LogError($"Map route '{route.RouteId}' reuses route waypoint ID " +
                            $"'{waypoint.WaypointId}' used by another route.");

                if (route.Role == MapRouteRole.Branch &&
                    (!points.TryGetValue(route.MergePointId, out var merge) ||
                     merge.Kind != MapPointKind.Merge))
                    Debug.LogError($"Route '{route.RouteId}' references a missing or non-merge point '" +
                        route.MergePointId + "'.");
                else if (route.Role == MapRouteRole.Branch &&
                    points.TryGetValue(route.MergePointId, out var mergePoint))
                {
                    if ((route.EndWaypoint.Position - mergePoint.Position).sqrMagnitude > 0.000001f)
                        Debug.LogError($"Route '{route.RouteId}' does not end at its merge point " +
                            $"'{route.MergePointId}'.");

                    var joinsSharedMain = routes.Values.Any(candidate =>
                        candidate.Role == MapRouteRole.Main &&
                        candidate.Waypoints.Any(waypoint =>
                            (waypoint.Position - mergePoint.Position).sqrMagnitude <= 0.000001f));
                    if (!joinsSharedMain)
                        Debug.LogError($"Route '{route.RouteId}' merge point '{route.MergePointId}' " +
                            "is not represented on a shared main route.");
                }

                foreach (var anchorId in route.BlockingAnchorIds)
                    if (!placementAnchors.ContainsKey(anchorId))
                        Debug.LogError($"Route '{route.RouteId}' references missing anchor '{anchorId}'.");
                foreach (var pointId in route.RelatedPointIds)
                    if (!points.ContainsKey(pointId))
                        Debug.LogError($"Route '{route.RouteId}' references missing point '{pointId}'.");
            }

            foreach (var anchor in placementAnchors.Values)
            {
                if (anchor.Role == PlacementAnchorRole.RouteBlock &&
                    !routes.ContainsKey(anchor.AssociatedRouteId))
                    Debug.LogError($"Anchor '{anchor.AnchorId}' references missing route '" +
                        anchor.AssociatedRouteId + "'.");
                if (anchor.Role != PlacementAnchorRole.RouteBlock &&
                    !points.ContainsKey(anchor.AssociatedPointId))
                    Debug.LogError($"Anchor '{anchor.AnchorId}' references missing point '" +
                        anchor.AssociatedPointId + "'.");
            }
        }

        private bool IsSceneAuthoring(Component component)
        {
            return component != null && component.gameObject.scene.handle == scene.handle;
        }

        private static bool HasPointOutsideBounds(MapRouteSnapshot route, MapBounds bounds)
        {
            for (var i = 0; i < route.Waypoints.Count; i++)
                if (!bounds.Contains(route.Waypoints[i].Position)) return true;
            return false;
        }

        private bool IsSolidStaticCollider(Collider2D collider)
        {
            if (collider == null || collider.isTrigger) return false;
            if (collider.gameObject.scene.handle != scene.handle) return false;
            var body = collider.attachedRigidbody;
            return body == null || body.bodyType == RigidbodyType2D.Static;
        }
    }
}
