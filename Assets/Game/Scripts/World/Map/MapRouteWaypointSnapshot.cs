using UnityEngine;

namespace SlimeBattle.World
{
    public readonly struct MapRouteWaypointSnapshot
    {
        public MapRouteWaypointSnapshot(MapRouteWaypointId waypointId, int sequence,
            Vector2 position, bool isGoal)
        {
            WaypointId = waypointId;
            Sequence = sequence;
            Position = position;
            IsGoal = isGoal;
        }

        public MapRouteWaypointId WaypointId { get; }
        public int Sequence { get; }
        public Vector2 Position { get; }
        public bool IsGoal { get; }
    }
}
