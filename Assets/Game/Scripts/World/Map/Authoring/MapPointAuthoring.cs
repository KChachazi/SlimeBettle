using UnityEngine;

namespace SlimeBattle.World
{
    [DisallowMultipleComponent]
    public sealed class MapPointAuthoring : MonoBehaviour
    {
        [SerializeField] private string pointId;
        [SerializeField] private MapPointKind kind;
        [SerializeField] private string configId;

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

            var worldPosition = transform.position;
            snapshot = new MapPointSnapshot(PointId, kind, worldPosition, configId);
            error = string.Empty;
            return true;
        }
    }
}
