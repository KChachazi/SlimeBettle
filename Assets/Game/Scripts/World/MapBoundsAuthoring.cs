using UnityEngine;

namespace SlimeBattle.World
{
    [DisallowMultipleComponent]
    public sealed class MapBoundsAuthoring : MonoBehaviour
    {
        [SerializeField] private Vector2 localCenter;
        [SerializeField] private Vector2 localSize = new Vector2(20f, 12f);

        internal bool TryGetWorldBounds(out MapBounds bounds)
        {
            bounds = default(MapBounds);
            if (localSize.x <= 0f || localSize.y <= 0f ||
                float.IsNaN(localSize.x) || float.IsNaN(localSize.y) ||
                float.IsInfinity(localSize.x) || float.IsInfinity(localSize.y) ||
                float.IsNaN(localCenter.x) || float.IsNaN(localCenter.y) ||
                float.IsInfinity(localCenter.x) || float.IsInfinity(localCenter.y))
            {
                return false;
            }

            var halfSize = localSize * 0.5f;
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (var x = -1; x <= 1; x += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    var localCorner = localCenter + new Vector2(halfSize.x * x, halfSize.y * y);
                    var worldCorner = transform.TransformPoint(localCorner);
                    min = Vector2.Min(min, worldCorner);
                    max = Vector2.Max(max, worldCorner);
                }
            }

            if (float.IsNaN(min.x) || float.IsNaN(min.y) ||
                float.IsInfinity(min.x) || float.IsInfinity(min.y) ||
                float.IsNaN(max.x) || float.IsNaN(max.y) ||
                float.IsInfinity(max.x) || float.IsInfinity(max.y) ||
                max.x <= min.x || max.y <= min.y)
            {
                return false;
            }

            bounds = new MapBounds(min, max);
            return true;
        }
    }
}
