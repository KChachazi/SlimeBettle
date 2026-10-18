using System.Collections.Generic;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Slimes;
using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.Actors.AI.DecisionTree
{
    /// <summary>Supplies one authored default route beneath status, guide and combat priorities.</summary>
    public sealed class DefaultRoutePerceptionSource : IActorPerceptionSource
    {
        private const float ArrivalDistance = 0.5f;

        private readonly IActorPerceptionSource inner;
        private readonly SlimeManager slimes;
        private readonly MapRouteSnapshot route;
        private readonly Dictionary<ActorId, int> nextWaypointByActor =
            new Dictionary<ActorId, int>();

        public DefaultRoutePerceptionSource(IActorPerceptionSource inner,
            SlimeManager slimes, MapRouteSnapshot route)
        {
            this.inner = inner;
            this.slimes = slimes;
            this.route = route;
        }

        public bool TryGetActorSnapshot(ActorId actorId, out ActorSnapshot snapshot) =>
            inner.TryGetActorSnapshot(actorId, out snapshot);

        public PerceptionSnapshot GetPerception(ActorId actorId)
        {
            var perception = inner.GetPerception(actorId);
            if (!slimes.TryGetSnapshot(actorId, out var slime) || !slime.IsAlive ||
                slime.IsCarrying || !route.IsValid)
            {
                nextWaypointByActor.Remove(actorId);
                return perception;
            }

            if (!nextWaypointByActor.TryGetValue(actorId, out var nextWaypoint))
                nextWaypoint = 1;
            while (nextWaypoint < route.Waypoints.Count &&
                   (slime.Position - route.Waypoints[nextWaypoint].Position).sqrMagnitude <=
                   ArrivalDistance * ArrivalDistance)
                nextWaypoint++;
            nextWaypointByActor[actorId] = nextWaypoint;

            // Holding at the last checkpoint keeps a route-driven Slime inside authored space.
            if (nextWaypoint >= route.Waypoints.Count) return perception;
            var destination = route.Waypoints[nextWaypoint].Position;
            return new PerceptionSnapshot(perception.SampleTick, perception.Targets,
                hasDestination: true, destination: destination, guides: perception.Guides,
                hasEscapeDestination: perception.HasEscapeDestination,
                escapeDestination: perception.EscapeDestination,
                fleeCheckSucceeded: perception.FleeCheckSucceeded);
        }
    }
}
