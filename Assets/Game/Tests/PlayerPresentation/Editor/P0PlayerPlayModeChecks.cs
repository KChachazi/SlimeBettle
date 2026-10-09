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
            Check(manager.GetCurrentContext() != null && input.GetInputMode() == InputMode.Gameplay,
                "Bootstrap creates a running level and enables gameplay input");
            Check(ui.GetState().ObjectiveText.Contains("TEST:") && ui.GetState().SlimeCount == -1,
                "HUD reads the initial objective snapshot without inventing actor counts");

            var keyboard = DeviceInputSystem.AddDevice<Keyboard>();
            var mouse = DeviceInputSystem.AddDevice<Mouse>();
            var attackCount = 0;
            var primaryCount = 0;
            var token = input.Subscribe<PlayerCommandSubmittedEvent>(data =>
            {
                if (data.Command.Kind == PlayerCommandKind.Attack) { attackCount++; }
                if (data.Command.Kind == PlayerCommandKind.PrimaryAction) { primaryCount++; }
            });
            var subscribedInput = input;
            try
            {
                // Keep the pointer in the world, outside HUD and diagnostic controls.
                var worldPixel = new Vector2(Screen.width * 0.6f, Screen.height * 0.45f);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel });
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(4);
                var before = probe.position;
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return Frames(5);
                Check(probe.position.y > before.y, "Keyboard input reaches the test movement consumer",
                    "focused=" + Application.isFocused + ", keyboardEnabled=" + keyboard.enabled +
                    ", W=" + keyboard.wKey.ReadValue() + ", mode=" + input.GetInputMode() +
                    ", snapshot=" + input.ReadSnapshot().Movement + ", frame=" + Time.frameCount);
                Check(input.ReadSnapshot().Movement == Vector2.up && input.ReadSnapshot().Movement == Vector2.up,
                    "Repeated snapshot reads preserve the current movement input");
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);
                before = probe.position;
                yield return Frames(3);
                Check(Vector3.Distance(probe.position, before) < 0.001f, "Releasing movement stops the consumer");

                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return Frames(4);
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);
                Check(attackCount == 1, "A held attack key submits one attack edge");
                yield return Click(mouse, worldPixel);
                Check(primaryCount == 1 && Vector3.Distance(probe.position, camera.ScreenToWorld(worldPixel)) < 0.01f,
                    "World click produces one semantic action on the gameplay plane",
                    "commands=" + primaryCount + ", actual=" + probe.position +
                    ", expected=" + camera.ScreenToWorld(worldPixel));

                var headerPixel = new Vector2(Screen.width * 0.3f, Screen.height - 35f);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = headerPixel }.WithButton(MouseButton.Left));
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel }.WithButton(MouseButton.Left));
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel });
                yield return Frames(3);
                Check(primaryCount == 1, "A UI-origin drag never turns into a gameplay click");

                before = probe.position;
                var cameraBefore = camera.GetState();
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel }.WithButton(MouseButton.Middle));
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel + new Vector2(80f, 0f) }.WithButton(MouseButton.Middle));
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel });
                yield return Frames(3);
                Check(Vector3.Distance(camera.GetState().Position, cameraBefore.Position) > 0.01f &&
                    Vector3.Distance(probe.position, before) < 0.001f, "Camera drag changes presentation without moving the probe");

                var pauseButton = GameObject.Find("PauseButton").GetComponent<RectTransform>();
                var pausePixel = RectTransformUtility.WorldToScreenPoint(null, pauseButton.position);
                yield return Click(mouse, pausePixel);
                Check(manager.GetLevelState() == LevelState.Paused && input.GetInputMode() == InputMode.UiOnly,
                    "The real uGUI Pause button pauses Core and suppresses gameplay input");
                before = probe.position;
                DeviceInputSystem.QueueStateEvent(mouse, new MouseState { position = worldPixel });
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space));
                yield return Frames(4);
                Check(Vector3.Distance(probe.position, before) < 0.001f && attackCount == 1,
                    "Paused movement and attack produce no gameplay behavior");
                ui.SubmitCommand(UiCommand.Resume);
                yield return Frames(4);
                Check(Vector3.Distance(probe.position, before) < 0.001f && attackCount == 1,
                    "Resume does not inherit held movement or attack");
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return Frames(3);
                Check(manager.GetLevelState() == LevelState.Paused, "Escape produces a single pause transition");
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return Frames(3);
                Check(manager.GetLevelState() == LevelState.Running, "Escape remains available to resume from UI-only mode");
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Frames(3);

                ui.enabled = false;
                Check(manager.ReportResult(LevelResult.Succeeded), "Core accepts a simulated upstream success result");
                ui.enabled = true;
                yield return Frames(3);
                Check(ui.GetState().LevelState == LevelState.Succeeded && GameObject.Find("ResultPanel") != null,
                    "Late-enabled UI reconstructs the result and opens its panel");
                Check(input.GetInputMode() == InputMode.UiOnly, "Completed levels block gameplay input");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    var imagePath = Path.ChangeExtension(SessionState.GetString(OutputKey, string.Empty), ".png");
                    ScreenCapture.CaptureScreenshot(imagePath);
                    yield return Frames(5);
                }

                input.Unsubscribe(token);
                for (var iteration = 0; iteration < 5; iteration++)
                {
                    var previous = manager.GetCurrentContext();
                    ui.SubmitCommand(UiCommand.Restart);
                    ui.SubmitCommand(UiCommand.Restart);
                    yield return WaitForLevel(manager, previous);
                    input = UnityEngine.Object.FindObjectOfType<PlayerInputSystem>();
                    ui = UnityEngine.Object.FindObjectOfType<UISystem>();
                    Check(UnityEngine.Object.FindObjectsOfType<BattleHudView>().Length == 1 &&
                        UnityEngine.Object.FindObjectsOfType<CameraSystem>().Length == 1 &&
                        UnityEngine.Object.FindObjectsOfType<EventSystem>().Length == 1 &&
                        input.GetInputMode() == InputMode.Gameplay,
                        "Restart " + (iteration + 1) + " creates one clean HUD, camera and EventSystem");
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
                if (subscribedInput != null) { subscribedInput.Unsubscribe(token); }
                DeviceInputSystem.RemoveDevice(keyboard);
                DeviceInputSystem.RemoveDevice(mouse);
            }
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
