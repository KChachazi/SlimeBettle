using System;
using UnityEngine;

namespace SlimeBattle.Core
{
    public sealed class GameRoot : MonoBehaviour
    {
        private static GameRoot instance;

        [SerializeField]
        private LevelManager levelManager;

        private ApplicationState applicationState = ApplicationState.Created;
        private bool initialized;

        public event Action<ApplicationStateChangedEvent> ApplicationStateChanged;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeApplication();
        }

        public void InitializeApplication()
        {
            if (initialized)
            {
                return;
            }

            SetState(ApplicationState.Initializing);

            if (levelManager == null)
            {
                levelManager = GetComponentInChildren<LevelManager>(true);
            }

            if (levelManager == null)
            {
                var levelManagerObject = new GameObject(nameof(LevelManager));
                levelManagerObject.transform.SetParent(transform);
                levelManager = levelManagerObject.AddComponent<LevelManager>();
            }

            levelManager.Initialize();
            levelManager.ReturnToPreviousFlowRequested += OnReturnToPreviousFlowRequested;
            initialized = true;
            SetState(ApplicationState.Ready);
        }

        public ApplicationState GetApplicationState()
        {
            return applicationState;
        }

        public LevelManager GetLevelManager()
        {
            return levelManager;
        }

        public void RequestEnterLevel(LevelId levelId)
        {
            if (applicationState != ApplicationState.Ready || levelManager == null)
            {
                return;
            }

            levelManager.RequestLoadLevel(levelId);
        }

        public void RequestQuit()
        {
            if (applicationState == ApplicationState.Quitting ||
                applicationState == ApplicationState.Stopped)
            {
                return;
            }

            SetState(ApplicationState.Quitting);
            Application.Quit();
        }

        private void OnReturnToPreviousFlowRequested(ReturnToPreviousFlowRequestedEvent request)
        {
            if (applicationState == ApplicationState.Ready)
                levelManager.RequestUnloadToScene("MainMenu");
        }

        private void SetState(ApplicationState nextState)
        {
            if (applicationState == nextState)
            {
                return;
            }

            var previousState = applicationState;
            applicationState = nextState;
            ApplicationStateChanged?.Invoke(
                new ApplicationStateChangedEvent(previousState, nextState));
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            if (levelManager != null)
            {
                levelManager.ReturnToPreviousFlowRequested -= OnReturnToPreviousFlowRequested;
                levelManager.Dispose();
            }

            instance = null;
            applicationState = ApplicationState.Stopped;
            initialized = false;
        }
    }
}
