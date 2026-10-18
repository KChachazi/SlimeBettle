using System.Collections.Generic;
using SlimeBattle.Mechanisms;

namespace SlimeBattle.Player.Placement
{
    /// <summary>
    /// Current-level bridge: Placement submits a kind and snapped position;
    /// MechanismSystem owns prefab creation and runtime state.
    /// </summary>
    public sealed class MechanismPlacementBackend : IPlacementBackend, IAnchorReplacementBackend
    {
        private readonly MechanismSystem mechanisms;
        public MechanismPlacementBackend(MechanismSystem mechanisms) { this.mechanisms = mechanisms; }
        public bool IsAvailable => mechanisms != null &&
            mechanisms.TryGetPlayerConfigId(MechanismKind.Food, out _) &&
            mechanisms.TryGetPlayerConfigId(MechanismKind.Furnace, out _) &&
            mechanisms.TryGetPlayerConfigId(MechanismKind.Obstacle, out _);
        public bool SupportsAnchorReplacement => IsAvailable;
        public PlacementResult RequestPlacement(PlacementRequest request)
        {
            if (!IsAvailable) return new PlacementResult(false, default, "Mechanism prefabs are not configured.");
            var kind = request.Item == PlacementItemId.Food ? MechanismKind.Food
                : request.Item == PlacementItemId.Furnace ? MechanismKind.Furnace
                : request.Item == PlacementItemId.Obstacle ? MechanismKind.Obstacle
                : MechanismKind.None;
            if (!mechanisms.TryGetPlayerConfigId(kind, out var configId))
                return new PlacementResult(false, default, "The selected mechanism is unavailable.");
            var result = request.AnchorId.IsValid
                ? mechanisms.RequestPlaceAtAnchor(configId, request.AnchorId)
                : mechanisms.RequestPlace(configId, request.WorldCenter);
            return new PlacementResult(result.Succeeded, result.MechanismId, result.Reason);
        }
        public PlacementBudgetSnapshot GetBudgetSnapshot() => mechanisms == null
            ? default : new PlacementBudgetSnapshot(
                mechanisms.ActivePlacedMechanismCount,
                mechanisms.MaximumActivePlacedMechanisms);
        public IReadOnlyList<PlacementObjectSnapshot> ReadPlacedObjects()
        {
            var placed = new List<PlacementObjectSnapshot>();
            if (mechanisms != null)
            {
                foreach (var snapshot in mechanisms.GetActiveSnapshots())
                {
                    if (snapshot.IsPlayerPlaced && snapshot.State != MechanismState.Removed && snapshot.State != MechanismState.Exhausted)
                    { placed.Add(new PlacementObjectSnapshot(snapshot.MechanismId, snapshot.Position)); }
                }
            }
            return placed.AsReadOnly();
        }
    }
}
