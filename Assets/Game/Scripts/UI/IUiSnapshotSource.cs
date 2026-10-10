using SlimeBattle.Core;

namespace SlimeBattle.UI
{
    /// <summary>
    /// Scene adapter queries the public snapshots of ObjectiveSystem and actor managers.
    /// Called once on binding, on level changes, and at most five times per second.
    /// Level state and ID are always read independently from LevelManager.
    /// </summary>
    public interface IUiSnapshotSource
    {
        UiState ReadSnapshot(LevelContext context);
    }
}
