using UnityEngine;

namespace SlimeBattle.World
{
    /// <summary>Scene binding that exposes an offline map definition to MapManager.</summary>
    [DisallowMultipleComponent]
    public sealed class MapLayoutAuthoring : MonoBehaviour
    {
        [SerializeField] private FirstLevelMapDefinition definition;
        [SerializeField] private bool showDebugGeometry = true;

        internal FirstLevelMapDefinition Definition => definition;
        public bool ShowDebugGeometry => showDebugGeometry;

        internal bool TryGetSnapshot(out MapLayoutSnapshot snapshot, out string error)
        {
            snapshot = null;
            if (definition == null)
            {
                error = "Map layout definition is not assigned.";
                return false;
            }
            return definition.TryBuildSnapshot(out snapshot, out error);
        }

        private void OnDrawGizmos()
        {
            if (!showDebugGeometry || definition == null ||
                !definition.TryBuildSnapshot(out var layout, out _)) return;

            var previousColor = Gizmos.color;
            foreach (var area in layout.Areas)
            {
                Gizmos.color = area.Area.Kind == MapAreaKind.Road
                    ? new Color(0.15f, 0.9f, 0.35f, 0.9f)
                    : area.Area.TerrainKind == MapTerrainKind.Swamp
                        ? new Color(0.1f, 0.65f, 1f, 0.95f)
                        : new Color(1f, 0.75f, 0.15f, 0.9f);
                if (area.IsPolygon)
                {
                    for (var i = 0; i < area.Vertices.Count; i++)
                        Gizmos.DrawLine(area.Vertices[i],
                            area.Vertices[(i + 1) % area.Vertices.Count]);
                }
                else Gizmos.DrawWireSphere(area.Center, area.Radius);
            }

            foreach (var route in layout.Routes)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
                for (var i = 1; i < route.Waypoints.Count; i++)
                    Gizmos.DrawLine(route.Waypoints[i - 1].Position,
                        route.Waypoints[i].Position);
                foreach (var waypoint in route.Waypoints)
                    Gizmos.DrawSphere(waypoint.Position, 0.07f);
            }

            foreach (var point in layout.Points)
            {
                Gizmos.color = point.Kind == MapPointKind.EnemySpawn
                    ? Color.red
                    : point.Kind == MapPointKind.Trap
                        ? Color.magenta
                        : Color.white;
                Gizmos.DrawSphere(point.Position, 0.13f);
                if (point.Kind == MapPointKind.Trap && point.InteractionRadiusPixels > 0f)
                    Gizmos.DrawWireSphere(point.Position,
                        point.InteractionRadiusPixels / definition.DesignPixelsPerUnityUnit);
            }
            Gizmos.color = previousColor;
        }
    }
}
