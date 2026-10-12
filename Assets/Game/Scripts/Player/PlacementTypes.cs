using SlimeBattle.Mechanisms;
using UnityEngine;

namespace SlimeBattle.Player.Placement
{
    public enum PlacementItemId { None, Food, Furnace, Obstacle }
    public enum PlacementConfirmationMode { Single, Continuous }
    public enum PlacementValidationStatus { Inactive, NoPosition, Free, Occupied, OutOfBounds, Unavailable, InvalidPosition }

    public readonly struct PlacementPreview
    {
        public PlacementPreview(PlacementItemId item, Vector3Int cell, Vector3 center,
            PlacementValidationStatus status, string message)
        {
            Item = item;
            Cell = cell;
            WorldCenter = center;
            Status = status;
            Message = message ?? string.Empty;
        }
        public PlacementItemId Item { get; }
        public Vector3Int Cell { get; }
        public Vector3 WorldCenter { get; }
        public PlacementValidationStatus Status { get; }
        public string Message { get; }
        public bool HasPosition => Status != PlacementValidationStatus.Inactive && Status != PlacementValidationStatus.NoPosition &&
            Status != PlacementValidationStatus.InvalidPosition;
        public bool CanConfirm => Status == PlacementValidationStatus.Free;
    }

    public readonly struct PlacementValidationResult
    {
        public PlacementValidationResult(PlacementPreview preview) { Preview = preview; }
        public PlacementPreview Preview { get; }
        public bool IsValid => Preview.CanConfirm;
        public string Reason => Preview.Message;
    }

    public readonly struct PlacementRequest
    {
        public PlacementRequest(PlacementItemId item, Vector3Int cell, Vector3 worldCenter)
        { Item = item; Cell = cell; WorldCenter = worldCenter; }
        public PlacementItemId Item { get; }
        public Vector3Int Cell { get; }
        public Vector3 WorldCenter { get; }
    }

    public readonly struct PlacementResult
    {
        public PlacementResult(bool succeeded, MechanismId mechanismId, string reason)
        { Succeeded = succeeded; MechanismId = mechanismId; Reason = reason ?? string.Empty; }
        public bool Succeeded { get; }
        public MechanismId MechanismId { get; }
        public string Reason { get; }
    }

    public readonly struct PlacementObjectSnapshot
    {
        public PlacementObjectSnapshot(MechanismId id, Vector3 position) { Id = id; Position = position; }
        public MechanismId Id { get; }
        public Vector3 Position { get; }
    }

    /// <summary>Unknown is distinct from zero. Limits/cooldowns remain owned by the configured backend.</summary>
    public readonly struct PlacementBudgetSnapshot
    {
        public PlacementBudgetSnapshot(int activeCount, int maximumActiveCount)
        { HasData = true; ActiveCount = activeCount; MaximumActiveCount = maximumActiveCount; }
        public bool HasData { get; }
        public int ActiveCount { get; }
        public int MaximumActiveCount { get; }
    }
}
