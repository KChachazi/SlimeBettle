using System.Collections.Generic;
using SlimeBattle.Mechanisms;

namespace SlimeBattle.Player.Placement
{
    /// <summary>
    /// Read-only bridge for the current master API. Fails explicitly until the mechanism owner
    /// supplies object creation requests; never instantiates authoring objects in the caller.
    /// </summary>
    public sealed class PendingMechanismPlacementBackend : IPlacementBackend
    {
        private readonly MechanismSystem mechanisms;
        public PendingMechanismPlacementBackend(MechanismSystem mechanisms) { this.mechanisms = mechanisms; }
        public bool IsAvailable => false;
        public PlacementResult RequestPlacement(PlacementRequest request)
        { return new PlacementResult(false, default, "Mechanism-owned creation API is not connected."); }
        public PlacementBudgetSnapshot GetBudgetSnapshot() { return default; }
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
