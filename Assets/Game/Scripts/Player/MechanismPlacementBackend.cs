using System.Collections.Generic;
using SlimeBattle.Mechanisms;

namespace SlimeBattle.Player.Placement
{
    /// <summary>
    /// Current-level bridge: Placement submits a kind and snapped position;
    /// MechanismSystem owns prefab creation and runtime state.
    /// </summary>
    public sealed class MechanismPlacementBackend : IPlacementBackend,
        IPlacementPreviewInfoProvider, IAnchorReplacementBackend, IPlacementRemovalBackend
    {
        private readonly MechanismSystem mechanisms;
        public MechanismPlacementBackend(MechanismSystem mechanisms) { this.mechanisms = mechanisms; }
        public bool IsAvailable => mechanisms != null &&
            mechanisms.TryGetPlayerConfigId(MechanismKind.Food, out _) &&
            mechanisms.TryGetPlayerConfigId(MechanismKind.Furnace, out _) &&
            mechanisms.TryGetPlayerConfigId(MechanismKind.Obstacle, out _);
        public bool SupportsAnchorReplacement => IsAvailable;
        public bool SupportsRemoval => mechanisms != null;
        public bool TryGetAttractionRadiusWorldUnits(PlacementItemId item,
            out float radius)
        {
            radius = 0f;
            var kind = item == PlacementItemId.Food ? MechanismKind.Food
                : item == PlacementItemId.Furnace ? MechanismKind.Furnace
                : MechanismKind.None;
            return mechanisms != null &&
                mechanisms.TryGetPlayerAttractionRadiusWorldUnits(kind, out radius);
        }
        public PlacementResult RequestRemoval(MechanismId mechanismId)
        {
            var removed = mechanisms != null && mechanisms.RequestRemove(mechanismId);
            return new PlacementResult(removed, mechanismId, removed ? string.Empty : "机关已失效或当前无法拆除。");
        }
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
                ? mechanisms.RequestPlaceAtAnchor(configId, request.AnchorId,
                    request.RotationDegrees)
                : mechanisms.RequestPlace(configId, request.WorldCenter,
                    request.RotationDegrees);
            return new PlacementResult(result.Succeeded, result.MechanismId, result.Reason);
        }
        public PlacementBudgetSnapshot GetBudgetSnapshot()
        {
            if (mechanisms == null) return default;
            return mechanisms.HasCampaignInventory
                ? new PlacementBudgetSnapshot(mechanisms.ActivePlacedMechanismCount,
                    mechanisms.MaximumActivePlacedMechanisms,
                    mechanisms.GetAvailablePlacementStock(MechanismKind.Food),
                    mechanisms.GetAvailablePlacementStock(MechanismKind.Furnace),
                    mechanisms.GetAvailablePlacementStock(MechanismKind.Obstacle))
                : new PlacementBudgetSnapshot(mechanisms.ActivePlacedMechanismCount,
                    mechanisms.MaximumActivePlacedMechanisms);
        }
        public IReadOnlyList<PlacementObjectSnapshot> ReadPlacedObjects()
        {
            var placed = new List<PlacementObjectSnapshot>();
            if (mechanisms != null)
            {
                foreach (var snapshot in mechanisms.GetActiveSnapshots())
                {
                    if (snapshot.IsPlayerPlaced && snapshot.State != MechanismState.Removed && snapshot.State != MechanismState.Exhausted)
                    {
                        var item = snapshot.Kind == MechanismKind.Obstacle
                            ? PlacementItemId.Obstacle
                            : snapshot.Kind == MechanismKind.Food
                                ? PlacementItemId.Food : PlacementItemId.Furnace;
                        placed.Add(new PlacementObjectSnapshot(snapshot.MechanismId,
                            snapshot.Position, item, snapshot.RotationDegrees));
                    }
                }
            }
            return placed.AsReadOnly();
        }
    }
}
