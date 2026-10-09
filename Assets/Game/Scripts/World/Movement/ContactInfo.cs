using UnityEngine;

namespace SlimeBattle.World.Movement
{
    public readonly struct ContactInfo
    {
        public ContactInfo(Collider2D collider, Vector2 point, Vector2 normal)
        {
            Collider = collider;
            Point = point;
            Normal = normal;
        }

        public Collider2D Collider { get; }
        public Vector2 Point { get; }
        public Vector2 Normal { get; }
        public bool IsValid => Collider != null;
    }
}
