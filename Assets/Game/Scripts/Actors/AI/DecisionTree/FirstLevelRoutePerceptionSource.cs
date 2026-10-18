using System;
using System.Collections.Generic;
using SlimeBattle.Actors.AI.StateMachine;
using SlimeBattle.Mechanisms;
using SlimeBattle.Slimes;
using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.Actors.AI.DecisionTree
{
    /// <summary>Uses authored branches and their blocking anchors; each Slime chooses once at the fork.</summary>
    public sealed class FirstLevelRoutePerceptionSource : IActorPerceptionSource
    {
        private sealed class Progress
        {
            public MapRouteSnapshot Branch;
            public int BranchWaypoint = 1;
            public int MainWaypoint;
            public MechanismId FoodId;
        }

        private readonly IActorPerceptionSource inner;
        private readonly SlimeManager slimes;
        private readonly MechanismSystem mechanisms;
        private readonly MapRouteSnapshot[] branches;
        private readonly MapRouteSnapshot main;
        private readonly Vector2 fork;
        private readonly System.Random random;
        private readonly bool waitForRouteTrigger;
        private readonly float waitRadius;
        private readonly Dictionary<ActorId, Progress> progress = new Dictionary<ActorId, Progress>();
        private readonly Dictionary<ActorId, Vector2> waitCenters =
            new Dictionary<ActorId, Vector2>();
        private readonly Dictionary<ActorId, Vector2> waitDestinations =
            new Dictionary<ActorId, Vector2>();
        private readonly Dictionary<MechanismId, MechanismSnapshot> observedMechanisms =
            new Dictionary<MechanismId, MechanismSnapshot>();
        private bool routesReleased;
        private const float ArrivalDistance = 0.5f;
        private const float WaitArrivalDistance = 0.25f;

        public FirstLevelRoutePerceptionSource(IActorPerceptionSource inner,
            SlimeManager slimes, MapManager map, MechanismSystem mechanisms,
            IReadOnlyList<MapRouteId> orderedBranchIds, MapRouteId mainRouteId,
            System.Random random = null, bool waitForRouteTrigger = false,
            float waitRadius = 1.5f)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.slimes = slimes ?? throw new ArgumentNullException(nameof(slimes));
            this.mechanisms = mechanisms ?? throw new ArgumentNullException(nameof(mechanisms));
            this.waitForRouteTrigger = waitForRouteTrigger;
            this.waitRadius = Mathf.Max(0.1f, Mathf.Abs(waitRadius));
            routesReleased = !waitForRouteTrigger;
            if (map == null || orderedBranchIds == null || orderedBranchIds.Count != 3 ||
                !map.TryGetRoute(mainRouteId, out main) || main.Role != MapRouteRole.Main)
                throw new ArgumentException("Three branches and one main route are required.");
            branches = new MapRouteSnapshot[3];
            for (var i = 0; i < branches.Length; i++)
                if (!map.TryGetRoute(orderedBranchIds[i], out branches[i]) ||
                    branches[i].Role != MapRouteRole.Branch)
                    throw new ArgumentException("A configured branch route is missing.");
            var forks = map.GetMapPoints(MapPointKind.Fork);
            if (forks.Count != 1)
                throw new ArgumentException("Exactly one configured fork is required.");
            fork = forks[0].Position;
            this.random = random ?? new System.Random();
            if (waitForRouteTrigger) CaptureMechanismBaseline();
        }

        public bool RoutesReleased => routesReleased;

        /// <summary>Releases waiting Slimes into the authored three-route decision.</summary>
        public void ReleaseRoutes()
        {
            routesReleased = true;
        }

        public bool TryGetActorSnapshot(ActorId actorId, out ActorSnapshot snapshot) =>
            inner.TryGetActorSnapshot(actorId, out snapshot);

        public PerceptionSnapshot GetPerception(ActorId actorId)
        {
            var baseSnapshot = inner.GetPerception(actorId);
            if (!slimes.TryGetSnapshot(actorId, out var slime) || !slime.IsAlive ||
                slime.IsCarrying)
            {
                progress.Remove(actorId);
                waitCenters.Remove(actorId);
                waitDestinations.Remove(actorId);
                return baseSnapshot;
            }

            ObserveMechanismChanges();

            if (!routesReleased)
            {
                if (TryGetWaitingFoodDestination(slime, out var foodDestination))
                    return WithDestination(baseSnapshot, foodDestination);
                return WithDestination(baseSnapshot, GetWaitDestination(actorId, slime.Position));
            }

            if (!progress.TryGetValue(actorId, out var selected))
            {
                if ((slime.Position - fork).sqrMagnitude > ArrivalDistance * ArrivalDistance)
                    return WithDestination(baseSnapshot, fork);
                selected = ChooseAtFork(slime);
                if (selected == null)
                    return WithDestination(baseSnapshot, fork, clearGuides: true);
                progress.Add(actorId, selected);
            }

            var branch = selected.Branch.Waypoints;
            while (selected.BranchWaypoint < branch.Count &&
                   (slime.Position - branch[selected.BranchWaypoint].Position).sqrMagnitude <=
                   ArrivalDistance * ArrivalDistance)
                selected.BranchWaypoint++;
            if (selected.BranchWaypoint < branch.Count)
                return WithDestination(baseSnapshot, GuideDestination(slime, selected,
                    branch[selected.BranchWaypoint].Position));

            var waypoints = main.Waypoints;
            while (selected.MainWaypoint < waypoints.Count - 1 &&
                   (slime.Position - waypoints[selected.MainWaypoint].Position).sqrMagnitude <=
                   ArrivalDistance * ArrivalDistance)
                selected.MainWaypoint++;
            return WithDestination(baseSnapshot, GuideDestination(slime, selected,
                waypoints[selected.MainWaypoint].Position));
        }

        private Vector2 GetWaitDestination(ActorId actorId, Vector2 currentPosition)
        {
            if (!waitCenters.TryGetValue(actorId, out var center))
            {
                center = currentPosition;
                waitCenters.Add(actorId, center);
            }

            if (!waitDestinations.TryGetValue(actorId, out var destination) ||
                (currentPosition - destination).sqrMagnitude <=
                WaitArrivalDistance * WaitArrivalDistance)
            {
                var angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                var radius = Mathf.Sqrt((float)random.NextDouble()) * waitRadius;
                destination = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                waitDestinations[actorId] = destination;
            }

            return destination;
        }

        private bool TryGetWaitingFoodDestination(SlimeSnapshot slime,
            out Vector2 destination)
        {
            destination = default(Vector2);
            if (slime.PixelsPerUnit <= 0f) return false;

            var available = new List<MechanismSnapshot>();
            foreach (var source in mechanisms.GetActiveSnapshots())
            {
                if (source.Kind != MechanismKind.Food || !source.IsAvailable) continue;
                var range = source.AttractionRadiusPixels / slime.PixelsPerUnit;
                if ((source.Position - slime.Position).sqrMagnitude <= range * range)
                    available.Add(source);
            }
            if (available.Count == 0) return false;
            destination = available[random.Next(available.Count)].Position;
            return true;
        }

        private void ObserveMechanismChanges()
        {
            if (!waitForRouteTrigger || routesReleased) return;

            var current = new Dictionary<MechanismId, MechanismSnapshot>();
            foreach (var snapshot in mechanisms.GetActiveSnapshots())
            {
                current[snapshot.MechanismId] = snapshot;
                if (!observedMechanisms.TryGetValue(snapshot.MechanismId, out var previous))
                {
                    if (snapshot.IsPlayerPlaced && snapshot.Kind == MechanismKind.Food)
                        routesReleased = true;
                    continue;
                }

                if (snapshot.Kind == MechanismKind.Obstacle &&
                    (snapshot.State != previous.State ||
                     snapshot.IsAvailable != previous.IsAvailable))
                    routesReleased = true;
            }

            foreach (var previous in observedMechanisms)
                if (!current.ContainsKey(previous.Key) &&
                    (previous.Value.Kind == MechanismKind.Obstacle ||
                     previous.Value.Kind == MechanismKind.Food))
                    routesReleased = true;

            observedMechanisms.Clear();
            foreach (var snapshot in current)
                observedMechanisms[snapshot.Key] = snapshot.Value;
        }

        private void CaptureMechanismBaseline()
        {
            observedMechanisms.Clear();
            foreach (var snapshot in mechanisms.GetActiveSnapshots())
                observedMechanisms[snapshot.MechanismId] = snapshot;
        }

        private Progress ChooseAtFork(SlimeSnapshot slime)
        {
            var open = new List<int>();
            var obstacles = mechanisms.GetActiveSnapshots();
            for (var routeIndex = 0; routeIndex < branches.Length; routeIndex++)
            {
                var blocked = false;
                foreach (var anchorId in branches[routeIndex].BlockingAnchorIds)
                    foreach (var obstacle in obstacles)
                        if (obstacle.AnchorId == anchorId &&
                            obstacle.Kind == MechanismKind.Obstacle && obstacle.IsAvailable)
                            blocked = true;
                if (!blocked) open.Add(routeIndex);
            }
            if (open.Count == 0) return null;

            var foodRoutes = new List<int>();
            var foodIds = new List<MechanismId>();
            foreach (var food in obstacles)
            {
                if (food.Kind != MechanismKind.Food || !food.IsAvailable ||
                    slime.PixelsPerUnit <= 0f ||
                    (food.Position - slime.Position).sqrMagnitude >
                    Mathf.Pow(food.AttractionRadiusPixels / slime.PixelsPerUnit, 2f))
                    continue;
                var nearest = float.PositiveInfinity;
                var candidate = -1;
                foreach (var index in open)
                {
                    var distance = DistanceToRouteSquared(food.Position, branches[index]);
                    if (distance >= nearest) continue;
                    nearest = distance;
                    candidate = index;
                }
                if (candidate >= 0)
                {
                    foodRoutes.Add(candidate);
                    foodIds.Add(food.MechanismId);
                }
            }
            var foodChoice = foodRoutes.Count > 0 ? random.Next(foodRoutes.Count) : -1;
            var chosen = foodChoice >= 0 ? foodRoutes[foodChoice]
                : open.Contains(1) ? 1 : open[0];
            var branchEnd = branches[chosen].EndWaypoint.Position;
            var mainStart = 0;
            var best = float.PositiveInfinity;
            for (var i = 0; i < main.Waypoints.Count; i++)
            {
                var distance = (main.Waypoints[i].Position - branchEnd).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                mainStart = i;
            }
            return new Progress { Branch = branches[chosen],
                FoodId = foodChoice >= 0 ? foodIds[foodChoice] : default,
                MainWaypoint =
                Mathf.Min(mainStart + 1, main.Waypoints.Count - 1) };
        }

        private Vector2 GuideDestination(SlimeSnapshot slime, Progress selected,
            Vector2 routeDestination)
        {
            var sources = mechanisms.GetActiveSnapshots();
            var availableFood = new List<MechanismSnapshot>();
            MechanismSnapshot furnace = default;
            var furnaceDistance = float.PositiveInfinity;
            foreach (var source in sources)
            {
                if (!source.IsAvailable || slime.PixelsPerUnit <= 0f ||
                    (source.Position - slime.Position).sqrMagnitude >
                    Mathf.Pow(source.AttractionRadiusPixels / slime.PixelsPerUnit, 2f))
                    continue;
                if (source.Kind == MechanismKind.Food)
                    availableFood.Add(source);
                else if (source.Kind == MechanismKind.Furnace)
                {
                    var distance = (source.Position - slime.Position).sqrMagnitude;
                    if (distance >= furnaceDistance) continue;
                    furnace = source;
                    furnaceDistance = distance;
                }
            }
            foreach (var food in availableFood)
                if (food.MechanismId == selected.FoodId) return food.Position;
            if (availableFood.Count > 0)
            {
                var food = availableFood[random.Next(availableFood.Count)];
                selected.FoodId = food.MechanismId;
                return food.Position;
            }
            selected.FoodId = default;
            if (furnace.MechanismId.IsValid)
                return furnace.Position;
            return routeDestination;
        }

        private static float DistanceToRouteSquared(Vector2 position, MapRouteSnapshot route)
        {
            var nearest = float.PositiveInfinity;
            for (var i = 1; i < route.Waypoints.Count; i++)
            {
                var start = route.Waypoints[i - 1].Position;
                var end = route.Waypoints[i].Position;
                var segment = end - start;
                var length = segment.sqrMagnitude;
                var t = length <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(position - start, segment) / length);
                nearest = Mathf.Min(nearest, (position - (start + segment * t)).sqrMagnitude);
            }
            return nearest;
        }

        private static PerceptionSnapshot WithDestination(PerceptionSnapshot source,
            Vector2 destination, bool clearGuides = false) =>
            new PerceptionSnapshot(source.SampleTick, source.Targets, true, destination,
                clearGuides ? null : source.Guides, source.HasEscapeDestination,
                source.EscapeDestination, source.FleeCheckSucceeded);
    }
}
