using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.World.Movement
{
    /// <summary>
    /// Advances a non-Actor motor through the ordered waypoints of a MapManager route.
    /// It does not perform physics or choose a policy for a blocked segment.
    /// </summary>
    public sealed class FixedRouteMovementAdapter
    {
        private const float DefaultStartTolerance = 0.05f;

        private readonly MapRouteSnapshot route;
        private readonly IFixedRouteSegmentMotor motor;
        private readonly float startTolerance;
        private MovementRequestId activeRequestId;
        private int destinationWaypointIndex = 1;
        private Vector2 lastPosition;
        private ContactInfo lastContactInfo;

        public FixedRouteMovementAdapter(MapRouteSnapshot route,
            IFixedRouteSegmentMotor motor, float startTolerance = DefaultStartTolerance)
        {
            this.route = route;
            this.motor = motor;
            this.startTolerance = startTolerance;
            State = FixedRouteMovementState.NotStarted;
        }

        public FixedRouteMovementState State { get; private set; }
        public int DestinationWaypointIndex => destinationWaypointIndex;
        public MovementRequestId ActiveRequestId => activeRequestId;
        public Vector2 LastPosition => lastPosition;
        public ContactInfo LastContactInfo => lastContactInfo;
        public MapRouteSnapshot Route => route;

        public bool Start()
        {
            if (State != FixedRouteMovementState.NotStarted || motor == null ||
                !route.IsValid || !IsFinite(startTolerance) || startTolerance < 0f ||
                !motor.TryGetPosition(out var position) || !IsFinite(position))
            {
                State = FixedRouteMovementState.Rejected;
                return false;
            }

            lastPosition = position;
            var startPosition = route.Waypoints[0].Position;
            if ((position - startPosition).sqrMagnitude > startTolerance * startTolerance)
            {
                State = FixedRouteMovementState.Rejected;
                return false;
            }

            return SubmitCurrentSegment();
        }

        /// <summary>Polls the active segment and submits the next configured waypoint if reached.</summary>
        public bool Tick()
        {
            if (State != FixedRouteMovementState.Moving || !activeRequestId.IsValid ||
                !motor.TryGetResult(activeRequestId, out var result)) return false;

            lastPosition = result.Position;
            lastContactInfo = result.ContactInfo;
            switch (result.Result)
            {
                case MovementResult.InProgress:
                    return false;
                case MovementResult.ReachedDestination:
                    activeRequestId = default(MovementRequestId);
                    destinationWaypointIndex++;
                    if (destinationWaypointIndex >= route.Waypoints.Count)
                    {
                        State = FixedRouteMovementState.Completed;
                        return true;
                    }
                    return SubmitCurrentSegment();
                case MovementResult.Blocked:
                    activeRequestId = default(MovementRequestId);
                    State = FixedRouteMovementState.Blocked;
                    return true;
                case MovementResult.Cancelled:
                    activeRequestId = default(MovementRequestId);
                    State = FixedRouteMovementState.Cancelled;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Explicitly retries the same waypoint after a caller resolves a block.</summary>
        public bool RetryBlockedSegment()
        {
            if (State != FixedRouteMovementState.Blocked) return false;
            return SubmitCurrentSegment();
        }

        public bool Cancel(CancelReason reason = CancelReason.Interrupted)
        {
            if (State == FixedRouteMovementState.Moving && activeRequestId.IsValid)
            {
                if (!motor.CancelSegment(activeRequestId, reason)) return false;
                activeRequestId = default(MovementRequestId);
                State = FixedRouteMovementState.Cancelled;
                return true;
            }

            if (State != FixedRouteMovementState.Blocked) return false;
            State = FixedRouteMovementState.Cancelled;
            return true;
        }

        private bool SubmitCurrentSegment()
        {
            if (destinationWaypointIndex < 1 || destinationWaypointIndex >= route.Waypoints.Count ||
                !motor.SubmitSegment(route.Waypoints[destinationWaypointIndex].Position,
                    out var requestId) || !requestId.IsValid)
            {
                activeRequestId = default(MovementRequestId);
                State = FixedRouteMovementState.Rejected;
                return false;
            }

            activeRequestId = requestId;
            lastContactInfo = default(ContactInfo);
            State = FixedRouteMovementState.Moving;
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Vector2 value) =>
            IsFinite(value.x) && IsFinite(value.y);
    }
}
