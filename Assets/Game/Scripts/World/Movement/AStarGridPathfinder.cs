using System;
using System.Collections.Generic;
using UnityEngine;

namespace SlimeBattle.World.Movement
{
    internal static class AStarGridPathfinder
    {
        public const float CellSize = 0.5f;
        private const float SampleSpacing = 0.25f;
        private const int SnapRadiusCells = 3;
        private static readonly int[,] NeighborOffsets =
        {
            { 1, 0 }, { 0, 1 }, { -1, 0 }, { 0, -1 },
            { 1, 1 }, { -1, 1 }, { -1, -1 }, { 1, -1 }
        };

        private readonly struct Cell : IEquatable<Cell>
        {
            public Cell(int x, int y) { X = x; Y = y; }
            public int X { get; }
            public int Y { get; }
            public bool Equals(Cell other) => X == other.X && Y == other.Y;
            public override bool Equals(object obj) => obj is Cell other && Equals(other);
            public override int GetHashCode() => (X * 397) ^ Y;
        }

        private readonly struct QueueEntry : IComparable<QueueEntry>
        {
            public QueueEntry(Cell cell, float cost, float priority, int order)
            { Cell = cell; Cost = cost; Priority = priority; Order = order; }
            public Cell Cell { get; }
            public float Cost { get; }
            public float Priority { get; }
            private int Order { get; }
            public int CompareTo(QueueEntry other)
            {
                var comparison = Priority.CompareTo(other.Priority);
                if (comparison != 0) return comparison;
                comparison = Cost.CompareTo(other.Cost);
                return comparison != 0 ? comparison : Order.CompareTo(other.Order);
            }
        }

        public static bool TryFindPath(Vector2 start, Vector2 destination,
            MapBounds bounds, Func<Vector2, bool> canOccupy,
            out IReadOnlyList<Vector2> waypoints)
        {
            waypoints = Array.Empty<Vector2>();
            var size = bounds.Size;
            if (canOccupy == null || size.x <= 0f || size.y <= 0f ||
                !canOccupy(start)) return false;

            if (HasLineOfSight(start, destination, canOccupy))
            {
                waypoints = new[] { destination };
                return true;
            }

            var width = Mathf.CeilToInt(size.x / CellSize);
            var height = Mathf.CeilToInt(size.y / CellSize);
            var passable = new Dictionary<Cell, bool>();
            bool IsPassable(Cell cell)
            {
                if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height)
                    return false;
                if (!passable.TryGetValue(cell, out var result))
                {
                    result = canOccupy(ToWorld(cell, bounds));
                    passable.Add(cell, result);
                }
                return result;
            }

            if (!TryFindNearbyCell(start, bounds, width, height, IsPassable,
                canOccupy, requireLineOfSight: true, out var startCell) ||
                !TryFindNearbyCell(destination, bounds, width, height, IsPassable,
                    canOccupy, requireLineOfSight: canOccupy(destination),
                    out var goalCell)) return false;

            var open = new SortedSet<QueueEntry>();
            var cameFrom = new Dictionary<Cell, Cell>();
            var bestCost = new Dictionary<Cell, float> { [startCell] = 0f };
            var closed = new HashSet<Cell>();
            var order = 0;
            open.Add(new QueueEntry(startCell, 0f,
                Heuristic(startCell, goalCell), order++));

            while (open.Count > 0)
            {
                var entry = open.Min;
                open.Remove(entry);
                var current = entry.Cell;
                if (closed.Contains(current) ||
                    entry.Cost > bestCost[current] + 0.0001f) continue;
                if (current.Equals(goalCell))
                {
                    waypoints = BuildSmoothedPath(start, destination, bounds,
                        current, startCell, cameFrom, canOccupy);
                    return waypoints.Count > 0;
                }
                closed.Add(current);

                for (var i = 0; i < NeighborOffsets.GetLength(0); i++)
                {
                    var dx = NeighborOffsets[i, 0];
                    var dy = NeighborOffsets[i, 1];
                    var next = new Cell(current.X + dx, current.Y + dy);
                    if (closed.Contains(next) || !IsPassable(next)) continue;
                    if (dx != 0 && dy != 0 &&
                        (!IsPassable(new Cell(current.X + dx, current.Y)) ||
                         !IsPassable(new Cell(current.X, current.Y + dy)))) continue;

                    var stepCost = dx != 0 && dy != 0
                        ? CellSize * 1.41421356f : CellSize;
                    var candidateCost = bestCost[current] + stepCost;
                    if (bestCost.TryGetValue(next, out var previousCost) &&
                        candidateCost >= previousCost) continue;
                    bestCost[next] = candidateCost;
                    cameFrom[next] = current;
                    open.Add(new QueueEntry(next, candidateCost,
                        candidateCost + Heuristic(next, goalCell), order++));
                }
            }

            return false;
        }

        private static bool TryFindNearbyCell(Vector2 anchor, MapBounds bounds,
            int width, int height, Func<Cell, bool> isPassable,
            Func<Vector2, bool> canOccupy, bool requireLineOfSight,
            out Cell selected)
        {
            var origin = ToCell(anchor, bounds, width, height);
            var bestDistance = float.PositiveInfinity;
            selected = default(Cell);
            var found = false;
            for (var y = -SnapRadiusCells; y <= SnapRadiusCells; y++)
                for (var x = -SnapRadiusCells; x <= SnapRadiusCells; x++)
                {
                    var candidate = new Cell(origin.X + x, origin.Y + y);
                    if (!isPassable(candidate)) continue;
                    var center = ToWorld(candidate, bounds);
                    if (requireLineOfSight &&
                        !HasLineOfSight(anchor, center, canOccupy)) continue;
                    var distance = (center - anchor).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    selected = candidate;
                    found = true;
                }
            return found;
        }

        private static IReadOnlyList<Vector2> BuildSmoothedPath(Vector2 start,
            Vector2 destination, MapBounds bounds, Cell goal, Cell startCell,
            IReadOnlyDictionary<Cell, Cell> cameFrom, Func<Vector2, bool> canOccupy)
        {
            var cells = new List<Cell> { goal };
            var current = goal;
            while (!current.Equals(startCell))
            {
                if (!cameFrom.TryGetValue(current, out current))
                    return Array.Empty<Vector2>();
                if (!current.Equals(startCell)) cells.Add(current);
            }
            cells.Reverse();

            var rawPath = new List<Vector2>(cells.Count + 2);
            rawPath.Add(ToWorld(startCell, bounds));
            foreach (var cell in cells)
            {
                var point = ToWorld(cell, bounds);
                if ((rawPath[rawPath.Count - 1] - point).sqrMagnitude > 0.0001f)
                    rawPath.Add(point);
            }
            var lastPoint = rawPath.Count == 0 ? start : rawPath[rawPath.Count - 1];
            var endPoint = canOccupy(destination) &&
                HasLineOfSight(lastPoint, destination, canOccupy)
                    ? destination : lastPoint;
            if (rawPath.Count == 0 || (rawPath[rawPath.Count - 1] - endPoint).sqrMagnitude > 0.0001f)
                rawPath.Add(endPoint);

            var result = new List<Vector2>();
            var currentPosition = start;
            var nextIndex = 0;
            while (nextIndex < rawPath.Count)
            {
                var furthestVisible = -1;
                for (var candidate = rawPath.Count - 1; candidate >= nextIndex; candidate--)
                    if (HasLineOfSight(currentPosition, rawPath[candidate], canOccupy))
                    {
                        furthestVisible = candidate;
                        break;
                    }
                if (furthestVisible < 0) return Array.Empty<Vector2>();
                currentPosition = rawPath[furthestVisible];
                result.Add(currentPosition);
                nextIndex = furthestVisible + 1;
            }
            return result.AsReadOnly();
        }

        private static bool HasLineOfSight(Vector2 start, Vector2 end,
            Func<Vector2, bool> canOccupy)
        {
            if (!canOccupy(start) || !canOccupy(end)) return false;
            var distance = Vector2.Distance(start, end);
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / SampleSpacing));
            for (var i = 1; i < steps; i++)
            {
                var point = Vector2.Lerp(start, end, i / (float)steps);
                if (!canOccupy(point)) return false;
            }
            return true;
        }

        private static Cell ToCell(Vector2 point, MapBounds bounds,
            int width, int height) => new Cell(
                Mathf.Clamp(Mathf.FloorToInt((point.x - bounds.Min.x) / CellSize), 0, width - 1),
                Mathf.Clamp(Mathf.FloorToInt((point.y - bounds.Min.y) / CellSize), 0, height - 1));

        private static Vector2 ToWorld(Cell cell, MapBounds bounds) =>
            bounds.Min + new Vector2((cell.X + 0.5f) * CellSize,
                (cell.Y + 0.5f) * CellSize);

        private static float Heuristic(Cell a, Cell b)
        {
            var dx = Mathf.Abs(a.X - b.X);
            var dy = Mathf.Abs(a.Y - b.Y);
            var diagonal = Mathf.Min(dx, dy);
            return CellSize * (Mathf.Max(dx, dy) - diagonal) +
                CellSize * 1.41421356f * diagonal;
        }
    }
}
