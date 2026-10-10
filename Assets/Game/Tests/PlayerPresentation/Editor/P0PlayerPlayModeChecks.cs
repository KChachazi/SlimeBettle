using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SlimeBattle.Core;
using SlimeBattle.Player.Input;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using DeviceInputSystem = UnityEngine.InputSystem.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;
using PlayerInputSystem = SlimeBattle.Player.Input.InputSystem;

namespace SlimeBattle.Tests.PlayerPresentation.Editor
{
    /// <summary>Opt-in real Play Mode acceptance checks, resumed across domain reloads.</summary>
    [InitializeOnLoad]
    public static class P0PlayerPlayModeChecks
    {
        private const string ActiveKey = "SlimeBattle.P0Validation.Active";
        private const string ScenesKey = "SlimeBattle.P0Validation.Scenes";
        private const string OutputKey = "SlimeBattle.P0Validation.Output";
        private const string ResultKey = "SlimeBattle.P0Validation.Result";
        private const string BootstrapPath = "Assets/Game/Tests/PlayerPresentation/Bootstrap_P0Player.unity";
        private const string TestPath = "Assets/Game/Tests/PlayerPresentation/Test_PlayerPresentation.unity";
        private static readonly Stack<IEnumerator> routines = new Stack<IEnumerator>();
        private static readonly List<string> passed = new List<string>();
        private static int lastFrame = -1;
        private static float deadline;
        private static string runtimeError;

        [Serializable]
        private sealed class SceneBackup { public string[] paths; public bool[] enabled; }

        [Serializable]
        private sealed class ValidationResult
        {
            public string unityVersion;
            public bool success;
            public string failure;
            public string[] assertions;
        }

        static P0PlayerPlayModeChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            // Importing this file alone never starts a test or changes a scene.
            if (SessionState.GetBool(ActiveKey, false) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += Finish;
            }
        }

        [MenuItem("SlimeBattle/Tests/Run P0 Play Mode Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool(ActiveKey, false))
            {
                throw new InvalidOperationException("Start P0 validation outside Play Mode with no active run.");
            }
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { return; }
            P0PlayerChecks.Run();
            var original = EditorBuildSettings.scenes;
            SessionState.SetString(ScenesKey, JsonUtility.ToJson(new SceneBackup
            {
                paths = original.Select(scene => scene.path).ToArray(),
                enabled = original.Select(scene => scene.enabled).ToArray()
            }));
            var arguments = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(arguments, "-p0ReportPath");
            var output = index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] :
                Path.Combine(Path.GetTempPath(), "SlimeBattleP0-validation.json");
            SessionState.SetString(OutputKey, Path.GetFullPath(output));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            SessionState.SetString(ResultKey, string.Empty);
            SessionState.SetBool(ActiveKey, true);
            var scenes = original.Where(scene => scene.path != BootstrapPath && scene.path != TestPath).ToList();
            scenes.Add(new EditorBuildSettingsScene(BootstrapPath, true));
            scenes.Add(new EditorBuildSettingsScene(TestPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.OpenScene(BootstrapPath);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ActiveKey, false)) { return; }
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                passed.Clear();
                routines.Clear();
                routines.Push(CheckRuntime());
                deadline = Time.realtimeSinceStartup + 180f;
                lastFrame = -1;
                runtimeError = null;
                Application.logMessageReceived += OnRuntimeLog;
                EditorApplication.update += Tick;
                var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType != null) { EditorWindow.GetWindow(gameViewType).Focus(); }
            }
            else if (change == PlayModeStateChange.EnteredEditMode) { Finish(); }
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { return; }
            if (runtimeError != null) { Stop(new InvalidOperationException(runtimeError)); return; }
            if (Time.realtimeSinceStartup > deadline)
            {
                Stop(new TimeoutException("P0 Play Mode checks exceeded 180 seconds."));
                return;
            }
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
            var manager = UnityEngine.Object.FindObjectOfType<LevelManager>();
            while (manager == null)
            {
                yield return null;
                manager = UnityEngine.Object.FindObjectOfType<LevelManager>();
            }
            yield return WaitForLevel(manager);
            while (!Application.isBatchMode && !Application.isFocused) { yield return null; }
            var input = UnityEngine.Object.FindObjectOfType<PlayerInputSystem>();
            var ui = UnityEngine.Object.FindObjectOfType<UISystem>();
            var camera = UnityEngine.Object.FindObjectOfType<CameraSystem>();
            var probe = GameObject.Find("InputProbe_NotAnActor").transform;
            var marker = probe.parent.Find("TEST_ONLY_ClickMarker");
            Check(manager.GetCurrentContext() != null && input.GetInputMode() == InputMode.Gameplay,
                "Bootstrap creates a running level and enables gameplay input");
            Check(ui.GetState().ObjectiveText.Contains("TEST:") && ui.GetState().SlimeCount == -1,
                "HUD reads the initial objective snapshot without inventing actor counts");
            Check(marker != null && !marker.gameObject.activeSelf, "The test-only click marker starts hidden");

            var keyboard = DeviceInputSystem.AddDevice<Keyboard>();
            var mouse = DeviceInputSystem.AddDevice<Mouse>();
            var touchscreen = DeviceInputSystem.AddDevice<Touchscreen>();
            var attackCount = 0;
            var primaryCount = 0;
            var moveCount = 0;
            var pressedCount = 0;
            var movedCount = 0;
            var releasedCount = 0;
            var edgeWasReadable = false;
            var lastScreenPosition = Vector2.zero;
            var token = input.Subscribe<PlayerCommandSubmittedEvent>(data =>
            {
                if (data.Command.Kind == PlayerCommandKind.Attack) { attackCount++; }
                if (data.Command.Kind == PlayerCommandKind.Move) { moveCount++; }
                if (data.Command.Kind != PlayerCommandKind.PrimaryAction) { return; }
                primaryCount++;
                lastScreenPosition = data.Command.ScreenPosition;
                edgeWasReadable = input.ReadSnapshot().PointerPressed && input.ReadSnapshot().PointerPressed;
            });
            var pressedToken = input.Subscribe<PointerPressedEvent>(_ => pressedCount++);
            var movedToken = input.Subscribe<PointerMovedEvent>(_ => movedCount++);
            var releasedToken = input.Subscribe<PointerReleasedEvent>(_ => releasedCount++);
            var subscribedInput = input;
            try
            {
                var worldPixel = new Vector2(Screen.width * 0.6f, Screen.height * 0.45f);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel });
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(4);
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return Frames(5);
                Check(probe.position == Vector3.zero && moveCount == 0 && input.ReadSnapshot().Movement == Vector2.zero,
                    "WASD is unbound by default and the input probe stays still");
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return Frames(4);
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);
                Check(attackCount == 1 && probe.position == Vector3.zero, "The debug attack key records one request without moving the probe");

                var position = new Vector3(6f, -1f, 0f);
                worldPixel = camera.WorldToScreen(position);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel }.WithButton(MouseButton.Left));
                yield return Frames(3);
                Check(primaryCount == 1 && pressedCount == 1 && edgeWasReadable,
                    "One mouse press produces one primary action and a non-consuming snapshot edge");
                Check(input.ReadSnapshot().PointerHeld && input.ReadSnapshot().PointerHeld,
                    "Repeated snapshot reads preserve the held pointer fact");
                Check(Vector2.Distance(lastScreenPosition, worldPixel) < 0.01f && MarkerAt(marker, position),
                    "The mouse screen position projects to the test marker on the gameplay plane");
                yield return Frames(4);
                Check(primaryCount == 1, "Holding the pointer never repeats the primary action");
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel + Vector2.right * 20f }.WithButton(MouseButton.Left));
                yield return Frames(3);
                Check(movedCount > 0 && primaryCount == 1 && MarkerAt(marker, position),
                    "Pointer movement is reported separately without repeating the click marker action");
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel });
                yield return Frames(6);
                Check(releasedCount == 1 && probe.position == Vector3.zero && MarkerAt(marker, position),
                    "Release is reported once and never starts automatic probe movement");

                position = new Vector3(-4f, -2f, 0f);
                yield return Click(mouse, camera.WorldToScreen(position));
                Check(primaryCount == 2 && MarkerAt(marker, position), "Another mouse click relocates only the test marker");
                position = new Vector3(2f, -2f, 0f);
                yield return Tap(touchscreen, camera.WorldToScreen(position));
                Check(primaryCount == 3 && pressedCount == 3 && releasedCount == 3,
                    "Virtual touch and mouse share one primary, press and release per tap");
                Check(MarkerAt(marker, position) && probe.position == Vector3.zero,
                    "Virtual touch projects the marker correctly while the probe remains stationary");

                var headerPixel = new Vector2(Screen.width * 0.3f, Screen.height - 35f);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = headerPixel }.WithButton(MouseButton.Left));
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel }.WithButton(MouseButton.Left));
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel });
                yield return Frames(3);
                Check(primaryCount == 3 && MarkerAt(marker, position), "A UI-origin mouse drag cannot update the map marker");
                QueueTouch(touchscreen, InputTouchPhase.Began, headerPixel);
                yield return Frames(3);
                QueueTouch(touchscreen, InputTouchPhase.Moved, worldPixel);
                yield return Frames(3);
                QueueTouch(touchscreen, InputTouchPhase.Ended, worldPixel);
                yield return Frames(3);
                Check(primaryCount == 3 && MarkerAt(marker, position), "A UI-origin touch gesture cannot become a map action");

                var cameraBefore = camera.GetState();
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel }.WithButton(MouseButton.Middle));
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel + new Vector2(80f, 0f) }.WithButton(MouseButton.Middle));
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel });
                yield return Frames(3);
                Check(Vector3.Distance(camera.GetState().Position, cameraBefore.Position) > 0.01f &&
                    probe.position == Vector3.zero && MarkerAt(marker, position), "Camera drag changes presentation without changing world objects");
                var sizeBefore = camera.GetState().OrthographicSize;
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel, scroll = new Vector2(0f, 120f) });
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel });
                yield return Frames(3);
                Check(Mathf.Abs(camera.GetState().OrthographicSize - sizeBefore) > 0.01f && MarkerAt(marker, position),
                    "The mouse wheel zooms without changing marker world coordinates");

                // Exercise this component's Unity lifecycle callback, not an Android or OS focus test.
                input.SendMessage("OnApplicationFocus", false, SendMessageOptions.RequireReceiver);
                Check(!marker.gameObject.activeSelf && !input.ReadSnapshot().PointerHeld,
                    "Simulated focus loss clears the released gesture and pending marker");
                var beforeFocusCount = primaryCount;
                QueueTouch(touchscreen, InputTouchPhase.Began, worldPixel);
                yield return Frames(3);
                input.SendMessage("OnApplicationFocus", true, SendMessageOptions.RequireReceiver);
                yield return Frames(3);
                Check(primaryCount == beforeFocusCount && !marker.gameObject.activeSelf,
                    "Simulated focus return does not inherit a held touch");
                QueueTouch(touchscreen, InputTouchPhase.Ended, worldPixel);
                yield return Frames(3);
                position = new Vector3(0f, -2f, 0f);
                yield return Tap(touchscreen, camera.WorldToScreen(position));
                Check(primaryCount == beforeFocusCount + 1 && MarkerAt(marker, position),
                    "A fresh touch works after focus cleanup");

                var pauseButton = GameObject.Find("PauseButton").GetComponent<RectTransform>();
                yield return Click(mouse, RectTransformUtility.WorldToScreenPoint(null, pauseButton.position));
                Check(manager.GetLevelState() == LevelState.Paused && input.GetInputMode() == InputMode.UiOnly && !marker.gameObject.activeSelf,
                    "The real uGUI Pause button pauses Core and clears the pending marker");
                var beforePauseCount = primaryCount;
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel });
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                QueueTouch(touchscreen, InputTouchPhase.Began, worldPixel);
                yield return Frames(4);
                Check(attackCount == 1 && primaryCount == beforePauseCount && !marker.gameObject.activeSelf,
                    "Pause blocks new touch and debug attack requests");
                ui.SubmitCommand(UiCommand.Resume);
                yield return Frames(4);
                Check(attackCount == 1 && primaryCount == beforePauseCount && !marker.gameObject.activeSelf,
                    "Resume does not inherit the old marker, held touch or debug attack");
                QueueTouch(touchscreen, InputTouchPhase.Ended, worldPixel);
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);
                yield return Tap(touchscreen, camera.WorldToScreen(position));
                Check(primaryCount == beforePauseCount + 1 && MarkerAt(marker, position), "A fresh touch updates the marker after resume");
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return Frames(3);
                Check(manager.GetLevelState() == LevelState.Paused, "Escape produces a single pause transition");
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return Frames(3);
                Check(manager.GetLevelState() == LevelState.Running, "Escape remains available in UI-only mode");
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);
                Check(!marker.gameObject.activeSelf && probe.position == Vector3.zero, "Pause cycles preserve no old map action or probe motion");

                ui.enabled = false;
                Check(manager.ReportResult(LevelResult.Succeeded), "Core accepts a simulated upstream success result");
                ui.enabled = true;
                yield return Frames(3);
                Check(ui.GetState().LevelState == LevelState.Succeeded && GameObject.Find("ResultPanel") != null,
                    "Late-enabled UI reconstructs the result and opens its panel");
                Check(input.GetInputMode() == InputMode.UiOnly, "Completed levels block gameplay input");
                var beforeResultCount = primaryCount;
                yield return Tap(touchscreen, worldPixel);
                Check(primaryCount == beforeResultCount && !marker.gameObject.activeSelf && probe.position == Vector3.zero,
                    "Tapping a completed level cannot create a map action or move the probe");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    ScreenCapture.CaptureScreenshot(Path.ChangeExtension(SessionState.GetString(OutputKey, string.Empty), ".png"));
                    yield return Frames(5);
                }

                input.Unsubscribe(token);
                input.Unsubscribe(pressedToken);
                input.Unsubscribe(movedToken);
                input.Unsubscribe(releasedToken);
                QueueTouch(touchscreen, InputTouchPhase.Began, worldPixel);
                yield return Frames(3);
                for (var iteration = 0; iteration < 5; iteration++)
                {
                    var previous = manager.GetCurrentContext();
                    ui.SubmitCommand(UiCommand.Restart);
                    ui.SubmitCommand(UiCommand.Restart);
                    yield return WaitForLevel(manager, previous);
                    input = UnityEngine.Object.FindObjectOfType<PlayerInputSystem>();
                    ui = UnityEngine.Object.FindObjectOfType<UISystem>();
                    probe = GameObject.Find("InputProbe_NotAnActor").transform;
                    marker = probe.parent.Find("TEST_ONLY_ClickMarker");
                    Check(UnityEngine.Object.FindObjectsOfType<BattleHudView>().Length == 1 &&
                        UnityEngine.Object.FindObjectsOfType<CameraSystem>().Length == 1 &&
                        UnityEngine.Object.FindObjectsOfType<EventSystem>().Length == 1 && input.GetInputMode() == InputMode.Gameplay,
                        "Restart " + (iteration + 1) + " creates one clean HUD, camera and EventSystem");
                    Check(probe.position == Vector3.zero && !marker.gameObject.activeSelf && !input.ReadSnapshot().PointerHeld,
                        "Restart " + (iteration + 1) + " discards old gestures and test markers");
                    if (iteration == 0)
                    {
                        QueueTouch(touchscreen, InputTouchPhase.Ended, worldPixel);
                        yield return Frames(3);
                        camera = UnityEngine.Object.FindObjectOfType<CameraSystem>();
                        position = new Vector3(2f, -1f, 0f);
                        var newPrimaryCount = 0;
                        var newToken = input.Subscribe<PlayerCommandSubmittedEvent>(data =>
                        {
                            if (data.Command.Kind == PlayerCommandKind.PrimaryAction) { newPrimaryCount++; }
                        });
                        input.Unsubscribe(token); // A previous scene's token must not remove this subscription.
                        yield return Tap(touchscreen, camera.WorldToScreen(position));
                        Check(newPrimaryCount == 1 && MarkerAt(marker, position) && probe.position == Vector3.zero,
                            "A fresh touch works after restart and an old token cannot cancel the new channel");
                        input.Unsubscribe(newToken);
                    }
                    if (iteration < 4)
                    {
                        var result = iteration % 2 == 0 ? LevelResult.Failed : LevelResult.Succeeded;
                        Check(manager.ReportResult(result) && ui.GetState().LevelState == manager.GetLevelState(),
                            "Restart " + (iteration + 1) + " observes the new upstream result");
                    }
                }
            }
            finally
            {
                if (subscribedInput != null)
                {
                    subscribedInput.Unsubscribe(token);
                    subscribedInput.Unsubscribe(pressedToken);
                    subscribedInput.Unsubscribe(movedToken);
                    subscribedInput.Unsubscribe(releasedToken);
                }
                DeviceInputSystem.RemoveDevice(keyboard);
                DeviceInputSystem.RemoveDevice(mouse);
                DeviceInputSystem.RemoveDevice(touchscreen);
            }
        }

        private static bool MarkerAt(Transform marker, Vector3 worldPosition)
        {
            return marker.gameObject.activeSelf && Vector3.Distance(marker.position, worldPosition) < 0.01f;
        }

        private static IEnumerator WaitForLevel(LevelManager manager, LevelContext previous = null)
        {
            var limit = Time.realtimeSinceStartup + 30f;
            while (manager.GetLevelState() != LevelState.Running || manager.GetCurrentContext() == previous)
            {
                if (Time.realtimeSinceStartup > limit) { throw new TimeoutException("The test level did not reach a fresh Running context."); }
                yield return null;
            }
            yield return Frames(5);
        }

        private static IEnumerator Frames(int count)
        {
            for (var index = 0; index < count; index++) { yield return null; }
        }

        private static void QueueTouch(Touchscreen touchscreen, InputTouchPhase phase, Vector2 position)
        {
            DeviceInputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, phase = phase, position = position });
        }

        private static IEnumerator Tap(Touchscreen touchscreen, Vector2 position)
        {
            QueueTouch(touchscreen, InputTouchPhase.Began, position);
            yield return Frames(3);
            QueueTouch(touchscreen, InputTouchPhase.Ended, position);
            yield return Frames(3);
        }

        private static IEnumerator Click(Mouse mouse, Vector2 position)
        {
            DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return Frames(2);
            DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return Frames(2);
            DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return Frames(2);
        }

        private static void Check(bool condition, string description, string failureDetails = null)
        {
            if (!condition) { throw new InvalidOperationException(description +
                (failureDetails == null ? string.Empty : " [" + failureDetails + "]")); }
            passed.Add(description);
            Debug.Log("P0 PLAY PASS: " + description);
        }

        private static void Stop(Exception failure)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnRuntimeLog;
            while (routines.Count > 0) { (routines.Pop() as IDisposable)?.Dispose(); }
            var result = new ValidationResult
            {
                unityVersion = Application.unityVersion,
                success = failure == null,
                failure = failure?.ToString() ?? string.Empty,
                assertions = passed.ToArray()
            };
            SessionState.SetString(ResultKey, JsonUtility.ToJson(result, true));
            if (failure != null) { Debug.LogException(failure); }
            EditorApplication.ExitPlaymode();
        }

        private static void OnRuntimeLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                runtimeError = message + "\n" + stackTrace;
            }
        }

        private static void Finish()
        {
            if (!SessionState.GetBool(ActiveKey, false)) { return; }
            SessionState.SetBool(ActiveKey, false);
            var backup = JsonUtility.FromJson<SceneBackup>(SessionState.GetString(ScenesKey, string.Empty));
            EditorBuildSettings.scenes = backup.paths.Select((path, index) => new EditorBuildSettingsScene(path, backup.enabled[index])).ToArray();
            var json = SessionState.GetString(ResultKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                json = JsonUtility.ToJson(new ValidationResult { unityVersion = Application.unityVersion,
                    success = false, failure = "Play Mode ended before checks completed.", assertions = Array.Empty<string>() }, true);
            }
            var output = SessionState.GetString(OutputKey, string.Empty);
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, json);
            var success = JsonUtility.FromJson<ValidationResult>(json).success;
            Debug.Log("P0 Play Mode validation: " + (success ? "PASS" : "FAIL") + "; report: " + output);
            if (Application.isBatchMode || Environment.GetCommandLineArgs().Contains("-p0ExitAfterChecks"))
            {
                EditorApplication.Exit(success ? 0 : 1);
            }
        }
    }
}
