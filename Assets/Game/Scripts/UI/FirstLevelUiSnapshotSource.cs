using SlimeBattle.Core;
using SlimeBattle.Player.Placement;

namespace SlimeBattle.UI
{
    /// <summary>Read-only display data supplied by the mechanism/Core owners' future public adapters.</summary>
    public readonly struct FirstLevelStatusSnapshot
    {
        public FirstLevelStatusSnapshot(string weaponText, string resultText = "")
        { WeaponText = weaponText ?? string.Empty; ResultText = resultText ?? string.Empty; }
        public string WeaponText { get; }
        public string ResultText { get; }
    }

    public interface IFirstLevelStatusSource
    {
        FirstLevelStatusSnapshot ReadSnapshot(LevelContext context);
    }

    public sealed class FirstLevelUiSnapshotSource : IUiSnapshotSource
    {
        private readonly PlacementSystem placement;
        private readonly IFirstLevelStatusSource statusSource;
        public FirstLevelUiSnapshotSource(PlacementSystem placement, IFirstLevelStatusSource statusSource = null)
        { this.placement = placement; this.statusSource = statusSource; }

        public UiState ReadSnapshot(LevelContext context)
        {
            var status = statusSource != null ? statusSource.ReadSnapshot(context) : default;
            var slimes = context.SlimeManager;
            var enemies = context.EnemyManager;
            var limit = slimes != null ? slimes.MaxAliveCount : 20;
            var hint = placement?.Hint ?? "Choose Food, Furnace or Obstacle.";
            if (slimes != null && limit != 20) { hint += " First-level LevelConfig limit must be 20 (current: " + limit + ")."; }
            return new UiState(context.GetLevelId(), context.GetLevelState(), "Escort the weapon to the goal.",
                slimes != null ? slimes.GetAliveSnapshots().Count : -1,
                enemies != null ? enemies.GetAliveSnapshots().Count : -1, hint,
                status.ResultText, limit, string.IsNullOrEmpty(status.WeaponText) ? "ESCORT WEAPON  -- (data not connected)" : status.WeaponText);
        }
    }
}
