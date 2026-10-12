using System;
using System.Collections.Generic;
using SlimeBattle.Core;
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
        private readonly HashSet<Vector3Int> occupied = new HashSet<Vector3Int>();
        private Vector3 pointerWorld;
        private bool hasPosition;
        private bool isDisposed;
        private bool isConfirming;
        private bool hasOccupancyChanges;
        private int revision;
        private PlacementPreview preview;

        public PlacementSystem(PlacementGridAuthoring grid, Func<MapBounds?> readBounds,
            Func<LevelState> readPhase, IPlacementBackend backend, PlacementConfirmationMode confirmationMode)
        {
            this.grid = grid != null ? grid : throw new ArgumentNullException(nameof(grid));
            this.readBounds = readBounds ?? throw new ArgumentNullException(nameof(readBounds));
            this.readPhase = readPhase ?? throw new ArgumentNullException(nameof(readPhase));
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            if (!Enum.IsDefined(typeof(PlacementConfirmationMode), confirmationMode))
            { throw new ArgumentOutOfRangeException(nameof(confirmationMode)); }
            this.confirmationMode = confirmationMode;
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

        public void BeginPlacement(PlacementItemId itemId)
        {
            if (!Enum.IsDefined(typeof(PlacementItemId), itemId) || itemId == PlacementItemId.None)
            { throw new ArgumentOutOfRangeException(nameof(itemId)); }
            if (isDisposed) { throw new ObjectDisposedException(nameof(PlacementSystem)); }
            CancelPlacement();
            if (!IsRunning) { Hint = "Placement is only available while the level is running."; Changed?.Invoke(); return; }
            SelectedItem = itemId;
            preview = new PlacementPreview(itemId, default, default, PlacementValidationStatus.NoPosition, "Point at a map cell, then confirm.");
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
            var changed = hasOccupancyChanges || next.Cell != preview.Cell || next.Status != preview.Status ||
                next.WorldCenter != preview.WorldCenter || next.Item != preview.Item || next.Message != preview.Message;
            preview = next;
            if (changed) { Hint = preview.Message; Changed?.Invoke(); }
            hasOccupancyChanges = false;
            return new PlacementValidationResult(preview);
        }

        public PlacementResult ConfirmPlacement()
        {
            if (isConfirming) { return new PlacementResult(false, default, "A placement request is already in progress."); }
            if (!IsPlacing || !IsRunning || !hasPosition)
            { return Reject("Choose an item and a map cell while the level is running."); }
            preview = Evaluate(pointerWorld);
            if (!preview.CanConfirm) { return Reject(preview.Message); }
            var request = new PlacementRequest(SelectedItem, preview.Cell, preview.WorldCenter);
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
        }

        private PlacementPreview Evaluate(Vector3 worldPosition)
        {
            Vector3Int cell;
            Vector3 center;
            try { cell = grid.WorldToCell(worldPosition); center = grid.GetCellCenter(cell); }
            catch (ArgumentException exception)
            { return new PlacementPreview(SelectedItem, default, default, PlacementValidationStatus.InvalidPosition, exception.Message); }
            var bounds = readBounds();
            if (!bounds.HasValue) { return At(PlacementValidationStatus.Unavailable, "Map bounds are not configured."); }
            if (!bounds.Value.Contains(worldPosition) || !bounds.Value.Contains(center))
            { return At(PlacementValidationStatus.OutOfBounds, "Outside map bounds."); }
            try
            {
                var snapshots = backend.ReadPlacedObjects();
                if (snapshots == null) { return At(PlacementValidationStatus.Unavailable, "Placement snapshots are unavailable."); }
                var nextOccupied = new HashSet<Vector3Int>();
                foreach (var item in snapshots)
                {
                    if (item.Id.IsValid) { nextOccupied.Add(grid.WorldToCell(item.Position)); }
                }
                hasOccupancyChanges = !occupied.SetEquals(nextOccupied);
                occupied.Clear();
                occupied.UnionWith(nextOccupied);
                if (occupied.Contains(cell)) { return At(PlacementValidationStatus.Occupied, "This cell is occupied."); }
                if (!backend.IsAvailable) { return At(PlacementValidationStatus.Unavailable, "Mechanism creation API is not connected."); }
            }
            catch (Exception exception) { return At(PlacementValidationStatus.Unavailable, "Placement data unavailable: " + exception.Message); }
            return At(PlacementValidationStatus.Free, "Free cell. Confirm to submit placement.");

            PlacementPreview At(PlacementValidationStatus status, string message)
            { return new PlacementPreview(SelectedItem, cell, center, status, message); }
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
            Changed?.Invoke();
            Changed = null;
            PlacementCompleted = null;
        }
    }
}
