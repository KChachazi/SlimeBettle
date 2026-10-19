using System.Collections.Generic;
using SlimeBattle.Mechanisms;

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

    public interface IAnchorReplacementBackend
    {
        bool SupportsAnchorReplacement { get; }
    }

    /// <summary>Optional removal capability. The backend owns cleanup and configured cooldowns.</summary>
    public interface IPlacementRemovalBackend
    {
        bool SupportsRemoval { get; }
        PlacementResult RequestRemoval(MechanismId mechanismId);
    }
}
