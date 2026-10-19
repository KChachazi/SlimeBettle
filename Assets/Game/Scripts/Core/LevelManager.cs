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
        [SerializeField] private CampaignResourceConfig campaignResourceConfig;
        private CampaignResourceLedger campaignResources;
        private bool initialized;
        private bool disposed;
        private Coroutine loadCoroutine;
        private LevelContext currentContext;
        private LevelId currentLevelId;
        private LevelState currentState = LevelState.Unloaded;
        private bool startedCurrentSceneInPlace;

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

        public bool StartNewCampaign()
        {
            if (!TryEnsureCampaignResources(out var error))
            {
                Debug.LogError("LevelManager: " + error, this);
                return false;
            }
            campaignResources.StartNewCampaign();
            return true;
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

            if (!TryEnsureCampaignResources(out var campaignError))
            {
                NotifyLoadFailed(levelId, campaignError);
                return;
            }

            loadCoroutine = StartCoroutine(LoadLevelRoutine(levelId, false));
        }

        /// <summary>Starts the already open scene through the same context and result lifecycle.</summary>
        public bool StartCurrentSceneLevel()
        {
            if (!CanStartTransition() || currentContext != null ||
                currentState != LevelState.Unloaded) return false;
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scene.name))
                return false;
            if (!TryEnsureCampaignResources(out var campaignError))
            {
                NotifyLoadFailed(new LevelId(scene.name), campaignError);
                return false;
            }
            currentLevelId = new LevelId(scene.name);
            startedCurrentSceneInPlace = true;
            SetState(LevelState.Initializing);
            return TryInitializeLoadedScene(currentLevelId, false);
        }

        public void RequestRestartCurrentLevel()
        {
            if (!CanStartTransition() || !currentLevelId.IsValid)
            {
                return;
            }

            if (startedCurrentSceneInPlace)
            {
                loadCoroutine = StartCoroutine(RestartCurrentSceneRoutine());
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
            if (!CanStartTransition() || !currentLevelId.IsValid ||
                (currentState != LevelState.Running && currentState != LevelState.Paused &&
                 currentState != LevelState.Succeeded && currentState != LevelState.Failed))
            {
                return;
            }

            ReturnToPreviousFlowRequested?.Invoke(
                new ReturnToPreviousFlowRequestedEvent(currentLevelId));
        }

        /// <summary>Releases this level before loading an application-owned destination.</summary>
        public bool RequestUnloadToScene(string sceneName)
        {
            if (!CanStartTransition() || currentContext == null || !currentLevelId.IsValid)
                return false;
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName))
            {
                NotifyLoadFailed(currentLevelId, "The destination scene is not available in Build Settings.");
                return false;
            }
            loadCoroutine = StartCoroutine(UnloadToSceneRoutine(sceneName));
            return true;
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

            if (result == LevelResult.Succeeded && campaignResources != null &&
                currentContext != null && currentContext.SlimeManager != null)
                campaignResources.AwardLevel(currentLevelId,
                    currentContext.SlimeManager.GetAliveSnapshots().Count);

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
            startedCurrentSceneInPlace = false;
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
            startedCurrentSceneInPlace = false;
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
            TryInitializeLoadedScene(levelId, isRestart);
        }

        private IEnumerator UnloadToSceneRoutine(string sceneName)
        {
            var previousLevelId = currentLevelId;
            SetState(LevelState.Unloading);
            if (currentContext.ResultRules != null)
                currentContext.ResultRules.ResultDecided -= OnResultDecided;
            currentContext.Dispose();
            currentContext = null;
            currentLevelId = default;
            startedCurrentSceneInPlace = false;
            yield return null; // Finish deferred destruction and assign the transition handle.

            AsyncOperation operation;
            try { operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single); }
            catch (Exception exception)
            {
                FailActiveLoad(previousLevelId, exception.Message);
                yield break;
            }
            if (operation == null)
            {
                FailActiveLoad(previousLevelId, "Unity did not return a scene load operation.");
                yield break;
            }
            yield return operation;
            SetState(LevelState.Unloaded);
            loadCoroutine = null;
        }

        private bool TryInitializeLoadedScene(LevelId levelId, bool isRestart)
        {
            var registry = new ConfigRegistry(levelConfigs);
            var validation = registry.ValidateAll();
            if (!validation.IsValid || !registry.TryGetLevel(levelId, out var levelConfig))
            {
                FailActiveLoad(levelId, !validation.IsValid
                    ? string.Join("; ", validation.Errors)
                    : "LevelConfig is missing.");
                return false;
            }

            if (!TryEnsureCampaignResources(out var campaignError))
            {
                FailActiveLoad(levelId, campaignError);
                return false;
            }
            campaignResources.BeginLevel(levelId, isRestart);

            registry.Freeze();

            try
            {
                currentContext = new LevelContext(levelId, registry, levelConfig,
                    slimePrefab, enemyPrefab, guardPrefab, campaignResources);
                currentContext.Initialize();
                currentContext.ResultRules.ResultDecided += OnResultDecided;
            }
            catch (Exception exception)
            {
                FailActiveLoad(levelId, exception.Message);
                return false;
            }
            SetState(LevelState.Running);

            loadCoroutine = null;
            LevelStarted?.Invoke(new LevelStartedEvent(levelId, currentContext));

            if (isRestart)
            {
                LevelRestarted?.Invoke(new LevelRestartedEvent(levelId));
            }
            return true;
        }

        private IEnumerator RestartCurrentSceneRoutine()
        {
            var levelId = currentLevelId;
            SetState(LevelState.Unloading);
            if (currentContext != null)
            {
                currentContext.ResultRules.ResultDecided -= OnResultDecided;
                currentContext.Dispose();
                currentContext = null;
            }
            SetState(LevelState.Unloaded);
            yield return null; // Let deferred actor and mechanism destruction finish.
            currentLevelId = levelId;
            SetState(LevelState.Initializing);
            TryInitializeLoadedScene(levelId, true);
        }

        private void OnResultDecided(LevelResult result)
        {
            ReportResult(result);
        }

        private bool CanStartTransition()
        {
            return initialized && !disposed && loadCoroutine == null;
        }

        private bool TryEnsureCampaignResources(out string error)
        {
            if (campaignResourceConfig == null)
            {
                error = "CampaignResourceConfig is not assigned.";
                return false;
            }
            if (campaignResources == null)
            {
                if (!campaignResourceConfig.TryValidate(out error)) return false;
                campaignResources = new CampaignResourceLedger(campaignResourceConfig);
            }
            if (!campaignResources.IsStarted) campaignResources.StartNewCampaign();
            error = string.Empty;
            return true;
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
            startedCurrentSceneInPlace = false;

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
