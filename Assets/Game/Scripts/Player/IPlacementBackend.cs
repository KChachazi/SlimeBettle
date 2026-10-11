using System.Collections.Generic;

namespace SlimeBattle.Player.Placement
{
    /// <summary>
    /// Scene/Mechanism adapter. Only the backend creates objects. A successful synchronous request
    /// returns a valid ID and exposes the placed object in ReadPlacedObjects before returning.
    /// Snapshots contain active player-placed objects only, never actors or the escort weapon.
    /// Instances belong to one level context; do not reuse them after a restart.
    /// </summary>
    public interface IPlacementBackend
    {
        bool IsAvailable { get; }
        PlacementResult RequestPlacement(PlacementRequest request);
        IReadOnlyList<PlacementObjectSnapshot> ReadPlacedObjects();
        PlacementBudgetSnapshot GetBudgetSnapshot();
    }
}
