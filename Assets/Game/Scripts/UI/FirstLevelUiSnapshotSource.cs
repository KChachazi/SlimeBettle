using SlimeBattle.Core;
using SlimeBattle.Player.Placement;

namespace SlimeBattle.UI
{
    /// <summary>Read-only display data supplied by the mechanism/Core owners' future public adapters.</summary>
    public readonly struct FirstLevelStatusSnapshot
    {
        public FirstLevelStatusSnapshot(string weaponText, string resultText = "", string goalText = "",
            string waitingText = "", string routeHint = "")
        {
            WeaponText = weaponText ?? string.Empty; ResultText = resultText ?? string.Empty;
            GoalText = goalText ?? string.Empty; WaitingText = waitingText ?? string.Empty;
            RouteHint = routeHint ?? string.Empty;
        }
        public string WeaponText { get; }
        public string ResultText { get; }
        public string GoalText { get; }
        public string WaitingText { get; }
        public string RouteHint { get; }
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
            var limit = slimes != null ? slimes.MaxAliveCount : -1;
            var objective = context.LevelConfig != null &&
                context.LevelConfig.ObjectiveKind == Data.LevelObjectiveKind.EscortWeapon
                ? "Escort the weapon to the goal."
                : "Guide at least 6 slimes into the goal cave.";
            // PlacementSelectionView owns placement hints; the HUD reserves this slot for integration notices.
            var hint = slimes != null && limit != 20 ? "First-level limit should be 20 (configured: " + limit + ")." :
                placement == null ? "Placement module is not connected." : string.Empty;
            return new UiState(context.GetLevelId(), context.GetLevelState(), objective,
                slimes != null ? slimes.GetAliveSnapshots().Count : -1,
                enemies != null ? enemies.GetAliveSnapshots().Count : -1, hint,
                status.ResultText, limit, status.WeaponText, status.GoalText, status.WaitingText,
                !string.IsNullOrEmpty(status.RouteHint) ? status.RouteHint : ReadAnchorHint());
        }

        private string ReadAnchorHint()
        {
            var anchor = placement?.GetCurrentPreview().Anchor;
            if (!anchor.HasValue) { return string.Empty; }
            var value = anchor.Value;
            return "吸附点 " + value.AnchorId.Value + (value.AssociatedRouteId.IsValid ?
                " · 关联路线 " + value.AssociatedRouteId.Value : " · 关联点位 " + value.AssociatedPointId.Value);
        }
    }
}
