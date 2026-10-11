using System.Collections.Generic;
using NUnit.Framework;
using SlimeBattle.World;
using SlimeBattle.World.Movement;
using UnityEngine;

namespace SlimeBattle.Tests
{
    public sealed class FixedRouteMovementAdapterEditModeTests
    {
        private sealed class FakeMotor : IFixedRouteSegmentMotor
        {
            private readonly Dictionary<string, RouteSegmentMotorResult> results =
                new Dictionary<string, RouteSegmentMotorResult>();

            public Vector2 Position;
            public readonly List<Vector2> SubmittedDestinations = new List<Vector2>();
            public MovementRequestId LastRequestId { get; private set; }

            public bool TryGetPosition(out Vector2 position)
            {
                position = Position;
                return true;
            }

            public bool SubmitSegment(Vector2 destination, out MovementRequestId requestId)
            {
                requestId = MovementRequestId.NewId();
                LastRequestId = requestId;
                SubmittedDestinations.Add(destination);
                return true;
            }

            public bool TryGetResult(MovementRequestId requestId,
                out RouteSegmentMotorResult result) => results.TryGetValue(requestId.Value, out result);

            public bool CancelSegment(MovementRequestId requestId, CancelReason reason)
            {
                if (requestId != LastRequestId) return false;
                results[requestId.Value] = new RouteSegmentMotorResult(
                    MovementResult.Cancelled, Position, cancelReason: reason);
                return true;
            }

            public void Complete(MovementResult result, Vector2 position,
                ContactInfo contact = default(ContactInfo))
            {
                Position = position;
                results[LastRequestId.Value] = new RouteSegmentMotorResult(result,
                    position, contact);
            }
        }

        [Test]
        public void DriverSubmitsSegmentsInOrderAndLeavesBlockedPolicyToCaller()
        {
            var route = CreateRoute();
            var motor = new FakeMotor { Position = Vector2.zero };
            var adapter = new FixedRouteMovementAdapter(route, motor);

            Assert.IsTrue(adapter.Start());
            Assert.AreEqual(FixedRouteMovementState.Moving, adapter.State);
            Assert.AreEqual(new Vector2(2f, 0f), motor.SubmittedDestinations[0]);

            motor.Complete(MovementResult.ReachedDestination, new Vector2(2f, 0f));
            Assert.IsTrue(adapter.Tick());
            Assert.AreEqual(new Vector2(2f, 2f), motor.SubmittedDestinations[1]);
            Assert.AreEqual(2, adapter.DestinationWaypointIndex);

            motor.Complete(MovementResult.Blocked, new Vector2(1.5f, 2f));
            Assert.IsTrue(adapter.Tick());
            Assert.AreEqual(FixedRouteMovementState.Blocked, adapter.State);
            Assert.AreEqual(2, motor.SubmittedDestinations.Count);
            Assert.IsFalse(adapter.Tick());

            Assert.IsTrue(adapter.RetryBlockedSegment());
            Assert.AreEqual(new Vector2(2f, 2f), motor.SubmittedDestinations[2]);
            motor.Complete(MovementResult.ReachedDestination, new Vector2(2f, 2f));
            Assert.IsTrue(adapter.Tick());
            Assert.AreEqual(FixedRouteMovementState.Completed, adapter.State);
            Assert.IsTrue(route.GoalWaypoint.IsGoal);
        }

        [Test]
        public void DriverRejectsMotorThatDoesNotStartAtConfiguredRouteStart()
        {
            var motor = new FakeMotor { Position = Vector2.one };
            var adapter = new FixedRouteMovementAdapter(CreateRoute(), motor);

            Assert.IsFalse(adapter.Start());
            Assert.AreEqual(FixedRouteMovementState.Rejected, adapter.State);
            Assert.IsEmpty(motor.SubmittedDestinations);
        }

        private static MapRouteSnapshot CreateRoute()
        {
            return new MapRouteSnapshot(new MapRouteId("test_route"), new[]
            {
                new MapRouteWaypointSnapshot(new MapRouteWaypointId("start"),
                    0, Vector2.zero, false),
                new MapRouteWaypointSnapshot(new MapRouteWaypointId("turn"),
                    1, new Vector2(2f, 0f), false),
                new MapRouteWaypointSnapshot(new MapRouteWaypointId("goal"),
                    2, new Vector2(2f, 2f), true)
            });
        }
    }
}
