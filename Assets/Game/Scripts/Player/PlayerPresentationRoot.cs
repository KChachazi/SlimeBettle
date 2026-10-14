using System;
using SlimeBattle.Core;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using PlayerInputSystem = SlimeBattle.Player.Input.InputSystem;
using InputMode = SlimeBattle.Player.Input.InputMode;

namespace SlimeBattle.Player
{
    /// <summary>Scene composition and lifecycle adapter for input, camera and HUD.</summary>
    [RequireComponent(typeof(PlayerInputSystem), typeof(UISystem))]
    public sealed class PlayerPresentationRoot : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private CameraSystem cameraSystem;
        [SerializeField] private BattleHudView hudView;
        [SerializeField] private MonoBehaviour uiSnapshotSource;
        [SerializeField] private bool createDefaultHud = true;
        [SerializeField] private Transform hudParent;
        [SerializeField] private Transform eventSystemParent;
        private PlayerInputSystem inputSystem;
        private UISystem uiSystem;
        private bool isInitialized;
        private bool isBound;

        private void Awake()
        {
            inputSystem = GetComponent<PlayerInputSystem>();
            uiSystem = GetComponent<UISystem>();
        }

        private void Start()
        {
            if (isInitialized) { return; }
            // Bootstrap's LevelManager survives scene loads. Resolve once at scene entry.
            if (levelManager == null) { levelManager = FindObjectOfType<LevelManager>(); }
            if (cameraSystem == null) { cameraSystem = GetComponentInChildren<CameraSystem>(true); }
            if (levelManager == null || cameraSystem == null)
            {
                Debug.LogError("PlayerPresentationRoot requires a LevelManager and a CameraSystem.", this);
                enabled = false;
                return;
            }
            if (uiSnapshotSource != null && !(uiSnapshotSource is IUiSnapshotSource))
            {
                Debug.LogError("The UI snapshot component must implement IUiSnapshotSource.", this);
            }
            Initialize(levelManager, cameraSystem, uiSnapshotSource as IUiSnapshotSource, hudView);
        }

        public void Initialize(LevelManager manager, CameraSystem camera,
            IUiSnapshotSource source = null, BattleHudView view = null)
        {
            Unbind();
            levelManager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
            cameraSystem = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            if (inputSystem == null) { inputSystem = GetComponent<PlayerInputSystem>(); }
            if (uiSystem == null) { uiSystem = GetComponent<UISystem>(); }
            cameraSystem.Initialize();
            if (view != null) { hudView = view; }
            if (hudView == null && createDefaultHud)
            { hudView = BattleHudView.Create(hudParent != null ? hudParent : transform); }
            EnsureEventSystem();
            uiSystem.Initialize(levelManager, inputSystem, source, hudView);
            isInitialized = true;
            if (isActiveAndEnabled) { Bind(); }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) { return; }
            var eventObject = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventObject.transform.SetParent(eventSystemParent != null ? eventSystemParent : transform, false);
        }

        private void OnEnable() { if (isInitialized) { Bind(); } }
        private void OnDisable() { Unbind(); }

        private void Bind()
        {
            if (isBound || levelManager == null) { return; }
            levelManager.LevelStateChanged += OnLevelStateChanged;
            inputSystem.Enable();
            ApplyLevelState();
            isBound = true;
        }

        private void Unbind()
        {
            if (levelManager != null) { levelManager.LevelStateChanged -= OnLevelStateChanged; }
            if (inputSystem != null) { inputSystem.Disable(); }
            isBound = false;
        }

        private void OnLevelStateChanged(LevelStateChangedEvent eventData) { ApplyLevelState(); }

        private void ApplyLevelState()
        {
            var state = levelManager.GetLevelState();
            var mode = state == LevelState.Running ? InputMode.Gameplay :
                state == LevelState.Paused || state == LevelState.Succeeded || state == LevelState.Failed
                    ? InputMode.UiOnly : InputMode.Disabled;
            inputSystem.SetInputMode(mode);
        }

        private void LateUpdate()
        {
            if (isBound && cameraSystem != null) { cameraSystem.ApplyInput(inputSystem.ReadSnapshot()); }
        }
    }
}
