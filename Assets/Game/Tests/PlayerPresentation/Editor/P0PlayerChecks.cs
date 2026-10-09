using System;
using SlimeBattle.Core;
using SlimeBattle.Player.Input;
using SlimeBattle.Presentation.Camera;
using SlimeBattle.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using PlayerInputSystem = SlimeBattle.Player.Input.InputSystem;
using DeviceInputSystem = UnityEngine.InputSystem.InputSystem;

namespace SlimeBattle.Tests.PlayerPresentation.Editor
{
    /// <summary>Explicit editor checks; no changes to production scenes or test assemblies.</summary>
    public static class P0PlayerChecks
    {
        [MenuItem("SlimeBattle/Tests/Run P0 Player Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Run these checks outside Play Mode.");
            }
            var root = new GameObject("P0_PLAYER_CHECKS_TEMP");
            Keyboard keyboard = null;
            var updateMode = DeviceInputSystem.settings.updateMode;
            try
            {
                var cameraObject = new GameObject("Camera", typeof(UnityEngine.Camera), typeof(CameraSystem));
                cameraObject.transform.SetParent(root.transform);
                cameraObject.transform.position = new Vector3(2f, 3f, -10f);
                var camera = cameraObject.GetComponent<UnityEngine.Camera>();
                camera.pixelRect = new Rect(0f, 0f, 1280f, 720f);
                camera.aspect = 16f / 9f;
                var cameraSystem = cameraObject.GetComponent<CameraSystem>();
                cameraSystem.Initialize();
                var world = new Vector3(1.25f, -2.5f, 0f);
                Check(Vector3.Distance(cameraSystem.ScreenToWorld(cameraSystem.WorldToScreen(world)), world) < 0.001f,
                    "Screen/world round trip uses the gameplay plane");
                cameraSystem.SetBounds(new CameraBounds(new Vector2(-1f, -1f), new Vector2(1f, 1f)));
                cameraSystem.SubmitCommand(new CameraCommand(CameraCommandKind.Pan, new Vector2(100f, -100f)));
                Check(Vector2.Distance(cameraSystem.GetState().Position, Vector2.zero) < 0.001f,
                    "A map smaller than the viewport centers both axes");
                Check(cameraSystem.GetState().Position.z == -10f, "Camera commands preserve Z");
                cameraSystem.SetBounds(new CameraBounds(new Vector2(-30f, -20f), new Vector2(30f, 20f)));
                cameraSystem.SubmitCommand(new CameraCommand(CameraCommandKind.Focus, new Vector2(1000f, 1000f)));
                var state = cameraSystem.GetState();
                Check(state.Position.x + state.OrthographicSize * camera.aspect <= 30.001f &&
                    state.Position.y + state.OrthographicSize <= 20.001f, "Bounds include viewport extents");
                cameraSystem.SubmitCommand(new CameraCommand(CameraCommandKind.Zoom, Vector2.zero, -1000f));
                Check(cameraSystem.GetState().OrthographicSize > 0f, "Zoom cannot cross zero");
                var rejectedBounds = false;
                try { cameraSystem.SetBounds(new CameraBounds(Vector2.zero, Vector2.zero)); }
                catch (ArgumentException) { rejectedBounds = true; }
                Check(rejectedBounds, "Invalid bounds are rejected");

                DeviceInputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                keyboard = DeviceInputSystem.AddDevice<Keyboard>();
                var input = root.AddComponent<PlayerInputSystem>();
                input.Enable();
                input.SetInputMode(InputMode.Gameplay);
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                DeviceInputSystem.Update();
                input.SendMessage("Update");
                var moveCount = 0;
                var token = input.Subscribe<PlayerCommandSubmittedEvent>(data => moveCount++);
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                DeviceInputSystem.Update();
                input.SendMessage("Update");
                Check(moveCount == 1 && input.ReadSnapshot().Movement == Vector2.up,
                    "Keyboard maps to a semantic movement command");
                var firstSnapshot = input.ReadSnapshot();
                var repeatedSnapshot = input.ReadSnapshot();
                Check(firstSnapshot.Movement == Vector2.up && repeatedSnapshot.Movement == Vector2.up,
                    "Snapshot reads do not consume input");
                input.SetInputMode(InputMode.UiOnly);
                input.SendMessage("Update");
                Check(moveCount == 1 && input.ReadSnapshot().Movement == Vector2.zero,
                    "UI-only mode suppresses gameplay and clears held state");
                input.SetInputMode(InputMode.Gameplay);
                input.SendMessage("Update");
                Check(moveCount == 1, "Held input does not leak across mode changes");
                input.Disable();
                DeviceInputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                DeviceInputSystem.Update();
                input.SendMessage("Update");
                Check(moveCount == 1, "Disabled input emits no commands");
                input.Unsubscribe(token);

                var ui = root.AddComponent<UISystem>();
                var manager = root.AddComponent<LevelManager>();
                manager.Initialize();
                ui.Initialize(manager, input);
                var accepted = true;
                ui.UiCommandSubmitted += data => accepted = data.Accepted;
                ui.SubmitCommand(UiCommand.Restart);
                Check(!accepted, "Restart is rejected without a current level");
                ui.Refresh(new UiState(new LevelId("test"), LevelState.Succeeded, "objective"));
                Check(ui.GetState().LevelState == LevelState.Succeeded &&
                    manager.GetLevelState() == LevelState.Unloaded, "UI display never changes authoritative level state");
                ui.Initialize(manager, input);
                Check(ui.GetState().LevelState == LevelState.Unloaded, "Rebinding UI queries fresh state");
                Debug.Log("P0 Player Checks: PASS (14 assertions). Play Mode, touch and restart still require the test scene.");
            }
            finally
            {
                // Edit Mode does not guarantee runtime MonoBehaviour destroy callbacks.
                var input = root.GetComponent<PlayerInputSystem>();
                if (input != null)
                {
                    input.Disable();
                    input.SendMessage("OnDestroy");
                }
                UnityEngine.Object.DestroyImmediate(root);
                if (keyboard != null) { DeviceInputSystem.RemoveDevice(keyboard); }
                DeviceInputSystem.settings.updateMode = updateMode;
            }
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) { throw new InvalidOperationException("P0 check failed: " + message); }
        }
    }
}
