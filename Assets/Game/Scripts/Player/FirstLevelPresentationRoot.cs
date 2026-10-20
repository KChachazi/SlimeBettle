using System;
using System.Collections.Generic;
using SlimeBattle.Core;
using SlimeBattle.Player.Placement;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.UI;
using SlimeBattle.World;
using UnityEngine;
using PlayerInputSystem = SlimeBattle.Player.Input.InputSystem;

namespace SlimeBattle.Player
{
    /// <summary>Optional first-level composition; a backend is valid only for the context it was injected into.</summary>
    [DefaultExecutionOrder(-100), RequireComponent(typeof(PlayerPresentationRoot)), DisallowMultipleComponent]
    public sealed class FirstLevelPresentationRoot : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private CameraSystem cameraSystem;
        [SerializeField] private PlacementGridAuthoring grid;
        [SerializeField] private FirstLevelMapDefinition anchorMapDefinition;
        [SerializeField] private PlacementConfirmationMode confirmationMode;
        [SerializeField] private MonoBehaviour placementBackend;
        [SerializeField] private MonoBehaviour statusSnapshotSource;
        [SerializeField] private Transform selectionParent;
        [SerializeField] private PlacementSelectionView selectionPrefab;
        [SerializeField] private PlacementSelectionView selectionInstance;
        private PlayerInputSystem input;
        private IPlacementBackend injectedBackend;
        private IFirstLevelStatusSource statusSource;
        private LevelContext backendContext;
        private PlacementInputAdapter inputAdapter;
        private CastleFoodCartInteractionAdapter foodCartAdapter;
        private PlacementGridView gridView;
        private PlacementSelectionView selectionView;
        private GameplayFeedbackView gameplayFeedback;
        private bool isInitialized;
        private bool isBound;
        private float nextRefresh;
        private LevelContext cameraContext;
        public PlacementSystem Placement { get; private set; }

        private void Start()
        {
            if (isInitialized) { return; }
            // LevelManager belongs to the persistent Bootstrap scene.
            if (levelManager == null) { levelManager = FindObjectOfType<LevelManager>(); }
            if (levelManager == null || cameraSystem == null || grid == null || (selectionPrefab == null && selectionInstance == null))
            {
                Debug.LogError("FirstLevelPresentationRoot needs LevelManager, CameraSystem, Grid and selection prefab references.", this);
                enabled = false;
                return;
            }
            if (placementBackend != null && !(placementBackend is IPlacementBackend) ||
                statusSnapshotSource != null && !(statusSnapshotSource is IFirstLevelStatusSource))
            {
                Debug.LogError("First-level providers must implement their documented read/request interfaces.", this);
                enabled = false;
                return;
            }
            Initialize(levelManager, GetComponent<PlayerInputSystem>(), cameraSystem, grid,
                placementBackend as IPlacementBackend, confirmationMode, statusSnapshotSource as IFirstLevelStatusSource);
        }

        public void Initialize(LevelManager manager, PlayerInputSystem input, CameraSystem camera,
            PlacementGridAuthoring grid, IPlacementBackend backend, PlacementConfirmationMode mode,
            IFirstLevelStatusSource statusSource = null, PlacementSelectionView selectionPrefab = null,
            FirstLevelMapDefinition anchorMapDefinition = null)
        {
            Unbind();
            levelManager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
            this.input = input != null ? input : throw new ArgumentNullException(nameof(input));
            if (input != GetComponent<PlayerInputSystem>()) { throw new ArgumentException("Input must belong to this presentation root."); }
            cameraSystem = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            this.grid = grid != null ? grid : throw new ArgumentNullException(nameof(grid));
            if (anchorMapDefinition != null) { this.anchorMapDefinition = anchorMapDefinition; }
            this.selectionPrefab = selectionPrefab != null ? selectionPrefab : this.selectionPrefab;
            if (this.selectionPrefab == null && selectionInstance == null) throw new ArgumentNullException(nameof(selectionPrefab));
            grid.ValidateConfiguration();
            if (!Enum.IsDefined(typeof(PlacementConfirmationMode), mode))
            { throw new ArgumentOutOfRangeException(nameof(mode)); }
            confirmationMode = mode;
            injectedBackend = backend;
            backendContext = manager.GetCurrentContext();
            this.statusSource = statusSource ?? new FirstLevelRuntimeStatusSource();
            if (inputAdapter == null) { inputAdapter = GetComponent<PlacementInputAdapter>() ?? gameObject.AddComponent<PlacementInputAdapter>(); }
            if (gridView == null) { gridView = GetComponent<PlacementGridView>() ?? gameObject.AddComponent<PlacementGridView>(); }
            if (selectionView == null)
            { selectionView = selectionInstance != null ? selectionInstance : Instantiate(this.selectionPrefab, selectionParent != null ? selectionParent : transform); }
            isInitialized = true;
            if (isActiveAndEnabled) { Bind(); }
        }

        private void Bind()
        {
            if (isBound) { return; }
            isBound = true;
            levelManager.LevelStateChanged += OnPhaseChanged;
            levelManager.LevelStarted += OnStarted;
            CreateSession();
        }

        private void CreateSession()
        {
            Placement?.Dispose();
            var context = levelManager.GetCurrentContext();
            var backend = context == backendContext && injectedBackend != null ? injectedBackend :
                new MechanismPlacementBackend(context?.MechanismSystem);
            Placement = new PlacementSystem(grid, ReadBounds, levelManager.GetLevelState, backend, confirmationMode,
                ReadAnchors, anchorMapDefinition != null ? anchorMapDefinition.DesignPixelsPerUnityUnit : 0f,
                IsPlaceableMapPosition);
            var source = new FirstLevelUiSnapshotSource(Placement, statusSource);
            GetComponent<PlayerPresentationRoot>().Initialize(levelManager, cameraSystem, source);
            GetComponent<PlayerPresentationRoot>().InitializePlacement(Placement);
            gridView.Initialize(Placement, grid, cameraSystem.GetComponent<UnityEngine.Camera>());
            gridView.enabled = true;
            selectionView.Initialize(Placement);
            selectionView.enabled = true;
            if (gameplayFeedback == null)
                gameplayFeedback = GetComponent<GameplayFeedbackView>() ?? gameObject.AddComponent<GameplayFeedbackView>();
            Font feedbackFont = null;
            foreach (var label in selectionView.GetComponentsInChildren<UnityEngine.UI.Text>(true))
                if (label.font != null) { feedbackFont = label.font; break; }
            gameplayFeedback.Initialize(context, cameraSystem.GetComponent<UnityEngine.Camera>(), Placement, feedbackFont);
            foodCartAdapter = GetComponent<CastleFoodCartInteractionAdapter>();
            if (foodCartAdapter != null)
            {
                foodCartAdapter.Initialize(input, cameraSystem, Placement,
                    context?.MechanismSystem);
                foodCartAdapter.enabled = true;
            }
            inputAdapter.Initialize(input, cameraSystem, Placement, foodCartAdapter);
            inputAdapter.enabled = true;
            ApplyMapBounds();
        }

        private MapBounds? ReadBounds()
        {
            var map = levelManager.GetCurrentContext()?.MapManager;
            return map != null && map.TryGetMapBounds(out var bounds) ? bounds : (MapBounds?)null;
        }

        private IReadOnlyList<PlacementAnchorSnapshot> ReadAnchors()
        {
            return levelManager.GetCurrentContext()?.MapManager?.GetPlacementAnchors();
        }

        private bool IsPlaceableMapPosition(Vector2 position)
        {
            var map = levelManager.GetCurrentContext()?.MapManager;
            return map != null && map.CanOccupy(position,
                new OccupancyQuery(checkStaticBlockers: true));
        }

        private void ApplyMapBounds()
        {
            var bounds = ReadBounds();
            if (!bounds.HasValue) return;
            cameraSystem.SetBounds(new CameraBounds(bounds.Value.Min, bounds.Value.Max));
            var context = levelManager.GetCurrentContext();
            if (context == null || context == cameraContext) return;
            cameraContext = context;
            // Start at the level's actual actors instead of the map's empty origin corner.
            var focus = (bounds.Value.Min + bounds.Value.Max) * 0.5f;
            if (context.MechanismSystem != null && context.MechanismSystem.TryGetWeaponSnapshot(out var weapon))
                focus = weapon.Position;
            else if (context.SlimeManager != null)
            {
                var slimes = context.SlimeManager.GetAliveSnapshots();
                if (slimes.Count > 0) focus = slimes[0].Position;
            }
            cameraSystem.ResetOpeningView(focus);
        }

        private void OnStarted(LevelStartedEvent data) { CreateSession(); }
        private void OnPhaseChanged(LevelStateChangedEvent data)
        {
            // Resume must refresh the selection view even when the input adapter is disabled.
            // Every phase transition discards the previous selection; never restore its preview.
            Placement?.CancelPlacement();
        }
        private void Update()
        {
            if (!isBound || Placement == null || Time.unscaledTime < nextRefresh) { return; }
            nextRefresh = Time.unscaledTime + 0.2f;
            Placement.Refresh();
        }
        private void OnEnable() { if (isInitialized) { Bind(); } }
        private void OnDisable() { Unbind(); }
        private void Unbind()
        {
            if (levelManager != null)
            {
                levelManager.LevelStateChanged -= OnPhaseChanged;
                levelManager.LevelStarted -= OnStarted;
            }
            if (inputAdapter != null) { inputAdapter.enabled = false; }
            if (foodCartAdapter != null) { foodCartAdapter.enabled = false; }
            if (gridView != null) { gridView.enabled = false; }
            if (selectionView != null) { selectionView.enabled = false; }
            gameplayFeedback?.Unbind();
            Placement?.Dispose();
            isBound = false;
        }
        private void OnDestroy() { Unbind(); }
    }
}
