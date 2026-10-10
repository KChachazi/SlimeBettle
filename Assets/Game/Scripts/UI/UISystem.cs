using System;
using System.Collections.Generic;
using SlimeBattle.Core;
using SlimeBattle.Player.Input;
using UnityEngine;
using PlayerInputSystem = SlimeBattle.Player.Input.InputSystem;

namespace SlimeBattle.UI
{
    public sealed class UISystem : MonoBehaviour
    {
        [SerializeField] private BattleHudView view;
        private readonly HashSet<UiPanelId> openPanels = new HashSet<UiPanelId>();
        private LevelManager levelManager;
        private PlayerInputSystem inputSystem;
        private IUiSnapshotSource snapshotSource;
        private UiState state;
        private InputSubscriptionToken cancelToken;
        private float nextRefreshTime;
        private bool isBound;
        private bool isRestartPending;
        private bool hasLoggedSourceFailure;
        private string loadFailureHint;

        public event Action<UiPanelOpenedEvent> UiPanelOpened;
        public event Action<UiPanelClosedEvent> UiPanelClosed;
        public event Action<UiCommandSubmittedEvent> UiCommandSubmitted;

        public void Initialize(LevelManager manager, PlayerInputSystem input,
            IUiSnapshotSource source = null, BattleHudView hudView = null)
        {
            Unbind();
            levelManager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
            inputSystem = input;
            snapshotSource = source;
            if (hudView != null) { view = hudView; }
            isRestartPending = false;
            hasLoggedSourceFailure = false;
            loadFailureHint = string.Empty;
            if (isActiveAndEnabled) { Bind(); }
        }

        public void OpenPanel(UiPanelId panelId)
        {
            if (!Enum.IsDefined(typeof(UiPanelId), panelId)) { throw new ArgumentOutOfRangeException(nameof(panelId)); }
            if (openPanels.Add(panelId)) { UiPanelOpened?.Invoke(new UiPanelOpenedEvent(panelId)); }
            Render();
        }

        public void ClosePanel(UiPanelId panelId)
        {
            if (openPanels.Remove(panelId)) { UiPanelClosed?.Invoke(new UiPanelClosedEvent(panelId)); }
            Render();
        }

        public void Refresh(UiState uiState)
        {
            state = uiState;
            SetPanel(UiPanelId.Hud, true);
            SetPanel(UiPanelId.Pause, state.LevelState == LevelState.Paused);
            SetPanel(UiPanelId.Result, state.LevelState == LevelState.Succeeded || state.LevelState == LevelState.Failed);
            Render();
        }

        public UiState GetState() { return state; }

        public void SubmitCommand(UiCommand command)
        {
            var accepted = false;
            if (isBound && levelManager != null && !isRestartPending)
            {
                var before = levelManager.GetLevelState();
                switch (command)
                {
                    case UiCommand.Pause:
                        if (before == LevelState.Running)
                        {
                            levelManager.RequestPause();
                            accepted = levelManager.GetLevelState() == LevelState.Paused;
                        }
                        break;
                    case UiCommand.Resume:
                        if (before == LevelState.Paused)
                        {
                            levelManager.RequestResume();
                            accepted = levelManager.GetLevelState() == LevelState.Running;
                        }
                        break;
                    case UiCommand.Restart:
                        if (before == LevelState.Running || before == LevelState.Paused ||
                            before == LevelState.Succeeded || before == LevelState.Failed)
                        {
                            isRestartPending = true;
                            levelManager.RequestRestartCurrentLevel();
                            accepted = levelManager.GetLevelState() == LevelState.Loading ||
                                levelManager.GetLevelState() == LevelState.Unloading;
                            if (!accepted) { isRestartPending = false; }
                        }
                        break;
                }
            }
            Render();
            UiCommandSubmitted?.Invoke(new UiCommandSubmittedEvent(command, accepted));
        }

        private void OnEnable() { if (levelManager != null) { Bind(); } }
        private void OnDisable() { Unbind(); }

        private void Update()
        {
            if (!isBound || snapshotSource == null || Time.unscaledTime < nextRefreshTime) { return; }
            nextRefreshTime = Time.unscaledTime + 0.2f;
            ReadCurrentState();
        }

        private void Bind()
        {
            if (isBound) { return; }
            // Reconstruct from current snapshots before observing future events.
            ReadCurrentState();
            levelManager.LevelStateChanged += OnLevelStateChanged;
            levelManager.LevelLoadFailed += OnLoadFailed;
            levelManager.LevelStarted += OnLevelStarted;
            if (inputSystem != null) { cancelToken = inputSystem.Subscribe<CancelInputEvent>(OnCancel); }
            isBound = true;
            if (view != null) { view.Bind(this); }
        }

        private void Unbind()
        {
            if (levelManager != null)
            {
                levelManager.LevelStateChanged -= OnLevelStateChanged;
                levelManager.LevelLoadFailed -= OnLoadFailed;
                levelManager.LevelStarted -= OnLevelStarted;
            }
            if (inputSystem != null) { inputSystem.Unsubscribe(cancelToken); }
            cancelToken = default;
            isBound = false;
            if (view != null) { view.Unbind(); }
        }

        private void OnCancel(CancelInputEvent eventData)
        {
            // A lifecycle cancellation must not toggle pause. Only the key edge does.
            if (!eventData.IsUserRequested) { return; }
            var current = levelManager.GetLevelState();
            if (current == LevelState.Running) { SubmitCommand(UiCommand.Pause); }
            else if (current == LevelState.Paused) { SubmitCommand(UiCommand.Resume); }
        }

        private void OnLevelStateChanged(LevelStateChangedEvent eventData) { ReadCurrentState(); }

        private void OnLevelStarted(LevelStartedEvent eventData)
        {
            isRestartPending = false;
            loadFailureHint = string.Empty;
            ReadCurrentState();
        }

        private void OnLoadFailed(LevelLoadFailedEvent eventData)
        {
            isRestartPending = false;
            loadFailureHint = "Load failed: " + eventData.Reason;
            ReadCurrentState();
        }

        private void ReadCurrentState()
        {
            if (levelManager == null) { return; }
            var display = new UiState(default, LevelState.Unloaded, "Waiting for objective data");
            var context = levelManager.GetCurrentContext();
            if (snapshotSource != null && context != null &&
                levelManager.GetLevelState() != LevelState.Unloading)
            {
                try { display = snapshotSource.ReadSnapshot(context); }
                catch (Exception exception)
                {
                    if (!hasLoggedSourceFailure)
                    {
                        Debug.LogWarning("UI snapshot source is unavailable: " + exception.Message, this);
                        hasLoggedSourceFailure = true;
                    }
                }
            }
            Refresh(new UiState(levelManager.GetCurrentLevelId(), levelManager.GetLevelState(),
                display.ObjectiveText, display.SlimeCount, display.EnemyCount,
                string.IsNullOrEmpty(loadFailureHint) ? display.Hint : loadFailureHint, display.ResultText));
        }

        private void SetPanel(UiPanelId panel, bool shouldOpen)
        {
            if (shouldOpen && openPanels.Add(panel)) { UiPanelOpened?.Invoke(new UiPanelOpenedEvent(panel)); }
            else if (!shouldOpen && openPanels.Remove(panel)) { UiPanelClosed?.Invoke(new UiPanelClosedEvent(panel)); }
        }

        private void Render()
        {
            if (view != null)
            {
                view.Render(state, openPanels.Contains(UiPanelId.Hud), openPanels.Contains(UiPanelId.Pause),
                    openPanels.Contains(UiPanelId.Result), isRestartPending);
            }
        }
    }
}
