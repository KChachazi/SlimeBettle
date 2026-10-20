using System;
using System.Collections.Generic;
using SlimeBattle.Actors;
using SlimeBattle.Core;
using UnityEngine;

namespace SlimeBattle.World.Movement
{
    /// <summary>
    /// Executes movement requests by setting dynamic Rigidbody2D velocity. Slimes may opt
    /// into A* waypoints; physics still owns collision response for all actors.
    /// </summary>
    public sealed class MovementSystem : IDisposable
    {
        private const float ArrivalTolerance = 0.05f;

        private sealed class ActiveMovement
        {
            private IReadOnlyList<Vector2> waypoints;
            private int waypointIndex;

            public ActiveMovement(MovementRequestId requestId, Vector2 destination,
                IReadOnlyList<Vector2> waypoints)
            {
                RequestId = requestId;
                Destination = destination;
                this.waypoints = waypoints;
            }

            public MovementRequestId RequestId { get; }
            public Vector2 Destination { get; }
            public Vector2 CurrentWaypoint => waypoints == null || waypoints.Count == 0
                ? Destination : waypoints[waypointIndex];
            public Vector2 DesiredDirection { get; set; }
            public void ReplaceWaypoints(IReadOnlyList<Vector2> value)
            {
                waypoints = value;
                waypointIndex = 0;
            }
            public bool AdvanceWaypoint()
            {
                if (waypoints == null || waypointIndex + 1 >= waypoints.Count) return false;
                waypointIndex++;
                return true;
            }
        }

        private sealed class ActorBinding
        {
            public ActorBinding(ActorId actorId, Rigidbody2D body,
                ActorMovementBody collisionRelay, float worldSpeed,
                Action<Vector2> positionUpdated, Func<string, bool> movementCompleted,
                bool requireRoadArea, bool useAStar)
            {
                ActorId = actorId;
                Body = body;
                CollisionRelay = collisionRelay;
                WorldSpeed = worldSpeed;
                PositionUpdated = positionUpdated;
                MovementCompleted = movementCompleted;
                RequireRoadArea = requireRoadArea;
                UseAStar = useAStar;
                State = new MovementState(actorId, MovementResult.None,
                    default(MovementRequestId), body.position, body.position, body.velocity);
            }

            public ActorId ActorId { get; }
            public Rigidbody2D Body { get; }
            public ActorMovementBody CollisionRelay { get; }
            public float WorldSpeed { get; }
            public Action<Vector2> PositionUpdated { get; }
            public Func<string, bool> MovementCompleted { get; }
            public bool RequireRoadArea { get; }
            public bool UseAStar { get; }
            public ActiveMovement Active { get; set; }
            public MovementState State { get; set; }
        }

        private readonly MapManager mapManager;
        private readonly ILevelEventBus eventBus;
        private readonly Func<ActorId, MovementModifierSnapshot> modifierProvider;
        private readonly Dictionary<ActorId, ActorBinding> actors =
            new Dictionary<ActorId, ActorBinding>();
        private readonly Dictionary<MovementRequestId, ActorBinding> activeRequests =
            new Dictionary<MovementRequestId, ActorBinding>();
        private readonly Dictionary<ActorId, Collider2D> actorColliders =
            new Dictionary<ActorId, Collider2D>();
        private bool disposed;
        private bool paused;

        internal MovementSystem(MapManager mapManager, ILevelEventBus eventBus,
            Func<ActorId, MovementModifierSnapshot> modifierProvider = null)
        {
            this.mapManager = mapManager ?? throw new ArgumentNullException(nameof(mapManager));
            this.eventBus = eventBus;
            this.modifierProvider = modifierProvider ?? (_ => MovementModifierSnapshot.Neutral);
        }

        /// <summary>
        /// Registers a spawned actor. Move speed is authored in source-sprite pixels per second
        /// and converted with that sprite's pixels-per-unit value.
        /// </summary>
        internal bool RegisterActor(ActorId actorId, Rigidbody2D body,
            float moveSpeedPixelsPerSecond, float pixelsPerUnit,
            Action<Vector2> positionUpdated, Func<string, bool> movementCompleted,
            bool requireRoadArea = true, bool useAStar = false)
        {
            if (disposed || paused || !actorId.IsValid || body == null ||
                body.GetComponent<Collider2D>() == null || positionUpdated == null ||
                movementCompleted == null || float.IsNaN(moveSpeedPixelsPerSecond) ||
                float.IsInfinity(moveSpeedPixelsPerSecond) || moveSpeedPixelsPerSecond < 0f ||
                float.IsNaN(pixelsPerUnit) || float.IsInfinity(pixelsPerUnit) || pixelsPerUnit <= 0f ||
                actors.ContainsKey(actorId))
            {
                return false;
            }

            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.simulated = true;
            body.velocity = Vector2.zero;

            var collisionRelay = body.GetComponent<ActorMovementBody>();
            if (collisionRelay == null) collisionRelay = body.gameObject.AddComponent<ActorMovementBody>();

            var binding = new ActorBinding(actorId, body, collisionRelay,
                moveSpeedPixelsPerSecond / pixelsPerUnit, positionUpdated, movementCompleted,
                requireRoadArea, useAStar);
            var actorCollider = body.GetComponent<Collider2D>();
            foreach (var otherCollider in actorColliders.Values)
                if (otherCollider != null)
                    Physics2D.IgnoreCollision(actorCollider, otherCollider, true);
            actors.Add(actorId, binding);
            actorColliders.Add(actorId, actorCollider);
            collisionRelay.Bind(this, actorId);
            mapManager.NotifyActorPositionChanged(actorId, body.position);
            return true;
        }

        public MovementRequestId SubmitMovement(MovementRequest request)
        {
            if (disposed || paused || !request.IsValid ||
                !actors.TryGetValue(request.ActorId, out var binding) ||
                binding.Body == null || !binding.Body.simulated || binding.WorldSpeed <= 0f)
            {
                return default(MovementRequestId);
            }

            if (binding.Active != null)
                CancelMovement(binding.Active.RequestId, CancelReason.Superseded);

            IReadOnlyList<Vector2> waypoints = null;
            if (binding.UseAStar)
            {
                if (mapManager.TryGetMapBounds(out var bounds))
                {
                    var collider = binding.Body.GetComponent<Collider2D>();
                    var extents = collider == null ? Vector2.zero :
                        new Vector2(collider.bounds.extents.x, collider.bounds.extents.y);
                    var radius = Mathf.Max(extents.x, extents.y);
                    var occupancy = new OccupancyQuery(radius, requireWithinBounds: true,
                        checkStaticBlockers: true, roadTolerance: 0.2f,
                        ignoreRoadArea: !binding.RequireRoadArea);
                    if (!AStarGridPathfinder.TryFindPath(binding.Body.position,
                        request.Destination, bounds,
                        position => mapManager.CanOccupy(position, occupancy), out waypoints))
                        return default(MovementRequestId);
                }
                else waypoints = new[] { request.Destination };
            }

            var requestId = MovementRequestId.NewId();
            var active = new ActiveMovement(requestId, request.Destination, waypoints);
            binding.Active = active;
            binding.State = new MovementState(binding.ActorId, MovementResult.InProgress,
                requestId, binding.Body.position, request.Destination, binding.Body.velocity);
            activeRequests.Add(requestId, binding);
            return requestId;
        }

        public bool CancelMovement(MovementRequestId requestId, CancelReason reason)
        {
            if (disposed || !requestId.IsValid ||
                !activeRequests.TryGetValue(requestId, out var binding) || binding.Active == null)
            {
                return false;
            }

            var active = binding.Active;
            if (binding.Body != null) binding.Body.velocity = Vector2.zero;
            binding.Active = null;
            activeRequests.Remove(requestId);
            var position = binding.Body == null ? binding.State.Position : binding.Body.position;
            NotifyPositionUpdated(binding, position);
            binding.State = new MovementState(binding.ActorId, MovementResult.Cancelled,
                requestId, position, active.Destination, Vector2.zero,
                cancelReason: reason);
            return true;
        }

        public bool TryGetMovementState(ActorId actorId, out MovementState state)
        {
            if (!disposed && actors.TryGetValue(actorId, out var binding))
            {
                state = binding.State;
                return true;
            }

            state = default(MovementState);
            return false;
        }

        public bool TryGetContactInfo(ActorId actorId, out ContactInfo contactInfo)
        {
            if (!disposed && actors.TryGetValue(actorId, out var binding) &&
                binding.State.HasContact)
            {
                contactInfo = binding.State.ContactInfo;
                return true;
            }

            contactInfo = default(ContactInfo);
            return false;
        }

        internal void FixedTick(float fixedDeltaTime)
        {
            if (disposed || paused || fixedDeltaTime <= 0f ||
                float.IsNaN(fixedDeltaTime) || float.IsInfinity(fixedDeltaTime)) return;

            var snapshot = new List<ActorBinding>(actors.Values);
            foreach (var binding in snapshot)
            {
                var body = binding.Body;
                if (body == null || !body.simulated) continue;

                var position = body.position;
                NotifyPositionUpdated(binding, position);
                if (binding.Active == null)
                {
                    // 碰撞可能为没有移动请求的动态刚体带来速度；空闲 Actor 不应因此持续漂移。
                    body.velocity = Vector2.zero;
                    var previous = binding.State;
                    binding.State = new MovementState(binding.ActorId, previous.Result,
                        previous.RequestId, position, previous.Destination, body.velocity,
                        previous.ContactInfo, previous.CancelReason);
                    continue;
                }

                var active = binding.Active;
                var toDestination = active.CurrentWaypoint - position;
                var distance = toDestination.magnitude;
                if (distance <= ArrivalTolerance)
                {
                    if (active.AdvanceWaypoint())
                    {
                        body.velocity = Vector2.zero;
                        binding.State = new MovementState(binding.ActorId,
                            MovementResult.InProgress, active.RequestId, position,
                            active.Destination, Vector2.zero);
                        continue;
                    }
                    Complete(binding, active, MovementResult.ReachedDestination,
                        default(ContactInfo));
                    continue;
                }

                var modifiers = modifierProvider(binding.ActorId);
                var terrainSpeedMultiplier = mapManager.GetMovementSpeedMultiplier(position);
                var speed = binding.WorldSpeed * modifiers.SpeedMultiplier * terrainSpeedMultiplier;
                if (modifiers.IsRooted || float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f)
                {
                    body.velocity = Vector2.zero;
                    binding.State = new MovementState(binding.ActorId, MovementResult.InProgress,
                        active.RequestId, position, active.Destination, Vector2.zero);
                    continue;
                }

                active.DesiredDirection = toDestination / distance;
                var stepSpeed = Mathf.Min(speed, distance / fixedDeltaTime);
                var candidatePosition = position + active.DesiredDirection *
                    stepSpeed * fixedDeltaTime;
                var collider = body.GetComponent<Collider2D>();
                var extents = collider == null ? Vector2.zero :
                    new Vector2(collider.bounds.extents.x, collider.bounds.extents.y);
                var bodyRadius = Mathf.Max(extents.x, extents.y);
                var occupancy = new OccupancyQuery(bodyRadius,
                    requireWithinBounds: true, checkStaticBlockers: true,
                    roadTolerance: 0.2f, ignoreRoadArea: !binding.RequireRoadArea);
                if (!mapManager.CanOccupy(candidatePosition, occupancy))
                {
                    if (TryReplan(binding, active))
                    {
                        body.velocity = Vector2.zero;
                        binding.State = new MovementState(binding.ActorId,
                            MovementResult.InProgress, active.RequestId, position,
                            active.Destination, Vector2.zero);
                        continue;
                    }
                    Complete(binding, active, MovementResult.Blocked,
                        default(ContactInfo));
                    continue;
                }

                body.velocity = active.DesiredDirection * stepSpeed;
                binding.State = new MovementState(binding.ActorId, MovementResult.InProgress,
                    active.RequestId, position, active.Destination, body.velocity);
            }
        }

        internal void SyncPositions()
        {
            if (disposed) return;
            foreach (var binding in actors.Values)
            {
                if (binding.Body == null || !binding.Body.simulated) continue;
                var previous = binding.State;
                var position = binding.Body.position;
                NotifyPositionUpdated(binding, position);
                binding.State = new MovementState(binding.ActorId, previous.Result,
                    previous.RequestId, position, previous.Destination,
                    binding.Body.velocity, previous.ContactInfo, previous.CancelReason);
            }
        }

        internal void Pause()
        {
            if (disposed || paused) return;
            paused = true;
            var requests = new List<MovementRequestId>(activeRequests.Keys);
            foreach (var requestId in requests) CancelMovement(requestId, CancelReason.Pause);
            foreach (var binding in actors.Values)
                if (binding.Body != null) binding.Body.velocity = Vector2.zero;
        }

        internal void Resume()
        {
            if (!disposed) paused = false;
        }

        internal void UnregisterActor(ActorId actorId, CancelReason reason = CancelReason.Despawn)
        {
            if (disposed || !actors.TryGetValue(actorId, out var binding)) return;
            if (binding.Active != null) CancelMovement(binding.Active.RequestId, reason);
            if (binding.CollisionRelay != null) binding.CollisionRelay.Unbind(this);
            if (binding.Body != null)
            {
                if (actorColliders.TryGetValue(actorId, out var actorCollider) &&
                    actorCollider != null)
                    foreach (var otherCollider in actorColliders.Values)
                        if (otherCollider != null && otherCollider != actorCollider)
                            Physics2D.IgnoreCollision(actorCollider, otherCollider, false);
                binding.Body.velocity = Vector2.zero;
                binding.Body.simulated = false;
            }
            actorColliders.Remove(actorId);
            mapManager.NotifyActorRemoved(actorId);
            actors.Remove(actorId);
        }

        public void Dispose()
        {
            if (disposed) return;
            var actorIds = new List<ActorId>(actors.Keys);
            foreach (var actorId in actorIds) UnregisterActor(actorId, CancelReason.Disposed);
            activeRequests.Clear();
            disposed = true;
        }

        internal void NotifyCollision(ActorId actorId, Collider2D firstCollider,
            Collider2D secondCollider, Vector2 point, Vector2 normal)
        {
            if (disposed || paused || !actors.TryGetValue(actorId, out var binding) ||
                binding.Active == null) return;

            var obstacle = mapManager.IsMovementBlocker(firstCollider) ? firstCollider
                : mapManager.IsMovementBlocker(secondCollider) ? secondCollider : null;
            if (obstacle == null) return;

            var active = binding.Active;
            var toContact = point - binding.Body.position;
            if (toContact.sqrMagnitude > 0.0001f &&
                Vector2.Dot(active.DesiredDirection, toContact.normalized) <= 0.01f)
            {
                return;
            }

            if (TryReplan(binding, active))
            {
                binding.Body.velocity = Vector2.zero;
                binding.State = new MovementState(binding.ActorId,
                    MovementResult.InProgress, active.RequestId, binding.Body.position,
                    active.Destination, Vector2.zero);
                return;
            }

            Complete(binding, active, MovementResult.Blocked,
                new ContactInfo(obstacle, point, normal));
        }

        private bool TryReplan(ActorBinding binding, ActiveMovement active)
        {
            if (!binding.UseAStar || binding.Body == null ||
                !mapManager.TryGetMapBounds(out var bounds)) return false;
            var collider = binding.Body.GetComponent<Collider2D>();
            var extents = collider == null ? Vector2.zero :
                new Vector2(collider.bounds.extents.x, collider.bounds.extents.y);
            var occupancy = new OccupancyQuery(Mathf.Max(extents.x, extents.y),
                requireWithinBounds: true, checkStaticBlockers: true,
                roadTolerance: 0.2f, ignoreRoadArea: !binding.RequireRoadArea);
            if (!AStarGridPathfinder.TryFindPath(binding.Body.position,
                active.Destination, bounds,
                position => mapManager.CanOccupy(position, occupancy), out var waypoints) ||
                waypoints == null || waypoints.Count == 0) return false;
            active.ReplaceWaypoints(waypoints);
            return true;
        }

        private void Complete(ActorBinding binding, ActiveMovement active,
            MovementResult result, ContactInfo contactInfo)
        {
            if (binding.Active != active) return;

            var position = binding.Body == null ? binding.State.Position : binding.Body.position;
            if (binding.Body != null) binding.Body.velocity = Vector2.zero;
            binding.Active = null;
            activeRequests.Remove(active.RequestId);
            NotifyPositionUpdated(binding, position);
            binding.State = new MovementState(binding.ActorId, result,
                active.RequestId, position, active.Destination, Vector2.zero, contactInfo);

            if (result == MovementResult.Blocked)
            {
                eventBus?.Publish(new MovementBlockedEvent(binding.ActorId,
                    active.RequestId, position, contactInfo));
            }
            binding.MovementCompleted?.Invoke(active.RequestId.Value);
        }

        private void NotifyPositionUpdated(ActorBinding binding, Vector2 position)
        {
            binding.PositionUpdated?.Invoke(position);
            mapManager.NotifyActorPositionChanged(binding.ActorId, position);
        }
    }
}
