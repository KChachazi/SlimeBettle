using SlimeBattle.Core;

namespace SlimeBattle.UI
{
    public enum UiPanelId { Hud, Pause, Result }
    public enum UiCommand { Pause, Resume, Restart }

    /// <summary>Display data, never authoritative gameplay state. -1 means a count is unavailable.</summary>
    public readonly struct UiState
    {
        public UiState(LevelId levelId, LevelState levelState, string objectiveText,
            int slimeCount = -1, int enemyCount = -1, string hint = "", string resultText = "",
            int slimeLimit = -1, string weaponText = "")
        {
            LevelId = levelId;
            LevelState = levelState;
            ObjectiveText = objectiveText ?? string.Empty;
            SlimeCount = slimeCount;
            EnemyCount = enemyCount;
            Hint = hint ?? string.Empty;
            ResultText = resultText ?? string.Empty;
            SlimeLimit = slimeLimit;
            WeaponText = weaponText ?? string.Empty;
        }
        public LevelId LevelId { get; }
        public LevelState LevelState { get; }
        public string ObjectiveText { get; }
        public int SlimeCount { get; }
        public int EnemyCount { get; }
        public string Hint { get; }
        public string ResultText { get; }
        public int SlimeLimit { get; }
        public string WeaponText { get; }
    }

    public readonly struct UiPanelOpenedEvent
    {
        public UiPanelOpenedEvent(UiPanelId panelId) { PanelId = panelId; }
        public UiPanelId PanelId { get; }
    }

    public readonly struct UiPanelClosedEvent
    {
        public UiPanelClosedEvent(UiPanelId panelId) { PanelId = panelId; }
        public UiPanelId PanelId { get; }
    }

    public readonly struct UiCommandSubmittedEvent
    {
        public UiCommandSubmittedEvent(UiCommand command, bool accepted)
        {
            Command = command;
            Accepted = accepted;
        }
        public UiCommand Command { get; }
        public bool Accepted { get; }
    }
}
