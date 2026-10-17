using System;
using System.Collections.Generic;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.Player.Placement
{
    /// <summary>Per-level selection, preview and occupancy. Never instantiates a mechanism.</summary>
    public sealed class PlacementSystem : IDisposable
    {
        private readonly PlacementGridAuthoring grid;
        private readonly Func<MapBounds?> readBounds;
        private readonly Func<LevelState> readPhase;
        private readonly IPlacementBackend backend;
        private readonly PlacementConfirmationMode confirmationMode;
        private readonly Func<IReadOnlyList<PlacementAnchorSnapshot>> readAnchors;
        private readonly float anchorPixelsPerWorldUnit;
        private readonly List<PlacementAnchorSnapshot> anchors = new List<PlacementAnchorSnapshot>();
        private readonly List<Vector3> placedPositions = new List<Vector3>();
        private bool hasAnchorChanges;
        private string anchorError;
        private string placementError;
        private readonly HashSet<Vector3Int> occupied = new HashSet<Vector3Int>();
        private Vector3 pointerWorld;
        private bool hasPosition;
        private bool isDisposed;
        private bool isConfirming;
        private bool hasOccupancyChanges;
        private int revision;
        private PlacementPreview preview;

        public PlacementSystem(PlacementGridAuthoring grid, Func<MapBounds?> readBounds,
            Func<LevelState> readPhase, IPlacementBackend backend, PlacementConfirmationMode confirmationMode,
            Func<IReadOnlyList<PlacementAnchorSnapshot>> readAnchors = null, float anchorPixelsPerWorldUnit = 0f)
        {
            this.grid = grid != null ? grid : throw new ArgumentNullException(nameof(grid));
            this.readBounds = readBounds ?? throw new ArgumentNullException(nameof(readBounds));
            this.readPhase = readPhase ?? throw new ArgumentNullException(nameof(readPhase));
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (!Enum.IsDefined(typeof(PlacementConfirmationMode), confirmationMode))
            { throw new ArgumentOutOfRangeException(nameof(confirmationMode)); }
            this.confirmationMode = confirmationMode;
            this.readAnchors = readAnchors;
            this.anchorPixelsPerWorldUnit = anchorPixelsPerWorldUnit;
            grid.ValidateConfiguration();
        }

        public event Action Changed;
        public event Action<PlacementResult> PlacementCompleted;
        public bool IsPlacing => !isDisposed && SelectedItem != PlacementItemId.None;
        public bool IsRunning => !isDisposed && readPhase() == LevelState.Running;
        public PlacementItemId SelectedItem { get; private set; }
        public string Hint { get; private set; } = "Choose Food, Furnace or Obstacle.";
        public PlacementPreview GetCurrentPreview() { return preview; }
        public PlacementBudgetSnapshot GetBudgetSnapshot() { return isDisposed ? default : backend.GetBudgetSnapshot(); }
        public IReadOnlyCollection<Vector3Int> GetOccupiedCells() { return new List<Vector3Int>(occupied).AsReadOnly(); }
        public MapBounds? GetMapBounds() { return isDisposed ? null : readBounds(); }
        public IReadOnlyList<PlacementAnchorSnapshot> GetAnchors() { return anchors.AsReadOnly(); }

        public void BeginPlacement(PlacementItemId itemId)
        {
            if (!Enum.IsDefined(typeof(PlacementItemId), itemId) || itemId == PlacementItemId.None)
            { throw new ArgumentOutOfRangeException(nameof(itemId)); }
            if (isDisposed) { throw new ObjectDisposedException(nameof(PlacementSystem)); }
            CancelPlacement();
            if (!IsRunning) { Hint = "Placement is only available while the level is running."; Changed?.Invoke(); return; }
            SelectedItem = itemId;
            ReadAnchorData();
            ReadPlacedData();
            preview = new PlacementPreview(itemId, default, default, PlacementValidationStatus.NoPosition, "Click/tap a map cell to place it.");
            Hint = preview.Message;
            Changed?.Invoke();
        }

        public PlacementValidationResult PreviewPlacement(Vector3 worldPosition)
        {
            if (!IsPlacing || !IsRunning)
            {
                CancelPlacement();
                return new PlacementValidationResult(preview);
            }
            pointerWorld = worldPosition;
            hasPosition = true;
            var next = Evaluate(worldPosition);
            var changed = hasAnchorChanges || hasOccupancyChanges || next.Cell != preview.Cell || next.Status != preview.Status ||
                next.WorldCenter != preview.WorldCenter || next.Item != preview.Item || next.Message != preview.Message;
            preview = next;
            if (changed) { Hint = preview.Message; Changed?.Invoke(); }
            hasOccupancyChanges = false;
            hasAnchorChanges = false;
            return new PlacementValidationResult(preview);
        }

        public PlacementResult ConfirmPlacement()
        {
            if (isConfirming) { return new PlacementResult(false, default, "A placement request is already in progress."); }
            if (!IsPlacing || !IsRunning || !hasPosition)
            { return Reject("Choose an item and a map cell while the level is running."); }
            preview = Evaluate(pointerWorld);
            if (!preview.CanConfirm) { return Reject(preview.Message); }
            var request = new PlacementRequest(SelectedItem, preview.Cell, preview.WorldCenter,
                preview.Anchor?.AnchorId ?? default);
            var requestRevision = revision;
            PlacementResult result;
            isConfirming = true;
            try
            {
                result = backend.RequestPlacement(request);
                if (result.Succeeded && !result.MechanismId.IsValid)
                { result = new PlacementResult(false, default, "The placement backend returned no valid mechanism ID."); }
            }
            catch (Exception exception) { result = new PlacementResult(false, default, "Placement request failed: " + exception.Message); }
            finally { isConfirming = false; }
            if (isDisposed) { return result; }
            if (!IsRunning) { CancelPlacement(); }
            if (result.Succeeded)
            {
                occupied.Add(request.Cell);
                if (requestRevision == revision)
                {
                    if (confirmationMode == PlacementConfirmationMode.Single) { CancelPlacement(); }
                    else if (IsRunning) { preview = Evaluate(pointerWorld); }
                }
                Hint = "Placed " + request.Item + ".";
            }
            else { Hint = result.Reason; }
            Changed?.Invoke();
            PlacementCompleted?.Invoke(result);
            return result;
        }

        public void CancelPlacement()
        {
            revision++;
            SelectedItem = PlacementItemId.None;
            hasPosition = false;
            pointerWorld = default;
            preview = default;
            Hint = "Choose Food, Furnace or Obstacle.";
            if (!isDisposed) { Changed?.Invoke(); }
        }

        public void Refresh()
        {
            if (isDisposed) { return; }
            if (!IsRunning) { if (IsPlacing) { CancelPlacement(); } return; }
            if (IsPlacing && hasPosition) { PreviewPlacement(pointerWorld); }
            else if (IsPlacing)
            {
                ReadAnchorData(); ReadPlacedData();
                if (hasAnchorChanges || hasOccupancyChanges) { hasAnchorChanges = hasOccupancyChanges = false; Changed?.Invoke(); }
            }
        }

        private PlacementPreview Evaluate(Vector3 worldPosition)
        {
            PlacementAnchorSnapshot? anchor = null;
            Vector3Int cell;
            Vector3 center;
            try { cell = grid.WorldToCell(worldPosition); center = grid.GetCellCenter(cell); }
            catch (ArgumentException exception)
            { return new PlacementPreview(SelectedItem, default, default, PlacementValidationStatus.InvalidPosition, exception.Message); }
            ReadAnchorData();
            ReadPlacedData();
            var bounds = readBounds();
            if (!bounds.HasValue) { return At(PlacementValidationStatus.Unavailable, "Map bounds are not configured."); }
            if (!bounds.Value.Contains(worldPosition))
            { return At(PlacementValidationStatus.OutOfBounds, "Outside map bounds."); }
            if (anchorError != null) { return At(PlacementValidationStatus.Unavailable, anchorError); }
            var nearestDistance = float.PositiveInfinity;
            foreach (var candidate in anchors)
            {
                var distance = Vector2.SqrMagnitude((Vector2)worldPosition - candidate.Position);
                var radius = candidate.SnapRadiusPixels / anchorPixelsPerWorldUnit;
                // Sorted IDs provide a deterministic winner when two distances are equal.
                if (distance <= radius * radius && distance < nearestDistance)
                { nearestDistance = distance; anchor = candidate; }
            }
            if (anchor.HasValue)
            {
                center = new Vector3(anchor.Value.Position.x, anchor.Value.Position.y, grid.GetCellCenter(cell).z);
                cell = grid.WorldToCell(center);
            }
            if (!bounds.Value.Contains(center)) { return At(PlacementValidationStatus.OutOfBounds, "Outside map bounds."); }
            try
            {
                if (placementError != null) { return At(PlacementValidationStatus.Unavailable, placementError); }
                if (anchor.HasValue)
                {
                    var status = GetAnchorStatus(anchor.Value);
                    return At(status, AnchorMessage(anchor.Value, status));
                }
                if (occupied.Contains(cell)) { return At(PlacementValidationStatus.Occupied, "This cell is occupied."); }
                if (!backend.IsAvailable) { return At(PlacementValidationStatus.Unavailable, "Mechanism creation API is not connected."); }
            }
            catch (Exception exception) { return At(PlacementValidationStatus.Unavailable, "Placement data unavailable: " + exception.Message); }
            return At(PlacementValidationStatus.Free, "Free cell. Click/tap to place.");

            PlacementPreview At(PlacementValidationStatus status, string message)
            { return new PlacementPreview(SelectedItem, cell, center, status, message, anchor); }
        }

        private void ReadPlacedData()
        {
            var previousError = placementError;
            try
            {
                var snapshots = backend.ReadPlacedObjects();
                if (snapshots == null) { throw new InvalidOperationException("Placement snapshots are unavailable."); }
                var next = new HashSet<Vector3Int>();
                var nextPositions = new List<Vector3>();
                foreach (var item in snapshots)
                {
                    if (!item.Id.IsValid) { continue; }
                    next.Add(grid.WorldToCell(item.Position)); nextPositions.Add(item.Position);
                }
                hasOccupancyChanges |= !occupied.SetEquals(next);
                var samePositions = placedPositions.Count == nextPositions.Count;
                for (var i = 0; samePositions && i < nextPositions.Count; i++) { samePositions = placedPositions[i] == nextPositions[i]; }
                hasOccupancyChanges |= !samePositions;
                placedPositions.Clear(); placedPositions.AddRange(nextPositions);
                occupied.Clear(); occupied.UnionWith(next); placementError = null;
            }
            catch (Exception exception)
            {
                hasOccupancyChanges |= occupied.Count > 0;
                occupied.Clear(); placedPositions.Clear(); placementError = "Placement data unavailable: " + exception.Message;
            }
            hasOccupancyChanges |= previousError != placementError;
        }

        private void ReadAnchorData()
        {
            if (readAnchors == null) { return; }
            var previousError = anchorError;
            try
            {
                var source = readAnchors();
                if (source == null) { throw new InvalidOperationException("Anchor snapshots are unavailable."); }
                if (source.Count > 0 && (float.IsNaN(anchorPixelsPerWorldUnit) ||
                    float.IsInfinity(anchorPixelsPerWorldUnit) || anchorPixelsPerWorldUnit <= 0f))
                { throw new InvalidOperationException("Anchor design pixels per world unit are not configured."); }
                var next = new List<PlacementAnchorSnapshot>(source);
                next.Sort((a, b) => string.CompareOrdinal(a.AnchorId.Value, b.AnchorId.Value));
                for (var i = 0; i < next.Count; i++)
                {
                    if (!next[i].IsValid || i > 0 && next[i].AnchorId.Equals(next[i - 1].AnchorId))
                    { throw new InvalidOperationException("Anchor snapshots contain invalid or duplicate definitions."); }
                }
                var same = anchors.Count == next.Count;
                for (var i = 0; same && i < next.Count; i++) { same = SameAnchor(anchors[i], next[i]); }
                hasAnchorChanges |= !same;
                anchors.Clear(); anchors.AddRange(next); anchorError = null;
            }
            catch (Exception exception)
            {
                hasAnchorChanges |= anchors.Count > 0;
                anchors.Clear(); anchorError = "Anchor data unavailable: " + exception.Message;
            }
            hasAnchorChanges |= previousError != anchorError;
        }

        private static bool SameAnchor(PlacementAnchorSnapshot a, PlacementAnchorSnapshot b)
        {
            if (!a.AnchorId.Equals(b.AnchorId) || a.Role != b.Role || a.Position != b.Position ||
                a.SnapRadiusPixels != b.SnapRadiusPixels || a.AllowsReplacement != b.AllowsReplacement ||
                !a.AssociatedRouteId.Equals(b.AssociatedRouteId) || !a.AssociatedPointId.Equals(b.AssociatedPointId) ||
                a.AcceptedMechanismKinds.Count != b.AcceptedMechanismKinds.Count) { return false; }
            for (var i = 0; i < a.AcceptedMechanismKinds.Count; i++)
            { if (a.AcceptedMechanismKinds[i] != b.AcceptedMechanismKinds[i]) { return false; } }
            return true;
        }

        /// <summary>Local view feedback from the most recently read map and mechanism snapshots.</summary>
        public PlacementValidationStatus GetAnchorStatus(PlacementAnchorSnapshot anchor)
        {
            if (!IsPlacing || anchorError != null || placementError != null) { return PlacementValidationStatus.Unavailable; }
            var kind = SelectedItem == PlacementItemId.Food ? MechanismKind.Food :
                SelectedItem == PlacementItemId.Furnace ? MechanismKind.Furnace : MechanismKind.Obstacle;
            if (!anchor.Accepts(kind)) { return PlacementValidationStatus.TypeMismatch; }
            foreach (var position in placedPositions)
            {
                if (Vector2.SqrMagnitude((Vector2)position - anchor.Position) <= 0.00000001f)
                {
                    if (!anchor.AllowsReplacement) return PlacementValidationStatus.ReplacementDenied;
                    return backend is IAnchorReplacementBackend replacement &&
                        replacement.SupportsAnchorReplacement
                        ? PlacementValidationStatus.AnchorReplaceable
                        : PlacementValidationStatus.ReplacementUnavailable;
                }
            }
            if (occupied.Contains(grid.WorldToCell(anchor.Position))) { return PlacementValidationStatus.Occupied; }
            try { return backend.IsAvailable ? PlacementValidationStatus.AnchorFree : PlacementValidationStatus.Unavailable; }
            catch (Exception) { return PlacementValidationStatus.Unavailable; }
        }

        private static string AnchorMessage(PlacementAnchorSnapshot anchor, PlacementValidationStatus status)
        {
            var name = "吸附点 " + anchor.AnchorId.Value + "：";
            switch (status)
            {
                case PlacementValidationStatus.AnchorFree: return name + "可放置，点击地图放置。";
                case PlacementValidationStatus.AnchorReplaceable: return name + "可替换现有障碍，点击地图确认。";
                case PlacementValidationStatus.TypeMismatch: return name + "不接受所选机关类型。";
                case PlacementValidationStatus.ReplacementDenied: return name + "已占用，地图配置禁止替换。";
                case PlacementValidationStatus.ReplacementUnavailable: return name + "已占用，当前后端不支持安全替换。";
                case PlacementValidationStatus.Occupied: return name + "所在格已被其他机关占用。";
                default: return name + "机关创建接口未接入。";
            }
        }

        private PlacementResult Reject(string message)
        {
            var result = new PlacementResult(false, default, message);
            Hint = message;
            Changed?.Invoke();
            PlacementCompleted?.Invoke(result);
            return result;
        }

        public void Dispose()
        {
            if (isDisposed) { return; }
            CancelPlacement();
            isDisposed = true;
            occupied.Clear();
            anchors.Clear();
            placedPositions.Clear();
            Changed?.Invoke();
            Changed = null;
            PlacementCompleted = null;
        }
    }
}
