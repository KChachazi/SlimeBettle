using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SlimeBattle.Core;
using SlimeBattle.Player;
using SlimeBattle.Player.Input;
using SlimeBattle.Player.Placement;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DeviceInput = UnityEngine.InputSystem.InputSystem;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace SlimeBattle.Tests.PlayerPresentation.Editor
{
    /// <summary>Opt-in real Editor Play Mode tests. Never registers scenes in Build Settings.</summary>
    [InitializeOnLoad]
    public static class FirstLevelPlacementPlayChecks
    {
        public const string ScenePath = "Assets/Game/Tests/PlayerPresentation/Test_FirstLevelPlacement.unity";
        private const string ActiveKey = "SlimeBattle.PlacementValidation.Active";
        private const string OutputKey = "SlimeBattle.PlacementValidation.Output";
        private const string ResultKey = "SlimeBattle.PlacementValidation.Result";
        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        private static readonly List<string> passed = new List<string>();
        private static int lastFrame;
        private static float deadline;
        private static string runtimeError;
        [Serializable] private sealed class Report
        {
            public string unityVersion;
            public bool success;
            public string failure;
            public string[] assertions;
        }

        static FirstLevelPlacementPlayChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetBool(ActiveKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            { EditorApplication.delayCall += Finish; }
        }

        [MenuItem("SlimeBattle/Tests/Create First Level Placement Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { throw new InvalidOperationException("Exit Play Mode first."); }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("TEST_ONLY_FirstLevelPlacement", typeof(FirstLevelPlacementFixture));
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
        }

        [MenuItem("SlimeBattle/Tests/Run First Level Placement Play Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(ActiveKey, false))
            { throw new InvalidOperationException("Start validation outside Play Mode with no active run."); }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            P0PlayerChecks.Run();
            if (!File.Exists(ScenePath)) { CreateScene(); }
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-placementReportPath");
            var output = index >= 0 && index + 1 < args.Length ? args[index + 1] :
                Path.Combine(Path.GetTempPath(), "SlimeBattle-placement-validation.json");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            SessionState.SetString(OutputKey, output);
            SessionState.SetString(ResultKey, string.Empty);
            SessionState.SetBool(ActiveKey, true);
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) { return; }
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                passed.Clear(); routines.Clear(); routines.Push(CheckRuntime());
                deadline = Time.realtimeSinceStartup + 180f; lastFrame = -1; runtimeError = null;
                Application.logMessageReceived += OnRuntimeLog;
                EditorApplication.update += Tick;
                var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                if (type != null) { EditorWindow.GetWindow(type).Focus(); }
            }
            else if (change == PlayModeStateChange.EnteredEditMode) { Finish(); }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { return; }
            if (runtimeError != null) { Stop(new InvalidOperationException(runtimeError)); return; }
            if (Time.realtimeSinceStartup > deadline) { Stop(new TimeoutException("Placement Play Mode checks exceeded 180 seconds.")); return; }
            if (Time.frameCount == lastFrame) { return; }
            lastFrame = Time.frameCount;
            try
            {
                var routine = routines.Peek();
                if (!routine.MoveNext()) { routines.Pop(); }
                else if (routine.Current is IEnumerator nested) { routines.Push(nested); }
                if (routines.Count == 0) { Stop(null); }
            }
            catch (Exception exception) { Stop(exception); }
        }

        private static IEnumerator CheckRuntime()
        {
            yield return Frames(5);
            while (!Application.isFocused) { yield return null; }
            var fixture = UnityEngine.Object.FindObjectOfType<FirstLevelPlacementFixture>();
            Check(fixture != null && !fixture.GridView.IsVisible && fixture.Selection.IsVisible,
                "Isolated scene starts with selection visible and grid hidden");
            Check(fixture.GetComponentsInChildren<Text>().Any(text => text.text.Contains("4 / 20")) &&
                fixture.GetComponentsInChildren<Text>().Any(text => text.text.Contains("TEST WEAPON")),
                "HUD shows slime count/limit and explicitly simulated weapon data");
            var mouse = DeviceInput.AddDevice<Mouse>();
            var keyboard = DeviceInput.AddDevice<Keyboard>();
            var touch = DeviceInput.AddDevice<Touchscreen>();
            try
            {
                var point = new Vector3(0.2f, 0.3f);
                var pixel = fixture.Camera.WorldToScreen(point);
                yield return Click(mouse, ButtonPoint(fixture, "PlacementFoodButton"));
                Check(fixture.Placement.SelectedItem == PlacementItemId.Food && fixture.GridView.IsVisible,
                    "Clicking Food enters placement and shows the independent grid");
                Check(!fixture.Placement.GetCurrentPreview().HasPosition,
                    "Selecting a UI button does not project its screen position onto the map");
                yield return Move(mouse, pixel);
                Check(fixture.Placement.GetCurrentPreview().Cell == new Vector3Int(0, 0, 0) &&
                    fixture.Placement.GetCurrentPreview().WorldCenter == new Vector3(0.5f, 0.5f),
                    "Mouse hover projects through CameraSystem and snaps to Grid cell centre");
                var freeColor = fixture.GridView.PreviewColor;
                yield return Click(mouse, pixel);
                Check(fixture.Backend.Requests == 0, "World click previews without auto-confirming or creating an object");
                var selectedCell = fixture.Placement.GetCurrentPreview().Cell;
                var otherPixel = fixture.Camera.WorldToScreen(new Vector3(2.2f, 0.3f));
                yield return Move(mouse, otherPixel);
                yield return Move(mouse, fixture.Camera.WorldToScreen(new Vector3(2.2f, -1.2f)));
                Check(fixture.Placement.GetCurrentPreview().Cell == selectedCell,
                    "A map click locks the cell while the mouse crosses other map cells towards Confirm");
                yield return Click(mouse, otherPixel);
                Check(fixture.Placement.GetCurrentPreview().Cell == new Vector3Int(2, 0, 0),
                    "A second map click explicitly replaces the locked target");
                yield return Click(mouse, pixel);
                yield return Click(mouse, ButtonPoint(fixture, "PlacementConfirmButton"));
                Check(fixture.Backend.Requests == 1 && !fixture.Placement.IsPlacing && !fixture.GridView.IsVisible,
                    "Confirm submits exactly one request and single mode hides grid/preview");
                Check(fixture.Backend.ReadPlacedObjects()[0].Position == new Vector3(0.5f, 0.5f),
                    "Confirm submits the clicked world cell rather than the last cell crossed by the pointer");

                yield return Click(mouse, ButtonPoint(fixture, "PlacementFurnaceButton"));
                yield return Move(mouse, pixel);
                Check(fixture.Placement.GetCurrentPreview().Status == PlacementValidationStatus.Occupied &&
                    fixture.GridView.PreviewColor != freeColor, "An occupied cell has a distinct preview and blocks another type");
                Check(!fixture.Placement.ConfirmPlacement().Succeeded && fixture.Backend.Requests == 1,
                    "Occupied confirmation never reaches the backend");
                yield return Move(mouse, fixture.Camera.WorldToScreen(new Vector3(6.2f, 0.3f)));
                Check(fixture.Placement.GetCurrentPreview().Status == PlacementValidationStatus.OutOfBounds &&
                    fixture.GridView.PreviewColor != freeColor, "Outside map bounds has a distinct preview");
                Check(!fixture.Placement.ConfirmPlacement().Succeeded && fixture.Backend.Requests == 1,
                    "Out-of-bounds confirmation never reaches the backend");

                pixel = fixture.Camera.WorldToScreen(new Vector3(1.2f, 0.3f));
                yield return Move(mouse, pixel);
                fixture.Backend.RejectNext = true;
                yield return Click(mouse, ButtonPoint(fixture, "PlacementConfirmButton"));
                Check(fixture.Placement.IsPlacing && fixture.Placement.Hint.Contains("rejected") &&
                    fixture.Backend.ReadPlacedObjects().Count == 1, "Backend rejection keeps selection and does not occupy the cell");
                yield return Click(mouse, ButtonPoint(fixture, "PlacementConfirmButton"));
                Check(fixture.Backend.ReadPlacedObjects().Count == 2, "Furnace can be confirmed after a rejected request");

                yield return Click(mouse, ButtonPoint(fixture, "PlacementObstacleButton"));
                var touchPixel = fixture.Camera.WorldToScreen(new Vector3(-1.2f, -0.3f));
                yield return Tap(touch, touchPixel);
                Check(fixture.Placement.GetCurrentPreview().Cell == new Vector3Int(-2, -1, 0),
                    "Touch primary action projects and snaps negative coordinates");
                yield return Move(mouse, fixture.Camera.WorldToScreen(new Vector3(3.2f, 0.3f)));
                Check(fixture.Placement.GetCurrentPreview().Cell == new Vector3Int(-2, -1, 0),
                    "A touch-selected cell remains locked when a mouse moves elsewhere");
                yield return Tap(touch, ButtonPoint(fixture, "PlacementConfirmButton"));
                Check(fixture.Backend.ReadPlacedObjects().Count == 3, "Touch UI confirmation places the third type once");

                yield return Click(mouse, ButtonPoint(fixture, "PlacementFoodButton"));
                yield return Move(mouse, fixture.Camera.WorldToScreen(new Vector3(2.2f, -0.3f)));
                var beforeUiDrag = fixture.Placement.GetCurrentPreview().Cell;
                var uiPoint = ButtonPoint(fixture, "PlacementFoodButton");
                DeviceInput.QueueStateEvent(mouse, new MouseState { position = uiPoint }.WithButton(MouseButton.Left));
                yield return Frames(3);
                DeviceInput.QueueStateEvent(mouse, new MouseState { position = fixture.Camera.WorldToScreen(new Vector3(3.2f, 0.3f)) }.WithButton(MouseButton.Left));
                yield return Frames(3);
                Check(fixture.Input.ReadSnapshot().PointerBlocked && fixture.Placement.GetCurrentPreview().Cell == beforeUiDrag,
                    "UI-origin drag remains blocked when held over the map");
                DeviceInput.QueueStateEvent(mouse, new MouseState { position = pixel });
                yield return Frames(3);

                var oldPosition = fixture.Backend.ReadPlacedObjects()[0].Position;
                var fixedPixel = fixture.Camera.WorldToScreen(new Vector3(0.2f, -0.3f));
                yield return Move(mouse, fixedPixel);
                fixture.Camera.SubmitCommand(new CameraCommand(CameraCommandKind.Pan, Vector2.right * 1.2f));
                fixture.Camera.SubmitCommand(new CameraCommand(CameraCommandKind.Zoom, Vector2.zero, -0.5f));
                yield return Frames(4);
                Check(fixture.Placement.GetCurrentPreview().Cell == fixture.Grid.WorldToCell(fixture.Camera.ScreenToWorld(fixedPixel)) &&
                    fixture.Backend.ReadPlacedObjects()[0].Position == oldPosition,
                    "Camera pan/zoom reprojects stationary pointer and preserves placed world position");
                yield return Click(mouse, fixedPixel);
                var lockedCenter = fixture.Placement.GetCurrentPreview().WorldCenter;
                fixture.Camera.SubmitCommand(new CameraCommand(CameraCommandKind.Pan, Vector2.left * 1.2f));
                yield return Frames(4);
                Check(fixture.Placement.GetCurrentPreview().WorldCenter == lockedCenter,
                    "Camera movement preserves a clicked target in world space until another map click");
                ScreenCapture.CaptureScreenshot(Path.ChangeExtension(SessionState.GetString(OutputKey, ""), ".png"));
                yield return Frames(5);

                DeviceInput.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return Frames(3);
                DeviceInput.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);
                Check(!fixture.Placement.IsPlacing && !fixture.GridView.IsVisible, "Escape cancels placement and hides grid");
                Check(!UnityEngine.Object.FindObjectsOfType<SpriteRenderer>(true).Any(item => item.name == "PlacementCellPreview"),
                    "Cancellation destroys the preview object rather than retaining a stale hidden preview");
                yield return Click(mouse, ButtonPoint(fixture, "PlacementFoodButton"));
                yield return Move(mouse, fixedPixel);
                yield return Click(mouse, ButtonPoint(fixture, "PauseButton"));
                Check(fixture.Phase == LevelState.Paused && fixture.Input.GetInputMode() == InputMode.UiOnly &&
                    !fixture.GridView.IsVisible && !fixture.Selection.IsVisible, "Pause UI clears placement and disables map interaction");
                yield return Click(mouse, ButtonPoint(fixture, "ResumeButton"));
                Check(fixture.Phase == LevelState.Running && fixture.Selection.IsVisible && !fixture.Placement.IsPlacing,
                    "Resume restores selection without restoring a stale preview");

                yield return Click(mouse, ButtonPoint(fixture, "PlacementObstacleButton"));
                yield return Move(mouse, fixedPixel);
                fixture.Input.SendMessage("OnApplicationFocus", false);
                Check(!fixture.GridView.IsVisible && !fixture.Placement.IsPlacing, "Simulated Unity focus loss cancels placement immediately");
                fixture.Input.SendMessage("OnApplicationFocus", true);
                yield return Frames(3);
                yield return Click(mouse, ButtonPoint(fixture, "PlacementFoodButton"));
                yield return Move(mouse, fixedPixel);
                fixture.GridView.enabled = false;
                Check(!fixture.GridView.IsVisible, "Disabling grid view hides its owned world sprites");
                fixture.GridView.enabled = true;
                Check(fixture.GridView.IsVisible, "Re-enabling grid view redraws the current snapshot");
                fixture.Adapter.enabled = false;
                Check(!fixture.GridView.IsVisible && !fixture.Placement.IsPlacing, "Disabling input adapter cancels and unsubscribes");
                fixture.Adapter.enabled = true;
                var removedId = fixture.Backend.ReadPlacedObjects()[0].Id;
                fixture.Backend.Remove(removedId);
                fixture.Placement.BeginPlacement(PlacementItemId.Food);
                fixture.Placement.PreviewPlacement(oldPosition);
                Check(fixture.Placement.GetCurrentPreview().CanConfirm, "Removing a test object releases occupancy from read-only snapshots");

                yield return Click(mouse, ButtonPoint(fixture, "TEST Success"));
                Check(!fixture.GridView.IsVisible && !fixture.Selection.IsVisible &&
                    fixture.GetComponentsInChildren<Text>().Any(text => text.text.Contains("TEST ONLY: simulated upstream result")),
                    "Explicitly simulated success shows result UI and clears placement");
                var oldInput = fixture.Input;
                var oldToken = oldInput.Subscribe<PlayerCommandSubmittedEvent>(_ => { });
                for (var iteration = 0; iteration < 5; iteration++)
                {
                    var old = fixture;
                    var oldPlacement = fixture.Placement;
                    if (iteration == 0)
                    {
                        DeviceInput.QueueStateEvent(touch, new TouchState { touchId = 1, phase = TouchPhase.Began, position = pixel });
                        yield return Frames(3);
                        // Hold protection is isolated from concurrent mouse/touch UI arbitration.
                        fixture.ReloadTestScene();
                    }
                    else { yield return Click(mouse, ButtonPoint(fixture, "RestartButton")); }
                    while (fixture == old || fixture == null)
                    { yield return null; fixture = UnityEngine.Object.FindObjectOfType<FirstLevelPlacementFixture>(); }
                    yield return Frames(5);
                    Check(UnityEngine.Object.FindObjectsOfType<PlacementGridView>().Length == 1 &&
                        UnityEngine.Object.FindObjectsOfType<CameraSystem>().Length == 1 &&
                        UnityEngine.Object.FindObjectsOfType<EventSystem>().Length == 1 &&
                        UnityEngine.Object.FindObjectsOfType<PlacementSelectionView>().Length == 1,
                        "Scene reload " + (iteration + 1) + " owns exactly one grid, camera, selection and EventSystem");
                    Check(!oldPlacement.IsPlacing && !fixture.GridView.IsVisible && fixture.Backend.ReadPlacedObjects().Count == 0 &&
                        fixture.Placement.GetOccupiedCells().Count == 0, "Scene reload " + (iteration + 1) + " drops old selection, preview and occupancy");
                    if (iteration == 0)
                    {
                        Check(!fixture.Input.ReadSnapshot().PointerHeld && !fixture.Input.ReadSnapshot().PointerPressed && fixture.PrimaryActions == 0,
                            "A touch held across scene reload generates no replayed primary action or held gameplay input");
                        DeviceInput.QueueStateEvent(touch, new TouchState { touchId = 1, phase = TouchPhase.Ended, position = pixel });
                        yield return Frames(3);
                    }
                    var events = 0;
                    var token = fixture.Input.Subscribe<PlayerCommandSubmittedEvent>(data =>
                    { if (data.Command.Kind == PlayerCommandKind.PrimaryAction) { events++; } });
                    fixture.Input.Unsubscribe(oldToken);
                    yield return Click(mouse, ButtonPoint(fixture, "PlacementFoodButton"));
                    yield return Tap(touch, fixture.Camera.WorldToScreen(Vector3.zero));
                    Check(events == 1 && fixture.Placement.GetCurrentPreview().CanConfirm,
                        "Scene reload " + (iteration + 1) + " accepts one fresh touch; old token cannot remove new subscription");
                    fixture.Input.Unsubscribe(token);
                    fixture.SetPhase(iteration % 2 == 0 ? LevelState.Failed : LevelState.Succeeded);
                    yield return Frames(2);
                }
            }
            finally
            { DeviceInput.RemoveDevice(mouse); DeviceInput.RemoveDevice(keyboard); DeviceInput.RemoveDevice(touch); }
            yield return CheckCompositionLifecycle();
        }

        private static IEnumerator CheckCompositionLifecycle()
        {
            // Read existing registered scenes; never save them or write Build Settings.
            // Additional presentation objects live in the test-owned DontDestroyOnLoad root.
            Check(Application.CanStreamedLevelBeLoaded("Bootstrap") && Application.CanStreamedLevelBeLoaded("Level_test"),
                "Composition checks use already registered Bootstrap and Level_test scenes");
            yield return SceneManager.LoadSceneAsync("Bootstrap");
            while (SceneManager.GetActiveScene().name != "MainMenu") { yield return null; }
            var gameRoot = UnityEngine.Object.FindObjectOfType<GameRoot>();
            Check(gameRoot != null, "Existing Bootstrap provides the public GameRoot entry point");
            var manager = gameRoot.GetLevelManager();
            gameRoot.RequestEnterLevel(new LevelId("Level_test"));
            while (manager.GetLevelState() != LevelState.Running)
            {
                if (manager.GetLevelState() == LevelState.Failed) { throw new InvalidOperationException("Existing Level_test failed to load."); }
                yield return null;
            }
            var owner = new GameObject("TEST_ONLY_FirstLevelComposition");
            UnityEngine.Object.DontDestroyOnLoad(owner);
            var backend = new TestPlacementBackend();
            var nextBackend = new TestPlacementBackend();
            try
            {
                var gridObject = new GameObject("TEST_ONLY_Grid", typeof(Grid), typeof(PlacementGridAuthoring));
                gridObject.transform.SetParent(owner.transform, false);
                var grid = gridObject.GetComponent<PlacementGridAuthoring>();
                grid.Grid.cellSize = new Vector3(1f, 1f, 0f);
                var cameraObject = new GameObject("TEST_ONLY_Camera", typeof(UnityEngine.Camera), typeof(CameraSystem));
                cameraObject.transform.SetParent(owner.transform, false);
                // Projection/assembly only; don't draw over the existing scene camera.
                cameraObject.GetComponent<UnityEngine.Camera>().enabled = false;
                var root = owner.AddComponent<FirstLevelPresentationRoot>();
                var input = owner.GetComponent<SlimeBattle.Player.Input.InputSystem>();
                var camera = cameraObject.GetComponent<CameraSystem>();
                root.Initialize(manager, input, camera, grid, backend, PlacementConfirmationMode.Single);
                var ui = owner.GetComponent<UISystem>();
                var selection = owner.GetComponentInChildren<PlacementSelectionView>();
                var view = owner.GetComponent<PlacementGridView>();
                Check(selection.IsVisible && ui.GetState().SlimeLimit == manager.GetCurrentContext().SlimeManager.MaxAliveCount,
                    "Composition initializes from current public context and displays the actual configured slime limit");
                root.Placement.BeginPlacement(PlacementItemId.Food);
                root.Placement.PreviewPlacement(Vector3.zero);
                Check(root.Placement.GetCurrentPreview().CanConfirm && view.IsVisible,
                    "Composition reads actual MapManager bounds and accepts the injected TEST_ONLY backend");
                ui.SubmitCommand(UiCommand.Pause);
                yield return Frames(2);
                Check(manager.GetLevelState() == LevelState.Paused && !selection.IsVisible && !view.IsVisible && !root.Placement.IsPlacing,
                    "Real LevelManager pause hides selection and clears preview through the composition root");
                ui.SubmitCommand(UiCommand.Resume);
                yield return Frames(2);
                Check(manager.GetLevelState() == LevelState.Running && selection.IsVisible && !root.Placement.IsPlacing,
                    "Real LevelManager resume restores selection without reviving the old preview");
                var adapter = owner.GetComponent<PlacementInputAdapter>();
                adapter.enabled = false;
                ui.SubmitCommand(UiCommand.Pause);
                ui.SubmitCommand(UiCommand.Resume);
                Check(selection.IsVisible && !root.Placement.IsPlacing,
                    "Phase changes refresh selection independently of input-adapter cancellation callbacks");
                adapter.enabled = true;
                root.Placement.BeginPlacement(PlacementItemId.Food);
                root.Placement.PreviewPlacement(Vector3.zero);
                var oldPlacement = root.Placement;
                root.enabled = false;
                Check(!oldPlacement.IsPlacing && !selection.IsVisible && !view.IsVisible,
                    "Disabling the composition root disposes selection and hides owned views");
                root.enabled = true;
                Check(root.Placement != oldPlacement && selection.IsVisible && owner.GetComponentsInChildren<PlacementSelectionView>(true).Length == 1,
                    "Re-enabling the composition root creates a fresh session and reuses one selection view");
                root.Placement.BeginPlacement(PlacementItemId.Food);
                root.Placement.PreviewPlacement(Vector3.zero);
                Check(root.Placement.ConfirmPlacement().Succeeded && backend.Requests == 1,
                    "Composition confirmation invokes only the injected TEST_ONLY creation backend");
                var oldContext = manager.GetCurrentContext();
                oldPlacement = root.Placement;
                ui.SubmitCommand(UiCommand.Restart);
                while (manager.GetCurrentContext() == oldContext || manager.GetLevelState() != LevelState.Running)
                {
                    if (manager.GetLevelState() == LevelState.Failed) { throw new InvalidOperationException("Existing Level_test restart failed."); }
                    yield return null;
                }
                yield return Frames(2);
                root.Placement.BeginPlacement(PlacementItemId.Food);
                root.Placement.PreviewPlacement(Vector3.zero);
                Check(!oldPlacement.IsPlacing && root.Placement != oldPlacement &&
                    root.Placement.GetCurrentPreview().Status == PlacementValidationStatus.Unavailable &&
                    !root.Placement.ConfirmPlacement().Succeeded && backend.Requests == 1,
                    "Real LevelManager restart replaces the context and refuses to reuse the old creation backend");
                root.Initialize(manager, input, camera, grid, nextBackend, PlacementConfirmationMode.Single);
                root.Placement.BeginPlacement(PlacementItemId.Obstacle);
                root.Placement.PreviewPlacement(Vector3.zero);
                Check(root.Placement.ConfirmPlacement().Succeeded && nextBackend.Requests == 1 && backend.Requests == 1 &&
                    owner.GetComponentsInChildren<PlacementSelectionView>(true).Length == 1,
                    "Reinjecting a backend for the new context restores placement without duplicating selection UI");
                root.Placement.BeginPlacement(PlacementItemId.Furnace);
                manager.ReportResult(LevelResult.Failed); // Explicit simulated result, not a Core rule assertion.
                Check(ui.GetState().LevelState == LevelState.Failed && !selection.IsVisible && !view.IsVisible && !root.Placement.IsPlacing,
                    "Explicit public result reporting updates the real UI adapter and clears placement");
            }
            finally
            {
                UnityEngine.Object.Destroy(owner);
                backend.Dispose();
                nextBackend.Dispose();
                UnityEngine.Object.Destroy(gameRoot.gameObject);
            }
            yield return Frames(2);
        }

        private static Vector2 ButtonPoint(FirstLevelPlacementFixture fixture, string name)
        {
            var button = fixture.GetComponentsInChildren<Button>().First(item => item.name == name);
            return RectTransformUtility.WorldToScreenPoint(null, button.GetComponent<RectTransform>().TransformPoint(button.GetComponent<RectTransform>().rect.center));
        }
        private static IEnumerator Frames(int count) { for (var index = 0; index < count; index++) { yield return null; } }
        private static IEnumerator Move(Mouse mouse, Vector2 position)
        { DeviceInput.QueueStateEvent(mouse, new MouseState { position = position }); yield return Frames(3); }
        private static IEnumerator Click(Mouse mouse, Vector2 position)
        {
            yield return Move(mouse, position);
            DeviceInput.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return Frames(3);
            yield return Move(mouse, position);
        }
        private static IEnumerator Tap(Touchscreen touch, Vector2 position)
        {
            DeviceInput.QueueStateEvent(touch, new TouchState { touchId = 1, phase = TouchPhase.Began, position = position });
            yield return Frames(3);
            DeviceInput.QueueStateEvent(touch, new TouchState { touchId = 1, phase = TouchPhase.Ended, position = position });
            yield return Frames(3);
        }
        private static void Check(bool condition, string description)
        {
            if (!condition) { throw new InvalidOperationException(description); }
            passed.Add(description); Debug.Log("PLACEMENT PLAY PASS: " + description);
        }
        private static void OnRuntimeLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) { runtimeError = message + "\n" + stack; } }
        private static void Stop(Exception failure)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnRuntimeLog;
            while (routines.Count > 0) { (routines.Pop() as IDisposable)?.Dispose(); }
            SessionState.SetString(ResultKey, JsonUtility.ToJson(new Report { unityVersion = Application.unityVersion,
                success = failure == null, failure = failure?.ToString() ?? "", assertions = passed.ToArray() }, true));
            if (failure != null) { Debug.LogException(failure); }
            EditorApplication.ExitPlaymode();
        }
        private static void Finish()
        {
            if (!SessionState.GetBool(ActiveKey, false)) { return; }
            SessionState.SetBool(ActiveKey, false);
            var json = SessionState.GetString(ResultKey, "");
            if (string.IsNullOrEmpty(json)) { json = JsonUtility.ToJson(new Report { unityVersion = Application.unityVersion,
                success = false, failure = "Play Mode ended before checks completed.", assertions = Array.Empty<string>() }, true); }
            var output = SessionState.GetString(OutputKey, "");
            File.WriteAllText(output, json);
            var success = JsonUtility.FromJson<Report>(json).success;
            Debug.Log("Placement Play Mode validation: " + (success ? "PASS" : "FAIL") + "; report: " + output);
            if (Environment.GetCommandLineArgs().Contains("-placementExitAfterChecks")) { EditorApplication.Exit(success ? 0 : 1); }
        }
    }
}
