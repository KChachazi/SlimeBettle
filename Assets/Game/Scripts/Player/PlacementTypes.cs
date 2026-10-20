using SlimeBattle.Mechanisms;
using SlimeBattle.World;
using UnityEngine;

namespace SlimeBattle.Player.Placement
{
    public enum PlacementItemId { None, Food, Furnace, Obstacle }
    public enum PlacementConfirmationMode { Single, Continuous }
    public enum PlacementValidationStatus
    {
        Inactive, NoPosition, Free, Occupied, OutOfBounds, Unavailable, InvalidPosition,
        AnchorFree, AnchorReplaceable, TypeMismatch, ReplacementDenied, ReplacementUnavailable,
        InvalidArea, InventoryEmpty, CapacityReached, Removable, NothingToRemove
    }

    public readonly struct PlacementPreview
    {
        public PlacementPreview(PlacementItemId item, Vector3Int cell, Vector3 center,
            PlacementValidationStatus status, string message, PlacementAnchorSnapshot? anchor = null,
            MechanismId removalTarget = default, float rotationDegrees = 0f)
        {
            Item = item;
            Cell = cell;
            WorldCenter = center;
            Status = status;
            Message = message ?? string.Empty;
            Anchor = anchor;
            RemovalTarget = removalTarget;
            RotationDegrees = rotationDegrees;
        }
        public PlacementItemId Item { get; }
        public Vector3Int Cell { get; }
        public Vector3 WorldCenter { get; }
        public PlacementValidationStatus Status { get; }
        public string Message { get; }
        public PlacementAnchorSnapshot? Anchor { get; }
        public MechanismId RemovalTarget { get; }
        public float RotationDegrees { get; }
        public bool IsAnchor => Anchor.HasValue;
        public bool HasPosition => Status != PlacementValidationStatus.Inactive && Status != PlacementValidationStatus.NoPosition &&
            Status != PlacementValidationStatus.InvalidPosition;
        public bool CanConfirm => Status == PlacementValidationStatus.Free ||
            Status == PlacementValidationStatus.AnchorFree ||
            Status == PlacementValidationStatus.AnchorReplaceable || Status == PlacementValidationStatus.Removable;
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
        public PlacementRequest(PlacementItemId item, Vector3Int cell, Vector3 worldCenter,
            PlacementAnchorId anchorId = default, float rotationDegrees = 0f)
        { Item = item; Cell = cell; WorldCenter = worldCenter; AnchorId = anchorId; RotationDegrees = rotationDegrees; }
        public PlacementItemId Item { get; }
        public Vector3Int Cell { get; }
        public Vector3 WorldCenter { get; }
        public PlacementAnchorId AnchorId { get; }
        public float RotationDegrees { get; }
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
        public PlacementObjectSnapshot(MechanismId id, Vector3 position,
            PlacementItemId item = PlacementItemId.None, float rotationDegrees = 0f)
        { Id = id; Position = position; Item = item; RotationDegrees = rotationDegrees; }
        public MechanismId Id { get; }
        public Vector3 Position { get; }
        public PlacementItemId Item { get; }
        public float RotationDegrees { get; }
    }

    /// <summary>Unknown is distinct from zero. Limits/cooldowns remain owned by the configured backend.</summary>
    public readonly struct PlacementBudgetSnapshot
    {
        public PlacementBudgetSnapshot(int activeCount, int maximumActiveCount)
        {
            HasData = true;
            HasInventory = false;
            ActiveCount = activeCount;
            MaximumActiveCount = maximumActiveCount;
            FoodRemaining = 0;
            FurnaceRemaining = 0;
            ObstacleRemaining = 0;
        }

        public PlacementBudgetSnapshot(int activeCount, int maximumActiveCount,
            int foodRemaining, int furnaceRemaining, int obstacleRemaining)
        {
            HasData = true;
            HasInventory = true;
            ActiveCount = activeCount;
            MaximumActiveCount = maximumActiveCount;
            FoodRemaining = foodRemaining;
            FurnaceRemaining = furnaceRemaining;
            ObstacleRemaining = obstacleRemaining;
        }

        public bool HasData { get; }
        public bool HasInventory { get; }
        public int ActiveCount { get; }
        public int MaximumActiveCount { get; }
        public int FoodRemaining { get; }
        public int FurnaceRemaining { get; }
        public int ObstacleRemaining { get; }

        public int GetRemaining(PlacementItemId item)
        {
            switch (item)
            {
                case PlacementItemId.Food: return FoodRemaining;
                case PlacementItemId.Furnace: return FurnaceRemaining;
                case PlacementItemId.Obstacle: return ObstacleRemaining;
                default: return 0;
            }
        }
    }
}
