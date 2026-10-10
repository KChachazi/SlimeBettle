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

            public MapAreaAuthoring Authoring { get; }
            public MapAreaSnapshot Snapshot { get; }
        }

        private readonly HashSet<Collider2D> movementBlockers = new HashSet<Collider2D>();
        private readonly Dictionary<MapAreaId, AreaBinding> areas =
            new Dictionary<MapAreaId, AreaBinding>();
        private readonly Dictionary<MapPointId, MapPointSnapshot> points =
            new Dictionary<MapPointId, MapPointSnapshot>();
        private readonly Dictionary<MapPointKind, List<MapPointSnapshot>> pointsByKind =
            new Dictionary<MapPointKind, List<MapPointSnapshot>>();
        private readonly Dictionary<ActorId, HashSet<MapAreaId>> actorAreas =
            new Dictionary<ActorId, HashSet<MapAreaId>>();
        private readonly ILevelEventBus eventBus;
        private Scene scene;
        private MapBounds? mapBounds;
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
                var collider = binding.Authoring == null ? null : binding.Authoring.AreaCollider;
                if (collider == null || !collider.enabled ||
                    !collider.gameObject.activeInHierarchy || !collider.OverlapPoint(worldPosition))
                {
                    continue;
                }

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

        public IReadOnlyList<MapPointSnapshot> GetMapPoints(MapPointKind kind)
        {
            if (!IsBound || !pointsByKind.TryGetValue(kind, out var matches))
                return Array.Empty<MapPointSnapshot>();

            return matches.ToArray();
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
            pointsByKind.Clear();
            actorAreas.Clear();
            mapBounds = null;
            scene = default(Scene);
            disposed = true;
        }

        private HashSet<MapAreaId> GetAreaIdsAt(Vector2 position)
        {
            var result = new HashSet<MapAreaId>();
            foreach (var pair in areas)
            {
                var collider = pair.Value.Authoring == null
                    ? null : pair.Value.Authoring.AreaCollider;
                if (collider != null && collider.enabled &&
                    collider.gameObject.activeInHierarchy && collider.OverlapPoint(position))
                {
                    result.Add(pair.Key);
                }
            }

            return result;
        }

        private void Bind(Scene activeScene)
        {
            if (!activeScene.IsValid() || !activeScene.isLoaded) return;

            scene = activeScene;
            MapBoundsAuthoring foundBounds = null;
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

            foreach (var group in pointsByKind.Values)
                group.Sort((left, right) => string.CompareOrdinal(
                    left.PointId.Value, right.PointId.Value));
        }

        private bool IsSceneAuthoring(Component component)
        {
            return component != null && component.gameObject.scene.handle == scene.handle;
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
