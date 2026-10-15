using System;
using System.Collections.Generic;
using System.Linq;
using SlimeBattle.Core;
using SlimeBattle.Mechanisms;
using SlimeBattle.Player.Input;
using SlimeBattle.Player.Placement;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.UI;
using SlimeBattle.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PlayerInputSystem = SlimeBattle.Player.Input.InputSystem;

namespace SlimeBattle.Tests.PlayerPresentation
{
    /// <summary>TEST_ONLY: simulated mechanism/weapon/result data; no formal battle or Core result rule.</summary>
    public sealed class FirstLevelPlacementFixture : MonoBehaviour
    {
        public PlacementSystem Placement { get; private set; }
        public PlacementGridAuthoring Grid { get; private set; }
        public PlacementGridView GridView { get; private set; }
        public PlacementSelectionView Selection { get; private set; }
        public PlacementInputAdapter Adapter { get; private set; }
        public PlayerInputSystem Input { get; private set; }
        public CameraSystem Camera { get; private set; }
        public TestPlacementBackend Backend { get; private set; }
        public int PrimaryActions { get; private set; }
        public LevelState Phase { get; private set; } = LevelState.Running;
        private BattleHudView hud;

        private void Awake()
        {
            if (EventSystem.current == null)
            { new GameObject("TEST_ONLY_EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); }
            Grid = new GameObject("TEST_ONLY_Grid", typeof(UnityEngine.Grid), typeof(PlacementGridAuthoring))
                .GetComponent<PlacementGridAuthoring>();
            Grid.Grid.cellSize = new Vector3(1f, 1f, 0f); // Test authoring values, not a formal level default.
            Camera = new GameObject("TEST_ONLY_Camera", typeof(UnityEngine.Camera), typeof(CameraSystem)).GetComponent<CameraSystem>();
            Camera.GetComponent<UnityEngine.Camera>().backgroundColor = new Color(0.035f, 0.055f, 0.08f);
            Camera.GetComponent<UnityEngine.Camera>().orthographicSize = 6f;
            Camera.Initialize();
            Input = gameObject.AddComponent<PlayerInputSystem>();
            Input.Enable();
            Input.SetInputMode(InputMode.Gameplay);
            Input.Subscribe<PlayerCommandSubmittedEvent>(data =>
            { if (data.Command.Kind == PlayerCommandKind.PrimaryAction) { PrimaryActions++; } });
            Backend = new TestPlacementBackend(transform);
            Placement = new PlacementSystem(Grid, () => new MapBounds(new Vector2(-6f, -3f), new Vector2(6f, 3f)),
                () => Phase, Backend, PlacementConfirmationMode.Single);
            GridView = gameObject.AddComponent<PlacementGridView>();
            GridView.Initialize(Placement, Grid);
            var selectionPrefab = Resources.Load<PlacementSelectionView>("UI/PF_PlacementSelection");
            if (selectionPrefab == null) { throw new InvalidOperationException("Placement selection prefab is missing."); }
            Selection = Instantiate(selectionPrefab, transform);
            Selection.Initialize(Placement);
            Adapter = gameObject.AddComponent<PlacementInputAdapter>();
            Adapter.Initialize(Input, Camera, Placement);
            hud = BattleHudView.Create(transform);
            foreach (var button in hud.GetComponentsInChildren<Button>(true))
            {
                if (button.name == "PauseButton") { button.onClick.AddListener(() => SetPhase(LevelState.Paused)); }
                else if (button.name == "ResumeButton") { button.onClick.AddListener(() => SetPhase(LevelState.Running)); }
                else if (button.name == "RestartButton") { button.onClick.AddListener(ReloadTestScene); }
            }
            CreateTestControls();
            RenderHud();
        }

        public void SetPhase(LevelState phase)
        {
            Phase = phase;
            Input.SetInputMode(phase == LevelState.Running ? InputMode.Gameplay : InputMode.UiOnly);
            Placement.CancelPlacement();
            RenderHud();
        }

        public void ReloadTestScene()
        {
            // This isolated Editor test scene is deliberately never added to Build Settings.
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(gameObject.scene.path,
                new LoadSceneParameters(LoadSceneMode.Single));
#endif
        }

        private void Update() { Placement?.Refresh(); RenderHud(); }
        private void RenderHud()
        {
            if (hud == null || Placement == null) { return; }
            var result = Phase == LevelState.Succeeded || Phase == LevelState.Failed;
            hud.Render(new UiState(default, Phase, "TEST ONLY: placement pipeline; mechanism / weapon / result are simulated.",
                4, 0, Placement.Hint, "TEST ONLY: simulated upstream result", 20, "TEST WEAPON  -- (no formal weapon data)"),
                true, Phase == LevelState.Paused, result, false);
        }

        private void CreateTestControls()
        {
            var canvas = new GameObject("TEST_ONLY_Controls", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform, false);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 22;
            TestButton(canvas.transform, "TEST Success", 30f, () => SetPhase(LevelState.Succeeded));
            TestButton(canvas.transform, "TEST Failure", -30f, () => SetPhase(LevelState.Failed));
        }

        private static void TestButton(Transform parent, string name, float y, UnityEngine.Events.UnityAction action)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-100f, y);
            rect.sizeDelta = new Vector2(170f, 44f);
            item.GetComponent<Image>().color = new Color(0.25f, 0.17f, 0.12f);
            item.GetComponent<Button>().onClick.AddListener(action);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(item.transform, false);
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.text = name;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
        }

        private void OnDisable()
        {
            if (Adapter != null) { Adapter.enabled = false; }
            Placement?.Dispose();
            Backend?.Dispose();
            Input?.Disable();
        }
    }

    /// <summary>TEST_ONLY backend owns colored test sprites, never MechanismAuthoring or Actor objects.</summary>
    public sealed class TestPlacementBackend : IPlacementBackend, IDisposable
    {
        private readonly List<PlacementObjectSnapshot> snapshots = new List<PlacementObjectSnapshot>();
        private readonly Dictionary<MechanismId, GameObject> visuals = new Dictionary<MechanismId, GameObject>();
        private readonly Transform parent;
        private readonly Sprite foodSprite;
        private readonly Sprite furnaceSprite;
        private readonly Sprite obstacleSprite;
        private Sprite sprite;
        public TestPlacementBackend(Transform parent = null, Sprite foodSprite = null, Sprite furnaceSprite = null, Sprite obstacleSprite = null)
        { this.parent = parent; this.foodSprite = foodSprite; this.furnaceSprite = furnaceSprite; this.obstacleSprite = obstacleSprite; }
        public bool IsAvailable { get; set; } = true;
        public bool RejectNext { get; set; }
        public bool ThrowOnRequest { get; set; }
        public bool ReturnInvalidId { get; set; }
        public int Requests { get; private set; }
        public Action BeforeRequest { get; set; }
        public IReadOnlyList<PlacementObjectSnapshot> ReadPlacedObjects() { return snapshots.AsReadOnly(); }
        public PlacementBudgetSnapshot GetBudgetSnapshot() { return default; }
        public PlacementResult RequestPlacement(PlacementRequest request)
        {
            Requests++;
            BeforeRequest?.Invoke();
            if (ThrowOnRequest) { throw new InvalidOperationException("TEST backend failure"); }
            if (RejectNext || !IsAvailable)
            { RejectNext = false; return new PlacementResult(false, default, "TEST backend rejected this request"); }
            if (ReturnInvalidId) { return new PlacementResult(true, default, "TEST malformed success"); }
            var id = MechanismId.NewId();
            Add(id, request.WorldCenter);
            if (parent != null)
            {
                if (sprite == null) { sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f); }
                var item = new GameObject("TEST_ONLY_" + request.Item, typeof(SpriteRenderer));
                item.transform.SetParent(parent, false);
                item.transform.position = request.WorldCenter;
                item.transform.localScale = Vector3.one * 0.6f;
                var renderer = item.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 105;
                renderer.color = request.Item == PlacementItemId.Food ? Color.green :
                    request.Item == PlacementItemId.Furnace ? new Color(1f, 0.6f, 0.2f) : Color.gray;
                var illustration = request.Item == PlacementItemId.Food ? foodSprite : request.Item == PlacementItemId.Furnace ? furnaceSprite : obstacleSprite;
                if (illustration != null)
                {
                    renderer.sprite = illustration; renderer.color = Color.white;
                    item.transform.localScale = Vector3.one * (0.9f / Mathf.Max(illustration.bounds.size.x, illustration.bounds.size.y));
                }
                visuals.Add(id, item);
            }
            return new PlacementResult(true, id, "TEST backend accepted");
        }
        public void Add(MechanismId id, Vector3 position) { snapshots.Add(new PlacementObjectSnapshot(id, position)); }
        public void Remove(MechanismId id)
        {
            snapshots.RemoveAll(item => item.Id == id);
            if (visuals.TryGetValue(id, out var visual)) { Release(visual); visuals.Remove(id); }
        }
        public void Dispose()
        {
            foreach (var item in visuals.Values.ToArray()) { if (item != null) { Release(item); } }
            visuals.Clear();
            snapshots.Clear();
            if (sprite != null) { Release(sprite); sprite = null; }
        }
        private static void Release(UnityEngine.Object item)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(item); else UnityEngine.Object.DestroyImmediate(item); }
    }
}
