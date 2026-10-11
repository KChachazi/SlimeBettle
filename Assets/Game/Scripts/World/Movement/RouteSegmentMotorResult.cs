using UnityEngine;

namespace SlimeBattle.World.Movement
{
    public readonly struct RouteSegmentMotorResult
    {
        public RouteSegmentMotorResult(MovementResult result, Vector2 position,
            ContactInfo contactInfo = default(ContactInfo),
            CancelReason cancelReason = CancelReason.Interrupted)
        {
            Result = result;
            Position = position;
            ContactInfo = contactInfo;
            CancelReason = cancelReason;
        }

        public MovementResult Result { get; }
        public Vector2 Position { get; }
        public ContactInfo ContactInfo { get; }
        public CancelReason CancelReason { get; }
    }
}
