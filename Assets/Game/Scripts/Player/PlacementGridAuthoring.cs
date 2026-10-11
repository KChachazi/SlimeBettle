using System;
using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.Player.Placement
{
    /// <summary>Scene-owned rectangular XY Grid; origin, size and orientation are authoring data.</summary>
    [RequireComponent(typeof(Grid)), DisallowMultipleComponent]
    public sealed class PlacementGridAuthoring : MonoBehaviour
    {
        public Grid Grid => GetComponent<Grid>();

        public void ValidateConfiguration()
        {
            var grid = Grid;
            var size = grid.cellSize;
            if (grid.cellLayout != GridLayout.CellLayout.Rectangle || grid.cellSwizzle != GridLayout.CellSwizzle.XYZ ||
                grid.cellGap != Vector3.zero || !IsFinite(size) || size.x <= 0f || size.y <= 0f ||
                !IsFinite(transform.position) || !IsFinite(transform.lossyScale) ||
                transform.lossyScale.x <= 0f || transform.lossyScale.y <= 0f ||
                Vector3.Dot(transform.forward, Vector3.forward) < 0.9999f)
            {
                throw new ArgumentException("Placement requires a finite rectangular XY Grid, positive cell sizes/scales and no cell gaps.");
            }
        }

        public Vector3Int WorldToCell(Vector3 worldPosition)
        {
            if (!IsFinite(worldPosition)) { throw new ArgumentException("Placement position must be finite."); }
            var local = Grid.WorldToLocal(worldPosition);
            if (Mathf.Abs(local.x / Grid.cellSize.x) > 1000000f || Mathf.Abs(local.y / Grid.cellSize.y) > 1000000f)
            { throw new ArgumentException("Placement position exceeds the supported grid coordinate range."); }
            var cell = Grid.WorldToCell(worldPosition);
            return new Vector3Int(cell.x, cell.y, 0);
        }

        public Vector3 GetCellCenter(Vector3Int cell)
        {
            var center = Grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            center.z = transform.position.z;
            return center;
        }
        public Vector3 GetCellCorner(Vector3Int cell) { return Grid.CellToWorld(new Vector3Int(cell.x, cell.y, 0)); }

        public void GetCoveredRange(MapBounds bounds, out Vector3Int minimum, out Vector3Int maximum)
        {
            var a = WorldToCell(new Vector3(bounds.Min.x, bounds.Min.y, transform.position.z));
            var b = WorldToCell(new Vector3(bounds.Max.x, bounds.Min.y, transform.position.z));
            var c = WorldToCell(new Vector3(bounds.Min.x, bounds.Max.y, transform.position.z));
            var d = WorldToCell(new Vector3(bounds.Max.x, bounds.Max.y, transform.position.z));
            minimum = Vector3Int.Min(Vector3Int.Min(a, b), Vector3Int.Min(c, d));
            maximum = Vector3Int.Max(Vector3Int.Max(a, b), Vector3Int.Max(c, d));
        }

        internal static bool IsFinite(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }
    }
}
