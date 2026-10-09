using SlimeBattle.Core;
using SlimeBattle.Player;
using SlimeBattle.Player.Input;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.UI;
using UnityEngine;
using UnityEngine.UI;
using PlayerInputSystem = SlimeBattle.Player.Input.InputSystem;

namespace SlimeBattle.Tests.PlayerPresentation
{
    /// <summary>
    /// Input probe and simulated upstream results, not actors/combat/objective rules.
    /// Keep this component and all generated diagnostics in test scenes only.
    /// </summary>
    public sealed class PlayerPresentationFixture : MonoBehaviour, IUiSnapshotSource
    {
        private LevelManager levelManager;
        private PlayerInputSystem inputSystem;
        private CameraSystem cameraSystem;
        private SpriteRenderer probe;
        private Sprite sprite;
        private Vector2 movement;
        private SubscriptionToken commandToken;
        private SubscriptionToken cancelToken;
        private float flashUntil;
        private int attackRequests;

        private void Start()
        {
            levelManager = FindObjectOfType<LevelManager>();
            if (levelManager == null)
            {
                Debug.LogError("Start this test through Bootstrap_P0Player to load its registered configuration.", this);
                return;
            }

            var sceneRoot = new GameObject("SceneRoot");
            var systems = new GameObject("Systems");
            systems.transform.SetParent(sceneRoot.transform);
            var cameras = new GameObject("Cameras");
            cameras.transform.SetParent(sceneRoot.transform);
            var cameraObject = new GameObject("TestCamera", typeof(UnityEngine.Camera), typeof(AudioListener), typeof(CameraSystem));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(cameras.transform);
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            var camera = cameraObject.GetComponent<UnityEngine.Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.065f, 0.10f, 0.15f);
            camera.orthographicSize = 7f;
            cameraSystem = cameraObject.GetComponent<CameraSystem>();
            var presentation = systems.AddComponent<PlayerPresentationRoot>();
            presentation.Initialize(levelManager, cameraSystem, this);
            cameraSystem.SetBounds(new CameraBounds(new Vector2(-18f, -12f), new Vector2(18f, 12f)));
            inputSystem = systems.GetComponent<PlayerInputSystem>();
            BindInput();
            BuildWorld(sceneRoot.transform);
            BuildDiagnostics(sceneRoot.transform);
        }

        public UiState ReadSnapshot(LevelContext context)
        {
            return new UiState(context.GetLevelId(), context.GetLevelState(),
                "TEST: verify input, camera, pause, result and restart", hint:
                "WASD / arrows: probe | Click / tap: place probe | Space: attack request (" + attackRequests +
                ") | Middle drag / wheel: camera | Esc: pause", resultText:
                "Simulated upstream result. Restart reloads this test scene.");
        }

        private void OnCommand(PlayerCommandSubmittedEvent eventData)
        {
            switch (eventData.Command.Kind)
            {
                case PlayerCommandKind.Move: movement = eventData.Command.Movement; break;
                case PlayerCommandKind.PrimaryAction:
                    probe.transform.position = cameraSystem.ScreenToWorld(eventData.Command.ScreenPosition);
                    break;
                case PlayerCommandKind.Attack:
                    attackRequests++;
                    flashUntil = Time.unscaledTime + 0.2f;
                    break;
            }
        }

        private void OnCancel(CancelInputEvent eventData) { movement = Vector2.zero; }

        private void OnEnable() { if (inputSystem != null) { BindInput(); } }

        private void BindInput()
        {
            inputSystem.Unsubscribe(commandToken);
            inputSystem.Unsubscribe(cancelToken);
            commandToken = inputSystem.Subscribe<PlayerCommandSubmittedEvent>(OnCommand);
            cancelToken = inputSystem.Subscribe<CancelInputEvent>(OnCancel);
        }

        private void OnDisable()
        {
            movement = Vector2.zero;
            if (inputSystem == null) { return; }
            inputSystem.Unsubscribe(commandToken);
            inputSystem.Unsubscribe(cancelToken);
        }

        private void Update()
        {
            if (probe == null || levelManager == null) { return; }
            if (levelManager.GetLevelState() == LevelState.Running)
            {
                probe.transform.position += (Vector3)(movement * (4f * Time.deltaTime));
            }
            probe.color = Time.unscaledTime < flashUntil ? new Color(1f, 0.55f, 0.32f) : new Color(0.4f, 0.94f, 0.77f);
        }

        private void BuildWorld(Transform parent)
        {
            var world = new GameObject("World_InputProbeOnly");
            world.transform.SetParent(parent);
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);
            for (var x = -18; x <= 18; x += 2)
            {
                Tile("GridX", world.transform, new Vector3(x, 0f, 1f), new Vector2(0.025f, 24f), new Color(0.12f, 0.18f, 0.23f));
            }
            for (var y = -12; y <= 12; y += 2)
            {
                Tile("GridY", world.transform, new Vector3(0f, y, 1f), new Vector2(36f, 0.025f), new Color(0.12f, 0.18f, 0.23f));
            }
            probe = Tile("InputProbe_NotAnActor", world.transform, Vector3.zero, Vector2.one, new Color(0.4f, 0.94f, 0.77f));
        }

        private SpriteRenderer Tile(string name, Transform parent, Vector3 position, Vector2 scale, Color color)
        {
            var item = new GameObject(name, typeof(SpriteRenderer));
            item.transform.SetParent(parent);
            item.transform.position = position;
            item.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var renderer = item.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            return renderer;
        }

        private void BuildDiagnostics(Transform parent)
        {
            var canvasObject = new GameObject("TEST_ONLY_ResultControls", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            AddResultButton(canvasObject.transform, "TEST: SUCCESS", 24f, LevelResult.Succeeded);
            AddResultButton(canvasObject.transform, "TEST: FAILURE", 244f, LevelResult.Failed);
        }

        private void AddResultButton(Transform parent, string title, float x, LevelResult result)
        {
            var item = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, 24f);
            rect.sizeDelta = new Vector2(200f, 44f);
            item.GetComponent<Image>().color = new Color(0.22f, 0.26f, 0.34f);
            item.GetComponent<Button>().onClick.AddListener(() => levelManager.ReportResult(result));
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(item.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 18;
            label.text = title;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (inputSystem != null)
            {
                inputSystem.Unsubscribe(commandToken);
                inputSystem.Unsubscribe(cancelToken);
            }
            if (sprite != null) { Destroy(sprite); }
        }
    }
}
