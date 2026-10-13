using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using SlimeBattle.Data;
using SlimeBattle.Actors.Slimes;
using SlimeBattle.Actors.Enemies;
using SlimeBattle.Actors.Guards;

namespace SlimeBattle.Core
{
    public sealed class LevelManager : MonoBehaviour, IDisposable
    {
        [SerializeField] private ConfigAsset[] levelConfigs;
        [SerializeField] private SlimeActor slimePrefab;
        [SerializeField] private EnemyActor enemyPrefab;
        [SerializeField] private GuardActor guardPrefab;
        private bool initialized;
        private bool disposed;
        private Coroutine loadCoroutine;
        private LevelContext currentContext;
        private LevelId currentLevelId;
        private LevelState currentState = LevelState.Unloaded;

        public event Action<LevelStateChangedEvent> LevelStateChanged;
        public event Action<LevelLoadStartedEvent> LevelLoadStarted;
        public event Action<LevelStartedEvent> LevelStarted;
        public event Action<LevelRestartedEvent> LevelRestarted;
        public event Action<LevelLoadFailedEvent> LevelLoadFailed;
        public event Action<LevelResultReportedEvent> LevelResultReported;
        public event Action<ReturnToPreviousFlowRequestedEvent> ReturnToPreviousFlowRequested;

        private void Awake()
        {
            Initialize();
        }

        private void Update()
        {
            if (currentState == LevelState.Running) currentContext?.Tick(Time.deltaTime);
        }

        private void FixedUpdate()
        {
            if (currentState == LevelState.Running)
                currentContext?.FixedTick(Time.fixedDeltaTime);
        }

        public void Initialize()
        {
            if (initialized || disposed)
            {
                return;
            }

            initialized = true;
        }

        public void RequestLoadLevel(LevelId levelId)
        {
            if (!CanStartTransition())
            {
                return;
            }

            if (!levelId.IsValid)
            {
                NotifyLoadFailed(levelId, "The requested level ID is empty.");
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(levelId.Value))
            {
                NotifyLoadFailed(levelId, "The requested scene is not available in Build Settings.");
                return;
            }

            loadCoroutine = StartCoroutine(LoadLevelRoutine(levelId, false));
        }

        public void RequestRestartCurrentLevel()
        {
            if (!CanStartTransition() || !currentLevelId.IsValid)
            {
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(currentLevelId.Value))
            {
                NotifyLoadFailed(
                    currentLevelId,
                    "The current scene is not available in Build Settings.");
                return;
            }

            loadCoroutine = StartCoroutine(LoadLevelRoutine(currentLevelId, true));
        }

        public void RequestReturnToPreviousFlow()
        {
            if (!initialized || disposed || !currentLevelId.IsValid)
            {
                return;
            }

            ReturnToPreviousFlowRequested?.Invoke(
                new ReturnToPreviousFlowRequestedEvent(currentLevelId));
        }

        public void RequestPause()
        {
            if (!initialized || disposed || loadCoroutine != null)
            {
                return;
            }

            if (currentState != LevelState.Running)
            {
                return;
            }

            SetState(LevelState.Paused);
        }

        public void RequestResume()
        {
            if (!initialized || disposed || loadCoroutine != null)
            {
                return;
            }

            if (currentState != LevelState.Paused)
            {
                return;
            }

            SetState(LevelState.Running);
        }

        public bool ReportResult(LevelResult result)
        {
            if (!initialized || disposed || loadCoroutine != null)
            {
                return false;
            }

            if (currentState != LevelState.Running || !currentLevelId.IsValid)
            {
                return false;
            }

            SetState(result == LevelResult.Succeeded
                ? LevelState.Succeeded
                : LevelState.Failed);

            LevelResultReported?.Invoke(
                new LevelResultReportedEvent(currentLevelId, result));
            return true;
        }

        public LevelId GetCurrentLevelId()
        {
            return currentLevelId;
        }

        public LevelContext GetCurrentContext()
        {
            return currentContext;
        }

        public LevelState GetLevelState()
        {
            return currentState;
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            if (loadCoroutine != null)
            {
                StopCoroutine(loadCoroutine);
                loadCoroutine = null;
            }

            if (currentContext != null)
            {
                SetState(LevelState.Unloading);
                if (currentContext.ResultRules != null)
                    currentContext.ResultRules.ResultDecided -= OnResultDecided;
                currentContext.Dispose();
                currentContext = null;
            }

            currentLevelId = default(LevelId);
            currentState = LevelState.Unloaded;
            initialized = false;
            disposed = true;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private IEnumerator LoadLevelRoutine(LevelId levelId, bool isRestart)
        {
            if (currentContext != null)
            {
                SetState(LevelState.Unloading);
                if (currentContext.ResultRules != null)
                    currentContext.ResultRules.ResultDecided -= OnResultDecided;
                currentContext.Dispose();
                currentContext = null;
                currentLevelId = default(LevelId);
                SetState(LevelState.Unloaded);
            }

            SetState(LevelState.Loading);
            LevelLoadStarted?.Invoke(new LevelLoadStartedEvent(levelId, isRestart));

            AsyncOperation loadOperation;
            try
            {
                loadOperation = SceneManager.LoadSceneAsync(levelId.Value, LoadSceneMode.Single);
            }
            catch (Exception exception)
            {
                FailActiveLoad(levelId, exception.Message);
                yield break;
            }

            if (loadOperation == null)
            {
                FailActiveLoad(levelId, "Unity did not return a scene load operation.");
                yield break;
            }

            yield return loadOperation;

            currentLevelId = levelId;
            SetState(LevelState.Initializing);
            var registry = new ConfigRegistry(levelConfigs);
            var validation = registry.ValidateAll();
            if (!validation.IsValid || !registry.TryGetLevel(levelId, out var levelConfig))
            {
                FailActiveLoad(levelId, !validation.IsValid
                    ? string.Join("; ", validation.Errors)
                    : "LevelConfig is missing.");
                yield break;
            }

            registry.Freeze();

            try
            {
                currentContext = new LevelContext(levelId, registry, levelConfig,
                    slimePrefab, enemyPrefab, guardPrefab);
                currentContext.Initialize();
                currentContext.ResultRules.ResultDecided += OnResultDecided;
            }
            catch (Exception exception)
            {
                FailActiveLoad(levelId, exception.Message);
                yield break;
            }
            SetState(LevelState.Running);

            loadCoroutine = null;
            LevelStarted?.Invoke(new LevelStartedEvent(levelId, currentContext));

            if (isRestart)
            {
                LevelRestarted?.Invoke(new LevelRestartedEvent(levelId));
            }
        }

        private void OnResultDecided(LevelResult result)
        {
            ReportResult(result);
        }

        private bool CanStartTransition()
        {
            return initialized && !disposed && loadCoroutine == null;
        }

        private void SetState(LevelState nextState)
        {
            if (currentState == nextState)
            {
                return;
            }

            var previousState = currentState;
            currentState = nextState;

            if (currentContext != null)
            {
                currentContext.SetState(nextState);
            }

            LevelStateChanged?.Invoke(
                new LevelStateChangedEvent(currentLevelId, previousState, nextState));
        }

        private void NotifyLoadFailed(LevelId levelId, string reason)
        {
            LevelLoadFailed?.Invoke(new LevelLoadFailedEvent(levelId, reason));
        }

        private void FailActiveLoad(LevelId levelId, string reason)
        {
            currentLevelId = default(LevelId);

            if (currentContext != null)
            {
                currentContext.Dispose();
                currentContext = null;
            }

            SetState(LevelState.Failed);
            loadCoroutine = null;
            LevelLoadFailed?.Invoke(new LevelLoadFailedEvent(levelId, reason));
        }
    }
}
