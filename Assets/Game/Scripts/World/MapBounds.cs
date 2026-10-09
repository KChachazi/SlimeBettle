using UnityEngine;

namespace SlimeBattle.World
{
    public readonly struct MapBounds
    {
        public MapBounds(Vector2 min, Vector2 max)
        {
            Min = min;
            Max = max;
        }

        public Vector2 Min { get; }
        public Vector2 Max { get; }
        public Vector2 Size => Max - Min;
        public bool Contains(Vector2 position) =>
            position.x >= Min.x && position.x <= Max.x &&
            position.y >= Min.y && position.y <= Max.y;
    }
}
