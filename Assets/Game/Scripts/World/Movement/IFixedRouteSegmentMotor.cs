using UnityEngine;

namespace SlimeBattle.World.Movement
{
    /// <summary>
    /// Movement port for non-Actor payloads that follow a configured fixed route.
    /// Implementations own the body and collision response; this port reports facts only.
    /// </summary>
    public interface IFixedRouteSegmentMotor
    {
        bool TryGetPosition(out Vector2 position);
        bool SubmitSegment(Vector2 destination, out MovementRequestId requestId);
        bool TryGetResult(MovementRequestId requestId, out RouteSegmentMotorResult result);
        bool CancelSegment(MovementRequestId requestId, CancelReason reason);
    }
}
