using UnityEngine;

namespace SlimeBattle.World
{
    [DisallowMultipleComponent]
    public sealed class MapPointAuthoring : MonoBehaviour
    {
        [SerializeField] private string pointId;
        [SerializeField] private MapPointKind kind;
        [SerializeField] private string configId;
        [SerializeField, Min(0f)] private float interactionRadiusPixels;

        internal MapPointId PointId => new MapPointId(pointId);
        internal MapPointKind Kind => kind;

        internal bool TryGetSnapshot(out MapPointSnapshot snapshot, out string error)
        {
            snapshot = default(MapPointSnapshot);
            if (!PointId.IsValid)
            {
                error = "Point ID is empty.";
                return false;
            }

            if (!System.Enum.IsDefined(typeof(MapPointKind), kind))
            {
                error = "Point kind is invalid.";
                return false;
            }

            if (float.IsNaN(interactionRadiusPixels) ||
                float.IsInfinity(interactionRadiusPixels) || interactionRadiusPixels < 0f)
            {
                error = "Interaction radius must be finite and non-negative.";
                return false;
            }

            var worldPosition = transform.position;
            snapshot = new MapPointSnapshot(PointId, kind, worldPosition, configId,
                interactionRadiusPixels);
            error = string.Empty;
            return true;
        }
    }
}
