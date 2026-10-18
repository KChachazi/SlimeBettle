using SlimeBattle.Data;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEngine;

namespace SlimeBattle.Mechanisms
{
    // The weapon follows Map route facts; it does not submit Actor movement requests.
    internal sealed class WeaponRouteMotor : IFixedRouteSegmentMotor
    {
        private readonly Transform weapon;
        private readonly MapManager map;
        private readonly float maximumSpeed;
        private float speedFactor;
        private readonly OccupancyQuery occupancy;
        private MovementRequestId requestId;
        private Vector2 destination;
        private RouteSegmentMotorResult result;

        public WeaponRouteMotor(Transform weapon, MapManager map, WeaponConfig config)
        {
            this.weapon = weapon;
            this.map = map;
            maximumSpeed = config.TransportSpeedWorldUnits;
            occupancy = new OccupancyQuery(config.FootprintRadiusWorldUnits);
        }

        public void SetSpeedFactor(float factor)
        {
            speedFactor = Mathf.Clamp01(factor);
        }

        public bool TryGetPosition(out Vector2 position)
        {
            position = weapon == null ? default(Vector2) : (Vector2)weapon.position;
            return weapon != null;
        }

        public bool SubmitSegment(Vector2 target, out MovementRequestId id)
        {
            id = default(MovementRequestId);
            if (weapon == null || requestId.IsValid) return false;
            destination = target;
            id = MovementRequestId.NewId();
            requestId = id;
            result = new RouteSegmentMotorResult(MovementResult.InProgress, weapon.position);
            return true;
        }

        public void Tick(float deltaTime)
        {
            if (!requestId.IsValid || result.Result != MovementResult.InProgress ||
                weapon == null || deltaTime <= 0f) return;
            var current = (Vector2)weapon.position;
            var remaining = maximumSpeed * speedFactor * deltaTime;
            var next = current;
            while (remaining > 0f && next != destination)
            {
                var step = Mathf.Min(remaining, 0.1f);
                var candidate = Vector2.MoveTowards(next, destination, step);
                if (!map.CanOccupy(candidate, occupancy))
                {
                    weapon.position = new Vector3(next.x, next.y, weapon.position.z);
                    result = new RouteSegmentMotorResult(MovementResult.Blocked, next);
                    return;
                }
                remaining -= step;
                next = candidate;
            }
            weapon.position = new Vector3(next.x, next.y, weapon.position.z);
            result = new RouteSegmentMotorResult(
                next == destination ? MovementResult.ReachedDestination : MovementResult.InProgress,
                next);
        }

        public bool TryGetResult(MovementRequestId id, out RouteSegmentMotorResult segmentResult)
        {
            segmentResult = default(RouteSegmentMotorResult);
            if (!requestId.IsValid || id != requestId) return false;
            segmentResult = result;
            if (result.Result != MovementResult.InProgress)
                requestId = default(MovementRequestId);
            return true;
        }

        public bool CancelSegment(MovementRequestId id, CancelReason reason)
        {
            if (!requestId.IsValid || id != requestId) return false;
            var position = weapon == null ? default(Vector2) : (Vector2)weapon.position;
            result = new RouteSegmentMotorResult(MovementResult.Cancelled,
                position, cancelReason: reason);
            requestId = default(MovementRequestId);
            return true;
        }
    }
}
